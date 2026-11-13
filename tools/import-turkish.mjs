// Turns the Turkish book files (SeedData/Books/Turk/*_JUMBO_<N>-kitob_TR-UZ.docx: topics with Turkish words, Uzbek translations and
// example sentences) into the files the API seeds the Turkish study language from: SeedData/Books/Turk/book_NN_tr.json.
//
// The Turkish edition is a true companion of the English one: the English books were themselves edited (2026-11-13) so that word
// number N means the same thing in both languages for every one of the 3000 words (books 1 and 5 had a couple of topics renumbered
// for exactly this reason: see tools/enrich-missing-words.mjs for how the English side was filled in). A Turkish row therefore
// always shares its picture - and so its ru/en/uz translations - with the English word of the SAME number. If a later edition of
// either book ever breaks that for some id, this script stops and names it rather than writing a wrong picture.
//
// Run (PowerShell or Git Bash, Windows tar reads zip):  node tools/import-turkish.mjs [--docx <dir>] [--out <dir>]
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root = path.resolve(import.meta.dirname, '..');
const args = process.argv.slice(2);
const opt = name => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const booksDir = path.join(root, 'Jumbo.Api/SeedData/Books');
const docxDir = opt('--docx') ?? path.join(booksDir, 'Turk');
const outDir = opt('--out') ?? path.join(booksDir, 'Turk');

// The Uzbek column of the Turkish books is written with Turkish letters (ö ç ş ğ): converted to the Uzbek Latin alphabet of the app.
const toUzbek = s => s
  .replace(/Ö/g, 'O‘').replace(/ö/g, 'o‘').replace(/Ç/g, 'Ch').replace(/ç/g, 'ch').replace(/Ş/g, 'Sh').replace(/ş/g, 'sh')
  .replace(/Ğ/g, 'G‘').replace(/ğ/g, 'g‘').replace(/ʼ/g, '’').replace(/'/g, '’');

// ───────────── docx ─────────────
const unesc = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');
const textOf = frag => unesc([...frag.matchAll(/<w:t(?: [^>]*)?>([^<]*)<\/w:t>/g)].map(m => m[1]).join('')).replace(/\s+/g, ' ').trim();

function readDocumentXml(file) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'jumbo-docx-'));
  try {
    const attempts = [['unzip', ['-o', '-q', file, 'word/document.xml', '-d', tmp]],
      ['C:/Windows/System32/tar.exe', ['-xf', file, '-C', tmp, 'word/document.xml']], ['tar', ['-xf', file, '-C', tmp, 'word/document.xml']]];
    for (const [cmd, a] of attempts) { try { execFileSync(cmd, a, { stdio: 'ignore' }); break; } catch { /* next */ } }
    return fs.readFileSync(path.join(tmp, 'word/document.xml'), 'utf8');
  } finally { fs.rmSync(tmp, { recursive: true, force: true }); }
}

// word rows: [☐, №, Turkish word, Turkish sentence, ☐, №, Uzbek word, Uzbek sentence]; the heading above a table: "01 Merhaba ve Hoşça Kal · Salom va xayr"
function parseBook(file) {
  const xml = readDocumentXml(file);
  const topics = [];
  let pos = 0, heading = null, m;
  const tables = /<w:tbl>[\s\S]*?<\/w:tbl>/g;
  while ((m = tables.exec(xml))) {
    for (const p of xml.slice(pos, m.index).matchAll(/<w:p[ >][\s\S]*?<\/w:p>/g)) { const t = textOf(p[0]); if (t) heading = t; }
    pos = m.index + m[0].length;
    const rows = [...m[0].matchAll(/<w:tr[ >][\s\S]*?<\/w:tr>/g)].map(r => [...r[0].matchAll(/<w:tc>[\s\S]*?<\/w:tc>/g)].map(c => textOf(c[0])));
    const wordRows = rows.filter(r => r.length >= 8 && /^\d+$/.test(r[1]) && /^\d+$/.test(r[5]));
    if (wordRows.length === 0) continue;
    const [tr, uz] = (heading ?? '').split('·').map(x => x.replace(/\s+/g, ' ').replace(/^\d{1,2}\s+/, '').trim());
    topics.push({
      tr, uz: toUzbek(uz ?? ''),
      words: wordRows.map(r => {
        if (r[1] !== r[5]) throw new Error(`${path.basename(file)}: row ${r[1]} / ${r[5]} - Turkish and Uzbek numbers differ`);
        return { id: +r[1], tr: r[2], uz: toUzbek(r[6]), example_tr: r[3], example_uz: toUzbek(r[7]) };
      }),
    });
  }
  return topics;
}

