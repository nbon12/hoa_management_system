# Feature Specification: Ephemeral per-PR test environments

**Feature Branch**: `013-ephemeral-pr-envs`  
**Created**: 2026-06-20  
**Status**: Draft  
**Input**: User description: "Spin up real ephemeral per-PR environments (own R2 bucket, Neon database branch, Cloud Run service, Pages preview) provisioned on PR open and torn down on close/merge, so PR tests run against real infrastructure before merge (shift-left)."

## Why this feature exists *(context)*

The current pipeline validates a pull request against local substitutes (MinIO standing in for Cloudflare R2, a Testcontainer database) and then, only **after merge**, deploys to a single shared Dev environment and runs the real-stack smoke gate there. That ordering has repeatedly caused defects to reach `main` before being detected, and has produced failure modes that are artifacts of the shared, post-merge model rather than real product bugs:

- A storage-upload incompatibility that only manifested against real R2 (MinIO accepted the same calls) shipped to `main` and broke document downloads in Dev.
- A deadlock where a fix could not be promoted because the post-merge smoke gate — which exercises the *already-promoted* shared revision — required the fix to already be live.
- Smoke tests mutating shared Dev data (claiming a property, disabling auto-pay), forcing test-only cleanup endpoints and leaving persistent side effects.

This feature shifts validation **left**: every pull request gets its own disposable, production-like environment built from real services, so PR checks exercise the PR's actual code against the actual infrastructure it will use, before merge.

## Clarifications

### Session 2026-06-20

- Q: Which PRs qualify for an environment (the provisioning trigger)? → A: Auto-provision on any non-draft PR whose diff touches application or infrastructure paths (path-filtered); documentation-/spec-only PRs are skipped automatically.
- Q: What is the maximum environment lifetime before reclaim? → A: 7 days of inactivity (no new commits), with the clock reset on each push; no absolute-age cap. A reclaimed still-open PR is re-provisioned automatically on its next qualifying push, or on demand via a manual workflow re-run.
- Q: How are forked / untrusted PRs gated from provisioning billable resources? → A: Fork / non-collaborator PRs never provision (only same-repo PRs from members/owners do). Enforced by GitHub's native trust — the repo's "Require approval for all external contributors" Actions setting plus a workflow guard (`pull_request` event, not `pull_request_target`, with a `head.repo.full_name == github.repository` job condition) — not a hand-maintained allowlist. No fork-PR provisioning-on-approval flow in scope.
- Q: How are external integrations (Stripe/SendGrid/Twilio) handled per-environment? → A: Real provider test/sandbox mode (Stripe test keys, SendGrid sandbox, Twilio test credentials) with live inbound webhook delivery to each PR's endpoint — true end-to-end, no real charges/sends, no production credentials; not stubbed.
- Q: What budget ceiling should SC-008 assert, and where is it configured? → A: $25/month total PR-environment spend with a billing alert at 80%, defined as version-controlled code via a GCP `google_billing_budget` resource in OpenTofu (amount as a tfvar, filtered by the `pr-env` label) and validated by the existing IaC checks. Alert-only; an automatic hard-cap (disable-billing automation) is out of scope. CI minutes are free (public repo, standard runners), so Cloud Run is the dominant cost.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - PR validated against real storage and database (Priority: P1)

When a contributor opens a pull request that touches application or infrastructure code, the PR automatically gets an isolated database (a fresh branch of the production-shaped schema, seeded deterministically) and an isolated object-storage bucket using the **same storage technology used in production** (not a local substitute). The PR's automated tests run against these real resources.

**Why this priority**: This is the single highest-value slice. The root cause of the most damaging recent incident was that the local storage substitute behaved differently from the real service, so the defect was invisible until after merge. Real storage + an isolated real database on every PR closes that gap and eliminates cross-PR data interference, and it delivers value even without a per-PR application deployment.

**Independent Test**: Open a PR that exercises a document upload/download path; confirm the PR's checks fail when the code is incompatible with the real storage service and pass when it is compatible — entirely from PR checks, with no shared-environment dependency and no effect on any other PR.

> **Implementation note**: the backend xUnit suite always uses Testcontainers + MinIO (its fixture sets the connection/storage before the app starts), so it cannot be redirected at real services by env vars. The real-storage **incompatibility detection** is therefore realized **end-to-end** through the deployed PR app's Playwright document/payment flows (US2), while US1's provisioning step adds a real-resource **reachability** assertion (Neon branch host + an `aws s3 ls` of the per-PR R2 bucket) as the infra-layer evidence. In practice this couples Scenario 2's executable check to the US2 deployment.

**Acceptance Scenarios**:

