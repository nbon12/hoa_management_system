# Implementation Plan: Board Architectural Review (ARC)

**Branch**: `027-board-arc-review` | **Date**: 2026-10-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/027-board-arc-review/spec.md`

## Summary

Add the board's Architectural Applications workflow on top of spec 025's community scope, roles, board shell and private-link primitive:
- a per-community application list with Open/Closed tabs, search and live vote tallies;
- Approve / Revisions needed / Deny voting with recusal and a race-free decision under a per-community decision rule;
- a detail panel with audited, short-lived attachment links;
- info requests that never pause the review period;
- a "Needs your vote" card on Community Home;
- manager-recorded outcomes that email the owner (approved, revisions requested or denied) through the existing transactional outbox;
- an hourly, secret-authenticated sweep for pre-deadline reminders and per-community lapse rules.

The data model (applications as revision rows, attachments, votes, info requests, per-community ARC settings) is introduced here and shared with the sibling Resident Architectural Application Submission spec. This spec is built and tested against seeded applications.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend `HOAManagementCompany`, tests `HOAManagementCompany.Tests`); TypeScript / Angular 17.3 (frontend `neko-hoa`); HCL / OpenTofu ≥ 1.8 (one Cloud Scheduler job)

**Primary Dependencies**: All existing: FastEndpoints, EF Core 9 (Npgsql), ASP.NET Core Identity/JWT, `ICommunityScopeResolver`, `IDocumentStorage` (R2/MinIO), `OutboxMessage` + `OutboxDispatcher` + `SesEmailProvider`, `Microsoft.AspNetCore.RateLimiting`, Serilog, `TimeProvider` (BCL). Angular standalone components and signals. OpenTofu `hashicorp/google` (already pinned). **No new packages.**

**Storage**: PostgreSQL (Neon prod, Testcontainers CI/local).
- New tables: `CommunityArcSettings`, `ArchitecturalApplications`, `ArchitecturalAttachments`, `ArchitecturalVotes`, `ArchitecturalInfoRequests`.
- Modified: `OutboxMessages` (`OwnerId` nullable, + `RecipientUserId`, + check constraint).
- Attachment objects live in the existing bucket under `arc/`.

**Testing**:
- Backend: xUnit + Testcontainers (PostgreSQL, MinIO); a `[Theory]` matrix over the pure decision function; a fake `TimeProvider` for the sweep; Serilog `LogSink` for sensitive events.
- Frontend: Karma/Jasmine, Angular Testing Library, Playwright, Cypress, Storybook.

**Target Platform**: Cloud Run (backend, scale-to-zero), Cloudflare Pages (frontend), Cloud Scheduler (hourly sweep).

**Project Type**: Web application (existing Angular + .NET layout).

**Performance Goals**: From the spec:
- SC-006: first page of the list within 2 s for a community with 500 applications.
- SC-002: an attachment opens within 3 s.

The list query is index-backed on `(CommunityId, Status, DueDate)`. Tallies come from one grouped vote count per page, not per row.

**Constraints**:
- All authorization goes through `ICommunityScopeResolver` (025 FR-012), and the static-analysis test is extended to cover subfolders.
- Exactly one decision per application under concurrent votes, enforced with a row lock (R4).
- Emails go out exactly once, with outbox dedup keys and sweep stamps (R5, R6).
- Due-date math is in the community's time zone (R7).
- No durable public object URLs (FR-012).
- Owner emails never contain board votes or comments.

**Scale/Scope**:
- One management company; tens of communities; hundreds of applications per community per year; boards of 3–9.
- 5 new entities, 8 new enums, 1 modified entity.
- 10 endpoints (`contracts/architectural-applications.md`).
- 1 new frontend feature folder, 1 new service, 1 route, 2 nav entries.
- 1 seeder, 1 OpenTofu resource.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

- **Technology fit**: ✅ Angular, FastEndpoints, PostgreSQL/Neon, Identity + JWT, Cloudflare, Cloud Run, Sentry, Swashbuckle (dev only) and GitHub Actions, all reused. Cloud Scheduler is already the documented trigger for job endpoints (006 quickstart). This spec adds the first OpenTofu resource for one.
- **HOA tenancy**: ✅
  - Every new row is community-scoped: `ArchitecturalApplication.CommunityId` (checked equal to the property's), with children scoped through the application, and `CommunityArcSettings` keyed by community.
  - Cross-community access is denied by default with the 025 non-disclosing 403.
  - The only non-community-scoped endpoint is the secret-authenticated sweep. It acts on all communities but returns only counts.
- **API contracts**: ✅ The contract documents auth, the capability per endpoint, `limit`/`offset` (25/100), error codes, `no-store`, and the email payload contract. No existing endpoint changes shape. The `OutboxMessage.OwnerId` nullability is internal and not part of an API.
- **Security and operations**: ✅
  - No new secrets; the sweep reuses `scheduler-secret`.
  - Authorization is server-side through the resolver, with a recusal check.
  - Sensitive events are logged for attachment and detail access (025 FR-017), votes, outcomes and settings changes.
  - User text is length-capped and rendered as text, never HTML.
  - A new `board-writes` rate limit applies to all writes (R9).
  - Production error shape is unchanged (`DomainException` pattern).
  - Observability: new endpoints are covered by the existing Sentry and OpenTelemetry instrumentation (request traces, errors, environment and release tags). Sensitive events and spans carry IDs only, never comment text, owner names or storage keys, which a dedicated test checks (tasks T080).
- **File storage**: ✅ Objects in R2 (hosted) and MinIO (local/CI); PostgreSQL holds metadata and keys only; access is only through `IDocumentStorage` pre-signed URLs (5-minute expiry).
- **Caching/edge**: ✅ Every endpoint is `no-store`.
- **Testing discipline**: ✅
  - Test-first per user story.
  - Testcontainers for PostgreSQL and MinIO, with isolated communities per test.
  - `[Theory]` coverage of both decision rules × board sizes 3–6 × recusal × quorum at the due date.
  - A concurrency test for SC-005 and `TimeProvider`-driven sweep tests.
  - All frontend tools are ones the constitution requires.
- **CI/CD and documentation**: ✅ Sonar and Codecov (95% on new files), Repowise markers below. The OpenTofu change goes through the existing Trivy IaC scan.
- **Executable & living specs**:
  - ✅ The spec was clarified on 2026-10-07 (six answers recorded).
  - ✅ Every acceptance scenario in US1–US6 maps to a planned test. Task generation must cite each one.
  - ✅ This spec fills in 025's stubbed "Architectural Applications" nav entry and part of the placeholder Community Home without contradicting 025.
- **Spec independence & parallelism**: ✅
  - Hard dependency only on 025 (merged).
  - The sibling **Resident Architectural Application Submission** spec shares this data model. Whichever lands first creates the tables and the other extends them, and the data model here is written as the shared contract.
  - The sibling **Notification Settings** spec plugs into the `IArcNotificationPreferences` seam (R5), which allows all recipients until then.
  - Spec 2 (Community Overview & Metrics) can build the rest of Community Home in parallel, because the Needs-your-vote card is a separate section.

No gate failures.

## Project Structure

### Documentation (this feature)

```text
specs/027-board-arc-review/
├── spec.md
├── plan.md                 # this file
├── research.md             # Phase 0
├── data-model.md           # Phase 1
├── quickstart.md           # Phase 1
├── contracts/
│   └── architectural-applications.md
├── checklists/requirements.md
└── tasks.md                # /speckit.tasks (not created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── Domain/
│   ├── Entities/
│   │   ├── CommunityArcSettings.cs                 # new
│   │   ├── ArchitecturalApplication.cs             # new
│   │   ├── ArchitecturalAttachment.cs              # new
│   │   ├── ArchitecturalVote.cs                    # new
│   │   ├── ArchitecturalInfoRequest.cs             # new
│   │   └── OutboxMessage.cs                        # modified: OwnerId?, RecipientUserId
│   └── Enums/Arc*.cs                               # new (8 enums, data-model.md)
├── Features/
│   ├── Board/
│   │   ├── ICommunityScopeResolver.cs              # modified: 3 capabilities
│   │   ├── CommunityScopeResolver.cs               # modified: capability map
│   │   └── Architectural/                          # new
│   │       ├── ArcDecisionRules.cs                 # pure decision/wording/rule-text logic (R4)
│   │       ├── ArcQueries.cs                       # list/detail projections, tally, eligibility, recusal
│   │       ├── ArcEmailRenderer.cs                 # outbox payloads (plain text now; HTML later)
│   │       ├── IArcNotificationPreferences.cs      # seam for the Notification Settings spec
│   │       ├── ArcSweepService.cs                  # reminders, lapses, due-date decisions (R6)
│   │       ├── ApplicationsListEndpoint.cs
│   │       ├── ApplicationDetailEndpoint.cs
│   │       ├── AttachmentUrlEndpoint.cs
│   │       ├── CastVoteEndpoint.cs
│   │       ├── InfoRequestEndpoint.cs
│   │       ├── RecordOutcomeEndpoint.cs
│   │       ├── ResendOutcomeEmailEndpoint.cs
│   │       ├── ArcSettingsGetEndpoint.cs
│   │       ├── ArcSettingsPutEndpoint.cs
│   │       ├── ArcSweepJobEndpoint.cs              # X-Scheduler-Secret; static-analysis allow-list
│   │       ├── ArcApplicationFactory.cs            # number allocation, due date, snapshots, revisions
│   │       └── ArcModels.cs                        # request/response DTOs
│   └── Payments/Alerts/                            # touched only for OwnerId nullability
├── Infrastructure/Storage/                         # IDocumentStorage + S3DocumentStorage: + ExistsAsync
├── Seed/ArchitecturalSeeder.cs                     # new (R11); DatabaseSeeder calls it
├── Infrastructure/Persistence/
│   ├── ApplicationDbContext.cs                     # new DbSets + configuration
│   └── Migrations/<ts>_AddArchitecturalReview.cs   # new
└── Program.cs                                      # board-writes policy, TimeProvider.System, DI

