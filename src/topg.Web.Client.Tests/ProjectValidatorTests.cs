using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

public class ProjectValidatorTests
{
    private static IReadOnlyList<ValidationIssue> Validate(QuizProject project) => ProjectValidator.Validate(project);

    private static ValidationIssue Single(QuizProject project, string code) =>
        Assert.Single(Validate(project), i => i.Code == code);

    [Fact]
    public void Valid_project_has_no_issues()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_is_an_error(string name)
    {
        var project = Valid();
        project.Name = name;

        var issue = Single(project, "project.name.empty");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationTargetKind.Project, issue.TargetKind);
        Assert.Equal(project.Id, issue.TargetId);
        Assert.Equal("Quiz name is required.", issue.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pub-Quiz")]
    [InlineData("pub quiz")]
    [InlineData("pub--quiz")]
    [InlineData("-pub")]
    [InlineData("pub/quiz")]
    public void Invalid_folder_is_an_error(string folder)
    {
        var project = Valid();
        project.Folder = folder;

        Assert.Equal(ValidationSeverity.Error, Single(project, "project.folder.invalid").Severity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cdn.example.com")]
    [InlineData("/quiz")]
    [InlineData("ftp://cdn.example.com")]
    [InlineData("file:///c:/images")]
    public void Base_url_must_be_absolute_http(string baseUrl)
    {
        var project = Valid();
        project.BaseUrl = baseUrl;

        Assert.Equal("Base URL must be an absolute http(s) URL.", Single(project, "project.baseUrl.invalid").Message);
    }

    [Theory]
    [InlineData("http://cdn.example.com")]
    [InlineData("https://cdn.example.com/quiz/")]
    public void Http_and_https_base_urls_are_valid(string baseUrl)
    {
        var project = Valid();
        project.BaseUrl = baseUrl;

        Assert.Empty(Validate(project));
    }

    [Fact]
    public void Project_without_boards_is_an_error()
    {
        var project = Valid();
        project.Boards.Clear();

        Single(project, "project.noBoards");
    }

    [Fact]
    public void Board_without_categories_is_an_error_with_board_number()
    {
        var project = Valid();
        var empty = new BoardDraft();
        project.Boards.Add(empty);

        var issue = Single(project, "board.noCategories");
        Assert.Equal("Board 2 has no categories.", issue.Message);
        Assert.Equal(empty.Id, issue.TargetId);
        Assert.Equal(empty.Id, issue.BoardId);
    }

    [Fact]
    public void More_than_five_categories_is_a_warning()
    {
        var project = Valid();
        var board = project.Boards[0];
        board.Categories = Enumerable.Range(1, 6).Select(i => Category($"C{i}", Text(100))).ToList();

        var issue = Single(project, "board.tooManyCategories");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Board 1 has 6 categories – only 5 fit on the game board.", issue.Message);
    }

    [Fact]
    public void Five_categories_are_fine()
    {
        var project = Valid();
        project.Boards[0].Categories = Enumerable.Range(1, 5).Select(i => Category($"C{i}", Text(100))).ToList();

        Assert.Empty(Validate(project));
    }

    [Fact]
    public void Empty_category_name_is_an_error()
    {
        var project = Valid();
        var category = project.Boards[0].Categories[0];
        category.Name = " ";

        var issue = Single(project, "category.name.empty");
        Assert.Equal(category.Id, issue.TargetId);
        Assert.Equal(ValidationTargetKind.Category, issue.TargetKind);
    }

    [Fact]
    public void Duplicate_category_names_are_errors_on_both_categories_ignoring_surrounding_spaces()
    {
        var project = Valid();
        project.Boards[0].Categories[1].Name = " History ";

        var issues = Validate(project).Where(i => i.Code == "category.name.duplicate").ToList();
        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal("Category \"History\" exists twice on board 1.", i.Message));
    }

    [Fact]
    public void Same_category_name_on_different_boards_is_fine()
    {
        var project = Valid();
        project.Boards.Add(new BoardDraft { Categories = [Category("History", Text(100))] });

        Assert.Empty(Validate(project));
    }

    [Fact]
    public void Category_without_questions_is_a_warning()
    {
        var project = Valid();
        project.Boards[0].Categories[0].Questions.Clear();

        var issue = Single(project, "category.noQuestions");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Category \"History\" has no questions.", issue.Message);
    }

    [Fact]
    public void Question_without_text_is_an_error()
    {
        var project = Valid();
        var question = project.Boards[0].Categories[0].Questions[0];
        question.QuestionText = "";

        var issue = Single(project, "question.text.empty");
        Assert.Equal(question.Id, issue.TargetId);
        Assert.Equal(ValidationTargetKind.Question, issue.TargetKind);
        Assert.Equal(project.Boards[0].Id, issue.BoardId);
    }

    [Fact]
    public void Text_question_without_answer_is_an_error()
    {
        var project = Valid();
        ((TextQuestionDraft)project.Boards[0].Categories[0].Questions[0]).CorrectAnswer = "";

        Single(project, "question.answer.empty");
    }

    [Fact]
    public void Image_question_without_image_is_an_error()
    {
        var project = Valid();
        ((ImageQuestionDraft)project.Boards[0].Categories[1].Questions[0]).QuestionImage = null;

        Single(project, "question.image.missing");
    }

    [Fact]
    public void Image_question_without_answer_image_or_answer_text_is_fine()
    {
        var project = Valid();
        var question = (ImageQuestionDraft)project.Boards[0].Categories[1].Questions[0];
        question.AnswerImage = null;
        question.AnswerText = "";

        Assert.Empty(Validate(project));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Points_must_be_positive(int points)
    {
        var project = Valid();
        project.Boards[0].Categories[0].Questions[0].Points = points;

        Single(project, "question.points.invalid");
    }

    [Fact]
    public void Duplicate_points_in_a_category_are_warnings()
    {
        var project = Valid();
        project.Boards[0].Categories[0].Questions[1].Points = 100;

        var issues = Validate(project).Where(i => i.Code == "question.points.duplicate").ToList();
        Assert.Equal(2, issues.Count);
        Assert.All(issues, i =>
        {
            Assert.Equal(ValidationSeverity.Warning, i.Severity);
            Assert.Equal("Another question in \"History\" also has 100 points – order is undefined.", i.Message);
        });
    }

    [Fact]
    public void Same_points_in_different_categories_are_fine()
    {
        // Valid() already has 100 and 200 in both categories.
        Assert.DoesNotContain(Validate(Valid()), i => i.Code == "question.points.duplicate");
    }
}