1. **Given** a PR is opened that touches application or infrastructure code, **When** its checks begin, **Then** an isolated database and an isolated real-storage bucket dedicated to that PR are available to the tests.
2. **Given** a code change that is incompatible with the real storage service, **When** the PR's storage tests run, **Then** the checks fail on that PR (before merge) with a diagnosable error.
3. **Given** two PRs open simultaneously, **When** both run their tests, **Then** neither PR's data or storage is visible to or mutated by the other.
4. **Given** a PR's database, **When** tests run, **Then** it is seeded to a known, deterministic state independent of any other environment's history.

---

### User Story 2 - PR validated end-to-end against its own running application (Priority: P2)

Each qualifying pull request gets its own deployed application instance (its own API service and its own web preview) wired to that PR's database and storage, and the real-stack/browser smoke tests run against that instance. Because the instance runs the PR's code directly, there is no "candidate vs. promoted" split and no promotion step gating the tests.

**Why this priority**: This removes the post-merge promotion deadlock entirely and catches runtime/configuration issues (cross-origin access, startup, environment-specific configuration) on the PR rather than after merge. It depends on P1's per-PR data/storage to be meaningful.

**Independent Test**: Open a PR that changes a user-facing flow; confirm the browser smoke tests run against a URL serving that PR's code and that a regression in the flow fails the PR's checks before merge.

**Acceptance Scenarios**:

1. **Given** a qualifying PR, **When** its environment is provisioned, **Then** a web preview and an API instance running that PR's code are reachable and wired to that PR's database and storage.
2. **Given** the PR's web preview origin, **When** the application calls its API, **Then** cross-origin access is permitted for that environment without manual configuration.
3. **Given** the smoke tests run against the PR's instance, **When** a user-facing regression exists in the PR, **Then** the PR's checks fail before merge.
4. **Given** the PR's instance, **When** tests authenticate and exercise flows, **Then** they do so without depending on, or mutating, any shared long-lived environment.

---

### User Story 3 - Automatic teardown and cost control (Priority: P3)

When a pull request is merged or closed, its environment and all of its dedicated resources (database branch, storage bucket, application instance, web preview, secrets) are automatically destroyed. Environments that outlive a maximum lifetime or exceed cost guardrails are reclaimed automatically, and orphaned resources are detectable and cleaned up.

**Why this priority**: Real per-PR infrastructure costs money and accumulates risk if it leaks. Reliable teardown and guardrails make the approach sustainable; without them the feature becomes a cost and security liability. It depends on P1/P2 existing to have anything to tear down.

**Independent Test**: Merge or close a PR and confirm every dedicated resource for that PR is gone within the target window; force a stuck provision/teardown and confirm the orphan is detected and reclaimed.

**Acceptance Scenarios**:

1. **Given** a PR is merged or closed, **When** teardown runs, **Then** all resources dedicated to that PR are destroyed within the target window and none remain billable.
2. **Given** a PR environment exceeds its maximum lifetime, **When** the reclaim process runs, **Then** the environment is destroyed automatically and the PR is notified.
3. **Given** a provisioning or teardown step fails midway, **When** the reclaim/repair process runs, **Then** partially-created resources are detected and removed (no silent orphans).
4. **Given** a PR is reopened or force-updated, **When** checks rerun, **Then** the environment is recreated or refreshed cleanly without leftover state from the prior run.

---

### Edge Cases

