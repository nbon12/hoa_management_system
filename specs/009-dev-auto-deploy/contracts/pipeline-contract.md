# Contract: `deploy-dev` Pipeline Job

**Feature**: 009-dev-auto-deploy | **File**: `.github/workflows/test.yml` (new job)

This is the operational contract for the auto-deploy stage. It is the "interface" this feature
exposes: the trigger, the gates, and the promotion rules.

## Trigger

```yaml
on:
  push:
    branches: [main]      # already configured in test.yml
```

`deploy-dev` runs only when:

```yaml
if: github.ref == 'refs/heads/main' && github.event_name == 'push'
needs: [docker-push]      # reuse the image CI already built & pushed
concurrency:
  group: deploy-dev
  cancel-in-progress: true   # latest merged commit wins (FR-009, SC-007)
```

## Inputs

| Input | Source |
|-------|--------|
| Backend image | `sakurapatch/nekohoa-api:${{ github.sha }}` (from `docker-push`) |
| GCP auth | Workload Identity Federation (`google-github-actions/auth`) |
| Cloudflare auth | `secrets.CLOUDFLARE_API_TOKEN`, `secrets.CLOUDFLARE_ACCOUNT_ID` |
| Failure alert | `secrets.DEPLOY_ALERT_WEBHOOK_URL` |
| Dev runtime secrets | Google Secret Manager / Cloud Run secret refs (not GitHub) |

## Steps (ordered)

1. **Deploy backend candidate** (no traffic):
   `gcloud run deploy nekohoa-api-dev --image sakurapatch/nekohoa-api:${SHA} --no-traffic --tag candidate
   --region <region> --set-env-vars ASPNETCORE_ENVIRONMENT=Dev --set-secrets <secret refs>`
   → migrations apply + seed run at candidate startup against the **Neon Dev** DB.
2. **Health gate**: poll the candidate tagged URL `…/health` until `Healthy` (with timeout that
   accommodates startup migration time). Fail the job on timeout/unhealthy.
3. **Deploy frontend preview**: `npm ci && npm run build -- --configuration=dev`, then deploy
   `dist/neko-hoa/browser` to Cloudflare Pages as a **preview** deployment.
4. **E2E gate**: run the full E2E suite against the Dev frontend preview URL (API via
   `api-dev` domain or candidate URL). Fail the job on any test failure.
5. **Promote (only if steps 1–4 pass)**:
   - Backend: `gcloud run services update-traffic nekohoa-api-dev --to-tags candidate=100`.
   - Frontend: promote the preview to the Dev production alias.
6. **Notify on failure**: `if: failure()` → POST to `DEPLOY_ALERT_WEBHOOK_URL` with commit + failed
   step. Success emits no chat message (relies on GitHub deployment status).

## Guarantees (postconditions)

| ID | Guarantee |
|----|-----------|
| G1 (FR-001) | Every merge to `main` triggers this job with no manual step. |
| G2 (FR-006/FR-007/SC-003) | A failure in steps 1–4 leaves the prior healthy backend revision and frontend alias serving 100% of traffic; no promote occurs. |
| G3 (FR-002/FR-018) | The deployed artifact is the exact `:${sha}` image CI validated; re-running for the same SHA is idempotent. |
| G4 (FR-004/FR-004a) | Candidate startup applies migrations idempotently and seeds reference/synthetic data. |
| G5 (FR-009/SC-007) | A newer merge cancels an in-flight older deploy; the latest SHA is the one promoted. |
| G6 (FR-008/SC-008) | Status is visible on the GitHub run/deployment; failures additionally notify team chat. |
| G7 (SC-002) | Under normal conditions the change is live in Dev within 30 minutes incl. the E2E gate. |

## Failure modes

| Failure | Behavior |
|---------|----------|
| Image missing / registry outage | `docker-push` failed → `deploy-dev` does not run; prior Dev unchanged. |
| Migration fails at candidate startup | Candidate never becomes healthy → health gate fails → no promote. |
| Health gate timeout | Job fails → no promote → chat alert. |
| E2E failure | Job fails after preview/candidate exist but unpromoted → prior release serves → chat alert. |
| Cloudflare/GCP API error | Step fails → no promote → chat alert. |
| Concurrent newer merge | Older run cancelled before promote (G5). |
