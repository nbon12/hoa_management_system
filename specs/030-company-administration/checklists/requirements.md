# Specification Quality Checklist: Company Administration — Communities, Managers and Community Settings

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-10
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- **Owner answers (2026-10-10)**: Q1 (B) a Community Manager may remove co-managers, never the last manager; Q2 (custom) managers invite residents to a property by email and remove them; restricting resident rights is not built. No `[NEEDS CLARIFICATION]` markers remain.
- **Adversarial review (2026-10-11)**: five reviewers (consistency, testability, grounding in code and merged specs, security, completeness) raised 56 distinct findings.
  - F01–F22 were each checked by two independent skeptics. F20 and F21 were rejected; their optional suggestions were applied.
  - F23–F56 were checked by hand against the spec and code, after usage limits stopped the automated checks. Almost all were real; F23 was applied as the optional clarification its checker suggested.
  - The fixes are in the spec. The decisions they forced are listed under Clarifications › "Decisions made in spec review", for the owner to confirm or override.
- **Biggest change**: today registration needs a claim code and sign-in needs a linked home (`AuthService`). The spec now adds invitation sign-up (FR-022a) and sign-in without a home (FR-036a, US9), and reconciles specs 016 and 017 sub-spec A.
- **Technology names**: Serilog, FastEndpoints, Testcontainers, Sentry, the `board-writes`/`auth` rate limits and capability names appear only where the repo's spec template requires them (Constitution Requirements) or the owner's issue names them. This matches specs 025 and 027. User stories, most FRs and the success criteria are technology-agnostic.
- **Error codes** are named so that every denial scenario has an exact assertion (the CLAUDE.md natural-language-test rule).
- **Known gap, recorded as an assumption**: no property import exists, so a newly added community has no properties until seeded. A Properties spec is the natural follow-up.
- **Designs**: briefs for Claude Design are in `design/`.
