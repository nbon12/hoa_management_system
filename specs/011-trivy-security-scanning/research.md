# Phase 0 Research: Trivy Security Scanning Pipeline

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remain.

## D1 — Where the scans live (workflow placement)

**Decision**: One new dedicated workflow `.github/workflows/security-scan.yml` owns the `trivy config`
IaC scan and the PR/scheduled `trivy image` scan. The existing `test.yml` `docker-push` job is
refactored so the build → scan → push sequence happens **inside one job** on the main-push path.

**Rationale**:
- A dedicated workflow keeps security scanning legible and lets it run on the spec's triggers
  (PR + push + nightly schedule) without entangling the existing build/test matrix.
- GitHub Actions cannot express `needs:` across workflows, so the only way to *guarantee* "no push
  on a failing scan" (FR-011) on the post-merge main-push path is to make the scan a step that
  precedes the push step within the same job. Hence the surgical `docker-push` refactor: switch
  `docker/build-push-action` from `push: true` to a local build (`load: true, push: false`), run
  `trivy image` against the loaded tag, then push (separate `docker/build-push-action` with
  `push: true` reusing the buildx cache, or `docker push`).
- For PRs (where `test.yml` does not build/push), the new workflow's `image-scan` job builds the
  image locally (no push) so contributors get a pre-merge gate.

**Alternatives considered**:
- *Everything in `test.yml`*: rejected — bloats an already large workflow and couples scan triggers
  to the test matrix; the nightly schedule and SARIF concerns don't belong there.
- *Scan in a separate workflow + rely solely on branch protection to stop the push*: partially
  rejected. Branch protection (scan as required check) blocks the **merge**, which is the right gate
  for the normal PR→merge→deploy flow. But a direct push to `main` would race the scan against
  `docker-push`. Keeping the scan-before-push inside `docker-push` removes that race and makes FR-011
  literally true regardless of how the push was triggered.
- *Bake Trivy into the Dockerfile*: explicitly prohibited by the feature (FR-004).

## D2 — Reporting: SARIF upload + PR annotations vs Security tab

**Decision**: Run each Trivy scan twice per job — (a) `format: sarif` → upload via
`github/codeql-action/upload-sarif` with a distinct `category` per scan, and (b) `format: table`
with `exit-code: 1` to gate the build. Use the same SARIF upload for all triggers: on
`pull_request` runs GitHub renders findings as PR annotations; on `push`/`schedule` they populate
the **Security → Code scanning** tab. The table output stays in the run logs (FR-010).

**Rationale**:
- A single Trivy invocation cannot both emit SARIF *and* reliably fail with a filtered exit code in
  all versions; the two-pass pattern (report-all to SARIF, gate on the policy severities) is the
  documented, robust approach and gives full visibility (MEDIUM/LOW appear in SARIF) while only
  CRITICAL/HIGH fail the build.
- Distinct `category` values (`trivy-iac`, `trivy-image`) keep the two scanners' alerts from
  overwriting each other in Code Scanning.
- This is exactly the hybrid the spec clarified (Session 2026-06-14, Q1): PR findings on the PR,
  recurring findings on `main` in the Security tab.

**Dependency/Risk**: SARIF upload to Code Scanning requires GitHub code scanning to be enabled. It is
free on public repos; on a **private** repo it needs GitHub Advanced Security. If unavailable, the
upload step fails. Mitigation: guard the upload step with `continue-on-error: true` **and** always
upload the SARIF as a build artifact (`actions/upload-artifact`) so findings are never lost; the
table-format gate step is what actually fails the build, so gating does not depend on Code Scanning
being enabled. Quickstart documents enabling Code Scanning to get the Security-tab experience.

**Permissions**: the job needs `security-events: write` (for upload-sarif) and `contents: read`;
the PR-annotation path also benefits from `pull-requests: read`. Permissions are declared at the job
level, minimally.

## D3 — Severity policy & unfixable findings

**Decision**: A single workflow-level `env.TRIVY_SEVERITY: "CRITICAL,HIGH"` is referenced by every
Trivy step (FR-007). The image scan adds `--ignore-unfixed` (gate only on fixable findings) and
honors a root `.trivyignore` allowlist (FR-015). The gating pass uses `exit-code: 1`; the SARIF pass
uses `exit-code: 0` and reports all severities for visibility.

**Rationale**:
- One env var = one place to tighten later (e.g., add `MEDIUM`) without editing multiple steps.
- `--ignore-unfixed` prevents the pipeline from being permanently blocked by upstream CVEs that have
  no patch yet (clarified Session 2026-06-14, Q2). `.trivyignore` gives a reviewed escape hatch for
  documented exceptions; entries are CVE IDs with a comment and (optionally) an expiry note.
