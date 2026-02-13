#!/bin/bash
set -e

# Cleanup previous runs
rm -f test_file.txt
pkill -f "Ready.Api" || true
pkill -f "Ready.Worker" || true

# Start API in background
echo "Starting API..."
dotnet run --project src/Ready.Api/Ready.Api.csproj > api.log 2>&1 &
API_PID=$!

# Wait for API to be ready
echo "Waiting for API to start..."
for i in {1..30}; do
    if grep -q "Now listening on" api.log; then
        echo "API started!"
        break
    fi
    sleep 1
done

# Create dummy file
echo "This is a test file content for deduplication check" > test_file.txt

# Upload file 1
echo "Uploading file (Run 1)..."
RESPONSE1=$(curl -s -X POST http://localhost:5273/ingest/invoice \
  -H "X-Api-Key: demo-key-123" \
  -F "file=@test_file.txt")
echo "Response 1: $RESPONSE1"

DOC_ID1=$(echo $RESPONSE1 | grep -o '"documentId":"[^"]*"' | cut -d'"' -f4)
IS_NEW1=$(echo $RESPONSE1 | grep -o '"isNew":[^,}]*' | cut -d':' -f2 | tr -d ' ')

if [ -z "$DOC_ID1" ]; then
    echo "Error: Failed to get document ID from response 1"
    kill $API_PID
    exit 1
fi

echo "Document ID 1: $DOC_ID1"
echo "Is New 1: $IS_NEW1"

# Upload same file again (Run 2) - Should be deduplicated
echo "Uploading file (Run 2)..."
RESPONSE2=$(curl -s -X POST http://localhost:5273/ingest/invoice \
  -H "X-Api-Key: demo-key-123" \
  -F "file=@test_file.txt")
echo "Response 2: $RESPONSE2"

DOC_ID2=$(echo $RESPONSE2 | grep -o '"documentId":"[^"]*"' | cut -d'"' -f4)
IS_NEW2=$(echo $RESPONSE2 | grep -o '"isNew":[^,}]*' | cut -d':' -f2 | tr -d ' ')

echo "Document ID 2: $DOC_ID2"
echo "Is New 2: $IS_NEW2"

# Verification
if [ "$DOC_ID1" != "$DOC_ID2" ]; then
    echo "FAILURE: Document IDs do not match! Deduplication failed."
    kill $API_PID
    exit 1
fi

if [ "$IS_NEW1" != "true" ]; then
    echo "FAILURE: First upload should be new!"
    kill $API_PID
    exit 1
fi

if [ "$IS_NEW2" != "false" ]; then
    echo "FAILURE: Second upload should NOT be new!"
    kill $API_PID
    exit 1
fi

echo "SUCCESS: Deduplication verified."

# Start Worker to process the job
echo "Starting Worker..."
dotnet run --project src/Ready.Worker/Ready.Worker.csproj > worker.log 2>&1 &
WORKER_PID=$!

# Wait for worker to process job
echo "Waiting for worker to process job..."
for i in {1..30}; do
    if grep -q "Dequeued job" worker.log; then
        echo "Worker dequeued a job!"
        grep "Dequeued job" worker.log
        break
    fi
    sleep 1
done

if ! grep -q "Dequeued job" worker.log; then
    echo "FAILURE: Worker did not pick up the job within timeout."
    echo "Worker Logs:"
    cat worker.log
    kill $API_PID $WORKER_PID
    exit 1
fi

echo "SUCCESS: Worker picked up the job."

# Cleanup
echo "Cleaning up..."
kill $API_PID $WORKER_PID
rm test_file.txt api.log worker.log

echo "ALL TESTS PASSED!"
