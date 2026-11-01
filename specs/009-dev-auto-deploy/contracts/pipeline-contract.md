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
if: github.ref == 'refs/heads/main' && github.event_name == 'push' && vars.DEV_DEPLOY_ENABLED == 'true'
needs: [docker-push]      # reuse the image CI already built & pushed
concurrency:
  group: deploy-dev
  cancel-in-progress: true   # latest merged commit wins (FR-009, SC-007)
```

## Inputs

| Input | Source |
|-------|--------|
| Backend image | `${{ vars.GCP_REGION }}-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/images-dev/nekohoa-api:${{ github.sha }}` (Artifact Registry, pushed by `docker-push`). The same digest also goes to Docker Hub `sakurapatch/nekohoa-api` for the nightly Trivy re-scan. *(Amended 2026-10-28 by PR #242.)* |
| GCP auth | Workload Identity Federation (`google-github-actions/auth`) |
| Cloudflare auth | `secrets.CLOUDFLARE_API_TOKEN`, `secrets.CLOUDFLARE_ACCOUNT_ID` |
| Failure alert | `secrets.DEPLOY_ALERT_WEBHOOK_URL` |
| Dev runtime secrets | Google Secret Manager / Cloud Run secret refs (not GitHub) |

## Steps (ordered)

1. **Deploy backend candidate** (no traffic):
   `gcloud run deploy nekohoa-api-dev --image <region>-docker.pkg.dev/<project>/images-dev/nekohoa-api:${SHA}
   --no-traffic --tag candidate --region <region>`. This is an **image-only** deploy: the new revision inherits
   `ASPNETCORE_ENVIRONMENT=Dev`, the secret refs, probes and scaling from the OpenTofu-managed service.
   *(Superseded by specs/010-dev-env-iac-opentofu FR-007, PR #47: re-asserting `--set-env-vars` wiped
   tofu's non-secret env vars and crashed startup. Image path amended by PR #242.)*
   → migrations apply + seed run at candidate startup against the **Neon Dev** DB.
2. **Health gate**: poll the candidate tagged URL `…/health` until `Healthy` (with timeout that
   accommodates startup migration time). Fail the job on timeout/unhealthy.
3. **Deploy frontend preview**: `npm ci`; point `src/environments/environment.dev.ts` at the **candidate** URL; `npm run build -- --configuration=dev`; stamp `_headers` for that origin (`scripts/stamp-headers.mjs`); then `pages deploy dist/neko-hoa/browser --project-name=nekohoa-dev --branch=preview-<sha>`. *(Amended by PR #120, a 020-D follow-up: cookie auth binds the refresh token to its login origin, so the pre-promotion frontend must call the candidate.)*
4. **E2E gates**: (a) the Cypress suite (`npm run e2e:dev`) against the preview URL; (b) read `<SHARED_SECRET_PREFIX or dev>-scheduler-secret` from Secret Manager (PR #100, 017-A) and run the Playwright `@smoke` subset (`npm run e2e:playwright-smoke`) against the preview URL and candidate API (specs/012; curated by specs/014 FR-005, PR #71). Fail the job on any failure.
5. **Promote (only if steps 1–4 pass)**:
   - Backend: `gcloud run services update-traffic nekohoa-api-dev --to-tags candidate=100`.
   - Frontend: rebuild against the stable `https://api-dev.nekohoa.com` origin, restamp `_headers`, and `pages deploy ... --branch=main` (the Pages production branch). The preview build itself is not promoted.
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
