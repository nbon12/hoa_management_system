---
description: "Task list for Trivy Security Scanning Pipeline (011)"
---

# Tasks: Trivy Security Scanning Pipeline

**Input**: Design documents from `/specs/011-trivy-security-scanning/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/scan-workflow.md, quickstart.md

**Tests**: This is a CI/CD + infrastructure feature with no executable application code, so the
constitution's xUnit/Karma layers do not apply. "Tests" here are **fixture-based validation runs**
(per research D8) that exercise each user story's acceptance scenarios; they are included inline in
each story's phase.

**Organization**: Tasks are grouped by user story. Note a structural constraint specific to this
feature: User Story 1 and User Story 2 both add jobs to the **same** file
(`.github/workflows/security-scan.yml`), so their edits are sequential within that file even though
the jobs are logically independent. The `test.yml` edit (US1) is a different file and can proceed in
parallel with the `security-scan.yml` edits.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)

## Path Conventions

CI/infra feature — no `src/` tree. Deliverables live at:
`.github/workflows/security-scan.yml` (new), `.github/workflows/test.yml` (modified),
`.trivyignore` (new, repo root), `.github/dependabot.yml` (new).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Files and skeleton both stories build on.

- [ ] T001 [P] Create `.trivyignore` at the repo root with a header explaining the format (one CVE/misconfig ID per line + `# reason — owner — review/expiry` comment); start empty (FR-015)
- [ ] T002 Create `.github/workflows/security-scan.yml` skeleton: `name`, `on:` (`pull_request` → `main`, `push` → `main`, `schedule` nightly `cron: '0 7 * * *'`), job-level `permissions` (`contents: read`, `security-events: write`, `pull-requests: read`), and top-level `env` (`TRIVY_SEVERITY: 'CRITICAL,HIGH'`, `IMAGE_LOCAL_TAG`, `PUBLISHED_IMAGE: 'sakurapatch/nekohoa-api:latest'`) (FR-007/FR-014/FR-016)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Supply-chain tooling every story relies on so each added action can be SHA-pinned.

**⚠️ CRITICAL**: Complete before the user-story phases so pinning is part of each story, not an afterthought.

- [ ] T003 [P] Create `.github/dependabot.yml` enabling the `github-actions` ecosystem (weekly) so SHA pins receive update PRs (FR-008)
- [ ] T004 [P] Confirm the `pinact` (or `gh api repos/<owner>/<repo>/commits/<tag>`) resolution flow from quickstart is available for pinning `@<SHA>` placeholders added by later tasks (FR-008)

**Checkpoint**: Skeleton + pinning tooling ready — user stories can begin (US1 first as MVP).

---

## Phase 3: User Story 1 - Block vulnerable container images before they reach a registry (Priority: P1) 🎯 MVP

**Goal**: Scan the locally built backend image and fail on fixable CRITICAL/HIGH **before** any push to Docker Hub, so a vulnerable image never becomes deployable.

**Independent Test**: Build an image with a known HIGH/CRITICAL CVE → pipeline fails at the image scan and never pushes; a clean image passes and proceeds.

### Implementation for User Story 1

