# neko-hoa-mock — Mock Angular Reference Snapshot

This folder is a **read-only reference copy** of the Angular app's mock-backed services as they existed before the real API integration.

**Do not import or run anything from here.** It is purely for side-by-side comparison.

## Contents

| Path | What it shows |
|------|--------------|
| `services/mock-data.service.ts` | All hardcoded Sakura Heights data |
| `services/auth.service.ts` | Mock login/register (sessionStorage, no JWT) |
| `services/dashboard.service.ts` | Mock dashboard summary |
| `services/payments.service.ts` | Mock ledger, one-time & recurring payments |
| `services/property.service.ts` | Mock property / owner / directory |
| `services/community.service.ts` | Mock announcements, violations, calendar, documents |
| `models/index.ts` | Shared TypeScript interfaces |
| `guards/auth.guard.ts` | Signal-based auth guard |
| `features/auth/` | Login, register, portal-select components |
| `features/dashboard/` | Dashboard component |
| `shell/shell.component.ts` | App shell / nav |
| `app.config.ts` | Root providers |
| `app.routes.ts` | Route definitions |

## Git reference

The original mock code is permanently in git history:

```
git show 94c5050 -- neko-hoa/src/app/core/services/auth.service.ts
git checkout 94c5050 -- neko-hoa/src/app/core/services/auth.service.ts
```

Commit `94c5050` ("write specs") is the mock baseline.
