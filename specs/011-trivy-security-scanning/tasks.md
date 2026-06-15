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

**Organization**: Tasks are grouped by user story. Structural note: the `iac-config` and `image-scan`
jobs live in the **same** file (`.github/workflows/security-scan.yml`); the `image-scan` job declares
`needs: [iac-config]`, so a minimal `iac-config` skeleton is created in Foundational (Phase 2) and
US2 fleshes out its scan steps. The `test.yml` edit (US1) is a different file and can proceed in
parallel with the `security-scan.yml` edits.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)

## Path Conventions

CI/infra feature — no `src/` tree. Deliverables: `.github/workflows/security-scan.yml` (new),
`.github/workflows/test.yml` (modified), `.trivyignore` (new, repo root), `.github/dependabot.yml`
(new), and a `TRIVY_SEVERITY` repository variable.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Files, skeleton, and the single severity source both stories build on.

- [X] T001 [P] Create `.trivyignore` at the repo root with a header explaining the format (one CVE/misconfig ID per line + `# reason — owner — review/expiry` comment); start empty (FR-015)
- [X] T002 Create `.github/workflows/security-scan.yml` skeleton: `name`, `on:` (`pull_request` → `main`, `push` → `main`, `schedule` nightly `cron: '0 7 * * *'`), job-level `permissions` (`contents: read`, `security-events: write`, `pull-requests: read`), and top-level `env` (`TRIVY_SEVERITY: ${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}`, `IMAGE_LOCAL_TAG`, `PUBLISHED_IMAGE: 'sakurapatch/nekohoa-api:latest'`) (FR-007/FR-014/FR-016)
- [X] T003 [P] Define the `TRIVY_SEVERITY` repository variable (`gh variable set TRIVY_SEVERITY --body 'CRITICAL,HIGH'`) as the single severity source referenced by both `security-scan.yml` and `test.yml` (FR-007)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Supply-chain tooling + the `iac-config` job skeleton that `image-scan` depends on.

**⚠️ CRITICAL**: Complete before the user-story phases. T006 must exist before US1's `image-scan` can `needs: [iac-config]`.

- [X] T004 [P] Create `.github/dependabot.yml` enabling the `github-actions` ecosystem (weekly) so SHA pins receive update PRs (FR-008)
- [X] T005 [P] Confirm the `pinact` (or `gh api repos/<owner>/<repo>/commits/<tag>`) resolution flow from quickstart is available for pinning `@<SHA>` placeholders added by later tasks (FR-008)
- [X] T006 In `.github/workflows/security-scan.yml`, create the `iac-config` job with **only** the `actions/checkout` + `Detect IaC sources` guard step that sets output `present=true` when `infra/` exists and contains `*.tf`/`*.tofu`, else logs "no IaC to scan" and exits 0 (non-blocking no-op). This establishes the "runs first" stage that `image-scan` depends on and satisfies the empty-`infra/` behavior (FR-001 scaffold / FR-009)

**Checkpoint**: Skeleton + severity source + `iac-config` no-op stage + pinning tooling ready.

---

## Phase 3: User Story 1 - Block vulnerable container images before they reach a registry (Priority: P1) 🎯 MVP

**Goal**: Scan the locally built backend image and fail on fixable CRITICAL/HIGH **before** any push to Docker Hub. The image build is gated behind the IaC stage (`needs: [iac-config]`).

**Independent Test**: Build an image with a known HIGH/CRITICAL CVE → pipeline fails at the image scan and never pushes; a clean image passes and proceeds.

### Implementation for User Story 1