- **Concurrent PRs**: many open PRs each need a uniquely-named, non-colliding environment (names are namespaced by PR number). Given the modest-concurrency assumption, if a resource quota is exhausted the provisioning check **fails fast with a clear message** (it does not queue); the PR re-provisions on its next push once capacity frees up. Queueing is out of scope unless concurrency grows.
- **Provisioning failure**: if an environment cannot be created, the PR check must fail clearly (not hang) and must not leave partial resources behind.
- **Teardown failure / orphans**: a failed teardown must not silently leak resources; there must be a sweep that reclaims environments whose PR is already closed.
- **Long-lived PRs**: an open PR that goes idle must not keep an environment alive indefinitely — after 7 days with no new commits the environment is reclaimed (the clock resets on every push, so an actively-developed PR is never reclaimed mid-iteration). A reclaimed PR is re-provisioned on its next push, or on demand via a manual workflow re-run.
- **External integrations**: payment/notification providers (Stripe, SendGrid, Twilio) run per-environment in real test/sandbox mode with live webhook delivery to each PR's endpoint — true end-to-end exercise, no real charges/sends, no production credentials, not stubbed.
- **Secrets**: each environment needs its own credentials; these must never be exposed in logs or to forked-PR contexts, and must be revoked on teardown.
- **Forked / untrusted PRs**: fork / non-collaborator PRs are skipped entirely — they never gain real credentials or provision billable resources. GitHub's external-contributor approval gate and the `head.repo.full_name == github.repository` workflow guard ensure provisioning runs only for same-repo PRs from members/owners.
- **Non-qualifying PRs**: documentation-only or spec-only PRs should not provision an environment (no value, unnecessary cost).
- **Production data isolation**: no environment may contain or connect to real production data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provision, on opening of a qualifying pull request, an environment dedicated to that PR consisting of an isolated database, an isolated object-storage bucket using the same storage technology as production, and (for P2) an application instance and web preview.
- **FR-002**: Each PR environment MUST be isolated such that no PR can read or mutate another PR's data, storage, or running instance.
- **FR-003**: The PR's database MUST be seeded to a deterministic, known state on each environment creation, independent of any other environment's history.
- **FR-004**: The system MUST run the PR's automated checks (including storage and, for P2, real-stack/browser smoke tests) against the PR's own environment, and surface pass/fail status on the pull request before merge.
- **FR-005**: For P2, the PR's web preview MUST be wired to the PR's own API instance and database, and cross-origin access for the PR's preview origin MUST be permitted automatically (no manual per-PR configuration).
- **FR-006**: The system MUST destroy all resources dedicated to a PR automatically when the PR is merged or closed, within a defined target window.
- **FR-007**: The system MUST enforce a maximum environment lifetime of **7 days of inactivity** (no new commits to the PR), resetting the inactivity clock on each push; there is no absolute-age cap. Environments that exceed the inactivity window MUST be reclaimed, notifying the PR. A reclaimed still-open PR's environment MUST be re-provisioned automatically on the PR's next qualifying push, and MAY be re-provisioned on demand without a code change via a manual workflow re-run.
- **FR-008**: The system MUST detect and reclaim orphaned or partially-provisioned resources (e.g., from a failed provision/teardown, or a PR closed without successful teardown) without manual intervention.
- **FR-009**: The system MUST fail the PR check clearly (and not hang) if an environment cannot be provisioned, and MUST NOT leave billable resources behind on such failures.
- **FR-010**: Each environment MUST receive its own credentials/secrets, scoped to that environment, never exposed in logs, and revoked on teardown.
- **FR-011**: No PR environment may contain, reference, or connect to real production data or production resources.
- **FR-012**: The system MUST skip environment provisioning for PRs that do not exercise application or infrastructure behavior (e.g., documentation/spec-only changes).
- **FR-013**: External service integrations (Stripe, SendGrid, Twilio) MUST operate per-environment in the provider's real **test/sandbox mode** (Stripe test keys, SendGrid sandbox, Twilio test credentials) — never production credentials and never live charges/sends. Each environment MUST receive **live inbound webhook/callback delivery** (e.g., Stripe webhooks routed to the per-PR endpoint) so payment and notification flows are exercised genuinely end-to-end; deterministic stubbing is NOT the default for these providers.
- **FR-014**: The system MUST tag/label every provisioned resource with its owning PR so that cost and ownership are attributable and sweeps can identify reclaimable resources.
- **FR-015**: Contributions from forks or non-collaborators MUST NOT provision environments or obtain environment credentials. Only same-repo PRs from repository members/owners qualify. Enforcement MUST rely on GitHub's native trust model — the repository's "Require approval for all external contributors" Actions setting, the `pull_request` event (never `pull_request_target`, so secrets are withheld from fork runs), and a job guard requiring `head.repo.full_name == github.repository` — rather than a hand-maintained username allowlist. Provisioning-on-approval for fork PRs is explicitly out of scope.
- **FR-016**: Environment provisioning and teardown MUST be reproducible and defined as code, consistent with how the project's existing environments are managed.

### Key Entities *(include if feature involves data)*

- **PR Environment**: the unit provisioned per pull request; owns a set of dedicated resources, has a lifecycle (provisioning → ready → torn down/reclaimed), a 7-day inactivity lifetime (reset on each push, no absolute-age cap), an owning PR identifier, and a status reported back to the PR.
- **Isolated Database**: a per-PR database carrying the production-shaped schema, seeded deterministically; contains no production data.
- **Isolated Storage Bucket**: a per-PR object-storage container using the production storage technology; contains no production data.
- **Application Instance (P2)**: the per-PR running API service and web preview serving the PR's code, wired to that PR's data and storage.
- **Environment Credentials**: per-environment secrets, scoped and revocable, never exposed to untrusted contexts.

### Constitution Requirements *(mandatory when applicable)*

