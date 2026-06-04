# NekoHOA (Angular)

<!-- REPOWISE:START section=frontend-overview -->
Angular 19 resident portal. Core routes under `src/app/features/` (auth, dashboard, payments, property, community). API base URL from `src/environments/` (development: backend at `http://localhost:5212`, `api/v1`). Run: `npm start` from this directory; E2E: `npx playwright test`.
<!-- REPOWISE:END -->

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`.

## Build

Run `ng build` to build the project. Artifacts are stored in `dist/`.

## Tests

- Unit: `ng test`
- E2E: see `e2e/` and `playwright.config.ts`
