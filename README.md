# Ready API

A robust document processing API built with .NET 10.

## Features

- **Document Ingestion**: Upload documents via `POST /ingest/{workflowName}`.
- **Workflow Processing**: Asynchronous processing with `Ready.Worker`.
- **Deduplication**: Enforced uniqueness for documents based on Customer ID and SHA256 hash.
- **Atomic Job Queue**: Reliable job processing using PostgreSQL `FOR UPDATE SKIP LOCKED`.
- **API Key Authentication**: Secure access via `X-Api-Key` header.

## Prerequisites

- .NET 10 SDK
- Docker (for PostgreSQL)

## getting Started

1.  **Start Database**:
    ```bash
    docker-compose up -d
    ```

2.  **Run Migrations**:
    The API runs migrations automatically on startup, or you can run:
    ```bash
    dotnet ef database update -p src/Ready.Infrastructure -s src/Ready.Api
    ```

3.  **Start API**:
    ```bash
    dotnet run --project src/Ready.Api/Ready.Api.csproj
    ```

4.  **Start Worker**:
    ```bash
    dotnet run --project src/Ready.Worker/Ready.Worker.csproj
    ```

## Testing

Run the end-to-end test script:
```bash
./test_e2e.sh
```
