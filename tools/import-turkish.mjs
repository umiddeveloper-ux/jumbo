// Turns the Turkish book files (SeedData/Books/Turk/*_JUMBO_<N>-kitob_TR-UZ.docx: topics with Turkish words, Uzbek translations and
// example sentences) into the files the API seeds the Turkish study language from: SeedData/Books/Turk/book_NN_tr.json.
//
// The Turkish edition is NOT a copy of the English one: ~2970 of the 3000 words mean the same as the English word of the same
// number (or of another number: see CONCEPT), so a Turkish row shares the picture and the translations (ru / en / uz) of that English
// word ("concept"). 29 words have no English counterpart (NEW): they get a placeholder picture and their own translations,
// written here (ru / en; the Uzbek is the book's, converted to the Uzbek Latin alphabet of the app).
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

// ───────────── the Turkish edition vs the English one ─────────────
// Turkish word number -> number of the English word that means the same (default: the same number).
const CONCEPT = new Map();
// Numbers 1-12: the Turkish book has no "zero", so the numbers start one place earlier.
for (let i = 106; i <= 117; i++) CONCEPT.set(i, i + 1);
// The Turkish book swapped two topics of the English one: numbers 16 ... million (Turkish book 1) and school subjects (Turkish book 5).
for (let i = 271; i <= 285; i++) CONCEPT.set(i, i + 1095);          // 271 -> 1366 (sixteen) ... 285 -> 1380 (million)
for (let i = 1366; i <= 1378; i++) CONCEPT.set(i, i - 1095);        // 1366 -> 271 (favourite) ... 1378 -> 283 (Literature)
// Same meaning as an English word that stands somewhere else.
for (const [tr, en] of Object.entries({
  237: 627,    // raf = shelf
  525: 523,    // yemek yapmak (cook) = prepare
  590: 1059,   // pasaport = passport
  592: 608,    // kart = card
  606: 861,    // fis = receipt
  620: 813,    // saksi bitkisi (house plant) = plant
  692: 718,    // not (mark) = grade
  778: 625,    // giris = entrance
  1784: 361,   // uyandirmak = wake up
  2316: 1364,  // stick yapistirici (glue stick) = glue
  2842: 2243,  // sarj etmek (charge the phone) = charge
})) CONCEPT.set(+tr, en);

