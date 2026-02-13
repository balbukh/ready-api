#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────
# demo.sh — End-to-end invoice extraction demo
#
# Usage:
#   ./scripts/demo.sh path/to/invoice.pdf
#
# Environment:
#   READY_BASE_URL  (default: http://localhost:5273)
#   READY_API_KEY   (default: demo-key-123)
# ──────────────────────────────────────────────────────────────

# ── Check dependencies ────────────────────────────────────────

if ! command -v jq &>/dev/null; then
  echo "❌ jq is required but not installed."
  echo "   Install it with:  brew install jq  (macOS)"
  echo "                     apt-get install jq  (Debian/Ubuntu)"
  exit 2
fi

if ! command -v curl &>/dev/null; then
  echo "❌ curl is required but not installed."
  exit 2
fi

# ── Arguments & config ────────────────────────────────────────

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <path-to-invoice-file>"
  exit 1
fi

FILE="$1"
if [[ ! -f "$FILE" ]]; then
  echo "❌ File not found: $FILE"
  exit 1
fi

BASE_URL="${READY_BASE_URL:-http://localhost:5273}"
API_KEY="${READY_API_KEY:-demo-key-123}"

echo "══════════════════════════════════════════════════"
echo "  Ready — Invoice Extraction Demo"
echo "══════════════════════════════════════════════════"
echo "  File:     $FILE"
echo "  API:      $BASE_URL"
echo "  API Key:  ${API_KEY:0:8}..."
echo "══════════════════════════════════════════════════"
echo

# ── Step 1: Ingest ────────────────────────────────────────────

echo "📤 Uploading file..."
INGEST_RESPONSE=$(curl -s -w "\n%{http_code}" \
  -X POST "${BASE_URL}/ingest/invoice" \
  -H "X-Api-Key: ${API_KEY}" \
  -F "file=@${FILE}")

HTTP_CODE=$(echo "$INGEST_RESPONSE" | tail -1)
BODY=$(echo "$INGEST_RESPONSE" | sed '$d')

if [[ "$HTTP_CODE" != "200" ]]; then
  echo "❌ Ingest failed (HTTP $HTTP_CODE):"
  echo "$BODY"
  exit 1
fi

DOCUMENT_ID=$(echo "$BODY" | jq -r '.documentId')
IS_NEW=$(echo "$BODY" | jq -r '.isNew')

echo "✅ Document ingested"
echo "   Document ID: $DOCUMENT_ID"
echo "   New:         $IS_NEW"
echo

# ── Step 2: Poll for results ──────────────────────────────────

TIMEOUT=60
INTERVAL=2
ELAPSED=0

echo "⏳ Waiting for extraction (timeout: ${TIMEOUT}s)..."

while [[ $ELAPSED -lt $TIMEOUT ]]; do
  RESULT_RESPONSE=$(curl -s -w "\n%{http_code}" \
    "${BASE_URL}/results/${DOCUMENT_ID}?type=InvoiceExtract&version=v1" \
    -H "X-Api-Key: ${API_KEY}")

  RESULT_CODE=$(echo "$RESULT_RESPONSE" | tail -1)
  RESULT_BODY=$(echo "$RESULT_RESPONSE" | sed '$d')

  if [[ "$RESULT_CODE" == "200" ]]; then
    echo
    echo "══════════════════════════════════════════════════"
    echo "  ✅ Invoice Extracted!"
    echo "══════════════════════════════════════════════════"
    echo
    echo "$RESULT_BODY" | jq -r '
      "  Invoice #:  \(.payload.invoiceNumber // "—")",
      "  Date:       \(.payload.invoiceDate // "—")",
      "  Seller:     \(.payload.sellerName // "—")",
      "  Total:      \(.payload.total // "—") \(.payload.currency // "")",
      ""
    '

    echo "── Full payload ──"
    echo "$RESULT_BODY" | jq '.payload'
    exit 0
  fi

  printf "   %2ds / %ds ...\r" "$ELAPSED" "$TIMEOUT"
  sleep "$INTERVAL"
  ELAPSED=$((ELAPSED + INTERVAL))
done

# ── Timeout ───────────────────────────────────────────────────

echo
echo "❌ Timeout: extraction did not complete within ${TIMEOUT}s."
echo
echo "── Document status ──"
curl -s "${BASE_URL}/documents/${DOCUMENT_ID}" \
  -H "X-Api-Key: ${API_KEY}" | jq '.' 2>/dev/null || echo "(could not fetch status)"
exit 1
