# Feature Specification: Trivy Security Scanning Pipeline

**Feature Branch**: `011-trivy-security-scanning`  
**Created**: 2026-06-13  
**Status**: Draft  
**Input**: User description: "I want to implement Trivy security scanning for my OpenTofu (IaC) and Google Cloud Run deployment pipeline. Following industry best practices, I want to separate the scan into two distinct stages within GitHub Actions rather than baking Trivy into the Dockerfile layers. Two-stage scan: (1) `trivy config` on raw OpenTofu code, (2) build the container image, (3) `trivy image` on the locally built image before push. Pin all GitHub Actions to immutable commit SHAs. Fail the build on CRITICAL or HIGH severity findings only."

## Clarifications

### Session 2026-06-14

- Q: Where should Trivy scan findings be surfaced for triage? → A: Hybrid — results are uploaded as SARIF to GitHub Code Scanning so that PR-introduced findings appear as annotations on the pull request, while pushes/merges and the scheduled scan on `main` are tracked in the GitHub Security tab; a human-readable table also remains in the run logs.
- Q: How should the image scan treat CRITICAL/HIGH vulnerabilities that currently have no fix available upstream? → A: Ignore unfixed (block only when a fix exists) plus a reviewed `.trivyignore` allowlist for documented exceptions; ignored/allowlisted findings stay visible but do not fail the build.
- Q: What events should trigger the scanning workflow? → A: Pull requests targeting `main`, pushes/merges to `main`, and a nightly scheduled re-scan. Because a scheduled run has no PR build, it re-scans the most recently published image and `main`'s `infra/` to catch newly-disclosed CVEs in unchanged code.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Block vulnerable container images before they reach a registry (Priority: P1)

As an engineer deploying the HOA backend to Google Cloud Run (`nekohoa-api-dev`), I want the container image built in CI to be scanned for known vulnerabilities *before* it is pushed to the image registry (Docker Hub, `sakurapatch/nekohoa-api`), so that a critically vulnerable image never becomes pullable or deployable.

**Why this priority**: This is the highest-value safety net. Once the image is pushed to Docker Hub it can be pulled and deployed onto Cloud Run by the `009` deploy pipeline; catching critical/high vulnerabilities while the image still only exists on the CI runner is the cheapest and safest place to stop it. This story alone delivers a working MVP even before the IaC scanning stage exists.

**Independent Test**: Build an image known to contain a HIGH or CRITICAL CVE (e.g., an intentionally outdated base image) and confirm the pipeline fails at the image-scan stage and never reaches the push/deploy step. Then build a clean image and confirm the pipeline proceeds.

**Acceptance Scenarios**:

1. **Given** a freshly built container image that contains at least one CRITICAL or HIGH severity vulnerability, **When** the image vulnerability scan stage runs, **Then** the stage exits with a non-zero status, the pipeline stops, and the image is not pushed to any registry.
2. **Given** a freshly built container image whose only findings are MEDIUM or LOW severity, **When** the image vulnerability scan stage runs, **Then** the stage passes and the pipeline continues to subsequent steps.
3. **Given** the image scan runs, **When** it completes, **Then** a human-readable list of detected vulnerabilities (including severity, package, and fixed version where available) is visible in the workflow run logs.

---

### User Story 2 - Catch insecure Infrastructure-as-Code misconfigurations early (Priority: P2)

As an engineer authoring the OpenTofu configurations under `infra/` defined by feature `010-dev-env-iac-opentofu` (reusable `infra/modules/*` consumed by per-environment directories `infra/environments/{dev,staging,prod}`), I want my raw IaC source scanned for security misconfigurations at the very start of the pipeline, so that insecure infrastructure definitions are flagged before any build or deploy work happens.

**Why this priority**: Feature `010` is specified but not yet merged, so `infra/` may still be empty when this scan first runs; the stage therefore cannot block real infrastructure until those files land. Establishing the stage now against the agreed `infra/` path means it enforces automatically the moment `010` merges, and it runs first so misconfigurations are surfaced before compute is spent on building images. This security scan complements — and is ordered alongside — `010`'s own plan-only PR workflow and apply-on-merge workflow that operate on `infra/**`. It is P2 because it provides no enforcement value until IaC files exist.