- **Database/runtime**: Each PR environment uses an isolated database carrying the same schema as production; startup migrations must apply idempotently to a fresh per-PR database, and connection usage must respect low-connection-count/pooling expectations so many concurrent environments do not exhaust database capacity.
- **File storage**: Each PR environment uses an isolated bucket on the production storage technology (the local substitute is explicitly NOT used for PR validation, since masking real-service behavior is the gap this feature closes); each bucket holds only synthetic seed/test objects and is destroyed on teardown.
- **Security and abuse controls**: Per-environment credentials are scoped, never logged, and revoked on teardown; forked/untrusted PRs cannot provision billable resources or obtain credentials without a trusted gate; no environment touches production data or resources.
- **API contract / API docs**: Per-environment API instances behave as the application normally does; developer-only surfaces (e.g., interactive API docs) follow the same enable/disable rules the application already applies per environment.
- **Observability**: Provisioned resources are tagged with the owning PR (and a `pr-env` label) for cost attribution, the SC-008 budget filter, and orphan sweeps; environment lifecycle events (provision, ready, teardown, reclaim) are visible.
- **Cost guardrail**: a GCP `google_billing_budget` ($25/month, alert at 80%) is defined as OpenTofu code with the amount as a tfvar and validated by the existing IaC checks; it alerts only (no automatic billing hard-cap). CI minutes are free on the public repo's standard runners.
- **Quality gates**: Environment provisioning/teardown is defined as code, reproducible, and itself covered by checks (e.g., teardown reliability, orphan reclaim); changes here must not regress the existing post-merge deploy path.
- **Executable & living spec**: Each mandatory acceptance scenario maps to an automated check (provisioning, isolation, real-storage incompatibility detection, teardown, orphan reclaim) that can be run on demand; this spec stays in sync with the implementation before merge.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of qualifying pull requests run their storage and database tests against real (non-substitute) storage and an isolated database before merge.
- **SC-002**: Incompatibilities with the real storage service (the class of defect that previously reached `main`) are caught on the originating PR rather than after merge, in 100% of cases going forward.
- **SC-003**: A PR environment is ready for tests within 10 minutes of the PR check starting, for the 95th percentile of PRs.
- **SC-004**: 100% of PR environments are fully torn down within 30 minutes of the PR being merged or closed, with zero billable resources remaining.
- **SC-005**: Orphaned environments (PR already closed but resources still present) number zero on a daily sweep, or are auto-reclaimed within one sweep cycle.
- **SC-006**: Concurrent open PRs each receive a fully isolated environment with no observed cross-PR test interference.
- **SC-007**: The post-merge promotion deadlock no longer occurs: a fix can be validated and merged without requiring it to already be live in a shared environment.
- **SC-008**: Total PR-environment spend stays within a **$25/month** ceiling with a billing alert firing at 80% ($20). The ceiling is enforced as version-controlled code (a GCP `google_billing_budget`, amount exposed as an OpenTofu variable, filtered by the `pr-env` resource label) and validated by the existing IaC pipeline; total spend is attributable per PR via that label. (Alert-only tripwire; automatic billing hard-cap is out of scope. CI runner minutes are $0 on the public repo's standard runners, so Cloud Run is the dominant variable cost.)
- **SC-009**: No PR environment ever contains or connects to production data (verified by audit).

## Assumptions

- **Full real stack is in scope**: per the request, environments include real object storage, a real isolated database, a real application instance, and a web preview — not merely real storage in CI. (P1 can ship first and still deliver core value; P2/P3 complete the model.)
- **Existing infrastructure-as-code and managed services are reused**: the project already manages environments as code and uses managed services that support fast, cheap database branching and per-deploy web previews; this feature parameterizes that existing setup per PR rather than introducing a new stack.
- **Non-production credentials only**: payment/notification and other third-party integrations run in sandbox/test mode for all PR environments.
- **Qualifying PRs**: provisioning auto-triggers on any **non-draft** PR whose diff touches application or infrastructure paths (path-filtered); documentation-/spec-only PRs are skipped automatically. No opt-in label or maintainer action is required for trusted/internal PRs (fork handling is gated separately — see Fork policy).
- **Concurrency target**: the expected number of simultaneously-open qualifying PRs is modest (small team); quotas/budgets are sized accordingly and revisited if concurrency grows.
- **Fork policy**: fork / non-collaborator PRs never provision — only same-repo PRs from members/owners do. Enforced by the repository's "Require approval for all external contributors" Actions setting plus a `pull_request` workflow with a `head.repo.full_name == github.repository` guard; no hand-maintained allowlist and no fork-PR provisioning-on-approval flow. (Supporting repo settings: external-contributor approval gate enabled; `GITHUB_TOKEN` default read-only; infra secrets scoped to a required-reviewer GitHub Environment.)
- **Out of scope**: this spec does not address the defects that ephemeral environments would NOT fix (global rate-limiter partitioning, smoke-suite scope curation, and environment-name gating); those are covered by a separate specification so each can be scoped independently.
