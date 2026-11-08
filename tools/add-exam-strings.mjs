// Adds the strings of the two kinds of exam (typing / test) and of "the exam goes on after F5" to the website resources (en, ru, uz).
// Run once:  node tools/add-exam-strings.mjs   (keys that exist are skipped)
import fs from 'node:fs';
import path from 'node:path';

const dir = path.resolve(import.meta.dirname, '../JumboWeb/JumboWeb/Resources');
const rows = [
  // key, en, ru, uz
  ['Exam_KindTitle', 'Kind of exam', 'Вид экзамена', 'Imtihon turi'],
  ['Exam_KindType', 'Typing', 'Ввод перевода', 'Tarjimani yozish'],
  ['Exam_KindTypeHint', 'Type the word for the translation you see.', 'Печатаешь слово по переводу.', "Tarjima bo'yicha so'zni yozasiz."],
  ['Exam_KindTest', 'Test', 'Тест', 'Test'],
  ['Exam_KindTestHint', 'Choose the right word out of four.', 'Выбираешь правильное слово из четырёх.', "To'rtta so'zdan to'g'risini tanlaysiz."],
  ['ExamQuiz_TestHint', 'Choose the right word', 'Выбери правильное слово', "To'g'ri so'zni tanlang"],
  ['ExamQuiz_Resumed', 'The exam goes on from where you stopped.', 'Экзамен продолжается с того места, где ты остановился.', "Imtihon to'xtagan joyingizdan davom etadi."],
];
const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
for (const [file, col] of [['AppResources.resx', 1], ['AppResources.ru.resx', 2], ['AppResources.uz.resx', 3]]) {
  const p = path.join(dir, file);
  let s = fs.readFileSync(p, 'utf8');
  const crlf = s.includes('\r\n');
  if (crlf) s = s.replace(/\r\n/g, '\n');
  let add = '';
  for (const r of rows) {
    if (s.includes(`<data name="${r[0]}"`)) continue;
    add += `  <data name="${r[0]}" xml:space="preserve">\n    <value>${esc(r[col])}</value>\n  </data>\n`;
  }
  s = s.replace(/<\/root>\s*$/, add + '</root>\n');
  fs.writeFileSync(p, crlf ? s.replace(/\n/g, '\r\n') : s);
  console.log(file, 'ok');
}