**Independent Test**: Place a sample OpenTofu file containing a known misconfiguration under `infra/` and confirm the config scan stage reports it. With `infra/` absent or empty, confirm the stage completes without failing the pipeline.

**Acceptance Scenarios**:

1. **Given** `infra/` contains an OpenTofu file with a CRITICAL or HIGH severity misconfiguration, **When** the IaC configuration scan stage runs, **Then** the stage exits non-zero and the pipeline stops before the build stage.
2. **Given** `infra/` does not yet exist or contains no IaC files (current state of the project, before `010` merges), **When** the IaC configuration scan stage runs, **Then** the stage completes successfully (no findings) and does not block the pipeline.
3. **Given** the IaC configuration scan stage runs, **When** it completes, **Then** detected misconfigurations and their severities are visible in the workflow run logs.

---

### User Story 3 - Trustworthy, tamper-resistant pipeline (Priority: P2)

As a maintainer responsible for the security of the delivery pipeline itself, I want every third-party GitHub Action used by the scanning workflow to be pinned to an immutable commit SHA, so that a compromised or retagged upstream action cannot silently inject malicious behavior into our build.

**Why this priority**: Supply-chain integrity of the pipeline is foundational; a security-scanning workflow that itself runs mutable third-party code undermines its own purpose. It is grouped with the IaC stage at P2 because it is a cross-cutting constraint on how Stories 1 and 2 are built rather than a standalone user-visible flow.

**Independent Test**: Inspect the workflow definition and confirm no third-party action reference uses a branch name (e.g., `@master`, `@main`) or a floating version tag (e.g., `@v1`, `@v2`); every reference resolves to a full-length commit SHA.

**Acceptance Scenarios**:

1. **Given** the scanning workflow definition, **When** it is reviewed, **Then** every third-party action reference is pinned to a full-length immutable commit SHA.
2. **Given** an upstream action publishes a new release under an existing tag, **When** our pipeline runs, **Then** it continues to execute the exact pinned revision and is unaffected by the retag.

---

### Edge Cases

