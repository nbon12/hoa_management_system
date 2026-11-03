# Sub-Spec E: CI/CD & Infrastructure Least Privilege

**Feature Branch**: `021-security-hardening-subspec-e`
**Split from**: [016 umbrella](../016-security-hardening/spec.md) — shared plan/tasks/research/data-model/contracts live there.
**Created**: 2026-07-01
**Status**: Draft

## Overview

The fork-PR trust boundary is well designed (no `pull_request_target`; fork PRs receive no secrets and cannot trigger provisioning). The concentrated risk is **credential blast radius**: the CI deployer identity holds project-Owner and is assumable by any workflow run in the repository (including unmerged PR branches), and several PR-triggered jobs hand the full operator-secret set to code that arrives in the PR itself (infrastructure plan of PR-authored config; job-wide secrets exposed to PR-authored dependency install and end-to-end scripts). The action-pinning claim is also only half true — the most privileged workflows are pinned to mutable tags. Container and branch-protection hardening round out the sub-spec.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Least-privilege cloud identity, ref-scoped to the protected branch (Priority: P1)

The CI identity that can change infrastructure holds only the specific privileges it needs, and apply/deploy-capable identity is assumable only from the protected default branch — not from an arbitrary PR branch.

**Why this priority**: The deployer identity currently holds project-Owner and its assumption condition is repo-scoped, not ref-scoped. Any code execution in any workflow run — a PR-modified workflow, a compromised dependency, a hijacked action — can mint an Owner-level cloud token and read every secret, rewrite access controls, or destroy state. Fixing this de-fangs several other findings at once.

**Independent Test**: Attempt to assume apply/deploy-capable cloud identity from a non-default-branch workflow run; it is denied. Enumerate the deployer identity's roles; project-Owner is absent and only the required roles remain.

**Acceptance Scenarios**:

1. **Given** a workflow run on a non-default branch, **When** it attempts to assume apply/deploy-capable cloud identity, **Then** assumption is denied.
2. **Given** the deployer identity, **When** its granted roles are inspected, **Then** they are an enumerated least-privilege set with no project-Owner grant.
3. **Given** a read-only plan operation, **When** it runs on a PR, **Then** it uses a distinct read-only identity that cannot apply changes.

---

### User Story 2 - Never hand write-capable secrets to PR-authored code (Priority: P1)

Workflows triggered by pull requests do not expose write-capable secrets or the operator-secret set to code that arrived in the PR (infrastructure configuration, dependency install scripts, or end-to-end test code).

**Why this priority**: The infrastructure-plan job passes the full operator-secret map to a plan of PR-authored configuration, which can execute data sources/providers during plan and exfiltrate every secret pre-merge. Separately, per-PR environment jobs expose infrastructure secrets at job scope to steps that run PR-authored dependency install and e2e code. Both let a same-repo branch PR exfiltrate secrets without a merge.

**Independent Test**: Confirm the plan job runs with placeholder/read-only credentials, not the operator-secret set. Confirm secrets are scoped to only the specific steps that require them, not the whole job.

**Acceptance Scenarios**:

1. **Given** a PR that modifies infrastructure configuration, **When** the plan job runs, **Then** it has no access to write-capable operator secrets.
2. **Given** a per-PR environment job, **When** dependency-install and e2e steps run, **Then** those steps do not have infrastructure secrets in their environment.
3. **Given** any privileged infrastructure job, **When** it runs, **Then** it is gated behind a required-reviewer environment.

---

### User Story 3 - Pin the supply chain and lock the default branch (Priority: P2)

All third-party CI actions are pinned to immutable references, and the protected default branch requires the defined status checks to pass before merge. *(Per the 2026-07-02 status-checks-only decision, human review is not a mandated gate; an automated actor may merge on green — see FR-E7a accepted risk.)*

**Why this priority**: The privileged workflows pin actions to mutable tags — exactly the jobs holding the powerful credentials — so a hijacked tag executes with those credentials. There is no code-ownership/required-review file, and the branch-lock workflow is broken, so the default branch lacks an enforced human/automated review gate (this connects to Sub-Spec F).

**Independent Test**: Verify every action reference is a full commit digest. Verify branch protection requires the defined status checks to pass before merge.

**Acceptance Scenarios**:

1. **Given** any workflow, **When** its action references are inspected, **Then** each is pinned to an immutable commit digest.
2. **Given** the default branch, **When** a merge is attempted without passing the required status checks, **Then** it is blocked.

---

### User Story 4 - Harden containers and residual infrastructure exposure (Priority: P3)

Container images run as non-root, base images are digest-pinned, local-development services do not bind to externally reachable interfaces, and per-PR credentials are appropriately scoped.

**Why this priority**: Defense-in-depth. Application/frontend containers run as root; base images use mutable tags; local compose services bind to all interfaces; a shared per-PR database password is reused across all PR environments; the branch-name value flows into a scanner argument; and the branch-lock workflow declares an invalid permission scope and cannot function as written.

**Independent Test**: Inspect running containers for a non-root user; inspect image references for digests; inspect compose bindings; verify the scanner argument and branch-lock workflow are corrected.

**Acceptance Scenarios**:

1. **Given** a deployed container, **When** its runtime user is inspected, **Then** it is non-root.
2. **Given** the local compose stack, **When** its port bindings are inspected, **Then** management/data services bind to the loopback interface, not all interfaces.
3. **Given** the branch-name input to the scanner, **When** it is processed, **Then** it is passed as data (not interpolated into a shell argument) or validated against a safe pattern.

