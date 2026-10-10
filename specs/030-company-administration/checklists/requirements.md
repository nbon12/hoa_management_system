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

- [ ] No [NEEDS CLARIFICATION] markers remain
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

- **Open markers (2)**, to resolve with the owner or in `/speckit.clarify`:
  - **FR-019**: may a Community Manager end or downgrade *another* manager in their community, or is that Company Administrator only?
  - **FR-024**: are resident accounts, adding and removing residents, and restricting resident rights (poll voting) in this spec, or in a separate Resident Roster spec?
- **Technology names**: Serilog, FastEndpoints, Testcontainers, Sentry, the `board-writes` rate limit and the resolver capability names appear only where the repo's spec template requires them (Constitution Requirements) or where the owner's issue names them (FR-004, FR-040, FR-041). This matches specs 025 and 027. User stories, the remaining FRs and the success criteria are technology-agnostic.
- **Error codes** (`COMMUNITY_NAME_TAKEN`, `OPEN_WORK`, …) are named so that every denial scenario has an exact assertion (the CLAUDE.md natural-language-test rule).
- **Owner clarifications** from the issue #213 comment are recorded in the spec's Clarifications section (Session 2026-10-08). Two of them change the original draft: Community Managers may add communities and may appoint co-managers.
- **Designs**: none exist. A Claude Design prompt follows `/speckit.clarify`.