// ───────────── English books: the levels, the topics, which numbers exist ─────────────
const english = new Map();   // book -> { level, subjects: [name...], ids: Set }
for (const f of fs.readdirSync(booksDir).filter(f => /^book_\d+_.*\.json$/i.test(f))) {
  const j = JSON.parse(fs.readFileSync(path.join(booksDir, f), 'utf8'));
  english.set(j.metadata.book, { level: j.metadata.level, subjects: [...new Set(j.words.map(w => w.subject))], ids: new Set(j.words.map(w => w.id)) });
}
const allEnglishIds = new Set([...english.values()].flatMap(b => [...b.ids]));

// Typos in the docx itself, left as they are elsewhere in the book: the Uzbek example sentence names the wrong shade of blue
// (copied from the neighbouring "dark blue" row). Applied after parsing so a fresh run of the script does not lose the fix.
const EXAMPLE_UZ_FIX = { 104: 'Uning paltosi ko‘k.' };

// ───────────── build ─────────────
const docs = fs.readdirSync(docxDir).filter(f => /\.docx$/i.test(f) && !f.startsWith('~$'));
if (docs.length === 0) { console.error(`No .docx files in ${docxDir}`); process.exit(1); }

const problems = [];
const results = [];
for (const doc of docs) {
  const book = +(doc.match(/(\d+)-kitob/i) ?? [])[1];
  if (!book) { console.warn(`skipped ${doc}: no "<N>-kitob" in the name`); continue; }
  const en = english.get(book);
  if (!en) { problems.push(`${doc}: there is no English book ${book}`); continue; }
  const topics = parseBook(path.join(docxDir, doc));
  if (topics.length !== en.subjects.length) problems.push(`book ${book}: ${topics.length} topics in the Turkish book, ${en.subjects.length} in the English one`);

  const words = [];
  topics.forEach((t, ti) => {
    if (t.words.length !== 15) problems.push(`book ${book}, topic ${ti + 1} "${t.tr}": ${t.words.length} words`);
    for (const w of t.words) {
      if (!allEnglishIds.has(w.id)) problems.push(`book ${book}, word ${w.id} "${w.tr}": no English word ${w.id} to share the picture with`);
      for (const f of ['tr', 'uz', 'example_tr', 'example_uz']) if (!w[f]) problems.push(`book ${book}, word ${w.id}: no ${f}`);
      words.push({ id: w.id, topic: ti + 1, tr: w.tr, uz: w.uz, example_tr: w.example_tr, example_uz: EXAMPLE_UZ_FIX[w.id] ?? w.example_uz, concept: w.id });
    }
  });
  const ids = words.map(w => w.id);
  if (new Set(ids).size !== ids.length) problems.push(`book ${book}: duplicate word numbers`);
  const expected = [...en.ids].sort((a, b) => a - b);
  if (expected.join() !== [...ids].sort((a, b) => a - b).join()) problems.push(`book ${book}: the word numbers differ from the English book`);

  results.push({
    book, name: `book_${String(book).padStart(2, '0')}_tr.json`,
    json: {
      metadata: { book, level: en.level, language: 'tr', word_count: words.length, id_range: [Math.min(...ids), Math.max(...ids)],
        schema: ['id', 'topic', 'tr', 'uz', 'example_tr', 'example_uz', 'concept'] },
      topics: topics.map(t => ({ tr: t.tr, uz: t.uz })),
      words,
    },
  });
}

if (problems.length) { problems.forEach(p => console.error('PROBLEM: ' + p)); console.error('\nNothing written.'); process.exit(1); }

fs.mkdirSync(outDir, { recursive: true });
for (const r of results.sort((a, b) => a.book - b.book)) {
  fs.writeFileSync(path.join(outDir, r.name), JSON.stringify(r.json, null, 1) + '\n');
  console.log(`${r.name}: ${r.json.words.length} words, ${r.json.topics.length} topics`);
}
