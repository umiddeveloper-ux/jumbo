// Copies the website's UI strings that the mobile app also needs into the MAUI resource files
// (en / ru / uz / tr), so all clients speak the same words. Existing mobile keys are never overwritten.
// Run:  node tools/sync-mobile-strings.mjs
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const webDir = path.join(root, 'JumboWeb/JumboWeb/Resources');
const appDir = path.join(root, 'Jumbo/Resources/Strings');

// Website-only areas stay out of the app.
const EXCLUDE = /^(Placement|PQ|TJ|VD|PP|PA|TC|RP|Land|FS|Admin|Dl|Foot|Top|Exam|ExamResults|ExamQuiz|SiteHome|SiteLayout|NotFound|Nav|Role|Dash|TestLevel|TestMyLevel|TG|TS|TE|Strip|Tq|PM)(_|$)/;

// ...but the rating page of the app uses the rating strings, except the teacher's and the landing ones.
const RATING_SKIP = /^Rating_(Teacher|SeeAll|MiniTitle)/;

// Strings that exist only in the app: [key, ru, en, uz, tr]
const EXTRA = [
  ['Theme_Light', 'Светлая', 'Light', 'Yorug‘', 'Açık'],
  ['Theme_Dark', 'Тёмная', 'Dark', "Qorong'i", 'Koyu'],
  ['Theme_System', 'Как в телефоне', 'Like the phone', 'Telefondagidek', 'Telefondaki gibi'],
  ['Settings_Logout', 'Выйти из аккаунта', 'Log out', 'Hisobdan chiqish', 'Hesaptan çıkış yap'],
  ['Settings_LogoutConfirm', 'Выйти из аккаунта?', 'Log out of the account?', 'Hisobdan chiqasizmi?', 'Hesaptan çıkmak istiyor musun?'],
  ['Settings_Appearance', 'Оформление', 'Appearance', "Ko'rinish", 'Görünüm'],
  ['Settings_Yes', 'Да', 'Yes', 'Ha', 'Evet'],
  ['Settings_AvatarSaved', 'Аватар сохранён', 'Avatar saved', 'Avatar saqlandi', 'Avatar kaydedildi'],
  ['Settings_AutoSaved', 'Сохраняется автоматически', 'Saved automatically', 'Avtomatik saqlanadi', 'Otomatik kaydedilir'],
  ['Game_AllFound', 'Все слова найдены!', 'All the words found!', "Barcha so'zlar topildi!", 'Tüm kelimeler bulundu!'],
  ['Game_Locked', 'Жизни и JO закончились!', 'Out of lives and JO!', 'Jonlar va JO tugadi!', 'Canlar ve JO bitti!'],
  ['Game_LockedRegen', 'Жизни восстановятся через {0}', 'Lives come back in {0}', 'Jonlar {0} dan keyin tiklanadi', 'Canlar {0} sonra geri gelir'],
  ['Game_Correct', 'Правильно!', 'Correct!', "To'g'ri!", 'Doğru!'],
  ['Game_WrongOrder', 'Не тот порядок!', 'Wrong order!', "Tartib noto'g'ri!", 'Sıra yanlış!'],
  ['Game_RegenTime', '{0}ч {1:D2}м', '{0}h {1:D2}m', '{0}s {1:D2}d', '{0}sa {1:D2}dk'],
  ['Game_Recovering', 'Восстанавливается...', 'Recovering...', 'Tiklanmoqda...', 'Yenileniyor...'],
  [`Letter_Study`, `Изучаем букву «{0}»`, `Learning the letter “{0}”`, `«{0}» harfini o'rganamiz`, `"{0}" harfini öğreniyoruz`],
  [`Letter_WordsTitle`, `Слова на эту букву:`, `Words for this letter:`, `Bu harfdagi so'zlar:`, `Bu harfle başlayan kelimeler:`],
  [`Letter_TestTitle`, `Тестовый вариант`, `Multiple choice`, `Test varianti`, `Çoktan seçmeli`],
  [`Letter_TestDesc`, `Выбери правильный перевод из 4 вариантов ответа`, `Choose the right translation out of 4 answers`, `4 ta javobdan to'g'ri tarjimani tanlang`, `4 cevaptan doğru çeviriyi seç`],
  [`Letter_RecallTitle`, `Вспомни сам`, `Recall it yourself`, `O'zing eslab ko'r`, `Kendin hatırla`],
  [`Letter_RecallDesc`, `Посмотри на картинку и постарайся вспомнить слово самостоятельно`, `Look at the picture and try to recall the word yourself`, `Rasmga qarab, so'zni o'zing eslashga harakat qil`, `Resme bak ve kelimeyi kendin hatırlamaya çalış`],
  [`Letter_Heading`, `Буква «{0}»`, `Letter “{0}”`, `«{0}» harfi`, `"{0}" harfi`],
  [`Letter_ShowWord`, `👁  Показать слово`, `👁  Show the word`, `👁  So'zni ko'rsatish`, `👁  Kelimeyi göster`],
  [`Letter_DidRecall`, `Вы правильно вспомнили слово?`, `Did you recall the word correctly?`, `So'zni to'g'ri esladingizmi?`, `Kelimeyi doğru hatırladın mı?`],
  [`Letter_Recalled`, `✓  Вспомнил!`, `✓  I remembered!`, `✓  Esladim!`, `✓  Hatırladım!`],
  [`Letter_NotRecalled`, `✗  Не вспомнил`, `✗  I did not`, `✗  Eslay olmadim`, `✗  Hatırlayamadım`],
  [`Letter_Repeat`, `Повторить`, `Repeat`, `Takrorlash`, `Tekrarla`],
  [`Letter_NoWords`, `Слов на букву «{0}» пока нет`, `No words for the letter “{0}” yet`, `«{0}» harfiga so'zlar hozircha yo'q`, `"{0}" harfi için henüz kelime yok`],
  [`Letter_FindTranslation`, `Найди правильный перевод:`, `Find the right translation:`, `To'g'ri tarjimani toping:`, `Doğru çeviriyi bul:`],
  [`Letter_Learned`, `Буква «{0}» изучена!`, `The letter “{0}” is learned!`, `«{0}» harfi o‘rganildi!`, `"{0}" harfi öğrenildi!`],
  [`Letter_Again`, `Ещё раз`, `Once more`, `Yana bir bor`, `Tekrar`],
  [`Letter_RecalledCount`, `Вы вспомнили {0} из {1} слов!`, `You remembered {0} of {1} words!`, `Siz {1} ta so'zdan {0} tasini esladingiz!`, `{1} kelimeden {0} tanesini hatırladın!`],
  [`Awards_Balance`, `Баланс: {0} JO`, `Balance: {0} JO`, `Balans: {0} JO`, `Bakiye: {0} JO`],
  [`Awards_Bought`, `Успешно куплено: {0}!`, `Bought: {0}!`, `Sotib olindi: {0}!`, `Satın alındı: {0}!`],
  [`Scan_Close`, `✕ Закрыть`, `✕ Close`, `✕ Yopish`, `✕ Kapat`],
  [`Game_Round`, `Раунд {0}`, `Round {0}`, `{0}-raund`, `Tur {0}`],
  [`Game_Audience`, `Зал`, `Crowd`, `Zal`, `Seyirciler`],
  [`Game_AudienceHelp`, `👥 Помощь зала`, `👥 Ask the crowd`, `👥 Zal yordami`, `👥 Seyirciden yardım`],
  [`Game_Step`, `Ступень {0}`, `Step {0}`, `{0}-zina`, `Basamak {0}`],
  [`Subject_Watch`, `Смотреть на YouTube`, `Watch on YouTube`, `YouTube’da ko‘rish`, `YouTube'da izle`],
  [`Subject_NoVideo`, `Видео не добавлено`, `No video yet`, `Video qo'shilmagan`, 'Henüz video eklenmedi'],
  ['Settings_ScanQrHint', 'Войти на сайте по QR-коду', 'Sign in on the website with a QR code', 'Saytga QR-kod orqali kirish', 'Siteye QR kodla giriş yap'],
  // the app's own login/QR/footer strings (the website uses different key names for the same ideas, so these never match a web key)
  ['Username', 'Логин', 'Username', 'Login', 'Kullanıcı adı'],
  ['Password', 'Пароль', 'Password', 'Parol', 'Şifre'],
  ['ScanQrButton', 'Войти на сайте по QR', 'Sign in on website via QR', 'Saytga QR orqali kirish', 'Siteye QR ile giriş yap'],
  ['ScanQrTitle', 'Сканировать QR-код', 'Scan QR code', 'QR-kodni skanerlash', 'QR kodu tara'],
  ['ScanQrInstructions', 'Наведите камеру на QR-код, показанный на сайте', 'Point the camera at the QR code shown on the website', 'Kamerani saytdagi QR-kodga qarating', 'Kamerayı sitede gösterilen QR koda doğrult'],
  ['QrConfirmedMessage', 'Вход на сайте подтверждён', 'Website sign-in confirmed', 'Saytga kirish tasdiqlandi', 'Sitede giriş onaylandı'],
  ['LoginTitle', 'Вход', 'Sign in', 'Kirish', 'Giriş yap'],
  ['LoginWelcomeBack', 'С возвращением', 'Welcome back', 'Xush kelibsiz', 'Tekrar hoş geldin'],
  ['LoginButton', 'Войти', 'Sign in', 'Kirish', 'Giriş yap'],
  ['NoAccountLink', 'Нет аккаунта? Зарегистрироваться', "Don't have an account? Register", "Akkountingiz yo'qmi? Ro'yxatdan o'tish", 'Hesabın yok mu? Kayıt ol'],
  ['HaveAccountLink', 'Уже есть аккаунт? Войти', 'Already have an account? Sign in', 'Akkountingiz bormi? Kirish', 'Zaten hesabın var mı? Giriş yap'],
  ['InvalidCredentialsError', 'Неверный логин или пароль', 'Invalid username or password', "Login yoki parol noto'g'ri", 'Kullanıcı adı veya şifre hatalı'],
  ['Foot_Tagline', 'Учим английский играя: слова, игры и слонёнок Jumbo.', 'Learn English by playing: words, games and Jumbo the elephant.', "O'yinda ingliz tili: so'zlar, o'yinlar va fil Jumbo.", 'Oynayarak İngilizce öğren: kelimeler, oyunlar ve fil Jumbo.'],
  ['Foot_Get', 'Скачать приложение', 'Get the app', 'Ilovani yuklab olish', 'Uygulamayı indir'],
  ['Foot_Android', 'Android', 'Android', 'Android', 'Android'],
  ['Foot_Ios', 'iOS · App Store', 'iOS · App Store', 'iOS · App Store', 'iOS · App Store'],
  ['Foot_Windows', 'Windows', 'Windows', 'Windows', 'Windows'],
  ['Foot_Mac', 'macOS', 'macOS', 'macOS', 'macOS'],
  ['Foot_Site', 'Сайт', 'Site', 'Sayt', 'Site'],
  ['Foot_Subscribe', 'Обратная связь и новости', 'Feedback and news', 'Aloqa va yangiliklar', 'Geri bildirim ve haberler'],
  ['Foot_SubscribeHint', 'Напишите нам или оставьте почту: мы ответим и будем присылать только новости Jumbo.', 'Write to us or leave your e-mail: we answer and send only news of Jumbo.', "Bizga yozing yoki pochtangizni qoldiring: javob beramiz va faqat Jumbo yangiliklarini yuboramiz.", 'Bize yazın veya e-postanızı bırakın: yanıtlarız ve sadece Jumbo haberlerini göndeririz.'],
  ['Foot_EmailPlaceholder', 'Ваша почта', 'Your e-mail', 'Pochtangiz', 'E-posta adresiniz'],
  ['Foot_SubscribeButton', 'Отправить', 'Send', 'Yuborish', 'Gönder'],
  ['Foot_Sending', 'Отправляем…', 'Sending…', 'Yuborilmoqda…', 'Gönderiliyor…'],
  ['Foot_Subscribed', 'Спасибо! Мы всё получили.', 'Thank you! We got it.', 'Rahmat! Hammasini oldik.', 'Teşekkürler! Aldık.'],
  ['Foot_BadEmail', 'Проверьте адрес почты.', 'Please check the e-mail address.', 'Pochta manzilini tekshiring.', 'Lütfen e-posta adresini kontrol edin.'],
  ['Foot_Failed', 'Не удалось подписаться. Попробуйте позже.', 'Could not subscribe now. Please try again later.', "Hozir obuna bo'lib bo'lmadi. Keyinroq urinib ko'ring.", 'Şu an abone olunamadı. Lütfen daha sonra tekrar deneyin.'],
  ['Foot_Consent', 'Подписываясь, вы соглашаетесь с', 'By subscribing you agree to the', 'Obuna bo‘lib, siz quyidagiga rozilik bildirasiz:', 'Abone olarak şunu kabul edersiniz:'],
  ['Foot_Privacy', 'политикой конфиденциальности', 'privacy policy', 'maxfiylik siyosati', 'gizlilik politikası'],
  ['Foot_Schools', 'Для школ и учителей', 'For schools and teachers', "Maktab va o'qituvchilar uchun", 'Okullar ve öğretmenler için'],
  ['Foot_Delete', 'Удаление аккаунта', 'Delete account', "Hisobni o'chirish", 'Hesabı sil'],
  ['Foot_MessagePlaceholder', 'Ваше сообщение (необязательно)', 'Your message (optional)', 'Xabaringiz (majburiy emas)', 'Mesajınız (isteğe bağlı)'],
  ['Foot_NewsCheck', 'Присылайте мне новости Jumbo', 'Send me the news of Jumbo', 'Menga Jumbo yangiliklarini yuboring', 'Bana Jumbo haberlerini gönderin'],
  ['Foot_NeedSomething', 'Напишите сообщение или отметьте новости.', 'Write a message or tick the news.', 'Xabar yozing yoki yangiliklarni belgilang.', 'Bir mesaj yazın veya haberleri işaretleyin.'],
];
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const blocks = (xml) => {
  const out = new Map();
  for (const m of xml.matchAll(/  <data name="([^"]+)"[^>]*>[\s\S]*?<\/data>\r?\n/g)) out.set(m[1], m[0].replace(/\r\n/g, '\n'));
  return out;
};

