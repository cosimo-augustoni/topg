using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;

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
    public void Every_former_seed_quiz_has_a_sample()
    {
        Assert.Equal(["pop-culture-history.topgquiz", "science-nature.topgquiz"], SampleFileNames());
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
