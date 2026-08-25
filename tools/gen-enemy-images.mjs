#!/usr/bin/env node
// Generate enemy portraits via Together AI (FLUX.1-schnell-Free).
// Reads .env for TOGETHER_KEY, content/enemies/*.json for enemy defs.
// Saves PNGs to src/client/public/enemies/<id>.png. Skips existing files.

import { readFileSync, writeFileSync, existsSync, readdirSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const ROOT = join(dirname(__filename), '..');
const ENEMY_DIR = join(ROOT, 'content/enemies');
const OUT_DIR = join(ROOT, 'src/client/public/enemies');

const env = Object.fromEntries(
  readFileSync(join(ROOT, '.env'), 'utf8')
    .split('\n')
    .filter(l => l.includes('=') && !l.startsWith('#'))
    .map(l => { const i = l.indexOf('='); return [l.slice(0, i).trim(), l.slice(i + 1).trim()]; })
);

const TOGETHER_KEY = env.TOGETHER_KEY || process.env.TOGETHER_KEY;
if (!TOGETHER_KEY) { console.error('Missing TOGETHER_KEY in .env'); process.exit(1); }

const STYLE = 'dark fantasy creature portrait, oil painting style, grim, atmospheric, isolated on black background, centered, head and shoulders';

function buildPrompt(def) {
  return `${def.name}: ${def.description}. ${STYLE}`;
}

async function generate(prompt, outPath) {
  const res = await fetch('https://api.together.xyz/v1/images/generations', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${TOGETHER_KEY}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model: 'black-forest-labs/FLUX.1-schnell',
      prompt,
      width: 512,
      height: 512,
      steps: 4,
      n: 1,
      response_format: 'b64_json',
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Together API ${res.status}: ${text}`);
  }

  const data = await res.json();
  const b64 = data?.data?.[0]?.b64_json;
  if (!b64) throw new Error(`No b64_json in response: ${JSON.stringify(data).slice(0, 200)}`);

  writeFileSync(outPath, Buffer.from(b64, 'base64'));
}

mkdirSync(OUT_DIR, { recursive: true });

const files = readdirSync(ENEMY_DIR).filter(f => f.endsWith('.json'));
for (const file of files) {
  const def = JSON.parse(readFileSync(join(ENEMY_DIR, file), 'utf8'));
  const outPath = join(OUT_DIR, `${def.id}.png`);
  if (existsSync(outPath) && !process.argv.includes('--force')) {
    console.log(`skip ${def.id} (exists)`);
    continue;
  }
  const prompt = buildPrompt(def);
  process.stdout.write(`gen ${def.id}... `);
  try {
    await generate(prompt, outPath);
    console.log('ok');
  } catch (e) {
    console.log(`FAIL: ${e.message}`);
  }
  await new Promise(r => setTimeout(r, 1500)); // gentle rate limit
}

console.log('done');