// Words that have no English counterpart: their own translations (en / ru + the example sentence in en / ru); the Uzbek is the book's.
const NEW = {
  118: ['thirteen', 'тринадцать', 'My brother is thirteen years old.', 'Моему брату тринадцать лет.'],
  119: ['fourteen', 'четырнадцать', 'I have fourteen pencils in my bag.', 'У меня в сумке четырнадцать карандашей.'],
  120: ['fifteen', 'пятнадцать', 'I can count to fifteen.', 'Я умею считать до пятнадцати.'],
  594: ['safe', 'сейф', 'Keep your passport in the safe.', 'Храните паспорт в сейфе.'],
  599: ['Wi-Fi password', 'пароль от Wi-Fi', 'Can you tell me the Wi-Fi password?', 'Вы можете сказать мне пароль от Wi-Fi?'],
  612: ['cashier', 'кассир', 'The cashier is very kind.', 'Кассир очень добрый.'],
  614: ['shopping trolley', 'тележка для покупок', 'The shopping trolley is full.', 'Тележка для покупок полная.'],
  615: ['basket', 'корзина', 'The shopping basket is heavy.', 'Корзина для покупок тяжёлая.'],
  622: ['fireplace', 'камин', 'The fireplace is warm.', 'Камин тёплый.'],
  625: ['chandelier', 'люстра', 'A beautiful chandelier hangs in the room.', 'В комнате висит красивая люстра.'],
  704: ['middle school', 'средняя школа', 'Children start middle school at the age of eleven.', 'Дети идут в среднюю школу в одиннадцать лет.'],
  810: ['antonym', 'антоним', '"Cold" is the antonym of "hot".', '«Холодный» — антоним слова «горячий».'],
  861: ['dollar', 'доллар', 'This product costs only one dollar.', 'Этот товар стоит всего один доллар.'],
  886: ['role', 'роль', 'The actor plays an important role in the film.', 'Актёр играет в фильме важную роль.'],
  1051: ['land (plane)', 'приземляться', 'The plane is landing.', 'Самолёт приземляется.'],
  1059: ['take off (plane)', 'взлетать', 'The big plane takes off at five o\'clock.', 'Большой самолёт взлетает в пять часов.'],
  1081: ['multiply', 'умножить', 'Five times two is ten.', 'Пять умножить на два будет десять.'],
  1087: ['divide', 'делить', 'If you divide ten by two, you get five.', 'Если разделить десять на два, получится пять.'],
  1379: ['Russian (language)', 'русский язык', 'My grandmother learned Russian at school.', 'Моя бабушка учила русский язык в школе.'],
  1380: ['economics', 'экономика', 'Anna wanted to study economics.', 'Анна хотела изучать экономику.'],
  1772: ['sink', 'тонуть', 'The heavy stone sank to the bottom of the pool.', 'Тяжёлый камень утонул на дне бассейна.'],
  1775: ['break', 'ломать, разбивать', 'I broke my favourite cup.', 'Я разбил свою любимую чашку.'],
  2291: ['double-decker bus', 'двухэтажный автобус', 'London is famous for its red double-decker buses.', 'Лондон известен своими красными двухэтажными автобусами.'],
  2292: ['shuttle bus', 'автобус-шаттл', 'The airport provides a free shuttle bus.', 'Аэропорт предоставляет бесплатный автобус-шаттл.'],
  2697: ['mash', 'разминать, толочь', 'If you mash the bananas well, the cake will be softer.', 'Если хорошо размять бананы, пирог получится мягче.'],
  2813: ['spouse', 'супруг, супруга', 'He mentioned that his spouse works as a teacher.', 'Он упомянул, что его супруга работает учителем.'],
  2817: ['fiancée', 'невеста', 'He told us his fiancée likes classical music.', 'Он сказал нам, что его невеста любит классическую музыку.'],
  2818: ['fiancé', 'жених', 'She said her fiancé had bought a new house.', 'Она сказала, что её жених купил новый дом.'],
  2819: ['engagement', 'помолвка', 'She told me she had announced her engagement yesterday.', 'Она сказала мне, что вчера объявила о помолвке.'],
  1427: ['holiday (bayram)', 'праздник (байрам)', 'At the holiday people give each other gifts.', 'В праздник люди дарят друг другу подарки.'],
  2156: ['kill time', 'убивать время', 'Killing time at the airport is very boring.', 'Убивать время в аэропорту очень скучно.'],
};

