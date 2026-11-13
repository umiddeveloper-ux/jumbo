// Takes the pictures from the printed cards (Jumbo.Api/SeedData/Images/*.zip, one zip per topic, files "<word id>_<Word>.png", 1323x1843)
// and puts them in place of the drawn SVG pictures of the words:
//   1. cuts the middle picture out of every card (above the orange wave, under the level mark and the number),
//   2. makes a small JPEG of it and wraps it into an SVG (a red square with the picture inside; the app and the website draw SVG),
//   3. writes it into the "svg" field of the word in SeedData/Books/book_NN_*.json (so a reload of the book has it),
//   4. writes an SQL file that puts the same pictures into the database without touching the progress of the learners:
//        words 1..N -> svg_images of the word; the first word of every topic -> also the picture of the topic.
// Run:  node tools/import-card-images.mjs [out.sql]      (needs ffmpeg and tar in the PATH; default out: .local-test/card-images.sql)
// Apply:  docker exec -i jumbo-postgres psql -U littlepolyglot -d littlepolyglot < .local-test/card-images.sql
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import zlib from 'node:zlib';

const root = path.resolve(import.meta.dirname, '..');
const imagesDir = path.join(root, 'Jumbo.Api/SeedData/Images');
const booksDir = path.join(root, 'Jumbo.Api/SeedData/Books');
// usage: node tools/import-card-images.mjs [out.sql] [--from=N]   (--from: the SQL has only the words with number >= N, e.g. the new books)
const argv = process.argv.slice(2);
const FROM = parseInt((argv.find(a => a.startsWith('--from=')) ?? '--from=0').slice(7), 10);
const outSql = path.resolve(argv.find(a => !a.startsWith('--')) ?? path.join(root, '.local-test/card-images.sql'));
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'jumbo-cards-'));

