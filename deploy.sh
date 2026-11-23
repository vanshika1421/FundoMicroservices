#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"
COMPOSE=(docker compose --env-file .env.local -f compose.local.yaml)
SERVICES=(postgres redis kafka user-service notes-service notification-service api-gateway nginx)

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 1; }; }
ssh_vm() {
  if [[ -n "${VM_SSH_KEY:-}" ]]; then
    ssh -i "$VM_SSH_KEY" -o IdentitiesOnly=yes "$@"
  else
    ssh "$@"
  fi
}
need docker
need curl

if [[ ! -f .env.local ]]; then
  echo "Missing .env.local. Copy .env.local.example, set secrets, then rerun." >&2
  exit 1
fi

health_local() {
  local service id status attempt migrate_id migrate_state migrate_exit
  migrate_id="$("${COMPOSE[@]}" ps -aq user-migrate)"
  [[ -n "$migrate_id" ]] || { echo "FAIL user-migrate: no container"; "${COMPOSE[@]}" logs user-migrate; exit 1; }
  for attempt in $(seq 1 60); do
    migrate_state="$(docker inspect --format '{{.State.Status}}' "$migrate_id")"
    [[ "$migrate_state" == exited ]] && break
    sleep 2
  done
  migrate_exit="$(docker inspect --format '{{.State.ExitCode}}' "$migrate_id")"
  echo "user-migrate: exit $migrate_exit"
  [[ "$migrate_state" == exited && "$migrate_exit" == 0 ]] || { "${COMPOSE[@]}" logs --no-color user-migrate; exit 1; }

  for service in "${SERVICES[@]}"; do
    id="$("${COMPOSE[@]}" ps -q "$service")"
    [[ -n "$id" ]] || { echo "FAIL $service: no container"; "${COMPOSE[@]}" logs "$service"; exit 1; }
    for attempt in $(seq 1 60); do
      status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$id")"
      [[ "$status" == healthy || "$status" == running ]] && break
      [[ "$status" == unhealthy || "$status" == exited ]] && break
      sleep 2
    done
    echo "$service: $status"
    if [[ "$status" != healthy && "$status" != running ]]; then
      "${COMPOSE[@]}" logs --no-color "$service"
      exit 1
    fi
  done
}

verify_local_routes() {
  local url code
  for url in \
    http://127.0.0.1:8080/health/live \
    http://127.0.0.1:8080/swagger/index.html \
    http://127.0.0.1:8080/swagger/user/swagger.json \
    http://127.0.0.1:8080/swagger/notes/swagger.json \
    http://127.0.0.1:8080/swagger/notification/swagger.json; do
    code="$(curl -sS -o /dev/null -w '%{http_code}' "$url")"
    echo "GET $url -> $code"
    [[ "$code" == 200 ]] || return 1
  done
  code="$(curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/__unknown_route__)"
  echo "GET /__unknown_route__ -> $code (expected 404)"
  [[ "$code" == 404 ]]
}

local_deploy() {
  echo "== Build and start local stack =="
  "${COMPOSE[@]}" up --build -d
  health_local
  verify_local_routes
  echo "== Restart notes service and recheck route =="
  "${COMPOSE[@]}" restart notes-service
  health_local
  curl --fail --silent http://127.0.0.1:8080/swagger/notes/swagger.json >/dev/null
  echo "Notes Swagger route recovered after service restart: HTTP 200"
}

