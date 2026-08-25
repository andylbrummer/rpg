#!/usr/bin/env node
// Generate vendor portraits, tavern class portraits, and item images via Together AI FLUX.1-schnell.
// Reads .env TOGETHER_KEY. Saves PNGs under src/client/public/{vendors,tavern,items}. Skips existing
// unless --force. Mirrors tools/gen-enemy-images.mjs.

import { readFileSync, writeFileSync, existsSync, readdirSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const ROOT = join(dirname(__filename), '..');
const PUBLIC = join(ROOT, 'src/client/public');

const env = Object.fromEntries(
  readFileSync(join(ROOT, '.env'), 'utf8').split('\n')
    .filter(l => l.includes('=') && !l.startsWith('#'))
    .map(l => { const i = l.indexOf('='); return [l.slice(0, i).trim(), l.slice(i + 1).trim()]; })
);
const TOGETHER_KEY = env.TOGETHER_KEY || process.env.TOGETHER_KEY;
if (!TOGETHER_KEY) { console.error('Missing TOGETHER_KEY in .env'); process.exit(1); }

const FORCE = process.argv.includes('--force');

const VENDOR_STYLE = 'dark fantasy shopkeeper portrait, oil painting, grim atmospheric, head and shoulders, isolated on black background, centered';
const TAVERN_STYLE = 'dark fantasy mercenary character portrait, oil painting, grim, head and shoulders, isolated on black background, centered';
const ITEM_STYLE = 'dark fantasy game item icon, single object centered on black background, soft rim light, painterly, no text';

const CLASS_DESC = {
  bonewarden: 'a grave-warden in bone-plated armor wielding a spear',
  stillblade: 'a silent assassin with a single thin blade',
  cauterist: 'a battlefield surgeon with cautery irons and bandages',
  hollow: 'a gaunt empty-eyed wanderer drained of self',
  fieldwright: 'a fortress engineer with tools and a buckler',
  inkblood: 'a tattooed scribe-archivist marked with running ink',
  marcher: 'a road-worn veteran soldier with a heavy pack',
  ashmouth: 'a soot-covered firestarter wreathed in embers',
};

async function generate(prompt, outPath) {
  const res = await fetch('https://api.together.xyz/v1/images/generations', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${TOGETHER_KEY}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      model: 'black-forest-labs/FLUX.1-schnell',
      prompt, width: 512, height: 512, steps: 4, n: 1, response_format: 'b64_json',
    }),
  });
  if (!res.ok) throw new Error(`Together API ${res.status}: ${await res.text()}`);
  const data = await res.json();
  const b64 = data?.data?.[0]?.b64_json;
  if (!b64) throw new Error(`No b64_json: ${JSON.stringify(data).slice(0, 200)}`);
  writeFileSync(outPath, Buffer.from(b64, 'base64'));
}

async function gen(id, prompt, dir) {
  const outDir = join(PUBLIC, dir);
  mkdirSync(outDir, { recursive: true });
  const outPath = join(outDir, `${id}.png`);
  if (existsSync(outPath) && !FORCE) { console.log(`skip ${dir}/${id}`); return; }
  process.stdout.write(`gen ${dir}/${id}... `);
  try { await generate(prompt, outPath); console.log('ok'); }
  catch (e) { console.log(`FAIL: ${e.message}`); }
  await new Promise(r => setTimeout(r, 1500));
}

function readJsonDir(rel) {
  const dir = join(ROOT, rel);
  return readdirSync(dir).filter(f => f.endsWith('.json'))
    .flatMap(f => { const d = JSON.parse(readFileSync(join(dir, f), 'utf8')); return Array.isArray(d) ? d : [d]; });
}

// Vendors: one per faction + generic.
const factions = readJsonDir('content/factions');
await gen('generic', `A grizzled black-market trader behind a cluttered stall. ${VENDOR_STYLE}`, 'vendors');
for (const f of factions) {
  await gen(f.id, `${f.name} quartermaster. ${(f.identity || '').slice(0, 200)} ${VENDOR_STYLE}`, 'vendors');
}

// Tavern: one per class.
for (const [classId, desc] of Object.entries(CLASS_DESC)) {
  await gen(classId, `${desc}. ${TAVERN_STYLE}`, 'tavern');
}

// Items: one per item id.
const items = readJsonDir('content/items');
for (const it of items) {
  await gen(it.id, `${it.name}: ${it.description}. ${ITEM_STYLE}`, 'items');
}

console.log('done');
