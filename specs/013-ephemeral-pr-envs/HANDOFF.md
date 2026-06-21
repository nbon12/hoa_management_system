# Handoff — finish bringing up 013 ephemeral per-PR environments

**Branch:** `013-ephemeral-pr-envs` · **Repo:** `nbon12/hoa_management_system` (PUBLIC)

All **code is implemented and validated** (`tofu validate`/`fmt` green, 4 workflow YAMLs parse,
164/164 frontend unit tests pass, scripts `bash -n` clean). What remains is **operator/live work** that
needs cloud consoles + a real test run. Read `spec.md`, `plan.md`, `research.md`, `quickstart.md`, and
`tasks.md` first — the only unchecked tasks are **T009, T010, T034, T041**.

## Fixed identifiers
- GCP project `nekohoa-dev` · region `us-central1`
- Neon project `super-water-18090867` · inherited role `nekohoa_app`, db `nekohoa`
- Runtime SA `nekohoa-run-dev@nekohoa-dev.iam.gserviceaccount.com`
- Image repo `sakurapatch/nekohoa-api` · Pages project `nekohoa-dev` · state bucket `nekohoa-dev-tfstate`

## T009 — Create + seed the `pr-base` Neon branch
1. Find the Dev `main` branch id in Neon project `super-water-18090867`.
2. Run `scripts/create-pr-base.sh` with `NEON_API_KEY`, `NEON_PROJECT_ID=super-water-18090867`,
   `PARENT_BRANCH_ID=<dev main br-id>` (idempotent; prints the new `pr-base` branch id).
3. **Seed it**: run the backend once with `ASPNETCORE_ENVIRONMENT=Dev` and
   `ConnectionStrings__DefaultConnection` = pr-base pooled connection (or `dotnet run -- --seed`).
   Verify `resident@nekohoa.dev` exists.
4. Keep the **pr-base branch id** and the **`nekohoa_app` password** for T010.

## T010 — GitHub settings, secrets/vars, GCP budget prereqs

### Repo secrets — ALREADY PRESENT (do NOT re-add): 
`GCP_PROJECT_ID`, `GCP_WIF_PROVIDER`, `GCP_DEPLOY_SERVICE_ACCOUNT`, `NEON_API_KEY`,
`CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `STRIPE_SECRET_KEY_TEST`,
`DOCKER_HUB_USERNAME`, `DOCKER_HUB_TOKEN`. Vars present: `GCP_REGION`, `STRIPE_PUBLISHABLE_KEY`.

### Secrets to ADD (missing):
- `NEON_PR_BASE_BRANCH_ID` = the pr-base branch id (T009)
- `NEON_PR_ROLE_PASSWORD` = the `nekohoa_app` password on pr-base (T009)

### Variables to ADD (missing):
- `NEON_PROJECT_ID` = `super-water-18090867`
- `GCP_RUNTIME_SERVICE_ACCOUNT` = `nekohoa-run-dev@nekohoa-dev.iam.gserviceaccount.com`
- *(optional)* `SHARED_SECRET_PREFIX` = `dev` (workflows default to `dev`)

### Environment
Create environment **`pr-preview`** (Settings → Environments) with the owner as **Required reviewer**.
The 3 workflows reference `environment: pr-preview`. NOTE: this gates **every** pr-env run on a manual
approval click — if that friction is unwanted for solo use, instead remove the `environment: pr-preview:`
line from the 3 workflows and rely on repo-level secrets (fork safety still holds via the head-repo guard
+ the external-contributor approval setting below).

### Fork-safety setting (verify it's ON)
Settings → Actions → General → "Fork pull request workflows from outside contributors" →
**"Require approval for all external contributors."**

### GCP budget prereqs (for the SC-008 budget in the Dev root)
- Enable `billingbudgets.googleapis.com` on `nekohoa-dev` (re-apply bootstrap, which now lists it, or
  enable in console).
- The Dev root now has a **new required var `billing_account_id`** (no default) — existing
  `infra-apply`/`infra-plan` will FAIL until `TF_VAR_billing_account_id` (GCP billing account id
  `XXXXXX-XXXXXX-XXXXXX`) is provided wherever those workflows source `TF_VAR_*`.

## T041 — Live end-to-end (one real PR)
Open a non-draft PR touching `HOAManagementCompany/**` or `neko-hoa/**`. Approve if prompted. Watch the
**`pr-env`** workflow build→provision→audit→Stripe webhook→deploy→health→Pages→Cypress/Playwright. Confirm
the sticky PR comment's API `/health`→200 and web URL load, cross-origin works. **Measure SC-003 (≤10 min
to ready).** Push a 2nd commit to confirm refresh/cancel.

## T034 — Teardown
Close/merge the PR. Watch **`pr-env-teardown`**. **Measure SC-004 (≤30 min).** Confirm zero residue for
that PR: no `nekohoa-api-pr-<n>` Cloud Run, no `pr-<n>` Neon branch, no `nekohoa-pr-<n>-documents` R2
bucket, no `pr-<n>-*` secrets, no `state/pr/<n>` object, no Stripe endpoint with `metadata.pr=<n>`. Also
manually run **`pr-env-sweep`** and confirm it's clean.

## Finish
Mark T009/T010/T034/T041 `[X]` in `tasks.md`; report the pr-base id, which secrets/vars were added, the
SC-003/SC-004 times, and any failures. Stripe **test mode only**; never paste secret values anywhere.
