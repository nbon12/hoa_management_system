# Sub-Spec F: AI / Agentic Supply-Chain Security

**Feature Branch**: `016-security-hardening`
**Parent**: [`spec.md`](./spec.md)
**Created**: 2026-07-01
**Status**: Draft

## Overview

AI agents in this project can write code, run shell commands, merge pull requests, and trigger deploys. The continuous-integration pipeline itself contains **no** AI (verified: no in-CI model invocation; the code-intelligence indexing runs without a model), which eliminates the classic "fork PR reaches a privileged AI reviewer in CI" class. The real exposure is elsewhere: a **scheduled autonomous cloud agent that merges dependency pull requests** with shell + merge authority and no human gate, and the **local agent's permission and tooling configuration** that turns any successful prompt injection (from a PR diff, a document, a code comment, indexed content, or CI logs) into non-interactive command execution. This sub-spec treats **all content the agent reads as untrusted data** and constrains **what an agent is permitted to do without a human in the loop**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Constrain the autonomous merge agent (Priority: P1)

The scheduled agent that processes dependency update pull requests cannot be steered by attacker-controlled content (e.g., a dependency changelog or PR body) into merging malicious code, modifying unrelated code, or exfiltrating credentials, and it is never the sole approver of a merge to the protected branch.

**Why this priority**: A daily autonomous agent reads dependency PR content — which embeds upstream release notes/changelogs controlled by whoever publishes the package — into a context that holds shell + merge authority, is told to resolve everything without asking, and merges to the default branch silently (no notifications). Because merging triggers a deploy that runs with high-privilege cloud credentials, a steered or malicious merge is also a deploy. This is the highest-impact agentic finding and it does not require a human to open a malicious PR.

**Independent Test**: Provide a dependency PR whose changelog/body contains embedded instructions; the agent applies only the dependency version change (or defers to a human) and does not act on the embedded instructions, does not modify unrelated files, and does not exfiltrate. Confirm the agent cannot complete a merge to the protected branch without the enforced gates (required status checks + review).

**Acceptance Scenarios**:

1. **Given** a dependency PR whose body/changelog contains embedded instructions, **When** the agent processes it, **Then** it treats that text as data, acts only on structured metadata, and does not perform any action the embedded text requests.
2. **Given** the agent's merge attempt, **When** it targets the protected branch, **Then** the merge succeeds only if the required status checks pass and the PR is within the agent's permitted metadata-defined scope; the agent MUST NOT bypass or disable checks. *(Per the 2026-07-02 status-checks-only decision, no human review is required at this gate; the metadata/scope constraints and required checks are the enforced controls.)*
3. **Given** the agent restricts itself to permitted update types (e.g., patch/minor), **When** a PR falls outside that scope, **Then** the agent defers to a human rather than merging.
4. **Given** the agent performs an action, **When** it merges or modifies code, **Then** a notification is emitted so the activity is not silent.

---

### User Story 2 - Remove the local arbitrary-command bypass and add a deny backstop (Priority: P1)

The local agent's permission configuration cannot be used to run arbitrary shell commands without a prompt, and a deny list categorically blocks the dangerous command classes that prompt injection would target.

**Why this priority**: The permission allow-list contains a command-passthrough wrapper entry that auto-approves **any** wrapped shell command with no prompt, nullifying the otherwise-narrow allow-list. Combined with auto-accept mode and no deny rules, any injected instruction reaching the local agent executes non-interactively. This is the local equivalent of remote code execution via injection.

**Independent Test**: Confirm the passthrough wrapper entry is removed from all allow-lists and that deny rules block the enumerated dangerous classes (arbitrary command passthrough, piping remote content to a shell, merges, writes to agent configuration). Attempt an injected dangerous command; it is blocked or prompts, never auto-runs.

**Acceptance Scenarios**:

1. **Given** the permission configuration, **When** it is inspected, **Then** it contains no entry that auto-approves arbitrary wrapped commands.
2. **Given** a deny list, **When** a command in a dangerous class is attempted, **Then** it is blocked regardless of allow-list entries (deny takes precedence).

---

### User Story 3 - Pin and verify agent tooling in the command path (Priority: P2)

Binaries that the agent installs at session start and routes every shell command through are pinned to immutable, verified versions, so a compromise of their upstream cannot silently execute code in — or rewrite the commands of — a privileged agent session.

**Why this priority**: A session-start hook installs a helper by piping remote content from a mutable branch tip directly into a shell, and a pre-command hook then routes every shell command through that helper. Whoever controls the upstream (or a network position on the fetch) gets code execution in the agent's session and can rewrite the meaning of every command the agent runs.

