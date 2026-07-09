// 020-D FR-D2: stamp the deployment's API origin into the built _headers file (research D-R4).
// Usage: node scripts/stamp-headers.mjs <api-origin> [headers-file]
//   api-origin    e.g. https://api-dev.nekohoa.com or https://nekohoa-api-pr-42-xyz.run.app
//   headers-file  defaults to dist/neko-hoa/browser/_headers
// Exits non-zero if the origin is missing/invalid or the placeholder survives stamping, so an
// unstamped CSP can never ship silently.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const PLACEHOLDER = '__API_ORIGIN__';

const rawOrigin = process.argv[2];
if (!rawOrigin) {
  console.error('stamp-headers: missing <api-origin> argument');
  process.exit(1);
}

let origin;
try {
  const url = new URL(rawOrigin);
  origin = url.origin; // normalizes away any path/trailing slash
} catch {
  console.error(`stamp-headers: "${rawOrigin}" is not a valid absolute URL`);
  process.exit(1);
}

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const target = process.argv[3] ?? join(root, 'dist/neko-hoa/browser/_headers');

if (!existsSync(target)) {
  console.error(`stamp-headers: ${target} not found — run the build first`);
  process.exit(1);
}

const stamped = readFileSync(target, 'utf8').replaceAll(PLACEHOLDER, origin);
if (stamped.includes(PLACEHOLDER)) {
  console.error('stamp-headers: placeholder survived stamping');
  process.exit(1);
}
if (!stamped.includes(origin)) {
  console.error('stamp-headers: origin not present after stamping — template missing placeholder?');
  process.exit(1);
}

writeFileSync(target, stamped);
console.log(`stamp-headers: connect-src stamped with ${origin}`);
