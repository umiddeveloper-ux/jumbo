// Turns the printed book files (JUMBO N-kitob .docx: topics with English words, Russian translations and example
// sentences) into the book JSON files the API seeds from (Jumbo.Api/SeedData/Books/book_NN_<level>_<from>_<to>.json).
//
// The .docx only has English + Russian. What it does not have is taken from earlier data, so nothing is lost when a book
// is re-imported:
//   uz, example_uz, svg  <-  --enrich parts (enrich-book-<N>-p*.json: [{id, uz, example_uz, svg?}]), then
//                            the book's existing JSON (same English word AND same Russian meaning), then the --reuse
//                            folders (picture only: same English word with an overlapping Russian meaning).
// Words that still miss uz / svg are listed; without --allow-missing the run fails and writes nothing.
//
// Run:  node tools/import-books.mjs [--docx <dir>] [--out <dir>] [--enrich <dir>] [--reuse <dir>]... [--allow-missing]
// Defaults: --docx and --out = Jumbo.Api/SeedData/Books
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root = path.resolve(import.meta.dirname, '..');
const args = process.argv.slice(2);
const opt = (name, many = false) => {
  const out = [];
  for (let i = 0; i < args.length; i++) if (args[i] === name) out.push(args[++i]);
  return many ? out : out[0];
};
const booksDir = path.join(root, 'Jumbo.Api/SeedData/Books');
const docxDir = opt('--docx') ?? booksDir;
const outDir = opt('--out') ?? booksDir;
const enrichDir = opt('--enrich');
const reuseDirs = opt('--reuse', true);
const allowMissing = args.includes('--allow-missing');

// ───────────── docx ─────────────
const unesc = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');
const textOf = frag => unesc([...frag.matchAll(/<w:t(?: [^>]*)?>([^<]*)<\/w:t>/g)].map(m => m[1]).join('')).replace(/\s+/g, ' ').trim();

