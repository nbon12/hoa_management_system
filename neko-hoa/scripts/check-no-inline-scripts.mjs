// 020-D FR-D2 guard: the enforcing CSP (script-src without 'unsafe-inline') blocks inline
// scripts and inline event-handler attributes. This fails the build if the built index.html
// contains any — the classic re-offender is Angular's optimization.styles.inlineCritical, which
// emits `<link ... media="print" onload="this.media='all'">`; the CSP kills the onload handler
// and the app ships half-styled (only the inlined critical subset renders). Keeping this cheap
// PR-time check means re-enabling inlineCritical (or any future inline-script regression) fails
// CI at PR time instead of only at the deployed @smoke gate.
//
// Run: node scripts/check-no-inline-scripts.mjs [index.html]
//   defaults to dist/neko-hoa/browser/index.html (run after `ng build`).
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const target = process.argv[2] ?? join(root, 'dist/neko-hoa/browser/index.html');

if (!existsSync(target)) {
  console.error(`check-no-inline-scripts: ${target} not found — run the build first`);
  process.exit(1);
}

const html = readFileSync(target, 'utf8');
const violations = [];

// 1. Inline <script> blocks (a <script> with body and no src). External `<script src="...">`
//    is fine — it is covered by script-src 'self'.
const scriptTag = /<script\b([^>]*)>([\s\S]*?)<\/script>/gi;
for (const m of html.matchAll(scriptTag)) {
  const attrs = m[1];
  const body = m[2].trim();
  const hasSrc = /\bsrc\s*=/i.test(attrs);
  if (!hasSrc && body.length > 0) {
    violations.push(`inline <script> block (${body.length} chars): ${body.slice(0, 60)}…`);
  }
}

// 2. Inline event-handler attributes (onload, onclick, onerror, …) inside any tag. This is what
//    inlineCritical's async-stylesheet trick uses.
const inlineHandler = /<[^>]*?\son[a-z]+\s*=\s*["'][^"']*["'][^>]*>/gi;
for (const m of html.matchAll(inlineHandler)) {
  const handler = /\son[a-z]+\s*=\s*["'][^"']*["']/i.exec(m[0])?.[0]?.trim();
  violations.push(`inline event handler: ${handler} — in ${m[0].slice(0, 80)}…`);
}

// 3. javascript: URIs (also blocked by a strict CSP).
if (/\b(?:href|src)\s*=\s*["']\s*javascript:/i.test(html)) {
  violations.push('javascript: URI in href/src');
}

if (violations.length) {
  console.error(
    `check-no-inline-scripts: FAIL — ${target} contains CSP-incompatible inline script(s):\n` +
    violations.map(v => `  - ${v}`).join('\n') +
    `\n\nThe enforcing CSP (src/_headers) has no 'unsafe-inline'. If this came from Angular's\n` +
    `optimization.styles.inlineCritical, keep it disabled in angular.json.`);
  process.exit(1);
}

console.log(`check-no-inline-scripts: OK — ${target} has no inline scripts or handlers`);