// the picture inside the card (pixels of the 1323x1843 card): the card has a margin of 45, the level mark and the number are above y=270,
// the orange wave starts at y=1250. Two layouts exist: the picture on the red card itself (Pronouns, Numbers...), or inside a white rounded
// frame (Colours...): the second one is found by two white pixels inside the frame, and then only the white frame is cut out.
const LAYOUTS = {
  red:   { w: 1233, h: 1085, x: 45, y: 50 },        // from the top of the card: a picture may start beside the level mark and the number
  scene: { w: 1100, h: 850, x: 111, y: 270 },       // the picture fills the card and the marks are printed over it: only the part under them
  white: { w: 1020, h: 800, x: 150, y: 320 },
};
const OUT_W = 420;
function pixel(png, x, y) {
  const b = execFileSync('ffmpeg', ['-loglevel', 'error', '-i', png, '-vf', `crop=1:1:${x}:${y}`, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']);
  return [b[0], b[1], b[2]];
}
const isWhite = p => p.every(v => v > 240);
const isRed = p => p[0] > 150 && p[1] < 70 && p[2] < 80;
function layoutOf(png) {
  if (isWhite(pixel(png, 150, 400)) && isWhite(pixel(png, 150, 700))) return 'white';
  // the card red beside the level mark and the number: the picture stands on the red (it may start high, between the marks); otherwise it is a scene
  return isRed(pixel(png, 70, 260)) && isRed(pixel(png, 1250, 260)) ? 'red' : 'scene';
}

// the archives lie in the folder itself or in subfolders (one per book: 1, 2, 3 ...)
// A card that was cut short while it was saved (no end of the file: "chunk too big" for ffmpeg) is repaired: the last chunk gets its end (the
// missing bytes are zeros, the check sum is counted again) and the closing chunk IEND is added; the picture of a card is in the middle of it, the
// lost bytes are at the very end of the picture (below the wave).
function repairPng(file) {
  const b = fs.readFileSync(file);
  let p = 8, lastStart = 8, hasEnd = false;
  while (p + 8 <= b.length) {
    const len = b.readUInt32BE(p), type = b.toString('ascii', p + 4, p + 8);
    lastStart = p;
    if (type === 'IEND') { hasEnd = true; break; }
    p += 12 + len;
  }
  if (hasEnd && p + 12 <= b.length) return false;
  const len = b.readUInt32BE(lastStart), total = 12 + len;
  const chunk = Buffer.alloc(total);
  b.copy(chunk, 0, lastStart, Math.min(b.length, lastStart + total));
  chunk.writeUInt32BE(zlib.crc32(chunk.subarray(4, 8 + len)), 8 + len);
  const iend = Buffer.from([0, 0, 0, 0, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82]);
  fs.writeFileSync(file, Buffer.concat([b.subarray(0, lastStart), chunk, iend]));
  return true;
}

const zips = fs.readdirSync(imagesDir, { recursive: true }).filter(f => f.toLowerCase().endsWith('.zip')).sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
const cards = new Map();      // word id -> png path
for (const z of zips) {
  const dir = path.join(tmp, z.replace(/[\/]/g, '_').replace(/.zip$/i, ''));
  fs.mkdirSync(dir, { recursive: true });
  // the tar of Windows (bsdtar) reads zip; a GNU tar would take "D:" for a host name
  const sysTar = path.join(process.env.SystemRoot ?? 'C:\Windows', 'System32', 'tar.exe');
  execFileSync(fs.existsSync(sysTar) ? sysTar : 'tar', ['-xf', path.join(imagesDir, z), '-C', dir]);
  for (const f of fs.readdirSync(dir, { recursive: true })) {
    const m = /(?:^|[\\/])(\d+)_[^\\/]*\.png$/i.exec(f);
    if (m) {
      const file = path.join(dir, f);
      if (repairPng(file)) console.log('repaired (the file was cut short):', f);
      cards.set(parseInt(m[1], 10), file);
    }
  }
}
console.log(zips.length + ' zips, ' + cards.size + ' cards');

// The red of the card is replaced by the SOFT red of the site (the colour of level A1 mixed 30 % with white, see JumboWeb/wwwroot/soft-colors.css),
// so that the picture melts into the card of the game. Only the pixels close to the background colour are moved (the shading of the background
// and the soft edges of the characters are kept); set SOFT_BG = null to keep the original red.
const SOFT_BG = [0xED, 0x74, 0x76];
function softenBackground(buf, w, h) {
  // the colour of the background of the picture: the first of some places near the edges where it is the red of the card (a scene that fills the
  // picture - a bus, a hospital - may have something else in a corner)
  const spots = [[0, 0], [w - 16, 0], [0, Math.floor(h / 2)], [w - 16, Math.floor(h / 2)], [Math.floor(w / 2) - 8, 0]];
  let bg = null;
  for (const [sx, sy] of spots) {
    let r = 0, g = 0, b = 0, n = 0;
    for (let y = sy; y < sy + 16 && y < h; y++) for (let x = sx; x < sx + 16 && x < w; x++) { const o = (y * w + x) * 3; r += buf[o]; g += buf[o + 1]; b += buf[o + 2]; n++; }
    const c = [r / n, g / n, b / n];
    if (c[0] > 150 && c[1] < 70 && c[2] < 80) { bg = c; break; }
  }
  if (!bg) return false;      // a white frame (no red background): nothing to do
  const dr = SOFT_BG[0] - bg[0], dg = SOFT_BG[1] - bg[1], db = SOFT_BG[2] - bg[2];
  for (let o = 0; o < buf.length; o += 3) {
    const d = Math.hypot(buf[o] - bg[0], buf[o + 1] - bg[1], buf[o + 2] - bg[2]);
    const wgt = d <= 45 ? 1 : d >= 110 ? 0 : (110 - d) / 65;
    if (wgt === 0) continue;
    buf[o] = Math.max(0, Math.min(255, Math.round(buf[o] + wgt * dr)));
    buf[o + 1] = Math.max(0, Math.min(255, Math.round(buf[o + 1] + wgt * dg)));
    buf[o + 2] = Math.max(0, Math.min(255, Math.round(buf[o + 2] + wgt * db)));
  }
  return true;
}

function pictureSvg(png) {
  const layout = layoutOf(png);
  const CROP = LAYOUTS[layout];
  const H = Math.round(OUT_W * CROP.h / CROP.w);
  // the level mark (left) and the number (right) are painted over with the red of the card, so that the picture can start at the top
  let mask = '';
  if (layout === 'red') {
    const [r, g, b] = pixel(png, 70, 260);
    const col = '0x' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('');
    mask = `drawbox=x=0:y=0:w=490:h=220:color=${col}:t=fill,drawbox=x=1040:y=0:w=283:h=280:color=${col}:t=fill,`;
  }
  const raw = execFileSync('ffmpeg', ['-loglevel', 'error', '-i', png, '-vf', `${mask}crop=${CROP.w}:${CROP.h}:${CROP.x}:${CROP.y},scale=${OUT_W}:${H}:flags=lanczos`, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], { maxBuffer: 1 << 28 });
  const softened = SOFT_BG ? softenBackground(raw, OUT_W, H) : false;
  // the colour of the background of the square: the corner of the (softened) picture
  let r = 0, g = 0, b = 0;
  for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) { const o = (y * OUT_W + x) * 3; r += raw[o]; g += raw[o + 1]; b += raw[o + 2]; }
  const bg = softened ? '#' + SOFT_BG.map(v => v.toString(16).padStart(2, '0')).join('')
                      : '#' + [r, g, b].map(v => Math.round(v / 256).toString(16).padStart(2, '0')).join('');
  const jpg = png + '.jpg';
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', `${OUT_W}x${H}`, '-i', '-', '-q:v', '5', jpg], { input: raw, maxBuffer: 1 << 28 });
  const b64 = fs.readFileSync(jpg).toString('base64');
  // A picture on the red of the card (softened): it stands on the bottom edge of the square, the red goes on above it (seamless). A scene that
  // fills the whole picture (a bus, a cinema) has no such background: it covers the square (the sides are cut a little).
  const h = +(100 * CROP.h / CROP.w).toFixed(2);
  const img = softened || layout === 'white'
    ? `<image x='0' y='${(100 - h).toFixed(2)}' width='100' height='${h}' xlink:href='data:image/jpeg;base64,${b64}'/>`
    : `<image x='${(-(100 * CROP.w / CROP.h - 100) / 2).toFixed(2)}' y='0' width='${(100 * CROP.w / CROP.h).toFixed(2)}' height='100' xlink:href='data:image/jpeg;base64,${b64}'/>`;
  return `<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'><defs><clipPath id='c'><rect width='100' height='100' rx='18'/></clipPath></defs><g clip-path='url(#c)'><rect width='100' height='100' fill='${bg}'/>${img}</g></svg>`;
}

const svgById = new Map();

for (const [id, png] of [...cards].sort((a, b) => a[0] - b[0])) svgById.set(id, pictureSvg(png));

// 3. the books
const firstOfTopic = [];
for (const f of fs.readdirSync(booksDir).filter(n => /^book_\d+.*\.json$/.test(n))) {
  const p = path.join(booksDir, f);
  let text = fs.readFileSync(p, 'utf8');
  const j = JSON.parse(text);
  const seen = new Set();
  for (const w of j.words) {
    if (!svgById.has(w.id)) continue;
    if (!seen.has(w.subject)) { seen.add(w.subject); firstOfTopic.push(w.id); }
    const re = new RegExp('("id": ' + w.id + ',[\\s\\S]*?"svg": ")((?:[^"\\\\]|\\\\.)*)(")');
    if (!re.test(text)) throw new Error('no svg field for id ' + w.id + ' in ' + f);
    text = text.replace(re, (m, a, _old, c) => a + svgById.get(w.id) + c);
  }
  fs.writeFileSync(p, text);
  console.log(f, 'updated');
}

// 4. SQL
fs.mkdirSync(path.dirname(outSql), { recursive: true });
let sql = '-- generated by tools/import-card-images.mjs: the pictures of the printed cards instead of the drawn SVG\nBEGIN;\n';
for (const [id, svg] of svgById) {
  if (id < FROM) continue;
  sql += `UPDATE svg_images SET svg = $svg$${svg}$svg$ WHERE svg_image_id IN (SELECT svg_image_id FROM words WHERE word_number = ${id});\n`;
}
for (const id of firstOfTopic) {
  if (id < FROM) continue;
  sql += `UPDATE svg_images SET svg = $svg$${svgById.get(id)}$svg$ WHERE svg_image_id IN (SELECT s.svg_image_id FROM subjects s WHERE s.subject_id IN (SELECT subject_id FROM words WHERE word_number = ${id}));\n`;
}
sql += 'COMMIT;\n';
fs.writeFileSync(outSql, sql);
console.log('SQL:', outSql, (sql.length / 1e6).toFixed(1) + ' MB,', svgById.size, 'words,', firstOfTopic.length, 'topics');
fs.rmSync(tmp, { recursive: true, force: true });