- [ ] T005 [US1] In `.github/workflows/security-scan.yml`, add the `image-scan` job: `actions/checkout`, `docker/setup-buildx-action`; build the image locally (`load: true, push: false`, `file: HOAManagementCompany/Dockerfile`, tag `${IMAGE_LOCAL_TAG}-${{ github.sha }}`) on non-schedule events; `docker pull "$PUBLISHED_IMAGE"` on `schedule`; add a `Resolve scan target` step that outputs the published image on schedule and the local tag otherwise (FR-003/FR-016)
- [ ] T006 [US1] In the `image-scan` job, add the Trivy **SARIF** pass (`scan-type: image`, `ignore-unfixed: true`, `format: sarif`, `output: trivy-image.sarif`, `exit-code: '0'`), then `github/codeql-action/upload-sarif` (`category: trivy-image`, `continue-on-error: true`) and `actions/upload-artifact` fallback (FR-014)
- [ ] T007 [US1] In the `image-scan` job, add the Trivy **gating** pass (`scan-type: image`, `severity: ${{ env.TRIVY_SEVERITY }}`, `ignore-unfixed: true`, `trivyignores: .trivyignore`, `exit-code: '1'`, `format: table`) (FR-003/FR-005/FR-006/FR-015)
- [ ] T008 [P] [US1] Refactor `.github/workflows/test.yml` `docker-push` job: replace the single `push: true` step with `docker/setup-buildx-action` → build (`load: true, push: false`, tag `nekohoa-api:scan-${{ github.sha }}`) → Trivy image gate (`severity: 'CRITICAL,HIGH'`, `ignore-unfixed: true`, `trivyignores: .trivyignore`, `exit-code: '1'`) → `docker/login-action` → push (reuse buildx cache, tags `:latest` + `:${{ github.sha }}`) so the push step cannot run on a failing scan (FR-011)
- [ ] T009 [US1] Pin every third-party action introduced/touched in T005–T008 to a full 40-char commit SHA with a trailing `# vX.Y.Z` comment (run `pinact`) (FR-008)
- [ ] T010 [US1] **Validate**: temporarily set an outdated base image to introduce a fixable CRITICAL/HIGH → confirm both `image-scan` and the `docker-push` gate fail and no push occurs; revert and confirm a clean image passes; confirm a MEDIUM/LOW-only result does not fail (quickstart verification rows) (FR-005/FR-006/FR-011)

**Checkpoint**: US1 is the shippable MVP — vulnerable images are blocked before push, independent of US2/US3.

---

## Phase 4: User Story 2 - Catch insecure Infrastructure-as-Code misconfigurations early (Priority: P2)

**Goal**: Scan raw OpenTofu under `infra/` first; fail on fixable CRITICAL/HIGH misconfigurations, and pass as a no-op while `infra/` is still empty (pre-`010`).

**Independent Test**: Add a misconfigured `infra/*.tf` → job fails before the build; with no `infra/` files → job logs "no IaC to scan" and exits 0.

### Implementation for User Story 2

- [ ] T011 [US2] In `.github/workflows/security-scan.yml`, add the `iac-config` job with a `Detect IaC sources` guard step that sets output `present=true` only when `infra/` exists and contains `*.tf`/`*.tofu`, else logs the non-blocking pass and sets `present=false` (FR-009)
- [ ] T012 [US2] In the `iac-config` job, add the Trivy config **SARIF** pass (`scan-type: config`, `scan-ref: infra`, `format: sarif`, `output: trivy-iac.sarif`, `exit-code: '0'`) + `upload-sarif` (`category: trivy-iac`, `continue-on-error: true`) + artifact fallback, all gated on `steps.detect.outputs.present == 'true'` (FR-014)
- [ ] T013 [US2] In the `iac-config` job, add the Trivy config **gating** pass (`scan-type: config`, `scan-ref: infra`, `severity: ${{ env.TRIVY_SEVERITY }}`, `trivyignores: .trivyignore`, `exit-code: '1'`, `format: table`), gated on `present == 'true'` (FR-001/FR-005)
- [ ] T014 [US2] Pin every third-party action introduced in T011–T013 to a full 40-char commit SHA with `# vX.Y.Z` comment (FR-008)
- [ ] T015 [US2] **Validate**: with no `infra/`, confirm `iac-config` logs "no IaC to scan" and exits 0; add a temporary `infra/insecure.tf` with a known CRITICAL/HIGH misconfiguration → confirm the job fails before the build stage; remove the fixture (FR-001/FR-009)

**Checkpoint**: US1 and US2 both function independently; IaC gate is dormant-then-automatic on `010` merge.

---

## Phase 5: User Story 3 - Trustworthy, tamper-resistant pipeline (Priority: P2)

**Goal**: Guarantee every third-party action in the scanning workflow is pinned to an immutable SHA, hardening the pipeline against retag/supply-chain attacks.

**Independent Test**: Inspect the workflow — no third-party `uses:` resolves to a branch or floating tag; every one is a full commit SHA.

### Implementation for User Story 3

