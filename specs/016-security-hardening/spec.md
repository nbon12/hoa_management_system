# Feature Specification: Security Hardening Program

**Feature Branch**: `016-security-hardening`
**Created**: 2026-07-01
**Status**: Draft
**Input**: User description: "Incorporate security review findings (application, infrastructure, and AI/agentic supply-chain) into remediation specs, split across multiple focused spec documents by domain"

## Overview

A multi-agent security review of the HOAManagementCompany platform (backend, frontend, CI/CD, infrastructure, and the AI/agentic tooling that writes and deploys code) produced a set of findings ranging from Critical to Low. This feature is the **umbrella program** that tracks remediation of those findings. Because the findings span independent domains with different owners, blast radii, and test strategies, the work is **split into six focused specs** (below). Each sub-spec is independently plannable, testable, and deliverable; this umbrella document defines the shared goals, the severity inventory, and the cross-cutting success criteria.

This umbrella spec deliberately contains **no implementation detail**. Each sub-spec owns the concrete requirements for its domain.

## Sub-specifications

| # | Sub-spec | Scope | Top severity |
|---|----------|-------|--------------|
| A | [`spec-identity-access.md`](./spec-identity-access.md) | Registration/property-claim integrity, login brute-force resistance, account enumeration, anonymous destructive endpoints, token/JWT hardening | High |
| B | [`spec-payments-integrity.md`](./spec-payments-integrity.md) | Ledger write atomicity / double-credit, idempotency isolation, settlement amount cross-checks | Med–High |
| C | [`spec-platform-data-protection.md`](./spec-platform-data-protection.md) | PII redaction in logs, error-detail exposure, resource-exhaustion (pagination/rate limits), input validation, transport/security headers | High |
| D | [`spec-frontend-security.md`](./spec-frontend-security.md) | Token storage, content-security-policy, committed test credentials, request-origin scoping | High |
| E | [`spec-cicd-infra-least-privilege.md`](./spec-cicd-infra-least-privilege.md) | CI credential blast radius, PR-triggered secret exposure, action pinning, container hardening, branch protection | Critical |
| F | [`spec-ai-supply-chain.md`](./spec-ai-supply-chain.md) | Autonomous merge agent, prompt-injection surfaces, agent tool-permission model, unpinned agent tooling | Critical |

## Clarifications

### Session 2026-07-02

