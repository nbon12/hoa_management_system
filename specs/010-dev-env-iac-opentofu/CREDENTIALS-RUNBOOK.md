# Credential-gathering runbook — Dev environment provisioning (010)

> **Handoff brief for an AI assistant helping a human collect cloud credentials.**
> The Infrastructure-as-Code is already written, formatted, and `tofu validate`-clean. The ONLY
> remaining work before provisioning is filling 10 cloud-account credentials into one gitignored
> file, then running a short, ordered sequence of `tofu` commands. Your job is to help the human
> obtain each value and verify it works.

## Context

- **Repo:** `HOAManagementCompany` (full-stack HOA app: .NET 9 API + Angular frontend).
- **Goal of feature 010:** provision the entire **Dev** environment that the existing `009`
  auto-deploy pipeline deploys into — declaratively, with **OpenTofu ≥ 1.8** (installed: v1.12.2).
- **What gets created by `tofu apply`:** a Neon project/branch/db/role; a Google **Cloud Run** service
  `nekohoa-api-dev` + runtime & deployer service accounts + **Workload Identity Federation**; nine
  **GCP Secret Manager** secrets (`dev-*`); a **Cloudflare** Pages project, R2 bucket, and DNS for
  `dev.nekohoa.com` / `api-dev.nekohoa.com`.
- **The file to fill:** `infra/environments/dev/terraform.tfvars`
  (absolute: `/Users/nicholasbonilla/RiderProjects/HOAManagementCompany/infra/environments/dev/terraform.tfvars`).

## CRITICAL security rules (for the assistant)

1. `terraform.tfvars` is **gitignored** — confirmed. It must **never** be committed. Do not move secret
   values into any tracked file (`*.tfvars.example`, `.tf`, README, etc.).
2. **Never echo raw secret values into the chat/transcript.** When filling the file, write directly to
   disk (e.g. via a script that reads from a source and writes the file) rather than printing values.
3. **Do not overwrite the 4 already-filled values** in `operator_secrets`: `jwt-secret` (freshly
   generated), `stripe-secret-key`, `stripe-webhook-secret`, `scheduler-secret` (pulled from the
   gitignored `HOAManagementCompany/appsettings.Secrets.json`). Only fill the `REPLACE_ME_*` lines.
4. The cloud credentials are **infrastructure accounts the human owns** — you cannot create Google /
   Cloudflare / Neon accounts or mint their tokens. Guide the human; have them paste values, or have
   them run the verification commands.

## State of the IaC (so you don't re-litigate it)

- `tofu fmt -check`, `tofu validate` pass for `environments/dev`, `environments/staging`, and
  `bootstrap/state-bucket`. The community `kislerdm/neon 0.6.3` schema validated — attributes are correct.
- Providers are pinned; `.terraform.lock.hcl` files are committed; `.terraform/` is ignored.
- `.NET` host-environment startup validator + tests were added and pass (18/18).

---

## The 10 placeholders to fill (with line numbers + expected format)

Open `infra/environments/dev/terraform.tfvars`. Replace each `REPLACE_ME_*`:

| Line | Key | Expected value / format |
|------|-----|--------------------------|
| 7  | `gcp_project_id` | GCP **project ID** (not name), e.g. `nekohoa-prod-1234` |
| 21 | `neon_api_key` | Neon API key string (starts `napi_…`) |
| 24 | `cloudflare_api_token` | Cloudflare API token (~40 chars), scoped Pages+R2+DNS |
| 25 | `cloudflare_account_id` | 32-char hex account id |
| 26 | `cloudflare_zone_id` | 32-char hex zone id for `nekohoa.com` |
| 31 | `operator_secrets["sentry-dsn"]` | Sentry DSN `https://…@…ingest….sentry.io/…`, or `""` to disable |
| 34 | `operator_secrets["storage-service-url"]` | `https://<account_id>.r2.cloudflarestorage.com` |
| 35 | `operator_secrets["storage-access-key"]` | R2 Access Key ID |
| 36 | `operator_secrets["storage-secret-key"]` | R2 Secret Access Key |
| 41 | `deploy_alert_webhook_url` | Slack/Discord webhook URL (or placeholder; non-blocking) |

Also note `gcp_region` (line 8, default `us-central1`) and `neon_region_id` (line 20, default
`aws-us-east-1`) — defaults are fine unless the human prefers another region.

Quick check that nothing is left: `grep -n REPLACE_ME infra/environments/dev/terraform.tfvars`
(should return only the comment on line 4 once done — that line can stay).

---

## Prerequisites the human must have (verify first)

