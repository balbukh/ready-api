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

```bash
dotnet test
```

## Quick Demo

Run the full invoice extraction pipeline end-to-end:

**1. Start Postgres**
```bash
docker compose -f ops/docker-compose.yml up -d
```

**2. Start the API** (terminal 1)
```bash
dotnet run --project src/Ready.Api
```

**3. Start the Worker** (terminal 2)
```bash
dotnet run --project src/Ready.Worker
```

**4. Run the demo** (terminal 3)
```bash
# Uses defaults: http://localhost:5273 + demo-key-123
./scripts/demo.sh path/to/invoice.pdf

# Or with custom settings:
READY_BASE_URL=http://localhost:5273 \
READY_API_KEY=your-api-key \
  ./scripts/demo.sh path/to/invoice.pdf
```

The script uploads the file, polls for the extraction result, and prints a summary:
```
  Invoice #:  INV-001
  Date:       2025-01-15
  Seller:     Acme Corp
  Total:      544.50 EUR
```

**Diagnostics:** If extraction fails (invalid JSON or validation error), an error result is persisted:
```bash
curl "http://localhost:5273/results/{documentId}?type=InvoiceExtractError&version=v1" \
  -H "X-Api-Key: demo-key-123"
```

**CSV Export:** Download the extracted invoice as a CSV file:
```bash
curl -JO "http://localhost:5273/download/{documentId}?type=InvoiceCsv&version=v1" \
  -H "X-Api-Key: demo-key-123"
```

**Check Status:** Monitor processing progress:
```bash
curl -s "http://localhost:5273/status/{documentId}" \
  -H "X-Api-Key: demo-key-123" | jq .
```


> **Requires**: `jq` (`brew install jq` on macOS)

