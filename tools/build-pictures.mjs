// Makes the picture FILES of the words from the database: every picture of the table svg_images that has a photo inside its svg
// (<image ... xlink:href='data:image/jpeg;base64,...'>) becomes  <out>/<name>.webp , name = first 16 hex letters of the SHA-256 of the
// photo bytes (the same rule as Jumbo.Shared/PictureFiles.cs, so the API and this tool agree without a table).
//
// The svg shows a CENTRE CROP of the photo in a 100x100 square (the <image> is bigger than the square and cut by the viewBox), so the
// file is that exact square (not the whole photo): the client only has to round the corners (the svg clips with rx=18, i.e. 18 %).
// Size: the native size of the crop, at most 400 x 400, WebP quality 80.
//
//   node tools/build-pictures.mjs                  -> media-dev/pics  (the local database in the container jumbo-postgres)
//   node tools/build-pictures.mjs --out D:/x --jobs 8 --limit 20
//
// A file that exists is skipped (the name says what is inside). Needs node and ffmpeg on the PATH and docker for the local database.
// To make the files for the PRODUCTION pictures run it against a copy of the production pictures: --sql-file <file with lines
// "id<TAB>svg"> instead of the database (see tools/apply-pictures.ps1, which does not need it: the names are the same everywhere).
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createInterface } from 'node:readline';
import { createReadStream, existsSync, mkdirSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(`--${name}`); return i >= 0 ? args[i + 1] : def; };
const OUT = opt('out', 'media-dev/pics');
const JOBS = Number(opt('jobs', '8'));
const LIMIT = Number(opt('limit', '0'));
const CONTAINER = opt('container', 'jumbo-postgres');
const SQL_FILE = opt('sql-file', '');
const MAX_SIDE = 400;
const QUALITY = 80;

mkdirSync(OUT, { recursive: true });

const EMBEDDED = /data:image\/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)/;
const IMAGE_TAG = /<image\s+x='(-?[\d.]+)'\s+y='(-?[\d.]+)'\s+width='(-?[\d.]+)'\s+height='(-?[\d.]+)'/;
const FILL = /<rect width='100' height='100' fill='(#[0-9a-fA-F]{3,8})'/;

// width x height of a jpeg (the start-of-frame marker)
function jpegSize(b) {
  let i = 2;
  while (i + 9 < b.length) {
    if (b[i] !== 0xff) { i++; continue; }
    const t = b[i + 1];
    if (t >= 0xc0 && t <= 0xcf && t !== 0xc4 && t !== 0xc8 && t !== 0xcc) return { w: b.readUInt16BE(i + 7), h: b.readUInt16BE(i + 5) };
    i += 2 + b.readUInt16BE(i + 2);
  }
  return null;
}

// What to do with one svg: { name, bytes, filter } or null
function plan(svg) {
  const m = EMBEDDED.exec(svg);
  if (!m) return null;
  const bytes = Buffer.from(m[2], 'base64');
  const name = createHash('sha256').update(bytes).digest('hex').slice(0, 16) + '.webp';
  const g = IMAGE_TAG.exec(svg);
  if (!g) return { name, bytes, filter: null, geometry: 'whole' };
  const [x, y, w, h] = g.slice(1, 5).map(Number);
  if (!(w > 0 && h > 0)) return { name, bytes, filter: null, geometry: 'whole' };

  // the part of the photo that is inside the square 0..100
  const ix0 = Math.max(0, x), iy0 = Math.max(0, y), ix1 = Math.min(100, x + w), iy1 = Math.min(100, y + h);
  if (ix1 <= ix0 || iy1 <= iy0) return { name, bytes, filter: null, geometry: 'whole' };
  const size = m[1] === 'jpeg' ? jpegSize(bytes) : null;
  // pixels per svg unit: the native resolution of the photo, at most MAX_SIDE for the whole square
  const k = Math.min(MAX_SIDE / 100, size ? Math.min(size.w / w, size.h / h) : Infinity);
  const f = (n) => Number(n.toFixed(6));
  const crop = `crop=w=iw*${f((ix1 - ix0) / w)}:h=ih*${f((iy1 - iy0) / h)}:x=iw*${f((ix0 - x) / w)}:y=ih*${f((iy0 - y) / h)}`;
  const canvas = Math.round(100 * k);
  const sw = Math.max(1, Math.round((ix1 - ix0) * k)), sh = Math.max(1, Math.round((iy1 - iy0) * k));
  let filter = `${crop},scale=${sw}:${sh}:flags=lanczos`;
  if (sw !== canvas || sh !== canvas) {
    const fill = FILL.exec(svg)?.[1] ?? '#ffffff';
    filter += `,pad=${canvas}:${canvas}:${Math.round(ix0 * k)}:${Math.round(iy0 * k)}:color=${fill.replace('#', '0x')}`;
  }
  return { name, bytes, filter, geometry: `${x},${y},${w},${h}` };
}