// Words whose Turkish sentence is not a translation of the English sentence of their concept (the concept is another word, or
// the sentence is about something else): the translations of the sentence (en / ru) are written here; the word itself (en / ru / uz)
// is the concept's, the Uzbek sentence is the book's. Without this the "Build the phrase" game would show a hint that does not fit.
const OWN = {
  106: ['I have an older brother.', 'У меня есть старший брат.'],
  107: ['He has two children.', 'У него двое детей.'],
  108: ['Anna has three apples.', 'У Анны три яблока.'],
  109: ['Our house has four windows.', 'В нашем доме четыре окна.'],
  110: ['We have five lessons.', 'У нас пять уроков.'],
  111: ['She is six years old.', 'Ей шесть лет.'],
  112: ['There are seven days in a week.', 'В неделе семь дней.'],
  113: ['My sister is eight years old.', 'Моей сестре восемь лет.'],
  114: ['Her son is nine years old.', 'Её сыну девять лет.'],
  115: ['Kate has ten books.', 'У Кейт десять книг.'],
  116: ['This child is eleven years old.', 'Этому ребёнку одиннадцать лет.'],
  117: ['There are twelve months in a year.', 'В году двенадцать месяцев.'],
  237: ['My shampoo is on the shelf.', 'Мой шампунь на полке.'],
  271: ['My sister is sixteen years old.', 'Моей сестре шестнадцать лет.'],
  272: ['Tom is seventeen years old.', 'Тому семнадцать лет.'],
  273: ['Is your brother eighteen years old?', 'Твоему брату восемнадцать лет?'],
  274: ['They are not nineteen years old.', 'Им не девятнадцать лет.'],
  275: ['Can you count to twenty?', 'Ты умеешь считать до двадцати?'],
  276: ['There are thirty students in our class.', 'В нашем классе тридцать учеников.'],
  277: ['My father is forty years old.', 'Моему отцу сорок лет.'],
  278: ['This book has fifty pages.', 'В этой книге пятьдесят страниц.'],
  279: ['Our grandfather is sixty years old.', 'Нашему дедушке шестьдесят лет.'],
  280: ['Seventy people work here.', 'Здесь работает семьдесят человек.'],
  281: ['Her grandmother is eighty years old.', 'Её бабушке восемьдесят лет.'],
  282: ['The film is ninety minutes long.', 'Фильм идёт девяносто минут.'],
  283: ['I can count to a hundred.', 'Я умею считать до ста.'],
  284: ['A thousand people live in our village.', 'В нашей деревне живёт тысяча человек.'],
  285: ["My father doesn't have a million dollars.", 'У моего отца нет миллиона долларов.'],
  525: ['I cook dinner every day.', 'Я каждый день готовлю ужин.'],
  590: ['This is my passport.', 'Это мой паспорт.'],
  592: ['Could you give me two key cards, please?', 'Не могли бы вы дать мне две ключ-карты?'],
  606: ["Don't forget to take the receipt.", 'Не забудь взять чек.'],
  620: ['I have a green house plant by the window.', 'У окна у меня стоит зелёное комнатное растение.'],
  692: ['The teacher gives me a good mark.', 'Учитель ставит мне хорошую оценку.'],
  778: ['There is a beautiful green door at the back entrance of my house.', 'У заднего входа в мой дом красивая зелёная дверь.'],
  1366: ['Music was my favourite subject at school.', 'Музыка была моим любимым предметом в школе.'],
  1367: ['We had a maths lesson yesterday.', 'Вчера у нас был урок математики.'],
  1368: ['We will have a science lesson tomorrow.', 'Завтра у нас будет урок естествознания.'],
  1369: ['Tom studied history last year.', 'Том в прошлом году изучал историю.'],
  1370: ['Will we have a geography lesson on Tuesday?', 'У нас во вторник будет урок географии?'],
  1371: ['Her art lesson was very long.', 'Её урок рисования был очень долгим.'],
  1372: ['The children played music at the concert.', 'Дети играли музыку на концерте.'],
  1373: ['He will study biology at university.', 'Он будет изучать биологию в университете.'],
  1374: ["Sam didn't like chemistry at school.", 'Сэму в школе не нравилась химия.'],
  1375: ['Did you pass the physics exam?', 'Ты сдал экзамен по физике?'],
  1376: ['He will teach computer science next year.', 'В следующем году он будет преподавать информатику.'],
  1377: ['Kate learned English in London.', 'Кейт выучила английский в Лондоне.'],
  1378: ['We will read a novel in the literature lesson.', 'На уроке литературы мы будем читать роман.'],
  1523: ['Please log in to your account.', 'Пожалуйста, войдите в свою учётную запись.'],
  1784: ['The loud noise woke the baby.', 'Громкий шум разбудил ребёнка.'],
  2316: ['Please close the glue stick after use, or it will dry out quickly.', 'Пожалуйста, закрывайте клей-карандаш после использования, иначе он быстро высохнет.'],
  2842: ['He said he needed to charge his phone.', 'Он сказал, что ему нужно зарядить телефон.'],
};
// Uzbek of a new word where the book's own wording is too short to be a dictionary entry
const NEW_UZ = { 704: "o‘rta maktab" };
for (const id of Object.keys(NEW)) CONCEPT.set(+id, null);

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
  english.set(j.metadata.book, { level: j.metadata.level, subjects: [...new Set(j.words.map(w => w.subject))], ids: new Set(j.words.map(w => w.id)), words: j.words });
}
const allEnglishIds = new Set([...english.values()].flatMap(b => [...b.ids]));

