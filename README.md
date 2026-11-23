# Fundoo Microservices

## Local dependencies

The APIs target .NET 8. Start PostgreSQL, Redis, and Kafka before running the services. The included Compose file starts Redis and Kafka (and creates the note event topics); PostgreSQL is provisioned separately. This checkout has two database consumers: User (`UserDb`) and Notes (`NotesDb`). Notification does not use a database.

```powershell
docker compose up -d redis kafka kafka-init
```

Create the User and Notes databases on your PostgreSQL server. Apply the User EF migrations and run [the Notes schema script](src/Services/Notes/Fundoo.Notes.Infrastructure/Database/001_CreateNotesSchema.sql) against the Notes database. The User service uses Redis for login OTP challenges. Notes does not use Redis.

## Local ports and service calls

| Project | HTTP | HTTPS |
| --- | --- | --- |
| User API | `http://localhost:5195` | `https://localhost:7001` |
| Notes API | `http://localhost:5077` | `https://localhost:7002` |
| Notification API | `http://localhost:5230` | `https://localhost:7003` |
| API Gateway | `http://localhost:5012` | `https://localhost:7000` |

The gateway routes to the User and Notes HTTPS profiles. The User API's Development configuration calls Notification over `http://localhost:5230/`. If ports or schemes change, update the relevant launch profile, gateway destinations in [ocelot.json](src/ApiGateway/Fundoo.ApiGateway/ocelot.json), and `NotificationService:BaseUrl`.

Run User, Notes, Notification, and API Gateway as startup projects after the dependencies and databases are ready. Trust the ASP.NET Core HTTPS development certificate when using HTTPS profiles.

## Configuration and secrets

Use User Secrets for credentials and keys. Never commit real credentials. The settings below use the configuration keys consumed by this checkout.

### User API

```powershell
$userProject = "src/Services/User/Fundoo.User.Api"
dotnet user-secrets set "ConnectionStrings:UserDb" "Host=localhost;Port=5432;Database=fundoo_users;Username=postgres;Password=..." --project $userProject
dotnet user-secrets set "Jwt:SigningKey" "<strong random key of at least 32 bytes>" --project $userProject
dotnet user-secrets set "Authentication:Google:ClientId" "<Google OAuth client ID>" --project $userProject
dotnet user-secrets set "Authentication:Google:ClientSecret" "<Google OAuth client secret>" --project $userProject
dotnet user-secrets set "NotificationService:BaseUrl" "http://localhost:5230/" --project $userProject
```

Register `https://localhost:7001/signin-google` as the Google OAuth client's redirect URI. The callback links the Google identity and then requires email OTP verification before issuing a Fundoo access token.

The Redis connection defaults to `localhost:6379` in User API appsettings. Override `Redis:ConnectionString` with User Secrets if needed.

### Notes API

```powershell
$notesProject = "src/Services/Notes/Fundoo.Notes.Api"
dotnet user-secrets set "ConnectionStrings:NotesDb" "Host=localhost;Port=5432;Database=fundoo_notes;Username=postgres;Password=..." --project $notesProject
dotnet user-secrets set "Jwt:SigningKey" "<same key as User API>" --project $notesProject
```

Notes and User validate the same user access tokens, so the JWT issuer, audience, and signing key settings must agree. Kafka defaults to `localhost:9092`; override `Kafka:BootstrapServers` when needed.

### Notification API

```powershell
$notificationProject = "src/Services/Notification/Fundoo.Notification.Api"
dotnet user-secrets set "EmailSettings:Host" "<SMTP host>" --project $notificationProject
dotnet user-secrets set "EmailSettings:Port" "<SMTP port>" --project $notificationProject
dotnet user-secrets set "EmailSettings:SenderEmail" "<sender email>" --project $notificationProject
dotnet user-secrets set "EmailSettings:Username" "<SMTP username>" --project $notificationProject
dotnet user-secrets set "EmailSettings:Password" "<SMTP password>" --project $notificationProject
```

`EmailSettings:Port` has no fallback in the current SMTP service and must be set. The consumer and dead letter publisher use the Kafka configuration in appsettings; override it with User Secrets if Kafka is not at `localhost:9092`.

## Database initialization

Apply the User EF migrations:

```powershell
dotnet ef database update --project src/Services/User/Fundoo.User.Infrastructure --startup-project src/Services/User/Fundoo.User.Api
```