function convert(p) {
  return new Promise((resolve, reject) => {
    const ffArgs = ['-v', 'error', '-y', '-f', 'image2pipe', '-i', 'pipe:0'];
    if (p.filter) ffArgs.push('-vf', p.filter);
    ffArgs.push('-c:v', 'libwebp', '-quality', String(QUALITY), '-compression_level', '6', '-frames:v', '1', join(OUT, p.name));
    const ff = spawn('ffmpeg', ffArgs, { stdio: ['pipe', 'ignore', 'pipe'] });
    let err = '';
    ff.stderr.on('data', (d) => { err += d; });
    ff.on('error', reject);
    ff.on('close', (code) => (code === 0 ? resolve() : reject(new Error(`ffmpeg ${code}: ${err.trim().slice(0, 300)}`))));
    ff.stdin.on('error', () => { /* reported by the exit code */ });
    ff.stdin.end(p.bytes);
  });
}

// the rows: "id<TAB>svg" lines, from a file or from the local database
function rowStream() {
  if (SQL_FILE) return createInterface({ input: createReadStream(SQL_FILE, 'utf8'), crlfDelay: Infinity });
  const q = "select svg_image_id || E'\\t' || replace(replace(svg, E'\\r', ''), E'\\n', ' ') from svg_images where svg like '%data:image/%' order by svg_image_id";
  const psql = spawn('docker', ['exec', CONTAINER, 'psql', '-U', 'littlepolyglot', '-d', 'littlepolyglot', '-At', '-c', q], { stdio: ['ignore', 'pipe', 'inherit'] });
  psql.on('error', (e) => { console.error('docker failed:', e.message); process.exit(1); });
  return createInterface({ input: psql.stdout, crlfDelay: Infinity });
}

const t0 = Date.now();
const seen = new Map();            // name -> geometry (one file per photo)
const stats = { rows: 0, made: 0, existed: 0, same: 0, failed: 0, svgBytes: 0, jpegBytes: 0, conflicts: 0 };
const running = new Set();
const failures = [];

async function handle(id, svg) {
  const p = plan(svg);
  if (!p) return;
  stats.svgBytes += svg.length;
  stats.jpegBytes += p.bytes.length;
  if (seen.has(p.name)) {
    stats.same++;
    if (seen.get(p.name) !== p.geometry) { stats.conflicts++; console.warn(`! ${p.name}: the same photo with another crop (id ${id})`); }
    return;
  }
  seen.set(p.name, p.geometry);
  if (existsSync(join(OUT, p.name))) { stats.existed++; return; }
  try { await convert(p); stats.made++; }
  catch (e) { stats.failed++; failures.push(`id ${id}: ${e.message}`); }
}

for await (const line of rowStream()) {
  const tab = line.indexOf('\t');
  if (tab < 0) continue;
  stats.rows++;
  if (LIMIT && stats.rows > LIMIT) break;
  const job = handle(Number(line.slice(0, tab)), line.slice(tab + 1)).finally(() => running.delete(job));
  running.add(job);
  if (running.size >= JOBS) await Promise.race(running);
  if (stats.rows % 500 === 0) console.log(`  ${stats.rows} rows, ${stats.made} files made`);
}
await Promise.all(running);

const files = readdirSync(OUT).filter((f) => f.endsWith('.webp'));
const webpBytes = files.reduce((sum, f) => sum + statSync(join(OUT, f)).size, 0);
const mb = (n) => (n / 1048576).toFixed(1) + ' MB';
console.log(`\nrows with a photo: ${stats.rows}; files made: ${stats.made}; already there: ${stats.existed}; same photo again: ${stats.same}; failed: ${stats.failed}; crop conflicts: ${stats.conflicts}`);
console.log(`svg text (what is in the database): ${mb(stats.svgBytes)}; the photos inside it: ${mb(stats.jpegBytes)}; files in ${OUT}: ${files.length}, ${mb(webpBytes)}`);
console.log(`${((Date.now() - t0) / 1000).toFixed(0)} s`);
for (const f of failures.slice(0, 10)) console.error(f);
if (stats.failed) process.exit(1);
