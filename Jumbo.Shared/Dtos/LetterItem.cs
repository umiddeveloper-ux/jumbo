namespace Jumbo.Shared.Dtos;

public class LetterItem
{
    public string Letter { get; set; } = string.Empty;

    // Все слова для этой буквы в базе (определяет, активна ли кнопка)
    public int TotalCount { get; set; }

    // Слова, с которыми пользователь уже встретился в играх (отображается в бейдже)
    public int LearnedCount { get; set; }

    // -1 = нет слов для буквы / ни одно не изучалось → нейтральный
    //  0 = встречались, но ни разу не повторены    → светло-красный
    //  1 = все повторены ≥1 раз                    → светло-жёлтый
    //  2 = все повторены ≥2 раза                   → светло-серый
    //  3 = все повторены ≥3 раза                   → светло-зелёный
    public int RepetitionLevel { get; set; }

    public bool HasWords => TotalCount > 0;

    // Защита от деления на 0 в круговом прогресс-баре, когда для буквы нет слов
    public double ProgressMaximum => TotalCount > 0 ? TotalCount : 1;
}
