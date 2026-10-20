namespace Jumbo.Api.Services.Telegram;

// The bot's public page in Telegram: name, the text on the empty chat (description), the one-liner on its profile
// (short description) and the command menu, in three languages. Pushed to Telegram when the API starts; values that
// already match are left alone (Telegram limits how often a name may change). The avatar can only be set in
// @BotFather (/setuserpic): the file is bot-userpic.png in the repo root.
public static class TelegramProfile
{
    // language -> text. "" is what everybody else sees (Russian: most of our users).
    private static readonly (string Lang, string Name, string Short, string Description, (string Command, string Text)[] Commands)[] Profiles =
    [
        ("", "Jumbo",
            "Отчёты учебного заведения Jumbo.uz для директора: сводка и ссылка каждый понедельник.",
            "Я бот Jumbo — платформы для изучения английского. 🐯\n\nДиректорам учебных заведений: каждый понедельник я присылаю отчёт об успехах учеников. Учителям: заявки учеников в команду с кнопками «Принять» и «Отклонить» и напоминание, кто давно не занимался.\n\nКак подключиться: откройте кабинет на jumbo.uz, нажмите «Подключить мой Telegram» (для себя) или «Добавить получателя» (для директора) и откройте полученную ссылку.",
            [("start", "Как подключиться"), ("lang", "Язык сообщений")]),
        ("ru", "Jumbo",
            "Отчёты учебного заведения Jumbo.uz для директора: сводка и ссылка каждый понедельник.",
            "Я бот Jumbo — платформы для изучения английского. 🐯\n\nДиректорам учебных заведений: каждый понедельник я присылаю отчёт об успехах учеников. Учителям: заявки учеников в команду с кнопками «Принять» и «Отклонить» и напоминание, кто давно не занимался.\n\nКак подключиться: откройте кабинет на jumbo.uz, нажмите «Подключить мой Telegram» (для себя) или «Добавить получателя» (для директора) и откройте полученную ссылку.",
            [("start", "Как подключиться"), ("lang", "Язык сообщений")]),
        ("uz", "Jumbo",
            "Jumbo.uz ta'lim muassasasi hisobotlari direktor uchun: har dushanba qisqa hisobot va havola.",
            "Men Jumbo botiman — ingliz tilini o'rganish platformasi. 🐯\n\nTa'lim muassasalari direktorlariga: har dushanba kuni o'quvchilarning natijalari haqida hisobot yuboraman — nechta o'quvchi shug'ullangan va nechta so'z o'rganilgan. Saytga kirish shart emas.\n\nQanday ulanish: o'qituvchi jumbo.uz kabinetida «Qabul qiluvchi qo'shish» tugmasini bosadi va sizga havola yuboradi. Uni oching va Start tugmasini bosing.",
            [("start", "Qanday ulanish"), ("lang", "Xabarlar tili")]),
        ("en", "Jumbo",
            "Jumbo.uz institution reports for directors: a summary and link every Monday.",
            "I am the Jumbo bot — a platform for learning English. 🐯\n\nFor directors of institutions: every Monday I send a report on the pupils' progress — how many were active and how many words they learned. No need to log in to the site.\n\nHow to connect: the teacher opens the cabinet on jumbo.uz, presses “Add a recipient” and sends you a link. Open it and press Start.",
            [("start", "How to connect"), ("lang", "Language")]),
    ];

    private static readonly Dictionary<string, string[]> CommandTexts = new()
    {
        // command: [ru, uz, en]
        ["team"] = ["Мои команды", "Jamoalarim", "My teams"],
        ["teachers"] = ["Учителя учебного заведения", "Muassasa o'qituvchilari", "Institution teachers"],
        ["pending"] = ["Заявки учеников", "O'quvchi arizalari", "Pupil requests"],
        ["exams"] = ["Результаты экзаменов", "Imtihon natijalari", "Exam results"],
        ["absent"] = ["Кто давно не занимался", "Kim shug'ullanmagan", "Who stopped practising"],
        ["report"] = ["Отчёт учебного заведения за неделю", "Haftalik hisobot", "Weekly institution report"],
        ["month"] = ["Отчёт учебного заведения за месяц", "Oylik hisobot", "Monthly institution report"],
        ["me"] = ["Мой прогресс", "Mening natijam", "My progress"],
        ["quiz"] = ["Мини-викторина", "Mini-viktorina", "Mini quiz"],
        ["goal"] = ["Цель на день", "Kunlik maqsad", "Daily goal"],
        ["top"] = ["Рейтинг недели", "Haftalik reyting", "Weekly rating"],
        ["play"] = ["Открыть Jumbo", "Jumboni ochish", "Open Jumbo"],
        ["remind"] = ["Напоминания вкл/выкл", "Eslatmalarni yoqish/o'chirish", "Reminders on/off"],
        ["lang"] = ["Язык сообщений", "Xabarlar tili", "Language"],
        ["stop"] = ["Отключить уведомления", "Bildirishnomalarni o'chirish", "Turn the notifications off"],
    };

    private static readonly Dictionary<string, string[]> Menus = new()
    {
        ["teacher"] = ["team", "pending", "exams", "absent", "report", "lang", "stop"],
        ["director"] = ["team", "teachers", "pending", "exams", "absent", "report", "month", "lang", "stop"],
        ["recipient"] = ["report", "month", "lang", "stop"],
        ["pupil"] = ["me", "quiz", "goal", "top", "play", "remind", "lang", "stop"],
    };

    // The menu a connected chat sees (kind: teacher | director | recipient), in its language.
    public static (string Command, string Text)[] MenuFor(string kind, string lang)
    {
        var i = Math.Max(0, Array.IndexOf(TelegramTexts.Languages, lang));
        return Menus[Menus.ContainsKey(kind) ? kind : "recipient"].Select(c => (c, CommandTexts[c][i])).ToArray();
    }

    public static async Task SyncAsync(TelegramClient telegram, ILogger log)
    {
        foreach (var p in Profiles)
        {
            try
            {
                await EnsureAsync(telegram, "getMyName", "setMyName", "name", p.Lang, p.Name);
                await EnsureAsync(telegram, "getMyShortDescription", "setMyShortDescription", "short_description", p.Lang, p.Short);
                await EnsureAsync(telegram, "getMyDescription", "setMyDescription", "description", p.Lang, p.Description);

                var commands = p.Commands.Select(c => new { command = c.Command, description = c.Text }).ToArray();
                await telegram.CallAsync("setMyCommands", new Dictionary<string, object?> { ["commands"] = commands, ["language_code"] = p.Lang });
            }
            catch (Exception ex) { log.LogWarning("Telegram profile sync ({Lang}) failed: {Type}", p.Lang, ex.GetType().Name); }
        }
        log.LogInformation("Telegram bot profile synced");
    }

    private static async Task EnsureAsync(TelegramClient telegram, string get, string set, string field, string lang, string value)
    {
        if (await telegram.GetProfileValueAsync(get, field, lang) == value) return;
        await telegram.CallAsync(set, new Dictionary<string, object?> { [field] = value, ["language_code"] = lang });
    }
}