- **A GCP project with billing enabled.** Verify: `gcloud projects describe <PROJECT_ID>`.
- **`nekohoa.com` added as a Cloudflare zone** (registered + nameservers pointed at Cloudflare).
  Without the zone, `cloudflare_zone_id` won't exist and DNS records will fail.

---

## Where to get each value (click paths)

### GCP — `gcp_project_id` + authentication
1. https://console.cloud.google.com → project dropdown (top bar) → copy the **ID** column.
2. Install SDK + authenticate (the login is interactive):
   ```
   brew install --cask google-cloud-sdk
   gcloud auth application-default login        # browser; use the account that owns the project
   gcloud config set project <PROJECT_ID>
   ```
   ADC is how `tofu` authenticates to GCP — no JSON key goes in the tfvars.

### Neon — `neon_api_key`
1. https://console.neon.tech → profile (bottom-left) → **Account settings → API keys**
   (direct: https://console.neon.tech/app/settings/api-keys).
2. **Create new API key** → name `nekohoa-iac` → **copy now** (shown once).
3. Region IDs (if changing line 20): https://neon.tech/docs/introduction/regions.

### Cloudflare — account id, zone id
1. https://dash.cloudflare.com → click the **`nekohoa.com`** zone.
2. **Overview** page → right sidebar **API** section → copy **Account ID** and **Zone ID**.

### Cloudflare — `cloudflare_api_token` (Pages + R2 + DNS)
1. https://dash.cloudflare.com/profile/api-tokens → **Create Token → Create Custom Token**.
2. Permissions (add three rows):
   - **Account → Cloudflare Pages → Edit**
   - **Account → Workers R2 Storage → Edit**
   - **Zone → DNS → Edit**
3. Account Resources: your account. Zone Resources: **Specific zone → nekohoa.com**.
4. **Continue to summary → Create Token** → copy now.

### Cloudflare R2 — `storage-service-url`, `storage-access-key`, `storage-secret-key`
1. Dashboard left nav → **R2** (enable if prompted; needs a payment method, generous free tier).
2. `storage-service-url` = `https://<account_id>.r2.cloudflarestorage.com` (use the account id above).
3. **R2 → Manage R2 API Tokens → Create API token** → permission **Object Read & Write** → **Create**.
   Copy **Access Key ID** and **Secret Access Key** (shown once). (The *bucket* itself is created by
   `tofu`; these are just the S3-API creds the app uses.)

### Sentry — `sentry-dsn` (optional)
1. https://sentry.io → org → **Projects** (create a .NET project if none) →
   **Settings → Client Keys (DSN)** → copy DSN.
2. If not wanted in Dev, set the value to `""`.

### Deploy alert webhook — `deploy_alert_webhook_url`
- Slack: channel → **Apps → Incoming Webhooks** → copy `https://hooks.slack.com/services/…`.
- Discord: Server Settings → **Integrations → Webhooks → New Webhook → Copy Webhook URL**.
- Non-blocking: a placeholder is fine; update later.

---

## Verify each credential BEFORE applying (cheap pre-checks)

Run these to catch a bad token before a long `apply` (substitute the real value; do not print it):

```bash
# GCP — confirms ADC + project access
gcloud auth application-default print-access-token >/dev/null && echo "GCP ADC ok"
gcloud projects describe "$GCP_PROJECT_ID" >/dev/null && echo "GCP project ok"

# Cloudflare token — must report "This API Token is valid and active"
curl -s -H "Authorization: Bearer $CF_TOKEN" \
  https://api.cloudflare.com/client/v4/user/tokens/verify | grep -q '"status":"active"' \
  && echo "CF token ok"

# Cloudflare zone id resolves to nekohoa.com
curl -s -H "Authorization: Bearer $CF_TOKEN" \
  "https://api.cloudflare.com/client/v4/zones/$CF_ZONE_ID" | grep -q '"name":"nekohoa.com"' \
  && echo "CF zone ok"

# Neon key — lists projects (HTTP 200)
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $NEON_KEY" \
  https://console.neon.tech/api/v2/projects   # expect 200
```

---

## End-to-end run order (after the file is filled)

```bash
# 1) One-time: create the versioned GCS state bucket (uses LOCAL state)
cd infra/bootstrap/state-bucket
tofu init
tofu apply -var gcp_project_id=<PROJECT_ID> -var bucket_name=<GLOBALLY_UNIQUE_BUCKET>

# 2) Point the Dev backend at that bucket:
#    edit infra/environments/dev/backend.tf line 9 →
#      bucket = "<GLOBALLY_UNIQUE_BUCKET>"     (replaces REPLACE_WITH_STATE_BUCKET_FROM_BOOTSTRAP)

# 3) Plan (GO/NO-GO gate — review against the matrix, change nothing yet)
cd ../../environments/dev
tofu init                       # wires the GCS backend
tofu plan -out tf.plan          # confirm names: nekohoa-api-dev, ASPNETCORE_ENVIRONMENT=Dev, dev-* secrets

# 4) Apply — TWO STEPS for the TLS cert (api_dns_proxied stays false on first apply):
tofu apply tf.plan
#   …wait for the Cloud Run domain mapping cert to go active for api-dev.nekohoa.com…
#   then set api_dns_proxied = true in terraform.tfvars and:
tofu apply

# 5) Read the wiring values (sensitive ones individually)
tofu output
tofu output -raw wif_provider
tofu output -raw cloudflare_api_token        # sensitive
```

### Important gotchas (do not skip)
- **Two-step cert flow:** the first apply MUST have `api_dns_proxied = false` (grey-cloud) so Google
  can issue the managed cert; only flip to `true` after the cert is active. Flipping too early breaks
  ACME issuance.
- **Cloud Run image is pipeline-owned:** the module sets `ignore_changes` on the image — never
  "fix" the placeholder image in tofu; the 009 deploy job pushes the real `:sha`.
- **WIF is repo-scoped** to `nbon12/hoa_management_system` (in the attribute condition) — both PR
  plans and merge applies authenticate; no branch restriction.

---

## After apply: wire GitHub (operator does this from `tofu output`)

Two consumers need GitHub config; they overlap, so set each secret once.

**Repository SECRETS** (Settings → Secrets and variables → Actions → Secrets):

| GitHub secret | From | Used by |
|---------------|------|---------|
| `GCP_WIF_PROVIDER` | `tofu output -raw wif_provider` | 009 deploy + infra CI |
| `GCP_DEPLOY_SERVICE_ACCOUNT` | `tofu output -raw deployer_service_account` | 009 deploy + infra CI |
| `GCP_PROJECT_ID` | the project id | infra CI |
| `NEON_API_KEY` | the Neon key | infra CI |
| `CLOUDFLARE_API_TOKEN` | `tofu output -raw cloudflare_api_token` | 009 deploy + infra CI |
| `CLOUDFLARE_ACCOUNT_ID` | `tofu output -raw cloudflare_account_id` | 009 deploy + infra CI |
| `CLOUDFLARE_ZONE_ID` | the zone id | infra CI |
| `DEPLOY_ALERT_WEBHOOK_URL` | `tofu output -raw deploy_alert_webhook_url` | 009 deploy + infra CI |
| `TF_VAR_OPERATOR_SECRETS` | JSON of the 8 operator secrets (see below) | infra CI only |

`TF_VAR_OPERATOR_SECRETS` is the `operator_secrets` map as one **JSON object**:
`{"jwt-secret":"…","sentry-dsn":"…","stripe-secret-key":"…","stripe-webhook-secret":"…","storage-service-url":"…","storage-access-key":"…","storage-secret-key":"…","scheduler-secret":"…"}`

**Repository VARIABLES** (… → Variables):

| GitHub variable | From | When |
|-----------------|------|------|
| `GCP_REGION` | `tofu output -raw gcp_region` | with the secrets |
| `DEV_DEPLOY_ENABLED` | literal `true` | **LAST** — only after `https://api-dev.nekohoa.com/health` is green |

**Protected `prod` Environment** (for later, T030): repo **Settings → Environments → New environment
→ `prod`** → enable **Required reviewers**. This is what gates the (currently commented) Prod apply
job in `.github/workflows/infra-apply.yml`.

---

## Acceptance checks (after full apply)

```bash
gcloud run services describe nekohoa-api-dev --region <REGION> --format yaml | \
  grep -E "ASPNETCORE_ENVIRONMENT|containerPort|allUsers|/health|dev-"   # rows 1–8 of the matrix
tofu plan      # MUST report "No changes" — zero drift (SC-003)
curl -fsS https://api-dev.nekohoa.com/health     # healthy before flipping DEV_DEPLOY_ENABLED
```

Reference contracts (read-only, in this repo):
- `specs/010-dev-env-iac-opentofu/contracts/matrix-conformance.md` (name-by-name)
- `specs/010-dev-env-iac-opentofu/contracts/github-actions-wiring.md` (outputs → GitHub)
- `specs/010-dev-env-iac-opentofu/quickstart.md` (operator runbook)
