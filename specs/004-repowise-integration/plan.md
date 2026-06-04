# Implementation Plan: Repowise integration

**Branch**: `004-repowise-integration` | **Date**: 2026-06-04 | **Spec**: [spec.md](./spec.md)

## Summary

Replace the invalid `repowise/action@v1` workflow with self-hosted Repowise 0.16+: a **secretless** `repowise-gate` job (index-only + health + risk) and an optional `repowise-docs` job (wiki + marker validation). Expand marker regions and commit `repowise/generation-prompt.md` + `repowise/health-gates.yaml`.

## Repowise Documentation

**Status**: Complete (initial implementation)

### Configuration

- [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)
- [`.repowiseIgnore`](../../.repowiseIgnore)
- [`.github/workflows/repowise.yml`](../../.github/workflows/repowise.yml)

### Marker inventory

| File | Region ID |
|------|-----------|
| `README.md` | `section=overview`, `tech-stack`, `quickstart`, `documentation` |
| `neko-hoa/README.md` | `section=frontend-overview` |
| `HOAManagementCompany/Program.cs` | `domain=bootstrap` |
| `Features/Auth/AuthService.cs` | `domain=auth` |
| `Infrastructure/Persistence/ApplicationDbContext.cs` | `domain=schema` |
| `Domain/Entities/ApplicationUser.cs` | `domain=entities` |
| `Features/Dashboard/DashboardService.cs` | `domain=dashboard` |
| `Features/Payments/PaymentService.cs` | `domain=payments` |
| `Features/Property/PropertyService.cs` | `domain=property` |
| `Features/Community/CommunityService.cs` | `domain=community` |
| `Features/DevTools/E2ECleanupEndpoint.cs` | `domain=devtools` |

### CI

| Job | Secrets | Enforce merge |
|-----|---------|---------------|
| `repowise-gate` | None | Yes (after branch protection configured) |
| `repowise-docs` | `ANTHROPIC_API_KEY` or `OPENAI_API_KEY` optional | No (`continue-on-error: true`) |

### Human follow-up

1. Merge to `main` and tune `repowise/health-gates.yaml` from first gate artifact.
2. Add GitHub secret for docs job if LLM wiki is desired.
3. Require `repowise-gate` in branch protection.
4. Optionally install [Repowise PR Bot](https://github.com/apps/repowise-bot).
