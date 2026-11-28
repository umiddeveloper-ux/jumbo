using Jumbo.Shared;

namespace Jumbo.Tests;

// The order of the team names in the lists of a teacher / director (the journal, the reports, the exams, the picker).
public class TeamNamesTests
{
    [Fact]
    public void Classes_go_from_the_small_number_to_the_big_one()
    {
        // the list of the owner's screenshot: as text "10A" and "11A" came first
        var names = new[] { "10А", "11А", "3А", "3Б", "3В", "4А", "4Б", "5А", "5Б", "5В", "5Г", "6А", "6Б", "7А", "7Б", "7В", "8А", "8Б", "9А", "9Б" };
        var sorted = names.OrderBy(n => n, TeamNames.Natural).ToArray();
        Assert.Equal(new[] { "3А", "3Б", "3В", "4А", "4Б", "5А", "5Б", "5В", "5Г", "6А", "6Б", "7А", "7Б", "7В", "8А", "8Б", "9А", "9Б", "10А", "11А" }, sorted);
    }

    [Fact]
    public void The_letter_decides_inside_one_number_and_the_case_does_not_matter()
        => Assert.Equal(new[] { "2a", "2Б", "2В" }, new[] { "2В", "2a", "2Б" }.OrderBy(n => n, TeamNames.Natural).ToArray());

    [Fact]
    public void A_name_without_a_number_comes_after_the_numbered_ones()
        => Assert.Equal(new[] { "1А", "12Б", "А_2020", "Б_2020" }, new[] { "Б_2020", "12Б", "А_2020", "1А" }.OrderBy(n => n, TeamNames.Natural).ToArray());

    [Fact]
    public void A_separator_after_the_number_is_ignored_and_empty_names_do_not_break_it()
    {
        Assert.Equal(new[] { "3 А", "3-Б", "10 А" }, new[] { "10 А", "3-Б", "3 А" }.OrderBy(n => n, TeamNames.Natural).ToArray());
        var sorted = new string?[] { "5А", null, "" }.OrderBy(n => n, TeamNames.Natural).ToArray();
        Assert.Equal("5А", sorted[0]);
        Assert.Equal(3, sorted.Length);
    }

    [Fact]
    public void The_same_names_are_equal()
        => Assert.Equal(0, TeamNames.Natural.Compare("7Б", "7Б"));
}