HOAManagementCompany.Tests/
├── Unit/Architectural/                             # ArcDecisionRulesTheoryTests
├── Performance/ArcListPerformanceTests.cs
└── Integration/Board/
    ├── BoardScopeEnforcementStaticAnalysisTests.cs # modified: AllDirectories + sweep allow-list
    └── Architectural/                              # new: one class per user story,
                                                    # ArcDecisionRulesTheoryTests, ArcConcurrencyTests,
                                                    # ArcSweepTests, ArcSettingsTests, ArcEmailOutboxTests

neko-hoa/src/app/
├── app.routes.ts                                   # + board/architectural, board/arc-settings
├── core/services/
│   ├── architectural.service.ts                    # new
│   └── board-navigation.service.ts                 # stub → route; + ARC Settings (manager)
└── features/board/
    ├── architectural/                              # new
    │   ├── applications-page.component.ts
    │   ├── tally.component.ts (+ .stories)
    │   ├── application-detail-panel.component.ts (+ .stories)
    │   ├── cast-vote-card.component.ts
    │   ├── needs-your-vote-card.component.ts (+ .stories)
    │   ├── record-outcome.component.ts
    │   └── arc-settings.component.ts
    └── community-home/community-home.component.ts  # + <app-needs-your-vote-card>

infra/modules/environment/
└── scheduler.tf                                    # new: hourly google_cloud_scheduler_job → sweep
```

**Structure Decision**: Follows 025's layout. Backend board features stay under `Features/Board/`, with ARC in its own `Architectural/` subfolder, and the static scope test is widened to subfolders so the 025 guard still covers it. The frontend uses `features/board/architectural/`. No new project or top-level directory.

## Repowise Documentation

**Status**: Not started (implementation not yet begun).

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `Domain/Entities/ArchitecturalApplication.cs` | `domain=entities` | Revision-row model, snapshots, lifecycle |
| `Domain/Entities/CommunityArcSettings.cs` | `domain=entities` | Per-community governing-document rules |
| `Features/Board/Architectural/ArcDecisionRules.cs` | `domain=arc-decision` | Two-sided count, both rules, wording, rule text |
| `Features/Board/Architectural/ArcSweepService.cs` | `domain=arc-sweep` | Reminder/lapse idempotency and time-zone math |
| `Domain/Entities/OutboxMessage.cs` | `domain=entities` | Owner-or-user recipient, ARC kinds |
| `specs/027-board-arc-review/spec.md` | `section=summary` | Feature summary for the index |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=arc-decision -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

No constitution violations. Two choices widen the slice slightly and are recorded for reviewers:

| Choice | Why needed | Simpler alternative rejected because |
|--------|------------|--------------------------------------|
| Make `OutboxMessage.OwnerId` nullable and add `RecipientUserId` (touches payments) | Board emails go to users who may not be owners | A second outbox and dispatcher would duplicate tested delivery, dedup and failure handling (R5) |
| Add a Cloud Scheduler resource in OpenTofu | Lapse and reminder emails have legal timing weight and must run while Cloud Run is scaled to zero | An in-process timer doesn't fire at zero instances; manual scheduling leaves FR-027/028 unmet in Dev/Prod (R6) |
