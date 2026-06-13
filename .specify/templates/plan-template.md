# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]
**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Confirm the plan satisfies the active HOA Management Company Constitution:

- **Technology fit**: Angular frontend, .NET FastEndpoints REST API, PostgreSQL/Neon,
  Auth0, Cloudflare, Cloud Run, Docker/Docker Hub, Sentry, Swashbuckle in development
  only, and GitHub Actions are used or explicitly marked not applicable.
- **HOA tenancy**: HOA-scoped data includes an `hoa_id`, `association_id`, or equivalent
  tenant boundary; cross-HOA access is denied by default; intentional cross-HOA queries
  document authorization and result scope.
- **API contracts**: Endpoints document authentication, authorization, pagination
  (`limit`/`offset` for collections), error response shape, cacheability, and breaking
  contract migration notes.
- **Security and operations**: Secrets are externalized, Auth0 authorization is enforced
  server-side, structured Serilog logs and Sentry tracing are planned, and production
  errors do not leak system details.
- **File storage**: If file/blob storage is introduced, hosted environments use Cloudflare
  R2; local Docker Compose and local/CI tests use MinIO; PostgreSQL stores
  metadata/references, not large binary payloads.
- **Caching/edge**: API responses are cached only when explicitly safe; authenticated or
  user-specific responses are not edge-cached unless keyed and justified; static assets
  use hashed filenames where practical.
- **Testing discipline**: Tests are written first where applicable; backend persistence
  tests use PostgreSQL/Testcontainers and transaction isolation; data-varied cases use
  xUnit Theories; frontend tests use the constitution-approved tools.
- **CI/CD and documentation**: Sonar, Codecov, coverage, deployment environment isolation,
  and Repowise-generated documentation updates are accounted for.
- **Executable & living specs**: Acceptance criteria are backed by runnable, currently-passing
  tests; `spec.md` stays truthful (no drift — including older, already-merged specs); this
  feature's `spec.md` and `tasks.md` are slated to be updated before the PR (older specs only
  need their `spec.md` kept current; `tasks.md`/`plan.md`/`research.md` are not refreshed); and
  any contradiction with a former spec is reconciled so the spec corpus stays consistent.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Repowise Documentation

**Status**: [Bootstrapped | In progress | Complete]

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| [Add rows for each file touched] | `domain=*` or `section=*` | [What the region documents] |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=example -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

```markdown
<!-- REPOWISE:START section=example -->
... generated content ...
<!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
