// Copies the website's UI strings that the mobile app also needs into the MAUI resource files
// (en / ru / uz), so both clients speak the same words. Existing mobile keys are never overwritten.
// Run:  node tools/sync-mobile-strings.mjs
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const webDir = path.join(root, 'JumboWeb/JumboWeb/Resources');
const appDir = path.join(root, 'Jumbo/Resources/Strings');

// Website-only areas stay out of the app.
const EXCLUDE = /^(TJ|VD|PP|PA|TC|RP|Land|FS|Admin|Rating|Top|Exam|ExamResults|ExamQuiz|SiteHome|SiteLayout|NotFound|Nav|Role|Dash|TestLevel|TestMyLevel|TG|TS|TE|Strip)(_|$)/;

// Strings that exist only in the app: [key, ru, en, uz]
const EXTRA = [
  ['Theme_Light', 'Светлая', 'Light', 'Yorug‘'],
  ['Theme_Dark', 'Тёмная', 'Dark', "Qorong'i"],
  ['Theme_System', 'Как в телефоне', 'Like the phone', 'Telefondagidek'],
  ['Settings_Logout', 'Выйти из аккаунта', 'Log out', 'Hisobdan chiqish'],
  ['Settings_LogoutConfirm', 'Выйти из аккаунта?', 'Log out of the account?', 'Hisobdan chiqasizmi?'],
  ['Settings_Appearance', 'Оформление', 'Appearance', "Ko'rinish"],
  ['Settings_Yes', 'Да', 'Yes', 'Ha'],
  ['Settings_AvatarSaved', 'Аватар сохранён', 'Avatar saved', 'Avatar saqlandi'],
  ['Settings_AutoSaved', 'Сохраняется автоматически', 'Saved automatically', 'Avtomatik saqlanadi'],
  ['Game_AllFound', 'Все слова найдены!', 'All the words found!', "Barcha so'zlar topildi!"],
  ['Game_Locked', 'Жизни и LP закончились!', 'Out of lives and LP!', 'Jonlar va LP tugadi!'],
  ['Game_LockedRegen', 'Жизни восстановятся через {0}', 'Lives come back in {0}', 'Jonlar {0} dan keyin tiklanadi'],
  ['Game_Correct', 'Правильно!', 'Correct!', "To'g'ri!"],
  ['Game_WrongOrder', 'Не тот порядок!', 'Wrong order!', "Tartib noto'g'ri!"],
  ['Game_RegenTime', '{0}ч {1:D2}м', '{0}h {1:D2}m', '{0}s {1:D2}d'],
  ['Game_Recovering', 'Восстанавливается...', 'Recovering...', 'Tiklanmoqda...'],
  [`Letter_Study`, `Изучаем букву «{0}»`, `Learning the letter “{0}”`, `«{0}» harfini o'rganamiz`],
  [`Letter_WordsTitle`, `Слова на эту букву:`, `Words for this letter:`, `Bu harfdagi so'zlar:`],
  [`Letter_TestTitle`, `Тестовый вариант`, `Multiple choice`, `Test varianti`],
  [`Letter_TestDesc`, `Выбери правильный перевод из 4 вариантов ответа`, `Choose the right translation out of 4 answers`, `4 ta javobdan to'g'ri tarjimani tanlang`],
  [`Letter_RecallTitle`, `Вспомни сам`, `Recall it yourself`, `O'zing eslab ko'r`],
  [`Letter_RecallDesc`, `Посмотри на картинку и постарайся вспомнить слово самостоятельно`, `Look at the picture and try to recall the word yourself`, `Rasmga qarab, so'zni o'zing eslashga harakat qil`],
  [`Letter_Heading`, `Буква «{0}»`, `Letter “{0}”`, `«{0}» harfi`],
  [`Letter_ShowWord`, `👁  Показать слово`, `👁  Show the word`, `👁  So'zni ko'rsatish`],
  [`Letter_DidRecall`, `Вы правильно вспомнили слово?`, `Did you recall the word correctly?`, `So'zni to'g'ri esladingizmi?`],
  [`Letter_Recalled`, `✓  Вспомнил!`, `✓  I remembered!`, `✓  Esladim!`],
  [`Letter_NotRecalled`, `✗  Не вспомнил`, `✗  I did not`, `✗  Eslay olmadim`],
  [`Letter_Repeat`, `Повторить`, `Repeat`, `Takrorlash`],
  [`Letter_NoWords`, `Слов на букву «{0}» пока нет`, `No words for the letter “{0}” yet`, `«{0}» harfiga so'zlar hozircha yo'q`],
  [`Letter_FindTranslation`, `Найди правильный перевод:`, `Find the right translation:`, `To'g'ri tarjimani toping:`],
  [`Letter_Learned`, `Буква «{0}» изучена!`, `The letter “{0}” is learned!`, `«{0}» harfi o‘rganildi!`],
  [`Letter_Again`, `Ещё раз`, `Once more`, `Yana bir bor`],
  [`Letter_RecalledCount`, `Вы вспомнили {0} из {1} слов!`, `You remembered {0} of {1} words!`, `Siz {1} ta so'zdan {0} tasini esladingiz!`],
  [`Awards_Balance`, `Баланс: {0} LP`, `Balance: {0} LP`, `Balans: {0} LP`],
  [`Awards_Bought`, `Успешно куплено: {0}!`, `Bought: {0}!`, `Sotib olindi: {0}!`],
  [`Scan_Close`, `✕ Закрыть`, `✕ Close`, `✕ Yopish`],
  [`Game_Round`, `Раунд {0}`, `Round {0}`, `{0}-raund`],
  [`Game_Audience`, `Зал`, `Crowd`, `Zal`],
  [`Game_AudienceHelp`, `👥 Помощь зала`, `👥 Ask the crowd`, `👥 Zal yordami`],
  [`Game_Step`, `Ступень {0}`, `Step {0}`, `{0}-zina`],
  [`Subject_Watch`, `Смотреть на YouTube`, `Watch on YouTube`, `YouTube’da ko‘rish`],
  [`Subject_NoVideo`, `Видео не добавлено`, `No video yet`, `Video qo'shilmagan`],
  ['Settings_ScanQrHint', 'Войти на сайте по QR-коду', 'Sign in on the website with a QR code', 'Saytga QR-kod orqali kirish'],
];
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const blocks = (xml) => {
  const out = new Map();
  for (const m of xml.matchAll(/  <data name="([^"]+)"[^>]*>[\s\S]*?<\/data>\r?\n/g)) out.set(m[1], m[0].replace(/\r\n/g, '\n'));
  return out;
};

for (const f of ['AppResources.resx', 'AppResources.ru.resx', 'AppResources.uz.resx']) {
  const web = blocks(fs.readFileSync(path.join(webDir, f), 'utf8'));
  const appPath = path.join(appDir, f);
  let app = fs.readFileSync(appPath, 'utf8');
  const crlf = app.includes('\r\n'); if (crlf) app = app.replace(/\r\n/g, '\n');
  const have = new Set(blocks(app).keys());
  let add = '', n = 0;
  for (const [key, block] of web) {
    if (have.has(key) || EXCLUDE.test(key)) continue;
    add += block; n++;
  }
  const col = f === 'AppResources.resx' ? 2 : f === 'AppResources.ru.resx' ? 1 : 3;
  for (const row of EXTRA) {
    if (have.has(row[0])) continue;
    add += `  <data name="${row[0]}" xml:space="preserve">\n    <value>${esc(row[col])}</value>\n  </data>\n`; n++;
  }
  app = app.replace(/<\/root>\s*$/, add + '</root>\n');
  fs.writeFileSync(appPath, crlf ? app.replace(/\n/g, '\r\n') : app);
  console.log(f, 'added', n);
}