- **No IaC files yet**: The IaC scan stage must succeed (treat "nothing to scan" as a pass) so the pipeline is usable before `010` merges and creates `infra/`. It must not be silently skipped in a way that hides future regressions once files exist under `infra/`.
- **Vulnerability database unavailable**: If Trivy cannot download or refresh its vulnerability/misconfiguration database (network/registry outage), the scan stage should fail loudly rather than pass with zero findings, so a transient outage is never mistaken for a clean result.
- **Build failure**: If the container image fails to build, the image-scan stage must not run against a stale or missing image; the pipeline stops at the build stage.
- **Only MEDIUM/LOW findings**: Such findings must be reported for visibility but must not fail the build under the current policy.
- **Severity policy drift**: The set of failing severities (CRITICAL, HIGH) must be defined in one place so it can be tightened later (e.g., add MEDIUM) without editing multiple stages.
- **Mixed-severity result**: An image with both LOW and CRITICAL findings must fail (presence of any fixable CRITICAL/HIGH is sufficient to fail).
- **Unfixable CRITICAL/HIGH**: A CRITICAL/HIGH finding with no upstream fix available must be reported but must not fail the build (per the ignore-unfixed policy), so the pipeline is never permanently blocked by an un-actionable CVE.
- **Allowlisted finding**: A finding explicitly listed in the reviewed `.trivyignore` allowlist must remain visible in the results but must not fail the build; removing it from the allowlist restores enforcement.
- **Scheduled run with no PR build**: The nightly scheduled run has no fresh local image, so it must scan the most recently published image rather than skip the image stage; a clean PR build does not exempt `main` from later re-scans.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pipeline MUST run an IaC configuration scan against the raw OpenTofu source under `infra/` (recursively, covering both `infra/modules/*` and `infra/environments/*` per the `010` layout) as the first scanning stage, before any container image is built.
- **FR-002**: The pipeline MUST build the application container image as a distinct stage that runs only after the IaC configuration scan stage has passed.
- **FR-003**: The pipeline MUST run an image vulnerability scan against the locally built image, and this scan MUST occur before the image is pushed to the image registry (Docker Hub `sakurapatch/nekohoa-api`, the registry the `009` build-and-push pipeline uses).
- **FR-004**: Security scanning MUST be performed as separate pipeline stages and MUST NOT be embedded as layers or steps inside the application Dockerfile.
- **FR-005**: Each scan stage MUST fail the pipeline (non-zero exit) when it detects at least one finding of CRITICAL or HIGH severity, subject to the image-scan ignore-unfixed and allowlist policy in FR-015.
- **FR-006**: Each scan stage MUST allow the pipeline to continue when the only findings are MEDIUM or LOW severity (these are reported but non-blocking under the current policy).
- **FR-007**: The set of severities that cause a failure MUST be defined consistently across both scan stages and MUST be expressed so it can be tightened later without restructuring the workflow.
- **FR-008**: Every third-party GitHub Action referenced by the workflow MUST be pinned to a full-length immutable commit SHA; branch references and floating version tags are prohibited.
- **FR-009**: When `infra/` is absent or contains no scannable files, the IaC configuration scan stage MUST complete successfully without blocking the pipeline.
- **FR-010**: Each scan stage MUST surface human-readable results (severity, affected package/resource, and remediation/fixed version where available) in the workflow run logs.
- **FR-011**: A failing image vulnerability scan MUST prevent the image from being pushed to Docker Hub and MUST prevent any subsequent Cloud Run deploy step (owned by the `009` pipeline) from running.
- **FR-012**: If a scanner cannot obtain or refresh its vulnerability/misconfiguration data, the affected stage MUST fail rather than report a false "clean" result.
- **FR-013**: The workflow MUST be documented with setup instructions covering required configuration, the IaC scan path (`infra/`), and how to update SHA pins for the actions it depends on.
- **FR-014**: Each scan stage MUST emit results in a machine-readable format that is uploaded to GitHub Code Scanning (SARIF). On pull-request runs the findings MUST surface as annotations on the pull request; on push/merge and scheduled runs against `main` the findings MUST be tracked in the GitHub Security tab. This requires the workflow to hold the minimum permission needed to upload code-scanning results.
- **FR-015**: The image vulnerability scan MUST ignore vulnerabilities that have no fix available (block only on fixable CRITICAL/HIGH). A reviewed allowlist file (`.trivyignore`) MUST be supported for documented exceptions; ignored or allowlisted findings MUST remain visible in the results but MUST NOT fail the build.
- **FR-016**: The scanning workflow MUST run on pull requests targeting `main`, on pushes/merges to `main`, and on a nightly schedule. Because a scheduled run has no freshly built PR image, the scheduled run MUST scan the most recently published image (the latest `sakurapatch/nekohoa-api` tag the `009` pipeline produced) and the current `infra/` on `main`, so newly-disclosed CVEs in unchanged code are detected.

### Key Entities

- **IaC Configuration Scan Stage**: The first pipeline stage; inspects raw OpenTofu source for security misconfigurations and enforces the severity policy.
- **Container Build Stage**: The stage that produces the application image locally on the CI runner; gated behind a passing IaC scan.
- **Image Vulnerability Scan Stage**: The stage that inspects the locally built image for known vulnerabilities before any push; gated behind a successful build.
- **Severity Policy**: The shared definition of which severities (fixable CRITICAL, HIGH) fail the pipeline versus which are reported only (MEDIUM, LOW, and unfixable findings).
- **Vulnerability Allowlist**: A reviewed `.trivyignore` file recording documented, accepted exceptions (e.g., specific CVE IDs) that remain visible but do not fail the build.
- **Pinned Action Reference**: A third-party action identified by an immutable commit SHA rather than a mutable tag or branch.

### Constitution Requirements *(mandatory when applicable)*