**Independent Test**: Confirm the installer targets an immutable version with a verified checksum/signature and fails closed on mismatch; confirm the command-rewrite output is surfaced/constrained rather than blindly trusted.

**Acceptance Scenarios**:

1. **Given** the session-start installer, **When** it runs, **Then** it fetches a pinned version, verifies its integrity, and refuses to proceed on mismatch.
2. **Given** the pre-command rewrite hook, **When** it rewrites a command, **Then** the rewrite is constrained to safe wrappers (or surfaced), not executed as opaque trusted output.

---

### User Story 4 - Treat indexed/tool content as untrusted; verify before acting (Priority: P2)

Guidance that the agent loads from repository-indexed content or tool output is treated as untrusted data to be verified against source, not as authoritative instructions, and agent-configuration files require review to change.

**Why this priority**: Project guidance currently instructs the agent to "trust and act on" code-intelligence responses, which are derived from repository prose (docs, comments, configuration) an attacker could influence. Agent-configuration files are loaded as high-authority instructions and are not gated by required review.

**Independent Test**: Confirm the guidance is reworded to "verify before acting" and that agent-configuration paths require review. Provide indexed content containing an instruction; the agent does not perform a side-effectful action solely because indexed content told it to.

**Acceptance Scenarios**:

1. **Given** indexed or tool-returned content containing an instruction, **When** the agent reads it, **Then** it treats it as untrusted data and verifies against actual source before any action.
2. **Given** a change to an agent-configuration path, **When** it is proposed, **Then** it requires review before taking effect.

---

### User Story 5 - Verify the local model channel (Priority: P3)

The channel through which the local agent's prompts and completions flow is a known, trusted endpoint, so it cannot be silently observed or used to inject tool calls.

**Why this priority**: Defense-in-depth. Local agent traffic is routed through a localhost proxy endpoint; if that endpoint is untrusted or can be pre-empted by another local process, it becomes a position to read secrets in prompts and rewrite model output.

**Independent Test**: Confirm what process owns the proxy endpoint, that it is trusted and access-restricted, and document it (or remove the override if unnecessary).

**Acceptance Scenarios**:

1. **Given** the local model channel, **When** its ownership and access are reviewed, **Then** it is a documented, trusted, access-restricted endpoint (or the override is removed).

---

### Edge Cases

- Constraining the merge agent must not break legitimate, safe automated dependency updates — the goal is enforced gating and data-not-instructions handling, not disabling automation.
- Removing the command passthrough entry must not block the legitimate read-only helper commands the allow-list intends to permit.
- Pinning the installer must include a maintenance path to advance the pin (the update tooling should cover it) so it does not silently rot.
- Deny rules must be scoped so they block dangerous classes without preventing ordinary development commands.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-F1**: The autonomous merge agent MUST treat all pull-request body/changelog/diff/comment text as untrusted data, never as instructions; its decisions MUST be driven by structured metadata (author is the dependency bot, labels, update type, check status), not free-text content.
- **FR-F2**: Every merge the agent performs MUST pass enforced branch protection (required status checks), consistent with Sub-Spec E FR-E7. *(Clarified 2026-07-02: the merge gate is **status-checks-only** — human review is not mandated — so the constrained agent MAY complete a merge on green. This is an accepted residual risk (Sub-Spec E FR-E7a); the agent is constrained by FR-F1/F3/F4/F6 rather than by a human approver, and the agent MUST NOT bypass or disable required checks.)*
- **FR-F3**: The autonomous merge agent MUST restrict auto-merge to a defined safe scope (e.g., patch/minor dependency version updates from the trusted bot) and defer anything outside that scope to a human.
- **FR-F4**: Agent actions that merge or modify code MUST emit a notification so activity is observable rather than silent.
- **FR-F5**: The local agent permission configuration MUST NOT contain any entry that auto-approves arbitrary wrapped/passthrough shell commands.
- **FR-F6**: The local agent MUST enforce a **minimal targeted** deny list that categorically blocks the known bypass — arbitrary command passthrough — and writes to agent-configuration paths; deny MUST take precedence over allow. *(Clarified 2026-07-02: minimal targeted deny, not a broad dangerous-class set, to minimize false positives. Broader classes — piping remote content into a shell, repository merges/pushes, destructive file ops, secret reads — are intentionally left to interactive prompting rather than hard deny.)*
- **FR-F7**: Agent tooling installed at session start MUST be pinned to an immutable version and integrity-verified (checksum/signature) before execution, failing closed on mismatch; remote content MUST NOT be piped directly into a shell from a mutable reference.
- **FR-F8**: Any hook that rewrites shell commands before execution MUST constrain its output to safe, known wrappers (or surface the rewrite for inspection) rather than executing opaque rewrite output as trusted.
- **FR-F9**: Project guidance MUST instruct the agent to treat code-intelligence/tool output and indexed content as untrusted data to be verified against source before any side-effectful action; the competing "trust and act" guidance MUST be removed.
- **FR-F10**: Agent-configuration paths (permission/hook/instruction files) MUST require review before changes take effect (via the code-ownership/branch-protection gate).
- **FR-F11**: The local model channel MUST be confirmed to be a trusted, access-restricted local process, and documented (retained, not removed). *(Clarified 2026-07-02: keep the `ANTHROPIC_BASE_URL` proxy but verify ownership of `:8787` and document it.)* Third-party agent plugins in the command path (rtk and headroom) MUST be **retained but pinned and integrity-verified** like other tooling (FR-F7/F8), rather than removed. *(Clarified 2026-07-02: keep both tools for their token savings; close the supply-chain/rewrite risk via pinning + verification + constrained rewrite output.)*