vm_deploy() {
  local target="$1"
  need ssh
  need tar
  need base64
  [[ -f .env.k8s ]] || { echo "Copy .env.k8s.example to .env.k8s and set deployment secrets." >&2; exit 1; }
  local env_line env_key env_value env_export_key env_line_number=0
  while IFS= read -r env_line || [[ -n "$env_line" ]]; do
    env_line_number=$((env_line_number + 1))
    env_line="${env_line%$'\r'}"
    [[ "$env_line" =~ ^[[:space:]]*$ || "$env_line" =~ ^[[:space:]]*# ]] && continue
    if [[ "$env_line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
      env_key="${BASH_REMATCH[1]}"
      env_value="${BASH_REMATCH[2]}"
      case "$env_value" in
        \"*\") env_value="${env_value:1:${#env_value}-2}" ;;
        \'*\') env_value="${env_value:1:${#env_value}-2}" ;;
      esac
      # Google displays app passwords in groups; SMTP needs the compact value.
      [[ "$env_key" == SMTP_PASSWORD ]] && env_value="${env_value// /}"
      env_export_key="K8S_${env_key}"
      printf -v "$env_export_key" '%s' "$env_value"
      export "$env_export_key"
    else
      echo "Invalid .env.k8s line $env_line_number; expected KEY=value. Value hidden." >&2
      exit 1
    fi
  done < .env.k8s
  : "${K8S_POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD in .env.k8s}"
  : "${K8S_JWT_SIGNING_KEY:?Set JWT_SIGNING_KEY in .env.k8s}"
  [[ ${#K8S_JWT_SIGNING_KEY} -ge 32 ]] || { echo "JWT_SIGNING_KEY must be at least 32 characters." >&2; exit 1; }

  echo "== Build and verify locally before VM deployment =="
  local_deploy
  echo "== Import locally built images into k3s containerd on $target =="
  docker save fundoo-user:1.0.0 fundoo-user-migrator:1.0.0 fundoo-notes:1.0.0 fundoo-notification:1.0.0 fundoo-api-gateway:1.0.0 | ssh_vm "$target" 'sudo k3s ctr images import -'

  SECRET_FILE="$(mktemp)"
  trap 'rm -f "$SECRET_FILE"' EXIT
  local db_user db_notes
  db_user="Host=postgres;Port=5432;Database=fundoo_users;Username=fundoo;Password=${K8S_POSTGRES_PASSWORD}"
  db_notes="Host=postgres;Port=5432;Database=fundoo_notes;Username=fundoo;Password=${K8S_POSTGRES_PASSWORD}"
  b64() { printf %s "$1" | base64 | tr -d '\r\n'; }
  {
    printf 'apiVersion: v1\nkind: Secret\nmetadata:\n  name: fundoo-secrets\n  namespace: apps\ntype: Opaque\ndata:\n'
    printf '  postgres-password: "%s"\n' "$(b64 "$K8S_POSTGRES_PASSWORD")"
    printf '  user-db: "%s"\n' "$(b64 "$db_user")"
    printf '  notes-db: "%s"\n' "$(b64 "$db_notes")"
    printf '  jwt-signing-key: "%s"\n' "$(b64 "$K8S_JWT_SIGNING_KEY")"
    printf '  google-client-id: "%s"\n' "$(b64 "${K8S_GOOGLE_CLIENT_ID:-}")"
    printf '  google-client-secret: "%s"\n' "$(b64 "${K8S_GOOGLE_CLIENT_SECRET:-}")"
    printf '  smtp-host: "%s"\n' "$(b64 "${K8S_SMTP_HOST:-smtp.example.com}")"
    printf '  smtp-port: "%s"\n' "$(b64 "${K8S_SMTP_PORT:-587}")"
    printf '  smtp-sender-email: "%s"\n' "$(b64 "${K8S_SMTP_SENDER_EMAIL:-noreply@example.com}")"
    printf '  smtp-username: "%s"\n' "$(b64 "${K8S_SMTP_USERNAME:-}")"
    printf '  smtp-password: "%s"\n' "$(b64 "${K8S_SMTP_PASSWORD:-}")"
  } > "$SECRET_FILE"

  echo "== Copy manifests and apply kustomize bundle =="
  tar -czf - k8s | ssh_vm "$target" 'mkdir -p ~/fundoo-deploy && tar -xzf - -C ~/fundoo-deploy'
  ssh_vm "$target" 'sudo k3s kubectl create namespace apps --dry-run=client -o yaml | sudo k3s kubectl apply -f -'
  ssh_vm "$target" 'sudo k3s kubectl apply -f -' < "$SECRET_FILE"
  ssh_vm "$target" 'sudo k3s kubectl apply -k ~/fundoo-deploy/k8s'
  if ! ssh_vm "$target" 'sudo k3s kubectl wait --for=condition=complete job/user-migrate -n apps --timeout=10m'; then
    ssh_vm "$target" 'sudo k3s kubectl logs -n apps job/user-migrate --all-containers=true --tail=100'
    exit 1
  fi
  local deployment
  for deployment in nginx api-gateway user-service notes-service notification-service; do
    if ! ssh_vm "$target" "sudo k3s kubectl rollout status deployment/$deployment -n apps --timeout=5m"; then
      ssh_vm "$target" "sudo k3s kubectl get pods -n apps -l app=$deployment -o wide; sudo k3s kubectl logs -n apps -l app=$deployment --all-containers=true --tail=100"
      exit 1
    fi
  done
  ssh_vm "$target" 'sudo k3s kubectl get pods,svc,ingress -n apps'
  ssh_vm "$target" 'set -e; for path in /health/live /swagger/index.html /swagger/user/swagger.json /swagger/notes/swagger.json /swagger/notification/swagger.json; do code=$(curl -sS -o /dev/null -w "%{http_code}" "http://127.0.0.1:30080$path"); echo "GET $path -> $code"; test "$code" = 200; done; code=$(curl -sS -o /dev/null -w "%{http_code}" http://127.0.0.1:30080/__unknown_route__); echo "GET /__unknown_route__ -> $code (expected 404)"; test "$code" = 404'
  ssh_vm "$target" 'sudo k3s kubectl delete pod -n apps -l app=notes-service --wait=true'
  ssh_vm "$target" 'sudo k3s kubectl rollout status deployment/notes-service -n apps --timeout=5m'
  ssh_vm "$target" 'curl --fail --silent http://127.0.0.1:30080/swagger/notes/swagger.json >/dev/null && echo "Notes Swagger route recovered after pod restart: HTTP 200"'
  echo "VM verification passed on loopback. Allow TCP/30080 in the VM firewall/security group, then open http://<VM-IP>:30080/swagger from your browser."
}

case "${1:-local}" in
  local) local_deploy ;;
  vm) [[ -n "${2:-}" ]] || { echo "Usage: ./deploy.sh vm user@vm-host" >&2; exit 2; }; vm_deploy "$2" ;;
  *) echo "Usage: ./deploy.sh [local|vm user@vm-host]" >&2; exit 2 ;;
esac
