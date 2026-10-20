// Generates wwwroot/maps/world.svg: a light, simplified world map used as the faded background of the
// website's landing page. The 14 countries of the learning path are drawn in a second colour.
//
//   node tools/gen-world.mjs <dir-with-ne-geojson> <out-dir>
//
// Input: Natural Earth ne_50m_admin_0_countries (public domain) saved as <dir>/countries.geojson.
import fs from 'node:fs';
import path from 'node:path';

const [, , neDir, outDir] = process.argv;
if (!neDir || !outDir) { console.error('usage: node gen-world.mjs <ne-dir> <out-dir>'); process.exit(1); }

const JOURNEY = new Set(['UZB', 'JPN', 'EGY', 'CHN', 'BRA', 'IND', 'TUR', 'ITA', 'KOR', 'FRA', 'ESP', 'ARG', 'ROU', 'DEU']);
const WIDTH = 1000, LAT_TOP = 84, LAT_BOTTOM = -58;
const HEIGHT = Math.round((WIDTH * (LAT_TOP - LAT_BOTTOM)) / 360);
const proj = ([lon, lat]) => [((lon + 180) / 360) * WIDTH, ((LAT_TOP - lat) / (LAT_TOP - LAT_BOTTOM)) * HEIGHT];

const world = JSON.parse(fs.readFileSync(path.join(neDir, 'countries.geojson'), 'utf8'));

const area = (ring) => { let a = 0; for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) a += ring[j][0] * ring[i][1] - ring[i][0] * ring[j][1]; return Math.abs(a / 2); };

function simplify(pts, tol) { // Douglas-Peucker
  if (pts.length < 4) return pts;
  const keep = new Uint8Array(pts.length); keep[0] = keep[pts.length - 1] = 1;
  const stack = [[0, pts.length - 1]];
  while (stack.length) {
    const [a, b] = stack.pop(); let max = 0, idx = -1;
    const [ax, ay] = pts[a], [bx, by] = pts[b]; const dx = bx - ax, dy = by - ay, len = Math.hypot(dx, dy) || 1e-9;
    for (let i = a + 1; i < b; i++) { const d = Math.abs(dy * pts[i][0] - dx * pts[i][1] + bx * ay - by * ax) / len; if (d > max) { max = d; idx = i; } }
    if (max > tol) { keep[idx] = 1; stack.push([a, idx], [idx, b]); }
  }
  return pts.filter((_, i) => keep[i]);
}
function simplifyRing(ring, tol) { // closed ring: split at the farthest vertex so DP has a real baseline
  if (ring.length < 5) return ring;
  let far = 0, fd = -1; for (let i = 1; i < ring.length; i++) { const d = Math.hypot(ring[i][0] - ring[0][0], ring[i][1] - ring[0][1]); if (d > fd) { fd = d; far = i; } }
  return [...simplify(ring.slice(0, far + 1), tol), ...simplify(ring.slice(far), tol).slice(1)];
}

const paths = { w: [], j: [] };
for (const f of world.features) {
  const iso = f.properties.ADM0_A3;
  if (iso === 'ATA') continue; // Antarctica is cropped out
  const polys = f.geometry.type === 'Polygon' ? [f.geometry.coordinates] : f.geometry.coordinates;
  for (const p of polys) {
    const ring = p[0].map(proj);
    if (area(ring) < 1.2) continue; // specks
    const s = simplifyRing(ring, 0.5);
    if (s.length < 3) continue;
    paths[JOURNEY.has(iso) ? 'j' : 'w'].push('M' + s.map(([x, y]) => `${x.toFixed(1)} ${y.toFixed(1)}`).join('L') + 'Z');
  }
}

const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${WIDTH} ${HEIGHT}" role="img" aria-label="world">
  <path fill="#8FB0CE" d="${paths.w.join('')}"/>
  <path fill="#58CC02" d="${paths.j.join('')}"/>
</svg>
`;
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'world.svg'), svg);
console.log(`world.svg ${WIDTH}x${HEIGHT}, ${(svg.length / 1024).toFixed(0)} KB, ${paths.w.length + paths.j.length} shapes`);
