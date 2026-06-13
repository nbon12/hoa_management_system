# Feature Specification: Trivy Security Scanning Pipeline

**Feature Branch**: `011-trivy-security-scanning`  
**Created**: 2026-06-13  
**Status**: Draft  
**Input**: User description: "I want to implement Trivy security scanning for my OpenTofu (IaC) and Google Cloud Run deployment pipeline. Following industry best practices, I want to separate the scan into two distinct stages within GitHub Actions rather than baking Trivy into the Dockerfile layers. Two-stage scan: (1) `trivy config` on raw OpenTofu code, (2) build the container image, (3) `trivy image` on the locally built image before push. Pin all GitHub Actions to immutable commit SHAs. Fail the build on CRITICAL or HIGH severity findings only."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Block vulnerable container images before they reach a registry (Priority: P1)

As an engineer deploying the HOA backend/frontend to Google Cloud Run, I want the container image I build in CI to be scanned for known vulnerabilities *before* it is pushed to any registry, so that a critically vulnerable image never becomes deployable.

**Why this priority**: This is the highest-value safety net. An image that has already been pushed to a registry can be pulled and deployed; catching critical/high vulnerabilities while the image still only exists on the CI runner is the cheapest and safest place to stop it. This story alone delivers a working MVP even before the IaC scanning stage exists.

**Independent Test**: Build an image known to contain a HIGH or CRITICAL CVE (e.g., an intentionally outdated base image) and confirm the pipeline fails at the image-scan stage and never reaches the push/deploy step. Then build a clean image and confirm the pipeline proceeds.

**Acceptance Scenarios**:

1. **Given** a freshly built container image that contains at least one CRITICAL or HIGH severity vulnerability, **When** the image vulnerability scan stage runs, **Then** the stage exits with a non-zero status, the pipeline stops, and the image is not pushed to any registry.
2. **Given** a freshly built container image whose only findings are MEDIUM or LOW severity, **When** the image vulnerability scan stage runs, **Then** the stage passes and the pipeline continues to subsequent steps.
3. **Given** the image scan runs, **When** it completes, **Then** a human-readable list of detected vulnerabilities (including severity, package, and fixed version where available) is visible in the workflow run logs.

---

### User Story 2 - Catch insecure Infrastructure-as-Code misconfigurations early (Priority: P2)

As an engineer who will soon author OpenTofu configurations for Cloud Run and supporting infrastructure, I want my raw IaC source scanned for security misconfigurations at the very start of the pipeline, so that insecure infrastructure definitions are flagged before any build or deploy work happens.

**Why this priority**: The IaC authoring feature is not yet complete, so this stage cannot yet block real infrastructure. Establishing the stage now means it is ready the moment IaC files land, and it runs first so misconfigurations are surfaced before compute is spent on building images. It is P2 because it provides no enforcement value until IaC files exist.

**Independent Test**: Place a sample OpenTofu file containing a known misconfiguration into the scanned directory and confirm the config scan stage reports it. With no IaC files present, confirm the stage completes without failing the pipeline.

**Acceptance Scenarios**:

1. **Given** the scanned IaC directory contains an OpenTofu file with a CRITICAL or HIGH severity misconfiguration, **When** the IaC configuration scan stage runs, **Then** the stage exits non-zero and the pipeline stops before the build stage.
2. **Given** the scanned IaC directory contains no IaC files yet (current state of the project), **When** the IaC configuration scan stage runs, **Then** the stage completes successfully (no findings) and does not block the pipeline.
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

- **No IaC files yet**: The IaC scan stage must succeed (treat "nothing to scan" as a pass) so the pipeline is usable before the OpenTofu feature lands. It must not be silently skipped in a way that hides future regressions once files exist.
- **Vulnerability database unavailable**: If Trivy cannot download or refresh its vulnerability/misconfiguration database (network/registry outage), the scan stage should fail loudly rather than pass with zero findings, so a transient outage is never mistaken for a clean result.
- **Build failure**: If the container image fails to build, the image-scan stage must not run against a stale or missing image; the pipeline stops at the build stage.
- **Only MEDIUM/LOW findings**: Such findings must be reported for visibility but must not fail the build under the current policy.
- **Severity policy drift**: The set of failing severities (CRITICAL, HIGH) must be defined in one place so it can be tightened later (e.g., add MEDIUM) without editing multiple stages.
- **Mixed-severity result**: An image with both LOW and CRITICAL findings must fail (presence of any CRITICAL/HIGH is sufficient to fail).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pipeline MUST run an IaC configuration scan against the raw OpenTofu source as the first scanning stage, before any container image is built.
- **FR-002**: The pipeline MUST build the application container image as a distinct stage that runs only after the IaC configuration scan stage has passed.
- **FR-003**: The pipeline MUST run an image vulnerability scan against the locally built image, and this scan MUST occur before the image is pushed to any registry.
- **FR-004**: Security scanning MUST be performed as separate pipeline stages and MUST NOT be embedded as layers or steps inside the application Dockerfile.
- **FR-005**: Each scan stage MUST fail the pipeline (non-zero exit) when it detects at least one finding of CRITICAL or HIGH severity.
- **FR-006**: Each scan stage MUST allow the pipeline to continue when the only findings are MEDIUM or LOW severity (these are reported but non-blocking under the current policy).
- **FR-007**: The set of severities that cause a failure MUST be defined consistently across both scan stages and MUST be expressed so it can be tightened later without restructuring the workflow.
- **FR-008**: Every third-party GitHub Action referenced by the workflow MUST be pinned to a full-length immutable commit SHA; branch references and floating version tags are prohibited.
- **FR-009**: When the IaC source directory contains no scannable files, the IaC configuration scan stage MUST complete successfully without blocking the pipeline.
- **FR-010**: Each scan stage MUST surface human-readable results (severity, affected package/resource, and remediation/fixed version where available) in the workflow run logs.
- **FR-011**: A failing image vulnerability scan MUST prevent the image from being pushed to a registry and MUST prevent any subsequent deploy step from running.
- **FR-012**: If a scanner cannot obtain or refresh its vulnerability/misconfiguration data, the affected stage MUST fail rather than report a false "clean" result.
- **FR-013**: The workflow MUST be documented with setup instructions covering required configuration, the directory scanned for IaC, and how to update SHA pins for the actions it depends on.