- **Security and abuse controls**: This feature *is* a security control. It hardens the delivery pipeline against shipping vulnerable images and insecure infrastructure, and hardens the pipeline itself against supply-chain tampering via SHA-pinned actions. Scan results that block a build are security-relevant events and MUST be visible in the run logs for auditability.
- **Observability**: Scan outcomes (pass/fail and the findings that drove them) MUST be discoverable from the workflow run so failures can be triaged without re-running locally.
- **Quality gates**: This scanning workflow is itself a CI quality gate that runs alongside existing build/test gates and must not be bypassable for a passing merge into the protected branch. Like the `infra/**` files it scans, the workflow definitions added here carry no executable test layer, so the 95% line-coverage / Sonar / Codecov gates MUST exclude them (consistent with `010`'s decision to exclude `infra/**` from coverage); they are validated by their own run outcomes, not coverage. Other application-level constitution requirements (tenant boundary, authorization, API contract, database/runtime, file storage, accessibility, frontend testing) are **not applicable** — this feature adds no application code, endpoints, data, or UI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of pipeline runs that build an image with at least one CRITICAL or HIGH vulnerability stop before the image is pushed.
- **SC-002**: 0% of images containing a CRITICAL or HIGH vulnerability reach a registry through this pipeline.
- **SC-003**: A reviewer can confirm, in under 2 minutes by reading the workflow definition, that every third-party action is pinned to an immutable commit SHA.
- **SC-004**: When a scan fails, an engineer can identify the offending package/resource, its severity, and the available fix directly from the workflow run logs without running any tool locally.
- **SC-005**: With no IaC files present (before `010` merges), the pipeline completes end-to-end (IaC scan → build → image scan) without a false failure, and the IaC stage begins enforcing automatically once OpenTofu files appear under `infra/` — with no further workflow changes required.
- **SC-006**: The scan stages add no more than a few minutes of overhead to a typical pipeline run, keeping total feedback time acceptable for routine pull requests.

## Assumptions

- **Scanner choice**: Trivy is the mandated scanning tool for both the IaC configuration scan (`trivy config`) and the image vulnerability scan (`trivy image`), per the feature request.
- **CI platform**: Scanning runs in GitHub Actions; the workflow lives alongside the repository's existing CI configuration.
- **Two-stage separation**: Scanning is intentionally kept out of the application Dockerfile and implemented as discrete workflow stages, per the request.
- **IaC location**: Per feature `010-dev-env-iac-opentofu`, OpenTofu sources live under `infra/` — a reusable shared module (`infra/modules/*`) consumed by per-environment directories (`infra/environments/{dev,staging,prod}`), each with its own tfvars and backend config. The IaC scan targets `infra/` recursively. Until `010` merges, `infra/` may be absent or empty and the scan is a non-blocking pass.
- **Relationship to `010`'s workflows**: Feature `010` already defines a plan-only PR workflow and an apply-on-merge workflow operating on `infra/**`. This `trivy config` security scan is a complementary gate on the same `infra/**` source; planning should decide whether it lives in its own workflow or is wired into `010`'s plan-on-PR job, but in all cases it must gate (run before) any apply.
- **Severity policy (current phase)**: Only CRITICAL and HIGH severities block the build; MEDIUM and LOW are reported but allowed. This threshold is expected to tighten over time.
- **Triggering**: The scanning workflow runs on pull requests targeting `main`, on pushes/merges to `main`, and on a nightly schedule (see FR-016). The scheduled run re-scans the most recently published image and `main`'s `infra/` rather than a fresh PR build.
- **Results reporting**: Findings are uploaded as SARIF to GitHub Code Scanning — surfaced as PR annotations on pull-request runs and tracked in the GitHub Security tab on push/scheduled runs — with a human-readable table also kept in the run logs (see FR-014).
- **Unfixable findings**: The image scan uses an ignore-unfixed policy plus a reviewed `.trivyignore` allowlist, so only fixable CRITICAL/HIGH findings (and non-allowlisted ones) block the build (see FR-015).
- **Registry/deploy push is out of scope here**: The application image is `sakurapatch/nekohoa-api:<sha>`, built and pushed to **Docker Hub** by the existing `009-dev-auto-deploy` pipeline, which then deploys it to the `nekohoa-api-dev` Cloud Run service. This spec defines the scan gates that must pass *before* that push/deploy; the push and Cloud Run deploy mechanics themselves are owned by `009` and only required to be ordered *after* a successful image scan.
- **Dependency**: Full enforcement value of the IaC stage depends on feature `010-dev-env-iac-opentofu`, which is specified (with plan, tasks, and contracts) but not yet merged. This spec is intentionally written ahead of that merge so the gate enforces automatically against `infra/` the moment `010` lands.