- [X] T007 [US1] In `.github/workflows/security-scan.yml`, add the `image-scan` job with `needs: [iac-config]`: `actions/checkout`, `docker/setup-buildx-action`; build the image locally (`load: true, push: false`, `file: HOAManagementCompany/Dockerfile`, tag `${IMAGE_LOCAL_TAG}-${{ github.sha }}`) on non-schedule events; `docker pull "$PUBLISHED_IMAGE"` on `schedule`; add a `Resolve scan target` step (published image on schedule, local tag otherwise) (FR-001/FR-002/FR-003/FR-016)
- [X] T008 [US1] In the `image-scan` job, add the Trivy **SARIF** pass (`scan-type: image`, `ignore-unfixed: true`, `format: sarif`, `output: trivy-image.sarif`, `exit-code: '0'`), then `github/codeql-action/upload-sarif` (`category: trivy-image`, `continue-on-error: true`) and `actions/upload-artifact` fallback (FR-014)
- [X] T009 [US1] In the `image-scan` job, add the Trivy **gating** pass (`scan-type: image`, `severity: ${{ env.TRIVY_SEVERITY }}`, `ignore-unfixed: true`, `trivyignores: .trivyignore`, `exit-code: '1'`, `format: table`) (FR-003/FR-005/FR-006/FR-015)
- [X] T010 [P] [US1] Refactor `.github/workflows/test.yml` `docker-push` job: replace the single `push: true` step with `docker/setup-buildx-action` → build (`load: true, push: false`, tag `nekohoa-api:scan-${{ github.sha }}`) → Trivy image gate (`severity: ${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}`, `ignore-unfixed: true`, `trivyignores: .trivyignore`, `exit-code: '1'`) → `docker/login-action` → push (reuse buildx cache, tags `:latest` + `:${{ github.sha }}`) so the push step cannot run on a failing scan (FR-011/FR-007)
- [X] T011 [US1] Pin every third-party action introduced/touched in T007–T010 to a full 40-char commit SHA with a trailing `# vX.Y.Z` comment (run `pinact`) (FR-008)
- [X] T012 [US1] **Validate**: (a) introduce a fixable CRITICAL/HIGH via an outdated base image → confirm `image-scan` and the `docker-push` gate fail and no push occurs; revert → clean image passes; MEDIUM/LOW-only does not fail (FR-005/FR-006/FR-011). (b) **Allowlist**: add a temporary `.trivyignore` entry for the introduced finding → confirm it no longer fails the build, then remove it (FR-015 / analyze C2). (c) **DB-unavailable**: force a Trivy DB fetch failure (e.g. block the runner's DB registry/offline) → confirm the gate exits non-zero rather than reporting clean (FR-012 / analyze C1)

**Checkpoint**: US1 is the shippable MVP — vulnerable images are blocked before push, gated behind the IaC stage, independent of US2/US3.

---

## Phase 4: User Story 2 - Catch insecure Infrastructure-as-Code misconfigurations early (Priority: P2)

**Goal**: Make the `iac-config` job (skeleton from T006) actually scan `infra/`, fail on fixable CRITICAL/HIGH misconfigurations, and report via SARIF — while still passing as a no-op when `infra/` is empty (pre-`010`).

**Independent Test**: Add a misconfigured `infra/*.tf` → `iac-config` fails and `image-scan` never starts (gated by `needs`); with no `infra/` files → job logs "no IaC to scan" and exits 0.

### Implementation for User Story 2

- [X] T013 [US2] In the existing `iac-config` job, add the Trivy config **SARIF** pass (`scan-type: config`, `scan-ref: infra`, `format: sarif`, `output: trivy-iac.sarif`, `exit-code: '0'`) + `upload-sarif` (`category: trivy-iac`, `continue-on-error: true`) + artifact fallback, all gated on `steps.detect.outputs.present == 'true'` (FR-014)
- [X] T014 [US2] In the `iac-config` job, add the Trivy config **gating** pass (`scan-type: config`, `scan-ref: infra`, `severity: ${{ env.TRIVY_SEVERITY }}`, `trivyignores: .trivyignore`, `exit-code: '1'`, `format: table`), gated on `present == 'true'` (FR-001/FR-005)
- [X] T015 [US2] Pin every third-party action introduced in T013–T014 to a full 40-char commit SHA with `# vX.Y.Z` comment (FR-008)
- [X] T016 [US2] **Validate**: with no `infra/`, confirm `iac-config` logs "no IaC to scan" and exits 0; add a temporary `infra/insecure.tf` with a known CRITICAL/HIGH misconfiguration → confirm `iac-config` fails **and** `image-scan` does not start (the `needs` gate stops the build — US2-AS1); remove the fixture (FR-001/FR-002/FR-009)

**Checkpoint**: US1 and US2 both function independently; IaC gate is dormant-then-automatic on `010` merge.

---

## Phase 5: User Story 3 - Trustworthy, tamper-resistant pipeline (Priority: P2)

**Goal**: Guarantee every third-party action in the scanning workflow is pinned to an immutable SHA, hardening the pipeline against retag/supply-chain attacks.

**Independent Test**: Inspect the workflow — no third-party `uses:` resolves to a branch or floating tag; every one is a full commit SHA.

### Implementation for User Story 3

- [X] T017 [US3] Audit `.github/workflows/security-scan.yml` and the touched `test.yml` lines: run `grep -nE '@(main|master|v[0-9]+)\b' .github/workflows/security-scan.yml` and confirm no third-party action matches (all are 40-char SHAs); fix any stragglers (FR-008)
- [X] T018 [US3] Confirm `.github/dependabot.yml` (T004) targets `github-actions` so pins stay current, and that the SHA-update procedure is documented in `quickstart.md` (FR-008)
- [X] T019 [US3] **Validate**: for one pinned action, confirm the `# vX.Y.Z` comment matches the SHA (`gh api repos/<owner>/<repo>/commits/<tag> --jq .sha`); document the retag-immunity reasoning (SC-003)

**Checkpoint**: All three stories complete; pipeline self-hardened.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Operational wiring and verification that spans the stories.

- [X] T020 [P] Add `actionlint` validation (a CI step or a documented local run) for the new/edited workflow files; also assert no `trivy`/scanner step is embedded in `HOAManagementCompany/Dockerfile` (separation-of-stages check) (FR-004 / analyze C3)
- [X] T021 Configure `main` branch protection to require the `IaC config scan (trivy config)` and `Image vulnerability scan (trivy image)` status checks so a failing scan blocks the merge that would trigger the push/deploy and enforces IaC-before-build ordering on the cross-workflow post-merge path (quickstart §3) (FR-011/FR-002)
- [X] T022 Enable GitHub Code Scanning so SARIF populates the Security tab; confirm the artifact fallback still works when it is disabled (research D2) (FR-014)
- [X] T023 Verify coverage/Sonar/Codecov gates exclude the new workflow YAML and `.trivyignore` (consistent with `010` excluding `infra/**`) so a 0%-coverage check does not block the PR
- [X] T024 [P] Run `quickstart.md` end-to-end validation (every row of the verification table) — validated locally with Trivy 0.71.1 + Docker: vulnerable image blocked (node:18.12-bullseye-slim, exit 1), clean image/real `infra/` pass (exit 0), IaC misconfig caught (exit 1), empty `infra/` no-op (exit 0), all actions SHA-pinned. Nightly published-image row keys on the `schedule` event (not manually fireable) — logic in place, first run on the cron.
- [ ] T025 [P] (Optional follow-up) Pin the remaining floating-tag actions in `test.yml` via `pinact` — recommended hardening flagged in quickstart (out of this feature's required scope)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup; T006 (`iac-config` skeleton) is required by US1's `image-scan` `needs:`.
- **User Stories (Phase 3–5)**: depend on Setup + Foundational.
  - US1 (P1) is the MVP — do it first. Its `image-scan` job `needs: [iac-config]` (T006).
  - US2 (P2) fleshes out the existing `iac-config` job; edits the same file as US1 but a different job → sequence its edits after US1's.
  - US3 (P2) audits the actions added by US1 and US2 → runs after both.
- **Polish (Phase 6)**: after the user stories are in place.

### User Story Dependencies

- **US1**: depends on T006 (the `iac-config` skeleton). The `test.yml` edit T010 is a different file and runs in parallel within US1.
- **US2**: extends the `iac-config` job created in T006; logically independent of US1 but shares the file → sequence after US1.
- **US3**: depends on US1 + US2 (it verifies the union of actions they introduced).

### Parallel Opportunities

- T001 ∥ T002 ∥ T003 (different files/concerns; T003 is a repo-settings action).
- T004 ∥ T005 (different concerns); T006 edits the workflow file.
- Within US1: T010 (`test.yml`) ∥ T007–T009 (`security-scan.yml`); T011 pinning waits for both.
- T020, T024, T025 in Polish are independent.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup (T001–T003).
2. Phase 2 Foundational (T004–T006) — includes the `iac-config` no-op skeleton.
3. Phase 3 US1 (T007–T012) — build → scan → push gate on the backend image, gated behind the IaC stage.
4. **STOP and VALIDATE** US1 (T012), then ship.

### Incremental Delivery

1. Setup + Foundational → ready (IaC stage exists as a passing no-op).
2. US1 → validate → ship (MVP).
3. US2 → validate (dormant until `010` merges, then automatic) → ship.
4. US3 → audit/lock supply chain → ship.
5. Polish: branch protection, Code Scanning, actionlint, quickstart run.

---

## Notes

- [P] = different files, no incomplete dependencies.
- Severity is a **single repository variable** `vars.TRIVY_SEVERITY` (T003) referenced by both workflows (FR-007); the `|| 'CRITICAL,HIGH'` fallback keeps things working before it is set.
- SHA pinning is performed per story (T011/T015) so each story ships hardened; T017 is the final cross-story audit.
- The IaC fixture (T016), the vulnerable-base-image change and temporary `.trivyignore` entry (T012) are temporary validation artifacts — revert them after confirming the expected pass/fail.
- Commit after each task or logical group.