for (const f of ['AppResources.resx', 'AppResources.ru.resx', 'AppResources.uz.resx', 'AppResources.tr.resx']) {
  const web = blocks(fs.readFileSync(path.join(webDir, f), 'utf8'));
  const appPath = path.join(appDir, f);
  let app = fs.readFileSync(appPath, 'utf8');
  const crlf = app.includes('\r\n'); if (crlf) app = app.replace(/\r\n/g, '\n');
  const have = new Set(blocks(app).keys());
  let add = '', n = 0;
  for (const [key, block] of web) {
    if (have.has(key) || RATING_SKIP.test(key) || EXCLUDE.test(key)) continue;
    add += block; n++; have.add(key);
  }
  const col = f === 'AppResources.resx' ? 2 : f === 'AppResources.ru.resx' ? 1 : f === 'AppResources.uz.resx' ? 3 : 4;
  for (const row of EXTRA) {
    if (have.has(row[0])) continue;
    add += `  <data name="${row[0]}" xml:space="preserve">\n    <value>${esc(row[col])}</value>\n  </data>\n`; n++;
  }
  app = app.replace(/<\/root>\s*$/, add + '</root>\n');
  fs.writeFileSync(appPath, crlf ? app.replace(/\n/g, '\r\n') : app);
  console.log(f, 'added', n);
}
