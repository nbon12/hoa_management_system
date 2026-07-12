// 020-D T007: assert the stamped _headers is complete — no placeholder, Stripe + API origins
// present, baseline headers intact. Run: node scripts/stamp-headers.test.mjs [headers-file]
// (defaults to stamping a temp copy of the src template with a fixture origin, so it also
// guards the template itself in CI without needing a full build).
import { execFileSync } from 'node:child_process';
import { readFileSync, copyFileSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const FIXTURE_ORIGIN = 'https://api-fixture.nekohoa.test';

let target = process.argv[2];
if (!target) {
  const tmp = mkdtempSync(join(tmpdir(), 'headers-'));
  target = join(tmp, '_headers');
  copyFileSync(join(root, 'src/_headers'), target);
  execFileSync('node', [join(root, 'scripts/stamp-headers.mjs'), FIXTURE_ORIGIN, target]);
}

const content = readFileSync(target, 'utf8');
const failures = [];
const expect = (cond, msg) => { if (!cond) failures.push(msg); };

expect(!content.includes('__API_ORIGIN__'), 'placeholder __API_ORIGIN__ was not stamped');
expect(content.includes('Content-Security-Policy:'), 'missing Content-Security-Policy');
expect(/connect-src [^;\n]*https:\/\//.test(content), 'connect-src has no stamped https origin');
expect(content.includes('https://js.stripe.com'), 'missing js.stripe.com (Stripe script/frames)');
expect(content.includes('https://api.stripe.com'), 'missing api.stripe.com (Stripe.js XHR)');
expect(content.includes("object-src 'none'"), "missing object-src 'none'");
expect(content.includes("frame-ancestors 'none'"), "missing frame-ancestors 'none'");
expect(content.includes('X-Content-Type-Options: nosniff'), 'missing nosniff');
expect(content.includes('Referrer-Policy:'), 'missing Referrer-Policy');
expect(content.includes('Permissions-Policy:'), 'missing Permissions-Policy');

if (failures.length) {
  console.error('stamp-headers.test: FAIL\n - ' + failures.join('\n - '));
  process.exit(1);
}
console.log(`stamp-headers.test: OK (${target})`);
