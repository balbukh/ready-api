# Runbook
## Build
dotnet build Ready.sln

## DB
docker compose -f ops/docker-compose.yml up -d

## Worker
dotnet run --project src/Ready.Worker/Ready.Worker.csproj

## API
dotnet run --project src/Ready.Api/Ready.Api.csproj

## Demo flow
1) POST /ingest/{customerId}/{workflowName} (multipart)
2) Worker processes job
3) GET /status/{documentId}