// A plain card for a word that has no picture yet (the colour of the level of the book; replaced when the real picture is made)
const LEVEL_COLOUR = { A1: '#ed7476', A2: '#f2c14e', B1: '#6fba74', B2: '#9b8bf0' };
const escXml = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/'/g, '&apos;');
const placeholderSvg = (text, level) => {
  const size = text.length <= 6 ? 22 : text.length <= 10 ? 15 : 10;
  return `<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect width='100' height='100' rx='18' fill='${LEVEL_COLOUR[level] ?? '#ed7476'}'/>` +
    `<text x='50' y='56' text-anchor='middle' font-family='sans-serif' font-weight='bold' font-size='${size}' fill='#fff'>${escXml(text)}</text></svg>`;
};

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
      const concept = CONCEPT.has(w.id) ? CONCEPT.get(w.id) : w.id;
      const row = { id: w.id, topic: ti + 1, tr: w.tr, uz: NEW_UZ[w.id] ?? w.uz, example_tr: w.example_tr, example_uz: w.example_uz, concept };
      if (concept === null) {
        const [wordEn, wordRu, exEn, exRu] = NEW[w.id];
        row.new = { en: wordEn, ru: wordRu, example_en: exEn, example_ru: exRu, svg: placeholderSvg(w.tr.replace(/ \(.*\)$/, '').split(' – ')[0], en.level) };
      } else {
        if (!allEnglishIds.has(concept)) problems.push(`book ${book}, word ${w.id}: the English word ${concept} does not exist`);
        if (OWN[w.id]) row.own = { example_en: OWN[w.id][0], example_ru: OWN[w.id][1] };
        else if (concept !== w.id) problems.push(`book ${book}, word ${w.id}: linked to ${concept} but has no own sentence translation`);
      }
      for (const f of ['tr', 'uz', 'example_tr', 'example_uz']) if (!row[f]) problems.push(`book ${book}, word ${w.id}: no ${f}`);
      words.push(row);
    }
  });
  const ids = words.map(w => w.id);
  if (new Set(ids).size !== ids.length) problems.push(`book ${book}: duplicate word numbers`);
  const expected = [...en.ids].sort((a, b) => a - b);
  if (expected.join() !== [...ids].sort((a, b) => a - b).join()) problems.push(`book ${book}: the word numbers differ from the English book`);

  results.push({
    book, name: `book_${String(book).padStart(2, '0')}_tr.json`,
    json: {
      metadata: { book, level: en.level, language: 'tr', word_count: words.length, id_range: [Math.min(...ids), Math.max(...ids)], new_words: words.filter(w => w.new).length,
        schema: ['id', 'topic', 'tr', 'uz', 'example_tr', 'example_uz', 'concept', 'new'] },
      topics: topics.map(t => ({ tr: t.tr, uz: t.uz })),
      words,
    },
  });
}

if (problems.length) { problems.forEach(p => console.error('PROBLEM: ' + p)); console.error('\nNothing written.'); process.exit(1); }

fs.mkdirSync(outDir, { recursive: true });
for (const r of results.sort((a, b) => a.book - b.book)) {
  fs.writeFileSync(path.join(outDir, r.name), JSON.stringify(r.json, null, 1) + '\n');
  console.log(`${r.name}: ${r.json.words.length} words, ${r.json.topics.length} topics, ${r.json.metadata.new_words} new`);
}
