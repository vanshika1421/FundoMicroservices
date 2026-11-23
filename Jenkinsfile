pipeline {
    agent any

    environment {
        DOCKER_HUB_USER = 'vanshika2114'
        IMAGE_NAME = 'fundoo-user'
        PROJECT_PATH = 'src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj'
    }

    stages {
        stage('Checkout Code') {
            steps {
                git branch: 'main', url: 'https://github.com/vanshika1421/FundoMicroservices.git'
            }
        }

        stage('Generate Dockerfile') {
            steps {
                sh '''
cat > Dockerfile <<'EOF'
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT_PATH
WORKDIR /src
COPY . .
RUN dotnet publish "$PROJECT_PATH" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet $(basename $(ls *.runtimeconfig.json | head -n 1) .runtimeconfig.json).dll"]
EOF
                '''
            }
        }

        stage('Build and Push User API') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'docker-hub-creds', passwordVariable: 'DOCKER_PASS', usernameVariable: 'DOCKER_USER')]) {
                    sh '''
                        set -eu
                        echo "$DOCKER_PASS" | docker login -u "$DOCKER_USER" --password-stdin
                        docker build --build-arg PROJECT_PATH="$PROJECT_PATH" -t "$DOCKER_HUB_USER/$IMAGE_NAME:ci-build" .
                        docker push "$DOCKER_HUB_USER/$IMAGE_NAME:ci-build"
                    '''
                }
            }
        }
    }
}