### Key Entities

- **Autonomous merge agent**: The scheduled cloud agent; now metadata-driven, scope-limited, gated by branch protection, and non-silent.
- **Agent permission policy**: Allow-list without arbitrary passthrough, plus a precedence-taking deny list.
- **Agent tooling**: Session-start installer and command-rewrite hook, now pinned and integrity-verified.
- **Trust posture**: Indexed/tool content treated as untrusted data; agent-config changes gated by review.

### Security & Abuse Controls *(constitution subset)*

- **Instruction-source boundary**: Untrusted content (PRs, changelogs, logs, indexed prose, tool output) is data, not commands, for every agent.
- **Least privilege / human-in-the-loop**: No agent merges to or deploys from the protected branch without enforced gating; dangerous command classes are denied non-interactively.
- **Supply chain**: Agent tooling and plugins in the command path are pinned and integrity-verified; the model channel is trusted and documented.
- **Auditability**: Agent merges/modifications emit notifications; agent-configuration changes are reviewed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-F1**: A dependency PR carrying embedded instructions in its body/changelog results in 0 actions taken on those instructions and 0 modifications to unrelated files, verified by a controlled test.
- **SC-F2**: The autonomous agent cannot merge a PR with failing/pending required checks, cannot merge a PR outside its permitted metadata-defined scope, and cannot bypass or disable checks, verified by attempting each; agent merges/modifications emit a notification. (Merging an in-scope PR on green without human review is expected behavior under the accepted-risk status-checks-only policy.)
- **SC-F3**: The permission configuration contains 0 arbitrary-command-passthrough allow entries, and the deny list blocks the enumerated dangerous classes even when an allow entry would match, verified by inspection/test.
- **SC-F4**: The session-start installer fetches a pinned, integrity-verified version and fails closed on mismatch, verified by inspection; the command-rewrite hook does not execute unconstrained opaque output.
- **SC-F5**: Project guidance no longer instructs "trust and act" on tool/indexed content; agent-configuration paths require review, verified by inspection.
- **SC-F6**: The local model channel is documented and access-restricted (or removed), verified by review.
- **SC-F7**: CI remains free of any in-pipeline model that consumes untrusted PR content, verified by re-review.

## Assumptions

- The scheduled merge agent's configuration lives outside the repository (in the cloud agent/routines dashboard); this sub-spec captures its required end state, and the change is actioned there while the branch-protection gate is enforced in the repository/platform.
- Per the 2026-07-02 clarification, the AI merge agent is **retained** but constrained: its decisions are driven only by structured metadata (bot author, labels, update type, check status) — never free-text PR/changelog content — and every merge is gated behind branch protection (required status checks; human review is not mandated — status-checks-only, per Sub-Spec E FR-E7/FR-E7a). Because review is not required, the agent may merge in-scope PRs on green; this residual risk is accepted and offset by the metadata-only, scope-limited, notified, deny-listed constraints.
- The read-only helper commands the allow-list intends to permit remain available after the passthrough entry is removed.
- The update tooling that keeps pins current can advance the agent-tooling installer pin over time.
- CI has no in-pipeline AI today; this sub-spec's job is to keep it that way and to constrain the out-of-pipeline agents that do hold privilege.
