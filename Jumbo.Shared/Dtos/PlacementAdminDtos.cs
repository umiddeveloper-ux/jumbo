namespace Jumbo.Shared.Dtos;

// The admin page of the level test: the questions as they are stored, and what the admin sends back to change or add one.

public class PlacementAdminQuestionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
    public int? PassageId { get; set; }
    public string? PassageTitle { get; set; }
    public List<string> Audio { get; set; } = [];
    public bool IsActive { get; set; }

    // A recorded voice is uploaded (size in bytes), or null.
    public int? AudioFileSize { get; set; }
}

public class SavePlacementQuestionRequest
{
    public string Skill { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
    public int? PassageId { get; set; }
    public List<string> Audio { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public class PlacementAdminPassageDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
}

public class SavePlacementPassageRequest
{
    public string Level { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
