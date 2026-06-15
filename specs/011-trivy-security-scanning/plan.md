# Implementation Plan: Trivy Security Scanning Pipeline

**Branch**: `011-trivy-security-scanning` | **Date**: 2026-06-14 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/011-trivy-security-scanning/spec.md`

## Summary

Add Trivy-based security scanning to the GitHub Actions delivery pipeline as **two discrete
stages** (not Dockerfile layers): (1) a `trivy config` scan of the raw OpenTofu IaC under `infra/`
that runs first, and (2) a `trivy image` scan of the **locally built** backend container image that
runs *before* the image is pushed to Docker Hub (`sakurapatch/nekohoa-api`). The image
scan job declares `needs: [iac-config]` so the IaC scan runs **first** and the image build does not
start until it passes (FR-001/FR-002). Both stages enforce a
single shared severity policy — fail only on **fixable** CRITICAL/HIGH findings (`--ignore-unfixed`
+ a reviewed `.trivyignore` allowlist) — sourced from one repository variable `vars.TRIVY_SEVERITY`
referenced by both workflows (FR-007). They upload SARIF so PR-introduced findings annotate the PR
while pushes/scheduled runs on `main` populate the GitHub Security tab. All third-party actions in
the new workflow are pinned to immutable commit SHAs.

Technical approach: a new dedicated workflow `.github/workflows/security-scan.yml` (triggers:
`pull_request` → main, `push` → main, nightly `schedule`) owns the config scan and the PR/scheduled
image scan, with the image job gated on the config job; the existing `test.yml` `docker-push` job is
refactored to **build → scan → push** in sequence so the push physically cannot happen on a failing
scan (FR-011). The scan jobs are registered as required status checks so a failing scan blocks the
merge that would trigger a main-push deploy (this also enforces IaC-before-build ordering on the
cross-workflow post-merge path, where `needs:` cannot reach across workflows).

## Technical Context

**Language/Version**: YAML (GitHub Actions workflow syntax); Trivy CLI (via `aquasecurity/trivy-action`); OpenTofu/HCL is the *scanned* artifact, not authored here  
**Primary Dependencies**: `aquasecurity/trivy-action`, `github/codeql-action/upload-sarif`, `docker/build-push-action`, `docker/setup-buildx-action`, `docker/login-action`, `actions/checkout` — all SHA-pinned  
**Storage**: N/A (no application data, schema, or migrations)  
**Testing**: Workflow validation by execution (PR dry-run with intentionally vulnerable image / sample misconfigured `infra/` file); `actionlint` for static workflow linting; no xUnit/Karma surface  
**Target Platform**: GitHub Actions `ubuntu-latest` runners  
**Project Type**: CI/CD pipeline + infrastructure automation (no `src/` application code)  
**Performance Goals**: Scan stages add ≤ a few minutes to a typical run (SC-006); cache Trivy DB to keep warm runs fast  
**Constraints**: Fail only on fixable CRITICAL/HIGH (FR-005/FR-015); single severity source (FR-007); SHA-pinned actions (FR-008); IaC scan is a non-blocking pass when `infra/` is absent (FR-009)  
**Scale/Scope**: One backend image (`HOAManagementCompany/Dockerfile`); `infra/**` tree (currently empty until `010` merges); the frontend is Cloudflare Pages (static, no container) and is out of image-scan scope

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ Uses GitHub Actions (mandated CI/CD), Docker/Docker Hub (mandated container
  registry), and gates the Cloud Run deploy path — all constitution §2 technologies. Trivy is an
  additive security tool that touches none of the application stack. OpenTofu/`infra/` aligns with
  feature `010`.
- **HOA tenancy**: N/A — this feature provisions no HOA-scoped rows, queries, or data. The relevant
  boundary (environment isolation Dev/Staging/Prod) is owned by `009`/`010` and unaffected.
- **API contracts**: N/A — no endpoints added.
- **Security and operations**: ✅ This feature *is* a security control (§7/§8): it blocks vulnerable
  images before they reach Docker Hub and insecure IaC before apply, and hardens the pipeline itself
  via SHA-pinned actions. No secrets are committed; SARIF carries no sensitive payloads; the
  `security-events: write` permission is scoped to the upload step only.
- **File storage**: N/A — no blob storage introduced.
- **Caching/edge**: N/A — no API responses; Trivy's vulnerability DB cache is build-time only.
- **Testing discipline**: The 95%/Testcontainers/test-first rules target executable application code.
  Workflow YAML and `.trivyignore` have no executable test layer; they are validated by run outcome
  and `actionlint`. Coverage/Sonar/Codecov gates MUST exclude these files (consistent with `010`
  excluding `infra/**`). Justified cross-cutting CI slice under §11.
- **CI/CD and documentation**: ✅ Adds a CI quality gate; does not weaken existing Sonar/Codecov/
  coverage jobs. No Repowise marker regions are introduced (no source files with `REPOWISE` markers);
  the Repowise PR gate is a no-op for this YAML/doc-only change.

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/011-trivy-security-scanning/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (configuration objects, not DB entities)
├── quickstart.md        # Phase 1 output (setup + how-to)
├── contracts/
│   └── scan-workflow.md # Phase 1 output (workflow interface contract + job graph)
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit.specify)
```

### Source Code (repository root)

```text
.github/workflows/
├── security-scan.yml    # NEW — iac-config + image-scan jobs (PR + push + nightly)
└── test.yml             # MODIFIED — docker-push job refactored to build → scan → push

.trivyignore             # NEW — reviewed allowlist of accepted/exception findings (root)
infra/                   # SCANNED (owned by feature 010; may be absent until 010 merges)
HOAManagementCompany/
└── Dockerfile           # SCANNED image source (unchanged)
```

**Structure Decision**: No application source layout applies — this is a CI/infra feature. The unit
of delivery is the new `security-scan.yml` workflow plus a surgical edit to `test.yml`'s
`docker-push` job and a root `.trivyignore`. The IaC scan targets `infra/` (the path fixed by
`010`); the image scan targets the single backend Dockerfile. Frontend (Cloudflare Pages, no
container) is intentionally excluded from image scanning.

## Repowise Documentation

**Status**: Complete (no marker regions for this feature)

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| _none_ | — | This feature adds only workflow YAML, `.trivyignore`, and Markdown docs; none carry `REPOWISE` source-marker regions, so there is nothing to regenerate. The `repowise-gate` index/health check still runs on the PR. |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation (unchanged) |

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