- The IaC `trivy config` scan has no "unfixed" concept, so `--ignore-unfixed` applies to the image
  scan only; both still share the severity env.

**Alternatives considered**: block on *all* CRITICAL/HIGH regardless of fix — rejected as operationally
brittle (a single unfixable base-image CVE would block every merge).

## D4 — SHA pinning of actions (and how to maintain it)

**Decision**: Every third-party action in `security-scan.yml` (and the actions added to the
`docker-push` refactor) is pinned to a full 40-char commit SHA with a trailing `# vX.Y.Z` comment,
matching the repo's existing convention (e.g. `google-github-actions/auth@c200f369… # v2`). SHAs are
resolved at implementation time with `pinact` (or `gh api repos/<owner>/<repo>/commits/<tag>`), not
hand-typed from memory.

**Rationale**: FR-008 / supply-chain integrity (User Story 3). The repo already pins its sensitive
actions this way; we extend the practice to the security workflow. Pinning at implement-time (not in
the plan) avoids committing a guessed/incorrect SHA.

**Actions to pin**: `actions/checkout`, `aquasecurity/trivy-action`, `github/codeql-action/upload-sarif`,
`actions/upload-artifact`, `docker/setup-buildx-action`, `docker/build-push-action`,
`docker/login-action`. Quickstart documents the resolve/update procedure and a periodic refresh
(Dependabot for `github-actions` ecosystem keeps pins current with PRs that themselves pass the
scan).

**Note (scope)**: The pre-existing floating tags elsewhere in `test.yml` (`actions/checkout@v4`,
`docker/build-push-action@v5`, `codecov/codecov-action@v4`, etc.) are *not* in scope for this feature
beyond the `docker-push` lines we touch; re-pinning the whole file is a separate hardening task noted
in quickstart as a recommended follow-up.

## D5 — Triggers and the scheduled run's scan target

**Decision**: Triggers are `pull_request` (base `main`), `push` (`main`), and `schedule` (nightly
cron, e.g. `0 7 * * *` UTC). On PR/push the image-scan builds the image locally and scans it. On the
scheduled run there is no PR build, so the job instead **pulls and scans the most recently published
image** `sakurapatch/nekohoa-api:latest` and runs `trivy config` against `main`'s `infra/`.

**Rationale**: New CVEs are disclosed continuously; an image that was clean at push can become
vulnerable later (clarified Session 2026-06-14, Q3). The nightly re-scan of the published image
catches that drift and reports it to the Security tab. Branching the scan target by
`github.event_name == 'schedule'` keeps one workflow handling both modes.

## D6 — Handling an absent/empty `infra/` (pre-`010`)

**Decision**: The `iac-config` job checks for `infra/` and treats "directory absent or no scannable
files" as a successful no-op (FR-009). Implementation: a guard step (`if [ -d infra ] && find infra
-name '*.tf' -o -name '*.tofu' | grep -q .`) sets an output that conditions the Trivy step;
otherwise the job logs "no IaC to scan" and exits 0. `trivy config ./infra` would itself exit 0 on
an empty/missing target, but the explicit guard makes the intent auditable and avoids a confusing
"0 results" when the directory truly doesn't exist yet.

**Rationale**: Lets the pipeline be merged and useful *now*, before `010` lands, and start enforcing
automatically the moment `infra/*.tf` files appear — with no workflow change (SC-005).

## D7 — Image-scan build mechanics (load vs push)

**Decision**: Build with `docker/build-push-action` using `load: true, push: false` so the image
lands in the runner's local Docker daemon under a deterministic local tag (e.g.
`nekohoa-api:scan-${{ github.sha }}`); `trivy image` scans that local tag. On the main-push path the
subsequent push step re-uses the buildx cache so the artifact pushed is byte-identical to the one
scanned.

**Rationale**: Satisfies "scan the *locally built* image before push" (FR-003) and guarantees the
scanned artifact == the pushed artifact. `load: true` requires the default docker driver or
`docker/setup-buildx-action` with `driver: docker` (or `--load` support); documented in the contract.

## D8 — Static validation of the workflow

**Decision**: Add `actionlint` (and `trivy config` can additionally lint the workflow files) as a
lightweight check, and validate behavior with two manual fixtures during implementation: a sample
`infra/insecure.tf` with a known misconfiguration (expect fail) and a deliberately outdated base
image (expect fail), plus the clean happy path (expect pass) and the empty-`infra/` case (expect
pass). These map directly to the spec's acceptance scenarios.

**Rationale**: There is no unit-test harness for YAML; execution against fixtures is the faithful
test, and each fixture traces to an acceptance scenario / success criterion.
