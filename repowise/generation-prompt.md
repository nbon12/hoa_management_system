# Repowise marker generation — HOA Management Company

## Repository context

- Monorepo: .NET 9 FastEndpoints API (`HOAManagementCompany/`), Angular 19 frontend (`neko-hoa/`), legacy `frontend/` (deprecated; do not document as canonical).
- Tenancy: HOA/community scoped via `CommunityId` and JWT `propertyId` claim.
- Auth: ASP.NET Core Identity + custom JWT (not Auth0).
- Storage: PostgreSQL 17, MinIO local / Cloudflare R2 production.
- API contract: `neko-hoa/api/openapi.yaml`, prefix `api/v1`.

## Global rules

- Keep each region under ~25 lines; bullet lists preferred.
- Never invent endpoints or entities not present in code or OpenAPI.
- Do not duplicate README body outside marker regions.
- Use present tense; no marketing copy.

## Region catalog

| Marker ID | File | Required content |
|-----------|------|------------------|
| `section=overview` | `README.md` | Product summary, primary user roles |
| `section=tech-stack` | `README.md` | Accurate stack only (Identity + JWT, not Auth0) |
| `section=quickstart` | `README.md` | `docker compose`, seed, run API, Swagger |
| `section=documentation` | `README.md` | Links to specs, Repowise, contributing |
| `domain=bootstrap` | `HOAManagementCompany/Program.cs` | Middleware order, DI highlights, dev-only Swagger |
| `domain=auth` | `HOAManagementCompany/Features/Auth/AuthService.cs` | Register/login/refresh/logout/switch-property, token lifetimes |
| `domain=schema` | `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs` | DbSets, relationships, tenancy columns |
| `domain=entities` | `HOAManagementCompany/Domain/Entities/ApplicationUser.cs` | Identity extensions, navigation properties |
| `domain=dashboard` | `HOAManagementCompany/Features/Dashboard/DashboardService.cs` | Dashboard aggregation, ledger window |
| `domain=payments` | `HOAManagementCompany/Features/Payments/PaymentService.cs` | Ledger, drafts, recurring, one-time payment |
| `domain=property` | `HOAManagementCompany/Features/Property/PropertyService.cs` | Property, owner, directory fields, address history |
| `domain=community` | `HOAManagementCompany/Features/Community/CommunityService.cs` | Announcements, polls, events, documents, violations |
| `domain=devtools` | `HOAManagementCompany/Features/DevTools/E2ECleanupEndpoint.cs` | Dev-only E2E cleanup (Development env only) |
| `section=frontend-overview` | `neko-hoa/README.md` | App shell, routes, core services, API base URL |

## Per-domain notes

- **dashboard**: `GET /dashboard` summary for active property.
- **payments**: simulated ACH/card; no real gateway.
- **community**: `CommunityId` scopes resident-visible data.
- **devtools**: must note Development-only guard.

## Tone

Terse, factual, present tense.
