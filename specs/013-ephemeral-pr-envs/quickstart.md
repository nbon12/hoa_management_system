# Quickstart: Ephemeral per-PR test environments

**Feature**: 013-ephemeral-pr-envs · Operator + contributor runbook.

## One-time setup (operator)

These are repo/ops settings, not code (see research D10/D11):

1. **Trusted-CI gate** — Repo → Settings → Actions → General →
   *Fork pull request workflows from outside collaborators* → **"Require approval for all
   external contributors."** (Workflow token perms are already read-only.)
2. **Required-reviewer Environment for infra secrets** — Settings → Environments → new
   environment (e.g. `pr-preview`) with yourself as required reviewer; move the Neon /
   Cloudflare / GCP / Docker Hub / Stripe-test secrets into it. The provisioning job
   references `environment: pr-preview`.
3. **Enable billing budgets API** — bootstrap enables `billingbudgets.googleapis.com`;
   supply the billing account id and `pr_env_monthly_budget=25` tfvar; `tofu apply` the
   shared root once so the `google_billing_budget` (alert at 80%) exists.
4. **Seed the base branch** — ensure the `pr-base` Neon branch exists and is seeded (the
   sweep self-heals it from Dev if missing).
5. *(optional)* enable **"Require actions to be pinned to a full-length commit SHA"** after
   confirming all workflows pin actions to SHAs.

## Contributor flow (automatic)

1. Open a **non-draft** PR that touches `HOAManagementCompany/**`, `neko-hoa/**`, or the
   PR-env infra. (Doc/spec-only PRs are skipped — no env, FR-012.)
2. `pr-env.yml` builds the image, provisions the env, registers the Stripe test webhook,
   deploys the Pages branch, and runs the smoke gate.
3. Within ~10 min the PR shows a sticky comment with:
   - **API**: `https://nekohoa-api-pr-<n>-…run.app`
   - **Web**: `https://pr-<n>.nekohoa-dev.pages.dev`
   and a passing/failing check.
4. Push more commits → the env refreshes (concurrency-cancels the in-flight run).
5. Merge or close → `pr-env-teardown.yml` destroys everything within 30 min.

## Reclaim / re-up

- An open PR with **no new commits for 7 days** is reclaimed by the sweep (a comment says so).
- **Re-up**: push any qualifying commit, or re-run `pr-env.yml` from the Actions tab. A fresh
  env rebuilds identically; both behaviors reset the 7-day clock.

## Verifying the feature (maps to acceptance scenarios)

| Check | How |
|-------|-----|
| US1 real storage/db isolation | Open a PR touching document upload; confirm checks run against `nekohoa-pr-<n>-documents` (real R2) + the `pr-<n>` Neon branch, and a real-R2-incompatible change fails the PR. |
| US1 deterministic seed | Confirm the PR branch logs in with the seed user without a per-PR seed step. |
| US2 end-to-end app | Confirm `pr-<n>.nekohoa-dev.pages.dev` serves the PR's code wired to the PR API; a UI regression fails Playwright. |
| US2 CORS | Confirm the web preview calls the PR API cross-origin with no manual config. |
| US3 teardown | Close the PR; confirm zero resources labeled `pr-env=true,pr-number=<n>` remain (`gcloud run services list`, R2, Neon branches, `state/pr/<n>`). |
| US3 orphan/inactive | `workflow_dispatch` the sweep; confirm a closed-PR env and a >7-day-idle env are destroyed. |
| SC-008 budget | `tofu plan` shows the `google_billing_budget`; trigger an alert test or inspect the budget in GCP Billing. |

## Cost expectation

Scale-to-zero ⇒ idle envs cost ~pennies/day (storage only); CI minutes are free (public repo).
Cloud Run during smoke runs is the only meaningful cost; the $25/mo budget alerts at $20.
