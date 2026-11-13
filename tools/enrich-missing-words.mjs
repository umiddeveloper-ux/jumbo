// One-off fix for the new English book editions (2026-11-13): the new docx added/renamed ~85 words (mostly to line up with the
// Turkish edition's numbering - see tools/import-turkish.mjs), so tools/import-books.mjs has no old Uzbek/picture to reuse for them.
// This script writes Jumbo.Api/SeedData/Books/enrich-book-N-pFix.json files (the format tools/import-books.mjs already reads via
// --enrich): uz + example_uz, taken from the Turkish book at the SAME word number wherever the concept matches 1:1 (that book's own
// Uzbek, already proofread), and a picture: the OLD picture of the word that used to sit at this id or at the matching concept's old
// id when the meaning did not change, or a small hand-drawn flat icon (the style already used in the course for things a photo
// cannot show, e.g. "charger", "propose") for the words that are genuinely new.
// Run once (needs the ORIGINAL pre-fix book JSON as a baseline, and the Turkish book_NN_tr.json files):
//   node tools/enrich-missing-words.mjs <path to a JSON dump of the old {id: word} map>
// then: node tools/import-books.mjs --enrich Jumbo.Api/SeedData/Books --allow-missing
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const booksDir = path.join(root, 'Jumbo.Api/SeedData/Books');
const old = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));   // id -> the word row before today's docx edit (from git HEAD)
const trDir = path.join(booksDir, 'Turk');
const tr = {};
for (const f of fs.readdirSync(trDir).filter(f => f.endsWith('_tr.json')))
  for (const w of JSON.parse(fs.readFileSync(path.join(trDir, f), 'utf8')).words) tr[w.id] = w;

