using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;
using topg.Web.Client.Shared;

namespace topg.Web.Client.Tests;

public class SampleProjectTests
{
    private static string[] SampleFileNames() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Samples"), "*" + ProjectFile.Extension)
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static TheoryData<string> Samples() => new(SampleFileNames());

    private static ProjectFileContent Read(string fileName)
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", fileName));
        return ProjectFile.Read(file);
    }

    [Fact]
    public void Every_former_seed_quiz_and_the_hints_quiz_have_a_sample()
    {
        Assert.Equal(["guess-with-hints.topgquiz", "pop-culture-history.topgquiz", "science-nature.topgquiz"], SampleFileNames());
    }

    [Fact]
    public void Hints_sample_covers_every_hint_count_of_both_types_and_carries_the_hint_images()
    {
        var content = Read("guess-with-hints.topgquiz");
        var questions = content.Project.AllQuestions().OfType<TextQuestionDraft>().Where(q => q.Hints.Count > 0).ToList();

        Assert.All(new[] { HintType.Text, HintType.Image }, type => Assert.Equal(
            Enumerable.Range(1, TextQuestionDraft.MaxHints),
            questions.Where(q => q.HintType == type).Select(q => q.Hints.Count).Distinct().Order()));
        Assert.Contains(questions, q => q.HintType == HintType.Image && q.Hints.All(h => h.Text.Length > 0));
        Assert.Equal(content.Project.AllImages().Select(i => i.Hash).Distinct().Order(), content.ImagesByHash.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_has_two_full_boards_without_validation_issues(string fileName)
    {
        var project = Read(fileName).Project;

        Assert.Empty(ProjectValidator.Validate(project));
        Assert.Equal(ProjectFile.FileName(project), fileName);
        Assert.Equal(2, project.Boards.Count);
        Assert.All(project.Boards, board =>
        {
            Assert.Equal(ProjectValidator.MaxCategoriesPerBoard, board.Categories.Count);
            Assert.All(board.Categories, c => Assert.Equal([100, 200, 300, 400, 500, 600], c.Questions.Select(q => q.Points).Order()));
        });
    }

    [Fact]
    public void Image_question_of_the_pop_culture_sample_carries_its_images()
    {
        var content = Read("pop-culture-history.topgquiz");

        var question = Assert.Single(content.Project.AllQuestions().OfType<ImageQuestionDraft>());
        Assert.Equal("From which movie is this Character?", question.QuestionText);
        Assert.Equal("Nosferatu", question.AnswerText);
        Assert.NotNull(question.QuestionImage);
        Assert.NotNull(question.AnswerImage);
        Assert.Equal(
            new[] { question.QuestionImage.Hash, question.AnswerImage.Hash }.Order(),
            content.ImagesByHash.Keys.Order());
    }
}
