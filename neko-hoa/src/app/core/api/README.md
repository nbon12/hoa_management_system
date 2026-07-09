# Generated API contract types

<!-- REPOWISE:START section=contract -->
`generated-types.ts` is the **single client-side source of truth** for the backend API contract
(015-architecture-remediation US4, FR-011/FR-012). It is generated — never hand-edited — from the
backend's NSwag OpenAPI document by [`scripts/generate-api-types.mjs`](../../../../scripts/generate-api-types.mjs):

```bash
npm run generate:api-types   # backend `dotnet run -- --export-openapi` → openapi-typescript
```

Rules:

- **Types only.** Hand-written services in `core/services/` keep their structure and mappers;
  they type requests/responses with these generated shapes (directly or via re-exports from
  `core/models`).
- **No parallel definitions.** `core/models/index.ts` holds app-internal view-models and
  re-exports of canonical contract types; it must not redefine a shape that exists here.
  Storybook fixtures and specs type their stubs with the canonical types too.
- **Drift gate.** CI regenerates this file and runs `git diff --exit-code` on it — a backend
  contract change without regenerated committed types fails verification. If the gate fails,
  run the command above and commit the result.
- **No markers inside the generated file.** This README carries the Repowise region;
  `generated-types.ts` must stay byte-exact codegen output or the drift gate breaks.
<!-- REPOWISE:END -->
