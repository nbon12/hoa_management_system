#!/usr/bin/env bash
# Delete the per-PR Stripe TEST-mode webhook endpoint(s) on teardown/reclaim (013 D9, FR-006/FR-008).
# Idempotent: finds endpoints by metadata[pr] and deletes them; a missing endpoint is a no-op.
#
# Env:
#   STRIPE_SECRET_KEY_TEST   shared Stripe test secret (sk_test_…)
#   PR_NUMBER                pull-request number
set -euo pipefail

: "${STRIPE_SECRET_KEY_TEST:?}" "${PR_NUMBER:?}"

api() { curl -sS -u "${STRIPE_SECRET_KEY_TEST}:" "$@"; }

ids="$(api https://api.stripe.com/v1/webhook_endpoints?limit=100 \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print('\n'.join(e['id'] for e in d.get('data',[]) if e.get('metadata',{}).get('pr')=='${PR_NUMBER}'))")"

if [ -z "$ids" ]; then
  echo "No Stripe webhook endpoint for PR ${PR_NUMBER} (already clean)."
  exit 0
fi

while IFS= read -r id; do
  [ -z "$id" ] && continue
  api -X DELETE "https://api.stripe.com/v1/webhook_endpoints/${id}" >/dev/null && echo "Deleted ${id}"
done <<< "$ids"
