using Jumbo.Api.Services;
using Jumbo.Shared;

namespace Jumbo.Tests;

// The JO a won game gives: ONE table (Jumbo.Shared.GameRewards) for the server that gives them and for the website and the app that show them.
public class GameRewardsTests
{
    private static readonly string[] GameTypes = ["FindMe", "Sequence", "ListenAndFind", "Steps", "Phrase"];

    [Fact]
    public void The_server_gives_exactly_what_the_shared_table_says()
    {
        Assert.Same(GameRewards.ByGameType, LivesRules.GameRewards);
        foreach (var type in GameTypes)
            Assert.Equal(GameRewards.Of(type), LivesRules.GameRewards.GetValueOrDefault(type));
    }

    [Fact]
    public void Every_game_has_a_reward()
    {
        foreach (var type in GameTypes) Assert.True(GameRewards.Of(type) > 0, type);
        Assert.Equal(GameTypes.Length, GameRewards.ByGameType.Count);
    }

    [Fact]
    public void Every_game_key_has_the_reward_of_its_game()
    {
        foreach (var key in GameProgressRules.Games)
        {
            var type = key switch { "findme" => "FindMe", "listen" => "ListenAndFind", "phrase" => "Phrase", "sequence" => "Sequence", _ => "Steps" };
            Assert.Equal(GameRewards.Of(type), GameRewards.OfKey(key));
            Assert.True(GameRewards.OfKey(key) > 0, key);
        }
    }

    [Theory]
    [InlineData("FindMe", 5)]
    [InlineData("Sequence", 7)]
    [InlineData("ListenAndFind", 7)]
    [InlineData("Steps", 10)]
    [InlineData("Phrase", 8)]
    public void The_numbers_the_learners_know(string game, int jo) => Assert.Equal(jo, GameRewards.Of(game));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Chess")]
    [InlineData("findme")]       // a key is not a game name
    public void An_unknown_game_gives_nothing(string? game) => Assert.Equal(0, GameRewards.Of(game));

    [Theory]
    [InlineData("FINDME", 5)]
    [InlineData("listen", 7)]
    [InlineData("nothing", 0)]
    [InlineData(null, 0)]
    public void A_key_is_read_whatever_the_case(string? key, int jo) => Assert.Equal(jo, GameRewards.OfKey(key));

    [Fact]
    public void No_game_of_the_website_or_the_app_keeps_its_own_copy_of_the_number()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "Jumbo.slnx"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var offenders = new[] { "Jumbo", "JumboWeb" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root!, dir), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".razor")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("const int LpReward"))
            .ToList();
        Assert.Empty(offenders);
    }
}