- [ ] T016 [US3] Audit `.github/workflows/security-scan.yml` and the touched `test.yml` lines: run `grep -nE '@(main|master|v[0-9]+)\b' .github/workflows/security-scan.yml` and confirm no third-party action matches (all are 40-char SHAs); fix any stragglers (FR-008)
- [ ] T017 [US3] Confirm `.github/dependabot.yml` (T003) targets `github-actions` so pins stay current, and that the SHA-update procedure is documented in `quickstart.md` (FR-008)
- [ ] T018 [US3] **Validate**: for one pinned action, confirm the `# vX.Y.Z` comment matches the SHA (`gh api repos/<owner>/<repo>/commits/<tag> --jq .sha`); document the retag-immunity reasoning (SC-003)

**Checkpoint**: All three stories complete; pipeline self-hardened.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Operational wiring and verification that spans the stories.

- [ ] T019 [P] Add `actionlint` validation (a CI step or a documented local run) for the new/edited workflow files
- [ ] T020 Configure `main` branch protection to require the `IaC config scan (trivy config)` and `Image vulnerability scan (trivy image)` status checks so a failing scan blocks the merge that would trigger the push/deploy (quickstart §3) (FR-011)
- [ ] T021 Enable GitHub Code Scanning so SARIF populates the Security tab; confirm the artifact fallback still works when it is disabled (research D2) (FR-014)
- [ ] T022 Verify coverage/Sonar/Codecov gates exclude the new workflow YAML and `.trivyignore` (consistent with `010` excluding `infra/**`) so a 0%-coverage check does not block the PR
- [ ] T023 [P] Run `quickstart.md` end-to-end validation (every row of the verification table)
- [ ] T024 [P] (Optional follow-up) Pin the remaining floating-tag actions in `test.yml` via `pinact` — recommended hardening flagged in quickstart (out of this feature's required scope)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup; provides pinning tooling used by every story.
- **User Stories (Phase 3–5)**: depend on Setup + Foundational.
  - US1 (P1) is the MVP — do it first.
  - US2 (P2) edits the same `security-scan.yml` file as US1, so it follows US1's edits sequentially (not parallel on that file).
  - US3 (P2) audits the actions added by US1 and US2, so it runs after both.
- **Polish (Phase 6)**: after the user stories are in place.

### User Story Dependencies

- **US1**: independent; the MVP. (`test.yml` edit T008 is a different file from the `security-scan.yml` edits and can run in parallel within US1.)
- **US2**: logically independent of US1, but shares `security-scan.yml` → sequence its file edits after US1's.
- **US3**: depends on US1 + US2 (it verifies the union of actions they introduced).

### Parallel Opportunities

- T001 (`.trivyignore`) ∥ T002 (skeleton) — different files.
- T003 ∥ T004 — different concerns.
- Within US1: T008 (`test.yml`) ∥ T005–T007 (`security-scan.yml`) — different files; T009 pinning waits for both.
- T019, T023, T024 in Polish are independent.
- US1's `security-scan.yml` job and US2's `security-scan.yml` job are logically parallel but, being one file, should be edited sequentially to avoid conflicts.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup (T001–T002).
2. Phase 2 Foundational (T003–T004).
3. Phase 3 US1 (T005–T010) — build → scan → push gate on the backend image.
4. **STOP and VALIDATE** US1 (T010), then ship: vulnerable images are blocked before push even before the IaC stage exists.

### Incremental Delivery

1. Setup + Foundational → ready.
2. US1 → validate → ship (MVP).
3. US2 → validate (dormant until `010` merges, then automatic) → ship.
4. US3 → audit/lock supply chain → ship.
5. Polish: branch protection, Code Scanning, actionlint, quickstart run.

---

## Notes

- [P] = different files, no incomplete dependencies.
- SHA pinning is performed per story (T009/T014) so each story ships hardened; T016 is the final cross-story audit.
- The IaC fixture (T015) and the vulnerable-base-image change (T010) are temporary validation artifacts — revert them after confirming the expected pass/fail.
- Commit after each task or logical group; keep `TRIVY_SEVERITY` in `security-scan.yml` and the `severity:` value in `test.yml` in sync.