function readDocumentXml(file) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'jumbo-docx-'));
  try {
    try { execFileSync('unzip', ['-o', '-q', file, 'word/document.xml', '-d', tmp]); }
    catch { execFileSync('tar', ['-xf', file, '-C', tmp, 'word/document.xml']); }   // bsdtar (Windows) reads zip
    return fs.readFileSync(path.join(tmp, 'word/document.xml'), 'utf8');
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

// Returns { level, topics: [{ en, ru, words: [{ id, en, ru, example_en, example_ru }] }] }
function parseBook(file) {
  const xml = readDocumentXml(file);
  // The level is on the cover ("LEVEL A1 • BEGINNER"), split over several XML runs: look at the paragraph text.
  const cover = [...xml.slice(0, 200000).matchAll(/<w:p[ >][\s\S]*?<\/w:p>/g)].slice(0, 40).map(p => textOf(p[0])).join(' ');
  const level = (cover.match(/LEVEL\s+(A1|A2|B1|B2|C1|C2)/) ?? [])[1];
  const topics = [];
  let pos = 0, heading = null;
  const paragraphs = chunk => { for (const p of chunk.matchAll(/<w:p[ >][\s\S]*?<\/w:p>/g)) { const t = textOf(p[0]); if (t) heading = t; } };
  const tables = /<w:tbl>[\s\S]*?<\/w:tbl>/g;
  let m;
  while ((m = tables.exec(xml))) {
    paragraphs(xml.slice(pos, m.index));
    pos = m.index + m[0].length;
    const rows = [...m[0].matchAll(/<w:tr[ >][\s\S]*?<\/w:tr>/g)].map(r => [...r[0].matchAll(/<w:tc>[\s\S]*?<\/w:tc>/g)].map(c => textOf(c[0])));
    // word rows: [☐, №, word, sentence, ☐, №, russian word, russian sentence]
    const wordRows = rows.filter(r => r.length >= 8 && /^\d+$/.test(r[1]) && /^\d+$/.test(r[5]));
    if (wordRows.length === 0) continue;                       // grammar tables etc.
    // Headings look like "Hello and Goodbye · Привет и пока"; in the later books they carry a topic number: "01 Office & Work · ...".
    const [en, ru] = (heading ?? '').split('·').map(x => x.replace(/\s+/g, ' ').replace(/^\d{1,2}\s+/, '').trim());
    topics.push({
      en, ru,
      words: wordRows.map(r => {
        if (r[1] !== r[5]) throw new Error(`${path.basename(file)}: row ${r[1]} / ${r[5]} - English and Russian numbers differ`);
        return { id: +r[1], en: r[2], ru: r[6], example_en: r[3], example_ru: r[7] };
      }),
    });
  }
  return { level, topics };
}

// ───────────── earlier data ─────────────
const norm = s => s.toLowerCase().replace(/[^a-z0-9 ]/g, '').replace(/\s+/g, ' ').trim();
const ruTokens = s => new Set(s.toLowerCase().replace(/ё/g, 'е').split(/[,;/()\-–—.…?!]| и /).map(x => x.trim()).filter(Boolean));
const sameMeaning = (a, b) => { const B = ruTokens(b); return [...ruTokens(a)].some(x => B.has(x)); };

const readBookFiles = dir => !dir || !fs.existsSync(dir) ? [] :
  fs.readdirSync(dir).filter(f => /^book_\d+_.*\.json$/i.test(f)).map(f => ({ file: f, book: +f.match(/^book_(\d+)_/i)[1], json: JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8')) }));

const reuseWords = [...reuseDirs, outDir].flatMap(d => readBookFiles(d)).flatMap(b => b.json.words);
const reuseByEn = new Map();
for (const w of reuseWords) { const k = norm(w.en); if (!reuseByEn.has(k)) reuseByEn.set(k, []); reuseByEn.get(k).push(w); }

function enrichmentOf(book) {
  const byId = new Map();
  if (!enrichDir) return byId;
  for (const f of fs.readdirSync(enrichDir).filter(f => new RegExp(`^enrich-book-${book}-p\\d+\\.json$`).test(f)).sort())
    for (const x of JSON.parse(fs.readFileSync(path.join(enrichDir, f), 'utf8'))) byId.set(x.id, x);
  return byId;
}

// A typo in the 2026-11-13 docx itself (the Russian sentence is missing its first letter): fixed here so a fresh run of this
// script does not lose the correction. example_en: {id: fixed text}, same for example_ru.
const EXAMPLE_RU_FIX = { 1772: 'Тяжёлый камень утонул на дно бассейна.' };

// ───────────── build ─────────────
const docs = fs.readdirSync(docxDir).filter(f => /\.docx$/i.test(f) && !f.startsWith('~$'));
if (docs.length === 0) { console.error(`No .docx files in ${docxDir}`); process.exit(1); }

const problems = [];
const results = [];
for (const doc of docs) {
  const book = +(doc.match(/(\d+)-kitob/i) ?? [])[1];
  if (!book) { console.warn(`skipped ${doc}: no "<N>-kitob" in the name`); continue; }
  const { level, topics } = parseBook(path.join(docxDir, doc));
  if (!level) problems.push(`${doc}: level (LEVEL A1...) not found on the cover`);

  const existing = readBookFiles(outDir).find(b => b.book === book)?.json.words ?? [];
  const enrich = enrichmentOf(book);
  const words = [];
  for (const t of topics) for (const w of t.words) {
    const old = existing.find(x => norm(x.en) === norm(w.en) && sameMeaning(x.ru, w.ru));
    const e = enrich.get(w.id);
    const pic = old?.svg ?? (reuseByEn.get(norm(w.en)) ?? []).find(x => sameMeaning(x.ru, w.ru))?.svg;
    const row = {
      id: w.id, book, level, subject: t.en,
      en: w.en, ru: w.ru, uz: e?.uz ?? old?.uz ?? '',
      example_en: w.example_en, example_ru: EXAMPLE_RU_FIX[w.id] ?? w.example_ru, example_uz: e?.example_uz ?? old?.example_uz ?? '',
      svg: e?.svg ?? pic ?? '',
    };
    words.push(row);
    for (const f of ['uz', 'example_uz', 'svg']) if (!row[f]) problems.push(`book ${book}, word ${w.id} "${w.en}": no ${f}`);
  }

  const ids = words.map(w => w.id);
  if (new Set(ids).size !== ids.length) problems.push(`book ${book}: duplicate word numbers`);
  const json = {
    metadata: {
      book, level, word_count: words.length, id_range: [Math.min(...ids), Math.max(...ids)],
      schema: ['id', 'book', 'level', 'subject', 'en', 'ru', 'uz', 'example_en', 'example_ru', 'example_uz', 'svg'],
    },
    words,
  };
  const name = `book_${String(book).padStart(2, '0')}_${level.toLowerCase()}_${json.metadata.id_range[0]}_${json.metadata.id_range[1]}.json`;
  results.push({ book, name, json, topics: topics.length });
}

const missing = problems.filter(p => /no (uz|example_uz|svg)$/.test(p));
for (const p of problems.filter(p => !missing.includes(p))) console.error('PROBLEM: ' + p);
if (missing.length) {
  console.error(`${missing.length} fields are missing (first 30):`);
  missing.slice(0, 30).forEach(p => console.error('  ' + p));
}
if ((problems.length - missing.length) > 0 || (missing.length && !allowMissing)) {
  console.error('\nNothing written.' + (missing.length ? ' (use --allow-missing to write the file anyway)' : ''));
  process.exit(1);
}

fs.mkdirSync(outDir, { recursive: true });
for (const r of results) {
  for (const old of readBookFiles(outDir).filter(b => b.book === r.book && b.file !== r.name))
    fs.rmSync(path.join(outDir, old.file));          // the file name carries the id range: replace the old one
  fs.writeFileSync(path.join(outDir, r.name), JSON.stringify(r.json, null, 2) + '\n');
  console.log(`${r.name}: ${r.json.words.length} words, ${r.topics} topics`);
}
