repo: nbon12/hoa_management_system
branch: main

## Last sync

date: 2026-08-19T20:54:06Z

### Updated in this project

- Wrote `specs/021-board-overall-design/spec.md` — foundation spec for the board member effort.
- Grounded it in the real schema: no `Community` entity exists, `CommunityId` is a denormalized string, and there is no role model.
- Added the board / manager side wireframes: role-gated shell, board-mode toggle in the top bar, seven artboards.
- Earlier: Stripe-based redesign of the one-time and recurring billing pages.

## Screen map

| Project screen | Repo files |
| --- | --- |
| Board overall design spec (`specs/021-board-overall-design/spec.md`) | `Domain/Entities/Property.cs`, `Domain/Entities/ApplicationUser.cs`, `Domain/Entities/UserProperty.cs`, `Domain/Entities/Violation.cs`, `neko-hoa/src/app/shell/shell.component.ts`, `neko-hoa/src/app/app.routes.ts`, `.specify/templates/spec-template.md` |
| Board shell + nav (`wf-board.jsx`) | `neko-hoa/src/app/shell/shell.component.ts`, `neko-hoa/src/app/app.routes.ts` |
| Resident shell (`wf-shell.jsx`) | `neko-hoa/src/app/shell/shell.component.ts` |
| Sign-in / portal (`wf-auth.jsx`) | `neko-hoa/src/app/features/auth/`, `neko-hoa/src/app/core/guards/auth.guard.ts` |
| Stripe billing (`wf-stripe.jsx`) | `specs/006-stripe-payments/spec.md`, `HOAManagementCompany/Features/Payments/` |
| Wireframe palette (`wireframe-styles.css`) | `neko-hoa/src/styles.scss` |
