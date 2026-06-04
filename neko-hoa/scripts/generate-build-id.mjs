import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const adjectives = [
  'octocat', 'fluffy', 'cosmic', 'sneaky', 'sparkly', 'sleepy', 'bouncy',
  'crispy', 'velvet', 'turbo', 'wobbly', 'zesty', 'minty', 'jazzy', 'loopy',
];
const nouns = [
  'gift', 'mochi', 'pancake', 'rocket', 'whisker', 'noodle', 'taco',
  'pigeon', 'donut', 'pickle', 'badger', 'comet', 'scone', 'gizmo', 'sprout',
];

const pick = (items) => items[Math.floor(Math.random() * items.length)];
const buildId = `${pick(adjectives)}-${pick(nouns)}`;

const outPath = join(dirname(fileURLToPath(import.meta.url)), '../src/environments/build-id.ts');
writeFileSync(outPath, `export const BUILD_ID = '${buildId}';\n`);
console.log(`build-id: ${buildId}`);