### Key Entities

- **IaC Configuration Scan Stage**: The first pipeline stage; inspects raw OpenTofu source for security misconfigurations and enforces the severity policy.
- **Container Build Stage**: The stage that produces the application image locally on the CI runner; gated behind a passing IaC scan.
- **Image Vulnerability Scan Stage**: The stage that inspects the locally built image for known vulnerabilities before any push; gated behind a successful build.
- **Severity Policy**: The shared definition of which severities (CRITICAL, HIGH) fail the pipeline versus which are reported only (MEDIUM, LOW).
- **Pinned Action Reference**: A third-party action identified by an immutable commit SHA rather than a mutable tag or branch.

### Constitution Requirements *(mandatory when applicable)*

- **Security and abuse controls**: This feature *is* a security control. It hardens the delivery pipeline against shipping vulnerable images and insecure infrastructure, and hardens the pipeline itself against supply-chain tampering via SHA-pinned actions. Scan results that block a build are security-relevant events and MUST be visible in the run logs for auditability.
- **Observability**: Scan outcomes (pass/fail and the findings that drove them) MUST be discoverable from the workflow run so failures can be triaged without re-running locally.
- **Quality gates**: This scanning workflow is itself a CI quality gate that runs alongside existing build/test gates and must not be bypassable for a passing merge into the protected branch. Other application-level constitution requirements (tenant boundary, authorization, API contract, database/runtime, file storage, accessibility, frontend testing) are **not applicable** — this feature adds no application code, endpoints, data, or UI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of pipeline runs that build an image with at least one CRITICAL or HIGH vulnerability stop before the image is pushed.
- **SC-002**: 0% of images containing a CRITICAL or HIGH vulnerability reach a registry through this pipeline.
- **SC-003**: A reviewer can confirm, in under 2 minutes by reading the workflow definition, that every third-party action is pinned to an immutable commit SHA.
- **SC-004**: When a scan fails, an engineer can identify the offending package/resource, its severity, and the available fix directly from the workflow run logs without running any tool locally.
- **SC-005**: With no IaC files present, the pipeline completes end-to-end (IaC scan → build → image scan) without a false failure, and the IaC stage begins enforcing automatically once OpenTofu files are added — with no further workflow changes required.
- **SC-006**: The scan stages add no more than a few minutes of overhead to a typical pipeline run, keeping total feedback time acceptable for routine pull requests.

## Assumptions

- **Scanner choice**: Trivy is the mandated scanning tool for both the IaC configuration scan (`trivy config`) and the image vulnerability scan (`trivy image`), per the feature request.
- **CI platform**: Scanning runs in GitHub Actions; the workflow lives alongside the repository's existing CI configuration.
- **Two-stage separation**: Scanning is intentionally kept out of the application Dockerfile and implemented as discrete workflow stages, per the request.
- **IaC location**: OpenTofu sources will live in a dedicated, conventionally named directory (e.g., an `infra/` or `tofu/` path) that the IaC scan targets; the exact path is finalized when the IaC authoring feature lands. Until then the directory may be empty and the scan is a non-blocking pass.
- **Severity policy (current phase)**: Only CRITICAL and HIGH severities block the build; MEDIUM and LOW are reported but allowed. This threshold is expected to tighten over time.
- **Triggering**: The scanning workflow runs on pull requests targeting the protected branch and on pushes to it, consistent with the project's existing CI conventions; exact triggers are confirmed during planning.
- **Registry/deploy push is out of scope here**: This spec defines the scan gates that must pass *before* a push/deploy; the push and Cloud Run deploy mechanics themselves are owned by the deployment feature and only required to be ordered *after* a successful image scan.
- **Dependency**: Full enforcement value of the IaC stage depends on the not-yet-completed IaC/OpenTofu authoring feature; this spec is intentionally written ahead of that work so the gate is ready on arrival.