- Q: Property-claim proof-of-entitlement mechanism (Sub-spec A)? → A: One-time claim code only (mailed/emailed to the owner's contact on file); no administrator-approval path. A deliverable contact channel on file is a precondition for self-service claim.
- Q: Refresh-token storage target end-state (Sub-spec D)? → A: Full fix — backend-set HttpOnly/Secure/SameSite cookie, access token in memory, silent refresh on startup. No interim-only or frontend-only end-state.
- Q: Autonomous dependency-merge agent end-state (Sub-spec F)? → A: Keep the AI agent but drive its decisions only from structured metadata (bot author, labels, update type, check status) and gate every merge behind branch protection; never decide from free-text PR/changelog content.
- Q: Security-header / CSP delivery location (Sub-specs C and D)? → A: Repo-controlled — application middleware (API) plus a frontend build-output headers file (CSP) — asserted by automated tests; not the edge/dashboard.
- Q: Login lockout policy (Sub-spec A)? → A: Lock the account for 30 minutes after 10 failed attempts, counted per-account (independent of source IP).
- Q: Access-token revocation strength on logout/property-switch (Sub-spec A FR-A10)? → A: Accept the 15-minute stateless window as documented accepted risk; refresh-token rotation remains the control (no jti deny-list, no TTL reduction).
- Q: Registration/claim enumeration defense (Sub-spec A)? → A: Require an email-verification gate — the caller must prove control of the email before any registration/claim state is revealed or a claim code is issued.
- Q: One-time property-claim code validity window (Sub-spec A)? → A: 3 months (90 days), single-use. The long window is an accepted tradeoff for physical-mail delivery, mitigated by single-use, the email-verification gate, and delivery only to the owner's contact on file.
- Q: Remediate historical duplicate ledger credits (Sub-spec B)? → A: Forward-only — prevent new double-credits; existing duplicates are not auto-remediated in this program.
- Q: Settlement amount-mismatch handling (Sub-spec B FR-B5)? → A: Block the credit and record the mismatch in a manual review queue for human resolution.
- Q: Owner email-change handling (Sub-spec C FR-C6)? → A: The new address takes effect only after the owner verifies control of it; the login/identity store is kept in sync.
- Q: Deployed non-local (Dev) error detail (Sub-spec C FR-C5)? → A: Return only a generic error message plus a correlation ID; full detail is available only in local development and via server-side logs.
- Q: CSP rollout mode (Sub-spec D)? → A: Ship the CSP in enforcing mode from day one (no report-only phase), given no unsafe sinks and a known origin set.
- Q: CI deployer identity shape (Sub-spec E FR-E2)? → A: Split into a read-only plan identity (assumable from any ref) and a separate apply identity (assumable only from the protected default branch).
- Q: Dependency-PR merge policy / branch protection (Sub-specs E & F)? → A: Status-checks-only — required checks gate merges; human review is NOT mandated. The constrained agent may therefore merge on green; this residual risk is ACCEPTED and offset by the agent's metadata-only decisions, scope limit, notifications, and deny-list.
- Q: Per-PR ephemeral-env credential model (Sub-spec E FR-E12)? → A: Each PR environment uses a distinct database role/credential (no shared password).
- Q: Agent tooling (rtk/headroom) in the Bash hot path (Sub-spec F)? → A: Keep both, but pin the installer to an immutable version with checksum verification and constrain the command-rewrite output.
- Q: Local model channel ANTHROPIC_BASE_URL=127.0.0.1:8787 (Sub-spec F)? → A: Keep it, but confirm ownership, verify it is a trusted access-restricted local process, and document it.
- Q: Agent deny-list aggressiveness (Sub-spec F FR-F6)? → A: Minimal targeted deny — block arbitrary command passthrough and writes to agent-config paths (rather than a broad dangerous-class set), to minimize false positives.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eliminate the paths that grant an attacker real-world control (Priority: P1)

As the platform owner, I need the Critical/High findings that let an attacker take over a resident account, mint cloud-owner credentials, or make an AI agent execute attacker-supplied instructions to be closed first, because these are the findings with direct financial, data-breach, or full-environment-compromise impact.

**Why this priority**: These are the findings where a single exploited weakness yields durable, high-impact control (account takeover, project-Owner cloud token, autonomous merge/deploy of malicious code). They gate the value of everything else.

**Independent Test**: For each P1 finding, a regression test or configuration assertion demonstrates the exploit path is closed (e.g., a fork/branch PR cannot mint cloud credentials; a crafted account number cannot claim an unowned property; an injected instruction in indexed content does not trigger a side-effectful command).

**Acceptance Scenarios**:

1. **Given** the remediated system, **When** the exploit described in each P1 finding is attempted, **Then** it fails and the attempt is observable (logged/audited) where applicable.
2. **Given** the CI pipeline, **When** any workflow run assumes cloud identity, **Then** it receives only the enumerated privileges it needs, and apply-capable identity is reachable only from the protected default branch.

---

### User Story 2 - Close the data-leak and integrity gaps (Priority: P2)

As the platform owner, I need PII to stop leaking into external log sinks, ledger writes to be crash-safe against double-crediting, and account enumeration/lockout gaps closed, because these are Medium-severity findings that cause quiet, ongoing harm (privacy exposure, financial-record corruption, credential-stuffing enablement) rather than immediate takeover.

**Why this priority**: High-probability, medium-impact issues that degrade trust and correctness over time. They do not require an attacker to breach the perimeter first.

**Independent Test**: Each P2 finding has an automated test asserting the corrected behavior (e.g., emitted log events contain `[REDACTED]` in place of email; concurrent/redelivered settlement produces exactly one ledger credit; repeated failed logins lock the account).

**Acceptance Scenarios**:

1. **Given** a login or registration event, **When** the system emits telemetry, **Then** no configured sensitive field (email, name, token, card/account number) appears in any log sink.
2. **Given** a payment that settles via the deferred path, **When** the settlement webhook is redelivered or races with reconciliation, **Then** the owner's ledger is credited exactly once.

---

### User Story 3 - Apply defense-in-depth hardening (Priority: P3)

As the platform owner, I need the Low-severity and defense-in-depth items (algorithm pinning, security headers, non-root containers, input length caps, deny rules, digest-pinned images) addressed so that a future first-order mistake is contained rather than immediately exploitable.

**Why this priority**: These reduce blast radius and raise attacker cost. They are individually low-impact but collectively raise the floor.

**Independent Test**: Configuration and header assertions verify each control is present (e.g., responses carry the expected security headers; containers run as a non-root user; the permission config denies the enumerated dangerous command classes).

**Acceptance Scenarios**:

1. **Given** any deployed service, **When** its runtime identity and response headers are inspected, **Then** they match the hardened baseline defined in the relevant sub-spec.

---

### Edge Cases

- A remediation in one sub-spec must not reintroduce a finding covered by another (e.g., adding a security-header middleware must not break the frontend CSP requirements).
- Findings that appear in more than one review (e.g., the auto-merge risk, container-as-root, null-claim 500s) are assigned a single owning sub-spec to avoid duplicate or conflicting requirements; the owning sub-spec is named in this umbrella.
- Some findings are **accepted risks** (documented, not fixed) — each sub-spec must explicitly mark accepted-risk items rather than silently dropping them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The program MUST track every finding from the security review to a resolution state of one of: `fixed`, `accepted-risk (documented)`, or `superseded`. No finding may be silently dropped.
- **FR-002**: Each finding MUST be assigned to exactly one owning sub-spec; cross-cutting findings MUST name their owning sub-spec to prevent duplicate requirements.
- **FR-003**: Every finding marked `fixed` MUST have at least one automated test or enforced configuration assertion that fails if the vulnerability is reintroduced.
- **FR-004**: Every finding marked `accepted-risk` MUST record the rationale and the compensating control in its sub-spec.
- **FR-005**: The program MUST preserve prioritization: P1 (Critical/High) remediations MUST be deliverable and verifiable independently of P2/P3 work.
- **FR-006**: Remediations MUST NOT introduce new secrets into version control, weaken tenant isolation, or reduce existing passing test coverage.

### Key Entities

- **Finding**: A reviewed weakness with attributes: severity, domain, owning sub-spec, exploit path, resolution state, verifying test.
- **Sub-spec**: A focused remediation specification (A–F) owning a coherent set of findings.
- **Resolution state**: One of `fixed`, `accepted-risk`, `superseded`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Critical and High findings reach `fixed` state with a verifying automated test or enforced configuration check before the program is considered complete.
- **SC-002**: 100% of all findings (all severities) reach a documented resolution state (`fixed`, `accepted-risk`, or `superseded`); zero findings remain untracked.
- **SC-003**: Zero live credentials, tokens, or signing secrets are present in the tracked repository or in commits made from this point forward, after remediation.
- **SC-004**: No automated agent (CI or scheduled) can merge to, or deploy from, the protected default branch without either enforced status-check gating or human review, as verified by attempting the action.
- **SC-005**: An independent re-review of the remediated system reproduces zero of the previously reported Critical/High findings.
- **SC-006**: All existing test suites continue to pass, and the added regression tests pass, on the feature branch prior to merge.

## Assumptions

- The findings inventory used as input is the multi-agent review conducted 2026-07-01; if new findings emerge during planning they are added to the appropriate sub-spec.
- Some remediations (branch protection, the scheduled cloud merge-agent configuration, Cloudflare edge headers) live outside the repository and are actioned in their respective dashboards; the sub-specs capture the required end state even when the change is not a code change.
- The production environment already sources secrets from a managed secret store; committed dev/test placeholder values are in scope only for hygiene, not because production reuses them.
- Work proceeds on the `016-security-hardening` feature branch; each sub-spec may be planned and delivered as its own vertical slice.
