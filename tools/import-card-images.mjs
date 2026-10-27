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

const root = path.resolve(import.meta.dirname, '..');
const imagesDir = path.join(root, 'Jumbo.Api/SeedData/Images');
const booksDir = path.join(root, 'Jumbo.Api/SeedData/Books');
const outSql = path.resolve(process.argv[2] ?? path.join(root, '.local-test/card-images.sql'));
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'jumbo-cards-'));

// the picture inside the card (pixels of the 1323x1843 card): the card has a margin of 45, the level mark and the number are above y=270,
// the orange wave starts at y=1250. Two layouts exist: the picture on the red card itself (Pronouns, Numbers...), or inside a white rounded
// frame (Colours...): the second one is found by two white pixels inside the frame, and then only the white frame is cut out.
const LAYOUTS = {
  red:   { w: 1233, h: 850, x: 45,  y: 270 },
  white: { w: 1020, h: 800, x: 150, y: 320 },
};
const OUT_W = 420;
function pixel(png, x, y) {
  const b = execFileSync('ffmpeg', ['-loglevel', 'error', '-i', png, '-vf', `crop=1:1:${x}:${y}`, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']);
  return [b[0], b[1], b[2]];
}
const isWhite = p => p.every(v => v > 240);
function layoutOf(png) { return isWhite(pixel(png, 150, 400)) && isWhite(pixel(png, 150, 700)) ? 'white' : 'red'; }

const zips = fs.readdirSync(imagesDir).filter(f => f.toLowerCase().endsWith('.zip')).sort((a, b) => parseInt(a) - parseInt(b));
const cards = new Map();      // word id -> png path
for (const z of zips) {
  const dir = path.join(tmp, path.basename(z, '.zip'));
  fs.mkdirSync(dir, { recursive: true });
  // the tar of Windows (bsdtar) reads zip; a GNU tar would take "D:" for a host name
  const sysTar = path.join(process.env.SystemRoot ?? 'C:\Windows', 'System32', 'tar.exe');
  execFileSync(fs.existsSync(sysTar) ? sysTar : 'tar', ['-xf', path.join(imagesDir, z), '-C', dir]);
  for (const f of fs.readdirSync(dir, { recursive: true })) {
    const m = /(?:^|[\\/])(\d+)_[^\\/]*\.png$/i.exec(f);
    if (m) cards.set(parseInt(m[1], 10), path.join(dir, f));
  }
}
console.log(zips.length + ' zips, ' + cards.size + ' cards');

function pictureSvg(png) {
  const CROP = LAYOUTS[layoutOf(png)];
  const jpg = png + '.jpg';
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-i', png, '-vf', `crop=${CROP.w}:${CROP.h}:${CROP.x}:${CROP.y},scale=${OUT_W}:-1:flags=lanczos`, '-q:v', '5', jpg]);
  // the colour of the background: the corner of the picture
  const px = execFileSync('ffmpeg', ['-loglevel', 'error', '-i', jpg, '-vf', 'crop=24:24:0:0,scale=1:1', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']);
  const bg = '#' + [...px.subarray(0, 3)].map(v => v.toString(16).padStart(2, '0')).join('');
  const b64 = fs.readFileSync(jpg).toString('base64');
  const h = +(100 * CROP.h / CROP.w).toFixed(2);          // the picture is wider than high: it stands on the bottom edge of the square
  return `<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'><defs><clipPath id='c'><rect width='100' height='100' rx='18'/></clipPath></defs><g clip-path='url(#c)'><rect width='100' height='100' fill='${bg}'/><image x='0' y='${(100 - h).toFixed(2)}' width='100' height='${h}' xlink:href='data:image/jpeg;base64,${b64}'/></g></svg>`;
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
  sql += `UPDATE svg_images SET svg = $svg$${svg}$svg$ WHERE svg_image_id IN (SELECT svg_image_id FROM words WHERE word_number = ${id});\n`;
}
for (const id of firstOfTopic) {
  sql += `UPDATE svg_images SET svg = $svg$${svgById.get(id)}$svg$ WHERE svg_image_id IN (SELECT s.svg_image_id FROM subjects s WHERE s.subject_id IN (SELECT subject_id FROM words WHERE word_number = ${id}));\n`;
}
sql += 'COMMIT;\n';
fs.writeFileSync(outSql, sql);
console.log('SQL:', outSql, (sql.length / 1e6).toFixed(1) + ' MB,', svgById.size, 'words,', firstOfTopic.length, 'topics');
fs.rmSync(tmp, { recursive: true, force: true });
