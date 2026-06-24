# Phase 1 Data Model: Ephemeral per-PR test environments

**Feature**: 013-ephemeral-pr-envs · **Date**: 2026-06-21

This feature introduces **no application database schema changes**. The "entities" below are
infrastructure/lifecycle objects provisioned per pull request and the metadata that ties them
to an owning PR. They are modeled as OpenTofu resources + GitHub-workflow state, not EF Core
tables. Each PR environment's per-PR Neon branch carries the **existing** production-shaped
schema (seeded deterministically); no new tables, columns, or migrations are added by this feature.

## Entity: PR Environment

The unit provisioned per qualifying pull request. Identified by the PR number.

| Field | Type | Source / Notes |
|-------|------|----------------|
| `pr_number` | int | `github.event.number`; the identity key for all resources and state |
| `head_sha` | string | Commit under test; feeds the image tag `pr-<n>-<sha>` |
| `state_prefix` | string | `state/pr/<n>` in bucket `nekohoa-dev-tfstate` |
| `aspnet_environment` | enum | Always `Dev` (config-validation known set) |
| `status` | enum | `provisioning → ready → reclaimed/torn-down` (also `failed`) |
| `last_activity_at` | timestamp | Last commit time (GitHub API); drives 7-day inactivity reclaim |
| `labels` | map | `pr-env=true`, `pr-number=<n>` on GCP resources (budget + sweep) |
| `urls` | object | `{ api: <cloud-run-url>, web: pr-<n>.nekohoa-dev.pages.dev }` reported to the PR |

**Lifecycle / state transitions**:

```
(qualifying push) ──▶ provisioning ──success──▶ ready ──▶ checks run, status on PR
       ▲                   │                       │
       │                 failure                   ├─ PR merged/closed ─▶ torn-down (D7)
       │                   ▼                       ├─ inactive > 7 days ─▶ reclaimed (D8)
       │            failed (no billable            └─ orphaned (PR closed,
       │            resources left, FR-009)            env present) ─▶ reclaimed (D8)
       └──────────── next qualifying push re-provisions (FR-007) ◀──────┘
```

- **No absolute-age cap**: every push resets `last_activity_at`; only 7 days of *inactivity*
  triggers reclaim. A reclaimed-but-open PR re-provisions on its next push or a manual re-run.

## Owned resources (created & destroyed per PR)

| Resource | Name pattern | Tech | Isolation guarantee |
|----------|--------------|------|---------------------|
| Neon branch + endpoint + role + db | `pr-<n>` (forked from `pr-base`) | Neon Postgres (copy-on-write) | Own database; no cross-PR visibility (FR-002) |
| Cloud Run service | `nekohoa-api-pr-<n>` | Cloud Run v2 (scale-to-zero) | Own instance wired to this PR's DB+bucket |
| R2 bucket | `nekohoa-pr-<n>-documents` | Cloudflare R2 | Own object store; synthetic seed objects only (FR-011) |
| Pages branch deploy | branch `pr-<n>` on `nekohoa-dev` project | Cloudflare Pages | Own URL `pr-<n>.nekohoa-dev.pages.dev` |
| DB-connection secret | `pr-<n>-db-connection` | GCP Secret Manager | Per-PR; never logged; deleted on teardown (FR-010) |
| Stripe webhook secret | `pr-<n>-stripe-webhook` | GCP Secret Manager | Per-PR signing secret for the PR's webhook endpoint |
| Stripe webhook endpoint | one per PR (test mode) | Stripe API | Routes test events to this PR's API (FR-013) |
| OpenTofu state | object at `state/pr/<n>` | GCS (versioned) | Per-PR state lineage; deleted on teardown |

## Referenced shared primitives (never recreated per PR)

GCP project · Neon **project** `super-water-18090867` · WIF pool/provider `github-pool-dev` ·
runtime + deployer service accounts · 8 shared operator secrets (jwt, sentry, **Stripe test**
secret key, storage access/secret keys, scheduler) · `Stripe:PublishableKey` (test) ·
SendGrid + Twilio sandbox credentials · the `nekohoa-dev` Pages project · the `pr-base` Neon
branch · the state bucket `nekohoa-dev-tfstate`.

## Entity: Environment Credentials

Per-environment secrets, scoped and revocable (FR-010).

| Field | Type | Notes |
|-------|------|-------|
| `db_connection` | secret | Pooled Neon connection string for the PR branch (per-PR) |
| `stripe_webhook_secret` | secret | Signing secret for the PR's Stripe test webhook (per-PR) |
| `shared_operator_secrets` | secret refs | Reused from Dev (test mode); not duplicated per PR |

**Rules**: never emitted to logs or to fork-PR contexts; created during provisioning; deleted
during teardown/reclaim; only the per-PR secrets are unique, the rest are shared references.

## Validation / invariants

- **V1 (isolation)**: distinct `pr_number` ⇒ disjoint Neon branch, R2 bucket, Cloud Run
  service, and secret names; no resource name is shared across PRs (FR-002, SC-006).
- **V2 (no production data)**: branches fork only from `pr-base`/Dev-shaped seed; no PR env
  references production resources (FR-011, SC-009).
- **V3 (deterministic seed)**: a fresh PR branch carries the deterministic seed (via `pr-base`),
  and startup seeding is idempotent (FR-003).
- **V4 (clean failure)**: a failed provision leaves no billable resources — partial state is
  destroyed by the same run or the sweep (FR-008, FR-009).
- **V5 (attribution)**: every GCP resource carries `pr-env=true` + `pr-number=<n>` so cost is
  attributable and the sweep can identify reclaimable resources (FR-014, SC-008).
- **V6 (lifetime)**: an env is reclaimed after 7 days with no new commits; the clock resets on
  every push; there is no absolute-age cap (FR-007).