// ── small hand-drawn flat icons, the style already used in the books for things a photo cannot show well (rounded card + a
// few flat shapes) - same convention as e.g. the existing "charger"/"propose"/"bride" pictures. bg = the card's pastel colour. ──
const ICONS = {
  // id: svg
  104: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#2962FF'/><circle cx='50' cy='50' r='26' fill='#82B1FF'/></svg>", // blue swatch
  118: numberCard('13', '#FDE2E2', '#C62828'),
  119: numberCard('14', '#E0F7FA', '#00838F'),
  120: numberCard('15', '#F1F8E9', '#558B2F'),
  594: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#ECEFF1'/><rect x='24' y='20' width='52' height='60' rx='6' fill='#455A64'/><circle cx='50' cy='48' r='11' fill='none' stroke='#CFD8DC' stroke-width='4'/><circle cx='50' cy='48' r='2.5' fill='#CFD8DC'/><rect x='46' y='48' width='4' height='8' fill='#CFD8DC'/></svg>", // safe
  614: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFF8E1'/><path d='M18 24 H28 L38 62 H74 L82 34 H34' fill='none' stroke='#FFA000' stroke-width='5' stroke-linecap='round' stroke-linejoin='round'/><circle cx='44' cy='76' r='6' fill='#5D4037'/><circle cx='68' cy='76' r='6' fill='#5D4037'/></svg>", // shopping cart
  615: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFF3E0'/><path d='M26 46 H74 L68 80 H32Z' fill='#D7A15C'/><path d='M26 46 H74 M34 46 L40 30 M66 46 L60 30' fill='none' stroke='#8D6E3B' stroke-width='4' stroke-linecap='round'/><path d='M40 30 Q50 18 60 30' fill='none' stroke='#8D6E3B' stroke-width='4'/></svg>", // basket
  622: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#EFEBE9'/><rect x='22' y='18' width='56' height='64' rx='4' fill='#8D6E63'/><rect x='32' y='34' width='36' height='40' fill='#212121'/><path d='M38 70 Q44 54 50 70 Q56 50 62 70Z' fill='#FF7043'/></svg>", // fireplace
  625: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFFDE7'/><path d='M50 14 V26' stroke='#BCAAA4' stroke-width='3'/><circle cx='50' cy='30' r='6' fill='#8D6E63'/><path d='M30 42 Q50 30 70 42' fill='none' stroke='#8D6E63' stroke-width='3'/><circle cx='30' cy='46' r='7' fill='#FFD54F'/><circle cx='50' cy='52' r='7' fill='#FFD54F'/><circle cx='70' cy='46' r='7' fill='#FFD54F'/></svg>", // chandelier
  704: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E8EAF6'/><path d='M50 22 L88 38 L50 54 L12 38Z' fill='#3949AB'/><path d='M30 46 V64 Q50 76 70 64 V46' fill='none' stroke='#3949AB' stroke-width='4'/><path d='M88 38 V58' stroke='#3949AB' stroke-width='3'/></svg>", // secondary (graduation cap)
  810: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FCE4EC'/><path d='M20 50 H56 M20 50 L32 38 M20 50 L32 62' fill='none' stroke='#D81B60' stroke-width='6' stroke-linecap='round' stroke-linejoin='round'/><path d='M80 50 H44 M80 50 L68 38 M80 50 L68 62' fill='none' stroke='#8E24AA' stroke-width='6' stroke-linecap='round' stroke-linejoin='round'/></svg>", // antonym (two opposite arrows)
  886: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#EDE7F6'/><path d='M20 70 Q20 30 46 30 H50 Q50 20 60 20 Q56 30 62 30 Q78 34 78 70Z' fill='#7E57C2'/><circle cx='40' cy='50' r='4' fill='#fff'/><circle cx='62' cy='50' r='4' fill='#fff'/><path d='M36 62 Q50 72 66 62' fill='none' stroke='#fff' stroke-width='3'/></svg>", // role (theatre mask)
  1051: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E1F5FE'/><path d='M14 66 H86' stroke='#0288D1' stroke-width='3'/><path d='M22 66 L46 40 L58 44 L50 66Z M58 44 L78 36 L72 52 L58 52Z' fill='#0288D1'/><path d='M66 24 L74 32' stroke='#0288D1' stroke-width='3' stroke-linecap='round' marker-end='url(#a)'/></svg>", // land
  1059: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E3F2FD'/><path d='M16 60 L42 36 L54 40 L48 60Z M54 40 L76 30 L68 48 L54 50Z' fill='#1565C0'/><path d='M26 74 L76 60' stroke='#1565C0' stroke-width='3' stroke-linecap='round'/></svg>", // take off
  1081: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E8F5E9'/><path d='M30 30 L70 70 M70 30 L30 70' stroke='#2E7D32' stroke-width='9' stroke-linecap='round'/></svg>", // times (multiply)
  1087: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFF3E0'/><path d='M26 50 H74' stroke='#EF6C00' stroke-width='9' stroke-linecap='round'/><circle cx='50' cy='30' r='7' fill='#EF6C00'/><circle cx='50' cy='70' r='7' fill='#EF6C00'/></svg>", // divide
  1380: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E0F2F1'/><path d='M22 76 V52 M42 76 V38 M62 76 V58 M82 76 V26' stroke='#00695C' stroke-width='9' stroke-linecap='round'/></svg>", // Economics (bar chart)
  1427: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#1A237E'/><path d='M62 24 A26 26 0 1 0 62 76 A32 32 0 1 1 62 24Z' fill='#FFF176'/><path d='M74 32 l3 7 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1Z' fill='#FFF176'/></svg>", // Eid (crescent + star)
  1772: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#E1F5FE'/><path d='M14 62 Q50 50 86 62' fill='none' stroke='#0288D1' stroke-width='4'/><circle cx='50' cy='66' r='11' fill='#78909C'/><path d='M44 44 l4 10 M56 44 l-4 10' stroke='#0288D1' stroke-width='3' stroke-linecap='round'/></svg>", // sink
  1775: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFEBEE'/><path d='M30 30 L46 54 L22 58Z' fill='#90A4AE'/><path d='M54 50 L74 32 L80 60 L58 70Z' fill='#90A4AE'/><path d='M44 52 L56 60' stroke='#E53935' stroke-width='3' stroke-linecap='round'/></svg>", // break
  1784: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFF9C4'/><circle cx='42' cy='58' r='20' fill='#FFCC80'/><path d='M34 54h0M50 54h0' stroke='#5D4037' stroke-width='5' stroke-linecap='round'/><path d='M66 24 L74 32 M74 24 L66 32 M70 16 V36' stroke='#F57F17' stroke-width='3' stroke-linecap='round'/></svg>", // awake (startled face + alarm lines)
  2156: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#ECEFF1'/><circle cx='50' cy='52' r='30' fill='#fff' stroke='#607D8B' stroke-width='5'/><path d='M50 32 V52 L66 60' stroke='#607D8B' stroke-width='5' stroke-linecap='round'/><circle cx='50' cy='16' r='5' fill='#607D8B'/></svg>", // kill time (clock)
  2291: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFEBEE'/><rect x='12' y='26' width='76' height='46' rx='6' fill='#D32F2F'/><rect x='18' y='48' width='64' height='4' fill='#fff'/><rect x='20' y='32' width='15' height='12' fill='#B3E5FC'/><rect x='42' y='32' width='15' height='12' fill='#B3E5FC'/><rect x='64' y='32' width='15' height='12' fill='#B3E5FC'/><circle cx='28' cy='78' r='8' fill='#212121'/><circle cx='72' cy='78' r='8' fill='#212121'/></svg>", // double-decker
  2316: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#F1F8E9'/><rect x='38' y='14' width='24' height='56' rx='6' fill='#9CCC65'/><rect x='40' y='60' width='20' height='22' rx='4' fill='#FFF59D'/><circle cx='50' cy='28' r='5' fill='#fff'/></svg>", // gluestick
  2697: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FFF3E0'/><ellipse cx='50' cy='64' rx='34' ry='14' fill='#FFCC80'/><path d='M38 36 V58 M50 30 V58 M62 36 V58' stroke='#6D4C41' stroke-width='5' stroke-linecap='round'/></svg>", // mash (fork marks in a bowl)
  2813: "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='#FCE4EC'/><circle cx='40' cy='50' r='17' fill='none' stroke='#EC407A' stroke-width='6'/><circle cx='60' cy='50' r='17' fill='none' stroke='#FFB300' stroke-width='6'/></svg>", // spouse (two rings)
};
function numberCard(text, bg, fg) {
  const size = text.length > 2 ? 32 : 44;
  return `<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='20' fill='${bg}'/><text x='50' y='${50 + size * 0.32}' font-family='Arial' font-weight='700' font-size='${size}' text-anchor='middle' fill='${fg}'>${text}</text><rect x='24' y='80' width='52' height='5' rx='2.5' fill='${fg}'/><circle cx='24' cy='20' r='4' fill='${fg}'/><circle cx='76' cy='20' r='4' fill='${fg}'/></svg>`;
}

