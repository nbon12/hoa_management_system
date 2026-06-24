#!/usr/bin/env bash
# Register a per-PR Stripe TEST-mode webhook endpoint and store its signing secret (013 D9, FR-013).
# Creates an endpoint pointing at the running per-PR API and writes the whsec_… into the per-PR Secret
# Manager secret the container consumes (Stripe__WebhookSigningSecret). Idempotent: an existing endpoint
# for this PR (metadata[pr]) is reused. Test mode only — never production charges.
#
# Env:
#   STRIPE_SECRET_KEY_TEST   shared Stripe test secret (sk_test_…)
#   PR_NUMBER                pull-request number
#   API_URL                  per-PR Cloud Run base URL (no trailing slash)
#   GCP_PROJECT_ID           project holding the secret
set -euo pipefail

: "${STRIPE_SECRET_KEY_TEST:?}" "${PR_NUMBER:?}" "${API_URL:?}" "${GCP_PROJECT_ID:?}"

WEBHOOK_URL="${API_URL%/}/api/v1/payments/webhooks/stripe"
SECRET_ID="pr-${PR_NUMBER}-stripe-webhook"
EVENTS=(payment_intent.succeeded payment_intent.payment_failed setup_intent.succeeded charge.refunded)

api() { curl -sS -u "${STRIPE_SECRET_KEY_TEST}:" "$@"; }

# Reuse an existing endpoint for this PR if present (idempotent re-provision).
existing="$(api https://api.stripe.com/v1/webhook_endpoints?limit=100 \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print(next((e['id'] for e in d.get('data',[]) if e.get('metadata',{}).get('pr')=='${PR_NUMBER}'),''))")"

if [ -n "$existing" ]; then
  echo "Reusing Stripe webhook ${existing} for PR ${PR_NUMBER}"
  # Endpoint secret is only returned at create; rotate to guarantee we hold the current secret.
  api -X DELETE "https://api.stripe.com/v1/webhook_endpoints/${existing}" >/dev/null
fi

args=(-d "url=${WEBHOOK_URL}" -d "metadata[pr]=${PR_NUMBER}")
for e in "${EVENTS[@]}"; do args+=(-d "enabled_events[]=${e}"); done

resp="$(api https://api.stripe.com/v1/webhook_endpoints "${args[@]}")"
secret="$(printf '%s' "$resp" | python3 -c "import sys,json;print(json.load(sys.stdin).get('secret',''))")"

if [ -z "$secret" ]; then
  echo "ERROR: Stripe did not return a webhook signing secret:" >&2
  printf '%s\n' "$resp" >&2
  exit 1
fi

# Write the signing secret as a new version (becomes `latest`, which the container references).
printf '%s' "$secret" | gcloud secrets versions add "$SECRET_ID" \
  --project="$GCP_PROJECT_ID" --data-file=- >/dev/null
echo "Wrote signing secret to ${SECRET_ID} (latest) for PR ${PR_NUMBER}"
