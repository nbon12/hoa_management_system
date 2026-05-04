# Implementation Plan: Login Dashboard with Violations Summary

**Branch**: `001-dashboard-violations-summary` | **Date**: 2025-03-14 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/001-dashboard-violations-summary/spec.md`

**Note**: This plan is produced by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Implement post-login Dashboard with a 2×2 grid of summary boxes (Current Balance, Violations, Work Orders, Architecture Requests). Only the Violations box is functional in this feature: it shows the count of open violations across all properties the user owns, with a loading state and click-through to a My Violations page that lists those open violations. The other three boxes show placeholder text only. All data is scoped to the current user (Okta-authenticated); violations are filtered by open status and by properties owned by the user. **Frontend**: Angular app in `frontend/` at repository root (per constitution). Backend: .NET + PostgreSQL; dashboard and violations-by-user APIs in HOAManagementCompany.

## Technical Context

**Language/Version**: .NET 9 (C#)  
**Primary Dependencies**: ASP.NET Core, Entity Framework Core 9, Npgsql (PostgreSQL), ASP.NET Core Identity  
**Storage**: PostgreSQL (existing; migrations for Property and Violation–Property association as needed)  
**Testing**: xUnit, Playwright (PLAYWRIGHT_HEADLESS=true), EF Core against PostgreSQL per testing constitution  
**Target Platform**: Web (Linux/server); UI responsive (iPhone, tablet, desktop per constitution)  
**Project Type**: Web application (backend API + Angular frontend)  
**Performance Goals**: Dashboard and violation count load in under 2s; list page supports pagination (limit/offset)  
**Constraints**: Okta authentication; role-based access (Homeowner, Board Member); data isolation per user  
**Scale/Scope**: Single HOA portal; users see only their own violation count and list

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|--------|
| Backend: .NET REST API | Pass | Existing ASP.NET Core; add Dashboard/My Violations endpoints |
| Database: PostgreSQL | Pass | Existing Npgsql + EF Core |
| Frontend: Angular | Pass | Angular app in `frontend/` at repo root (per constitution) |
| Auth: Okta | Partial | Identity in place; Okta integration may exist or be in progress—ensure all endpoints and UI enforce auth |
| CI/CD: GitHub Actions | Pass | Existing workflows |
| Testing: Unit, integration, UI | Pass | Unit + integration + Playwright; transaction-per-test isolation per testing constitution |
| Pagination (collections) | Pass | My Violations list MUST support limit/offset |
| Global exception handler / meaningful errors | Pass | Per backend principles |
| Responsiveness (iPhone, tablet, desktop) | Pass | Required for Dashboard and My Violations UI |

**Gate result**: Pass.

## Project Structure

### Documentation (this feature)

```text
specs/001-dashboard-violations-summary/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (API contracts)
└── tasks.md             # Phase 2 (/speckit.tasks — not created by /speckit.plan)
```

### Source Code (repository root)

```text
frontend/                    # Angular app (at repo root)
├── src/
│   ├── app/
│   │   ├── pages/           # Dashboard, My Violations
│   │   ├── components/
│   │   └── services/
│   └── ...
└── ...

HOAManagementCompany/        # .NET backend (existing)
├── Controllers/             # DashboardController, ViolationsController (or extend existing)
├── Models/                  # Violation (existing), Property (new), Violation.PropertyId (new)
├── EntityFramework/         # ApplicationDbContext, migrations
├── Services/                # ViolationService (extend), DashboardService (new)
└── Constants/

HOAManagementCompany.Tests/
├── Unit/
├── Integration/
└── Playwright/
```

**Structure Decision**: Backend remains in HOAManagementCompany; frontend is an Angular application in `frontend/` at the repository root, per constitution. Dashboard and My Violations are Angular pages and components; API endpoints and data model (Property, Violation.PropertyId) live in the .NET backend.

## Complexity Tracking

> No constitution violations requiring justification.