// ── picture source for each id: 'self' (keep the OLD picture that already sat at this id), an old id number (copy that old
// word's picture), or omitted (use ICONS[id], a fresh hand-drawn one - must be in ICONS above). uzOverride/exampleUzOverride
// replace the Turkish book's own wording when it does not fit this exact word (a different part of speech, mostly). ──
const PLAN = {
  32: 'self', 82: 'self', 140: 'self', 216: 'self', 227: 'self', 612: 'self', 691: 'self', 693: 'self', 734: 'self',
  797: 'self', 908: 'self', 1314: 'self', 1523: 'self', 1519: 'self', 2192: 'self', 627: 'self',
  237: 627, 351: 886, 590: 1059, 592: 608, 620: 813, 692: 718, 525: 523, 606: 861, // 606 no longer needed (see below) but harmless if unused
  599: 2236, 861: 860, 2316: 1364, 2842: 2243, 2817: 2808, 2818: 2809, 2819: 2807, 1379: 559,
  1366: 271, 1368: 273, 1369: 274, 1370: 275, 1371: 276, 1372: 277, 1373: 278, 1374: 279, 1375: 280, 1376: 281, 1377: 282, 1378: 283, 1367: 272,
  271: 1366, 272: 1367, 273: 1368, 274: 1369, 275: 1370, 276: 1371, 277: 1372, 278: 1373, 279: 1374, 280: 1375, 281: 1376, 282: 1377, 283: 1378, 284: 1379, 285: 1380,
  // the rest (104,118,119,120,594,614,615,622,625,704,810,886,1051,1059,1081,1087,1380,1427,1772,1775,1784,2156,2291,2292,2697,2813)
  // have no plan entry: they fall through to ICONS, except 2292 which explicitly copies 2289 (a minibus - the closest existing vehicle).
  2292: 2289,
};
const UZ_OVERRIDE = { 1523: 'tizimga kirmoq', 1081: 'karra', 704: 'o‘rta' };

function svgFor(id) {
  if (PLAN[id] === 'self') return old[id]?.svg || null;
  if (typeof PLAN[id] === 'number') return old[PLAN[id]]?.svg || null;
  return ICONS[id] || null;
}

const missingIds = fs.readFileSync(process.argv[3], 'utf8').split('\n').filter(Boolean).map(l => +l.split('|')[0].trim());
const byBook = {};
const problems = [];
for (const id of missingIds) {
  const t = tr[id];
  const svg = svgFor(id);
  if (!svg) { problems.push(`id ${id}: no picture plan (add it to PLAN or ICONS)`); continue; }
  if (!t) { problems.push(`id ${id}: no Turkish row to take uz/example_uz from`); continue; }
  const book = Math.floor((id - 1) / 300) + 1;
  (byBook[book] ??= []).push({ id, uz: UZ_OVERRIDE[id] ?? t.uz, example_uz: t.example_uz, svg });
}
if (problems.length) { problems.forEach(p => console.error('PROBLEM: ' + p)); process.exit(1); }

for (const [book, rows] of Object.entries(byBook)) {
  const file = path.join(booksDir, `enrich-book-${book}-p1.json`);     // the name import-books.mjs's --enrich reader expects
  fs.writeFileSync(file, JSON.stringify(rows.sort((a, b) => a.id - b.id), null, 1) + '\n');
  console.log(`book ${book}: ${rows.length} words enriched -> ${path.basename(file)}`);
}