The EF tooling needs `ConnectionStrings:UserDb` available in its design-time configuration. The Notes API uses SQL commands directly and does not apply schema changes at startup; run its schema SQL script manually. There is no Notification database or Notification database script in this checkout.

## Kafka flow

The Notes API publishes `notes.created.v1` and `notes.updated.v1` events. The Notification API consumes these topics and sends note activity email. The Compose `kafka-init` service creates both topics and `notes.events.dead-letter.v1`. The dead letter topic stores malformed events that cannot be processed.

## Health checks

User and Notes expose `/health/live` and `/health/ready`. Notes readiness checks its PostgreSQL connection. User requires its PostgreSQL and Redis connections for normal operation.

## Local Docker and k3s deployment

The deployment uses one container per service and a single-node k3s cluster. NGINX is the only public entry point (NodePort 30080); Ocelot remains the internal API gateway. User, Notes, and Notification APIs, PostgreSQL, Redis, and Kafka are ClusterIP services.

```text
Client -> NGINX :30080 -> Ocelot -> ClusterIP API -> Pod
                           |-> User API
                           |-> Notes API
                           `-> Swagger UI and API specs
```

### Local first

1. Copy `.env.local.example` to `.env.local` and set a long random `JWT_SIGNING_KEY` and `POSTGRES_PASSWORD`. Use an alphanumeric database password so it can be embedded safely in the connection string. Set SMTP and Google OAuth values if those features are needed.
2. Run `bash ./deploy.sh local` from Git Bash or WSL with Docker Desktop's Linux engine running. This builds tagged images, starts PostgreSQL, Redis, Kafka, migrations, all APIs, Ocelot, and NGINX, then checks each health status, Swagger docs, 404 handling, and recovery after restarting Notes.
3. Open `http://localhost:8080/swagger`.

### Deploy to a VM running k3s

1. Install k3s on the VM with its packaged Traefik disabled (for example, install with `--disable=traefik`); this deployment uses the NGINX reverse-proxy Deployment in `k8s/nginx/` and does not use Traefik or ingress-nginx.
2. Copy `.env.k8s.example` to `.env.k8s`, set `POSTGRES_PASSWORD` and a random `JWT_SIGNING_KEY` of at least 32 characters, then configure SMTP and Google OAuth values as required. Keep both env files private; they are ignored by git.
3. Ensure the SSH account can run `sudo k3s` without an interactive password prompt. From Git Bash/WSL, pass the private key path if it is not loaded in your SSH agent: `VM_SSH_KEY=/d/Downloads/FundoVM1_key.pem bash ./deploy.sh vm azureuser@VM_HOST`. The script first completes local build and route checks, imports the images into the VM's containerd, applies the manifests, waits for rollouts and the EF migration job, probes Swagger and 404 routes, restarts Notes, and confirms route recovery.
4. Allow inbound TCP port `30080` in the VM firewall/cloud security group, then open `http://VM_IP:30080/swagger`.

The Kubernetes data PVCs use the single-node cluster's default StorageClass (k3s local-path). Use backups before relying on this single-node setup for durable production data. Secrets are generated by `deploy.sh` from `.env.k8s`; the generated Secret is sent over SSH and is not committed.

### Troubleshooting

- Docker daemon unavailable: start Docker Desktop and select the Linux container engine; `docker info` must succeed.
- App containers unhealthy: inspect with `docker compose --env-file .env.local -f compose.local.yaml logs SERVICE`.
- Pods stuck waiting for images: confirm `docker save`/`k3s ctr images import` completed and image names show with `sudo k3s ctr images list`.
- PostgreSQL PVC or init problems: inspect `kubectl logs -n apps statefulset/postgres` and `kubectl describe pod -n apps -l app=postgres`. Init scripts run only when the PVC is first initialized.
- Migration job failed: `kubectl logs -n apps job/user-migrate`; check the `fundoo-secrets` DB connection and that PostgreSQL is Ready.
- External request timeout: verify the VM has port `30080` open and inspect `kubectl get pods,svc -n apps` and `kubectl logs -n apps deployment/nginx`.
- Swagger docs return 502: inspect `api-gateway` and each API pod; gateway routes expect service port 8080.

> Runtime note: the selected .NET 8 target reached end of support on November 10, 2026. The images use the final 8.0.34 runtime and 8.0.428 SDK as requested, but plan a framework upgrade before relying on this for a public production service. See the official [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
