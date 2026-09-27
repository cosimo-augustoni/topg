using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;
using topg.Web.Client.Shared;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

public class HintEditingTests
{
    private static string[] Texts(TextQuestionDraft question) => [.. question.Hints.Select(h => h.Text)];

    [Fact]
    public void First_hint_sets_the_type_and_the_limit_is_ten()
    {
        var question = Text(100);

        for (var i = 0; i < TextQuestionDraft.MaxHints; i++)
        {
            Assert.NotNull(question.AddHint(HintType.Text));
        }

        Assert.Null(question.AddHint(HintType.Text));
        Assert.Equal(HintType.Text, question.HintType);
        Assert.Equal(TextQuestionDraft.MaxHints, question.Hints.Count);
    }

    [Fact]
    public void Hint_of_the_other_type_cannot_be_added()
    {
        var question = TextHints(100, "A");

        Assert.Throws<InvalidOperationException>(() => question.AddHint(HintType.Image));
    }

    [Fact]
    public void Moving_up_swaps_with_the_previous_hint_and_stops_at_the_edges()
    {
        var question = TextHints(100, "A", "B", "C");

        Assert.True(question.MoveHint(question.Hints[2], -1));
        Assert.Equal(["A", "C", "B"], Texts(question));

        Assert.False(question.MoveHint(question.Hints[0], -1));
        Assert.False(question.MoveHint(question.Hints[2], 1));
    }

    [Fact]
    public void Removing_keeps_the_order_and_the_last_one_clears_the_type()
    {
        var question = TextHints(100, "A", "B", "C");

        question.RemoveHint(question.Hints[1]);
        Assert.Equal(["A", "C"], Texts(question));
        Assert.Equal(HintType.Text, question.HintType);

        question.RemoveHint(question.Hints[0]);
        question.RemoveHint(question.Hints[0]);
        Assert.Empty(question.Hints);
        Assert.Null(question.HintType);
    }

    [Fact]
    public void Switching_the_hint_type_leaves_one_empty_hint_of_the_new_type()
    {
        var question = TextHints(100, "A", "B", "C");

        question.ChangeHintType(HintType.Image);

        Assert.Equal(HintType.Image, question.HintType);
        var hint = Assert.Single(question.Hints);
        Assert.Equal("", hint.Text);
        Assert.Null(hint.Image);
    }

    [Fact]
    public void Text_question_with_hints_asks_before_becoming_an_image_question()
    {
        var project = Valid();
        var question = TextHints(300, "A");
        project.Boards[0].Categories[0].Questions.Add(question);

        Assert.True(ProjectEditing.WouldLoseData(question, QuestionType.Image));
        Assert.False(ProjectEditing.WouldLoseData(Text(100), QuestionType.Image));
        project.ChangeQuestionType(question, QuestionType.Image);

        Assert.IsType<ImageQuestionDraft>(project.FindQuestion(question.Id)!.Question);
    }

    [Fact]
    public void Duplicate_copies_the_hints_in_order()
    {
        var category = Category("Cities", ImageHints(100, (Image("aaaaaaaaaaaaaaaa"), "One"), (Image("bbbbbbbbbbbbbbbb"), "")));
        var original = (TextQuestionDraft)category.Questions[0];

        var copy = Assert.IsType<TextQuestionDraft>(category.DuplicateQuestion(original));

        Assert.Equal(HintType.Image, copy.HintType);
        Assert.Equal(original.Hints.Select(h => (h.Image, h.Text)), copy.Hints.Select(h => (h.Image, h.Text)));
        Assert.NotSame(original.Hints, copy.Hints);
    }

    [Fact]
    public void Hint_images_are_images_of_the_question()
    {
        var question = ImageHints(100, (Image("aaaaaaaaaaaaaaaa"), ""), (null, "No image yet"));

        Assert.Equal([Image("aaaaaaaaaaaaaaaa")], question.Images());
    }
}

public class HintValidationTests
{
    private static QuizProject With(TextQuestionDraft question)
    {
        var project = Valid();
        project.Boards[0].Categories[0].Questions.Add(question);
        return project;
    }

    [Fact]
    public void Text_hint_without_text_is_an_error_naming_the_hint()
    {
        var question = TextHints(300, "A", "  ");

        var issue = Assert.Single(ProjectValidator.Validate(With(question)));

        Assert.Equal("question.hint.text.empty", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(question.Id, issue.TargetId);
        Assert.Equal("Hint 2 has no text.", issue.Message);
    }

    [Fact]
    public void Image_hint_without_image_is_an_error_but_an_empty_caption_is_fine()
    {
        var question = ImageHints(300, (null, "Caption"), (Image(), ""));

        var issue = Assert.Single(ProjectValidator.Validate(With(question)));

        Assert.Equal("question.hint.image.missing", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Hint 1 has no image.", issue.Message);
    }
}

public class HintJsonTests
{
    [Fact]
    public void Question_saved_before_hints_existed_opens_without_hints()
    {
        const string json = """{ "type": "text", "points": 300, "questionText": "Q", "correctAnswer": "A" }""";

        var question = Assert.IsType<TextQuestionDraft>(CreatorJson.Deserialize<QuestionDraft>(json));

        Assert.Null(question.HintType);
        Assert.Empty(question.Hints);
    }

    [Fact]
    public void Hints_round_trip_with_type_order_captions_and_images()
    {
        var question = ImageHints(300, (Image("aaaaaaaaaaaaaaaa"), "First"), (Image("bbbbbbbbbbbbbbbb"), ""));

        var copy = Assert.IsType<TextQuestionDraft>(CreatorJson.Deserialize<QuestionDraft>(CreatorJson.Serialize<QuestionDraft>(question)));

        Assert.Equal(HintType.Image, copy.HintType);
        Assert.Equal(question.Hints.Select(h => (h.Id, h.Text, h.Image)), copy.Hints.Select(h => (h.Id, h.Text, h.Image)));
    }
}
