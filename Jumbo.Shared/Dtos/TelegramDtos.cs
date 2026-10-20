namespace Jumbo.Shared.Dtos;

// What the cabinet shows about the Telegram bot: is it configured, and who is connected for this school.
public class TelegramStatusDto
{
    // False while the server has no bot token (the cabinet then hides the section).
    public bool Enabled { get; set; }

    public string BotUsername { get; set; } = string.Empty;

    public List<TelegramConnectionDto> Connections { get; set; } = new();
}

public class TelegramConnectionDto
{
    public int Id { get; set; }

    // Null while the link has not been opened yet ("waiting").
    public string? Name { get; set; }

    public bool Connected { get; set; }

    // A personal link of the signed-in teacher (not a report recipient).
    public bool IsMine { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConnectedAt { get; set; }

    // t.me/<bot>?start=<code>; only for links nobody has used yet.
    public string? Url { get; set; }
}