---

### Edge Cases

- Splitting plan (read-only, any ref) from apply (write, default branch only) identities must not break legitimate PR plan previews.
- Moving secrets to step scope must still provide them to the specific steps that genuinely need them (infrastructure operations), or those steps fail.
- Digest-pinning must be maintained over time (the dependency-update tooling already keeps action pins current and should extend to digests).
- Branch protection must still allow the intended automated workflow (dependency updates) through its defined gates rather than blocking all automation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-E1**: The infrastructure deployer identity MUST hold an enumerated least-privilege role set with no project-Owner grant.
- **FR-E2**: The deployer identity MUST be **split into two service accounts**: a read-only *plan* identity assumable from any ref, and a separate *apply/deploy* identity assumable only from the protected default branch. *(Clarified 2026-07-02: two-SA split, not a single ref-scoped SA.)*
- **FR-E3**: Pull-request-triggered jobs MUST NOT expose write-capable or operator secrets to plans of PR-authored configuration; plan runs MUST use placeholder or read-only credentials.
- **FR-E4**: Secrets in per-PR and infrastructure workflows MUST be scoped to only the specific steps that require them, never at job scope where PR-authored install/e2e code can read them.
- **FR-E5**: Privileged infrastructure jobs MUST be gated behind a required-reviewer environment.
- **FR-E6**: All third-party CI action references MUST be pinned to immutable commit digests.
- **FR-E7**: The default branch MUST enforce branch protection requiring the defined status checks to pass before merge. *(Clarified 2026-07-02: **status-checks-only** — human review is NOT mandated as a merge gate. This is an accepted trade-off; see the accepted-risk note below. A code-ownership file MAY be added to route review requests, but review is advisory, not a hard gate.)*
- **FR-E7a (accepted risk)**: Because merges are gated by status checks only, an automated actor (including the constrained dependency-merge agent in Sub-Spec F) can complete a merge on green without human review. This residual risk is **accepted** and offset by: the agent acting only on structured metadata (F FR-F1), its restriction to a safe update scope (F FR-F3), merge/modify notifications (F FR-F4), the deny-list (F FR-F6), and required status checks including security scans. If this risk is later deemed too high, FR-E7 is tightened to require review.
- **FR-E8**: Container images MUST run as a non-root user, and base images MUST be pinned by digest.
- **FR-E9**: Local-development compose services (management/data planes) MUST bind to the loopback interface rather than all interfaces.
- **FR-E10**: Attacker-influenced values (e.g., branch names) MUST be passed to tools as data or validated against a safe character pattern, never interpolated directly into a shell/command argument.
- **FR-E11**: The branch-lock workflow MUST either be corrected to function with an appropriately privileged credential or replaced by native branch protection (FR-E7); it MUST NOT remain a silently failing control.
- **FR-E12**: Each per-PR ephemeral environment MUST use a **distinct database role/credential**, so compromise of one PR environment does not disclose a credential valid for the others. *(Clarified 2026-07-02: distinct per-PR roles — not a shared rotated password.)*

### Key Entities

- **Deployer identity**: The CI cloud identity, now least-privilege and split into read-only (plan) and apply (default-branch-only) roles.
- **Workflow secret scope**: Secrets bound to specific steps, not whole jobs.
- **Action reference**: A third-party action pinned by immutable digest.
- **Branch protection / code ownership**: The enforced review and status-check gate on the default branch.

### Security & Abuse Controls *(constitution subset)*

- **Least privilege**: CI identities hold only required roles; apply capability is ref-scoped to the protected branch.
- **Supply chain**: Actions digest-pinned; base images digest-pinned; automated updates keep pins current.
- **Secrets handling**: No write-capable secrets reach PR-authored code; secrets never at job scope alongside untrusted install/e2e steps; per-PR credentials scoped/rotated.
- **Auditability**: Privileged jobs gated by required-reviewer environments; default-branch changes gated by protection + review.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-E1**: The deployer identity has 0 project-Owner grants and only enumerated roles, verified by inspecting the infrastructure definition.
- **SC-E2**: Apply/deploy-capable identity cannot be assumed from any non-default-branch workflow run, verified by attempting it.
- **SC-E3**: No pull-request-triggered job exposes write-capable/operator secrets to PR-authored plan/install/e2e code, verified by reviewing job and step scoping.
- **SC-E4**: 100% of third-party action references are digest-pinned.
- **SC-E5**: The default branch cannot be merged to without passing the required status checks, verified by attempting a merge with failing/pending checks.
- **SC-E6**: All deployed containers run as non-root and reference digest-pinned base images, verified by inspection.
- **SC-E7**: The fork-PR trust boundary remains intact (no `pull_request_target`; fork PRs get no secrets), verified by re-review.

## Assumptions

- The cloud platform supports ref-scoped workload-identity conditions and required-reviewer environments (it does; the auth limiter and existing environments demonstrate the primitives).
- Read-only plan previews are acceptable with placeholder values or a dedicated read-only identity.
- The dependency-update tooling that keeps action pins current can be extended to digests.
- Public unauthenticated ingress on the API service is a deliberate design choice (public API + smoke tests) and is accepted risk, provided the destructive dev-tools endpoint is secret-gated and environment-gated per Sub-Spec A.
