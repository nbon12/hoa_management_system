#!/usr/bin/env node
/**
 * Contract type generation (015 US4, FR-011 — types only, per spec Clarifications).
 *
 * Pipeline: build/run the backend with `--export-openapi` (writes the NSwag OpenAPI document and
 * exits; no HTTP serving beyond an ephemeral loopback bind, no database) → run `openapi-typescript`
 * → emit `src/app/core/api/generated-types.ts`.
 *
 * The output file is committed; CI re-runs this script and fails on `git diff --exit-code` when a
 * backend contract change lands without regenerated types. A failed export MUST fail this script —
 * never fall back to the stale committed file.
 *
 * Usage: npm run generate:api-types
 */
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(frontendRoot, '..');
const backendProject = join(repoRoot, 'HOAManagementCompany');
const outputFile = join(frontendRoot, 'src', 'app', 'core', 'api', 'generated-types.ts');

const workDir = mkdtempSync(join(tmpdir(), 'nekohoa-openapi-'));
const specFile = join(workDir, 'swagger.json');

try {
  console.log('[generate:api-types] exporting OpenAPI document from the backend…');
  execFileSync('dotnet', ['run', '--project', backendProject, '--', '--export-openapi', specFile], {
    stdio: 'inherit',
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
  });

  console.log('[generate:api-types] generating TypeScript contract types…');
  execFileSync(
    process.platform === 'win32' ? 'npx.cmd' : 'npx',
    ['openapi-typescript', specFile, '-o', outputFile],
    { stdio: 'inherit', cwd: frontendRoot },
  );

  console.log(`[generate:api-types] wrote ${outputFile}`);
} finally {
  rmSync(workDir, { recursive: true, force: true });
}
