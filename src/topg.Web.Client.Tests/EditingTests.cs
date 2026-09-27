using topg.Web.Client.Creator.Components;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;
using topg.Web.Client.Creator.Validation;
using topg.Web.Client.Shared;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

public class GameOrderTests
{
    [Fact]
    public void Categories_are_alphabetical_like_the_game()
    {
        var board = new BoardDraft { Categories = [Category("Sport"), Category(" Animals"), Category("music"), Category("Movies")] };

        Assert.Equal([" Animals", "Movies", "music", "Sport"], GameOrder.Categories(board).Select(c => c.Name));
    }

    [Fact]
    public void Questions_are_ordered_by_points_and_ties_keep_insertion_order()
    {
        var a = Text(300, "a");
        var b = Text(100, "b");
        var c = Text(300, "c");

        Assert.Equal([b, a, c], GameOrder.Questions(Category("X", a, b, c)));
    }
}

public class ProjectEditingTests
{
    private static readonly int[] Defaults = [100, 200, 300, 400, 500];

    [Fact]
    public void Starter_boards_have_five_categories_with_default_then_doubled_points_and_empty_texts()
    {
        var project = QuizProject.Create("Quiz", "https://cdn");
        project.AddStarterBoards(Defaults);

        Assert.Equal(2, project.Boards.Count);
        Assert.All(project.Boards, b => Assert.Equal(["Category 1", "Category 2", "Category 3", "Category 4", "Category 5"], b.Categories.Select(c => c.Name)));
        Assert.All(project.Boards.SelectMany(b => b.Categories).SelectMany(c => c.Questions), q => Assert.IsType<TextQuestionDraft>(q));
        Assert.All(project.Boards[0].Categories, c => Assert.Equal(Defaults, c.Questions.Select(q => q.Points)));
        Assert.All(project.Boards[1].Categories, c => Assert.Equal([200, 400, 600, 800, 1000], c.Questions.Select(q => q.Points)));

        var issueBoards = ProjectValidator.Validate(project).Where(i => i.Code == "question.text.empty").Select(i => i.BoardId).ToHashSet();
        Assert.Equal(project.Boards.Select(b => (Guid?)b.Id).ToHashSet(), issueBoards);
    }

    [Fact]
    public void Starter_boards_follow_custom_default_points()
    {
        var project = QuizProject.Create("Quiz", "https://cdn");
        project.AddStarterBoards([10, 20]);

        Assert.All(project.Boards[0].Categories, c => Assert.Equal([10, 20], c.Questions.Select(q => q.Points)));
        Assert.All(project.Boards[1].Categories, c => Assert.Equal([20, 40], c.Questions.Select(q => q.Points)));
    }

    [Fact]
    public void Find_returns_location_with_board_number()
    {
        var project = Valid();
        project.Boards.Insert(0, new BoardDraft());
        var question = project.Boards[1].Categories[1].Questions[0];

        var location = project.FindQuestion(question.Id);

        Assert.NotNull(location);
        Assert.Equal(2, location.BoardNumber);
        Assert.Equal("Animals", location.Category.Name);
        Assert.Equal(2, project.FindCategory(location.Category.Id)!.BoardNumber);
        Assert.Null(project.FindQuestion(Guid.NewGuid()));
    }

    [Fact]
    public void Move_board_swaps_neighbours_and_stops_at_the_edges()
    {
        var project = Valid();
        var first = project.Boards[0];
        var second = project.AddBoard();

        Assert.False(project.MoveBoard(first, -1));
        Assert.True(project.MoveBoard(first, 1));
        Assert.Equal([second, first], project.Boards);
        Assert.False(project.MoveBoard(first, 1));
    }

    [Fact]
    public void Duplicate_board_is_inserted_after_the_original_with_new_ids()
    {
        var project = Valid();
        var original = project.Boards[0];
        project.AddBoard();

        var copy = project.DuplicateBoard(original);

        Assert.Same(copy, project.Boards[1]);
        Assert.NotEqual(original.Id, copy.Id);
        var originalIds = original.Categories.SelectMany(c => c.Questions.Select(q => q.Id).Append(c.Id)).ToHashSet();
        Assert.DoesNotContain(copy.Categories.SelectMany(c => c.Questions.Select(q => q.Id).Append(c.Id)), originalIds.Contains);
        Assert.Equal(original.Categories.Select(c => c.Name), copy.Categories.Select(c => c.Name));
    }

    [Fact]
    public void New_category_names_fill_the_first_gap()
    {
        var board = new BoardDraft();
        board.AddCategory();
        board.AddCategory();
        board.Categories[0].Name = "History";

        Assert.Equal("Category 1", board.AddCategory().Name);
        Assert.Equal("Category 3", board.AddCategory().Name);
    }

    [Theory]
    [InlineData(new int[0], 100)]
    [InlineData(new[] { 100, 200 }, 300)]
    [InlineData(new[] { 100, 300 }, 200)]
    [InlineData(new[] { 100, 200, 300, 400, 500 }, 600)]
    [InlineData(new[] { 100, 200, 300, 400, 500, 600 }, 700)]
    [InlineData(new[] { 1000 }, 100)]
    public void Next_points_uses_the_first_missing_default_then_continues_the_step(int[] existing, int expected)
    {
        var category = Category("X", existing.Select(p => (QuestionDraft)Text(p)).ToArray());

        Assert.Equal(expected, category.NextPoints(Defaults));
    }

    [Fact]
    public void Next_points_continues_after_large_custom_values()
    {
        var category = Category("X", Defaults.Select(p => (QuestionDraft)Text(p)).Append(Text(1000)).ToArray());

        Assert.Equal(1100, category.NextPoints(Defaults));
    }

    [Fact]
    public void Fill_default_points_adds_only_missing_values()
    {
        var category = Category("X", Text(100), Text(300));

        Assert.Equal(3, category.FillDefaultPoints(Defaults));
        Assert.Equal(Defaults, category.Questions.Select(q => q.Points).Order());
        Assert.Equal(0, category.FillDefaultPoints(Defaults));
    }

    [Fact]
    public void Duplicate_question_gets_new_id_and_shares_images()
    {
        var project = Valid();
        var category = project.Boards[0].Categories[1];
        var original = (ImageQuestionDraft)category.Questions[0];

        var copy = Assert.IsType<ImageQuestionDraft>(category.DuplicateQuestion(original));

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.QuestionImage, copy.QuestionImage);
        Assert.Same(copy, category.Questions[1]);
    }

    [Fact]
    public void Move_question_and_category_between_containers()
    {
        var project = Valid();
        var history = project.Boards[0].Categories[0];
        var animals = project.Boards[0].Categories[1];
        var question = history.Questions[0];
        var board2 = project.AddBoard();

        project.MoveQuestion(question, animals);
        project.MoveCategory(history, board2);

        Assert.Contains(question, animals.Questions);
        Assert.DoesNotContain(question, history.Questions);
        Assert.Same(board2, project.FindCategory(history.Id)!.Board);
        Assert.Equal(100, question.Points);
    }

    [Fact]
    public void Text_to_image_keeps_shared_fields_and_moves_the_answer()
    {
        var project = Valid();
        var question = (TextQuestionDraft)project.Boards[0].Categories[0].Questions[0];
        question.AnswerType = AnswerType.Text;

        Assert.False(ProjectEditing.WouldLoseData(question, QuestionType.Image));
        var image = Assert.IsType<ImageQuestionDraft>(project.ChangeQuestionType(question, QuestionType.Image));

        Assert.Equal(question.Id, image.Id);
        Assert.Equal(100, image.Points);
        Assert.Equal(AnswerType.Text, image.AnswerType);
        Assert.Equal("Question?", image.QuestionText);
        Assert.Equal("Answer", image.AnswerText);
        Assert.Same(image, project.FindQuestion(question.Id)!.Question);
    }

    [Fact]
    public void Image_to_text_asks_first_when_images_would_be_lost()
    {
        var project = Valid();
        var image = (ImageQuestionDraft)project.Boards[0].Categories[1].Questions[0];

        Assert.True(ProjectEditing.WouldLoseData(image, QuestionType.Text));
        var text = Assert.IsType<TextQuestionDraft>(project.ChangeQuestionType(image, QuestionType.Text));

        Assert.Equal("A cat", text.CorrectAnswer);
        Assert.DoesNotContain(project.AllImages(), i => i == image.QuestionImage);
    }

    [Fact]
    public void Image_question_without_images_can_switch_without_asking()
    {
        Assert.False(ProjectEditing.WouldLoseData(new ImageQuestionDraft(), QuestionType.Text));
    }

    [Fact]
    public void Duplicate_project_gets_new_ids_name_and_resets_exports()
    {
        var project = Valid();
        project.LastExportedAt = DateTimeOffset.UtcNow;

        var copy = project.Duplicate();

        Assert.NotEqual(project.Id, copy.Id);
        Assert.Equal("Pub Quiz (copy)", copy.Name);
        Assert.Null(copy.LastExportedAt);
        Assert.Equal(project.Folder, copy.Folder);
        Assert.Empty(copy.AllQuestions().Select(q => q.Id).Intersect(project.AllQuestions().Select(q => q.Id)));
    }
}

public class ProjectSessionTests
{
    private readonly InMemoryCreatorStorage _storage = new();

    private async Task<(ProjectSession Session, QuizProject Project)> OpenValid()
    {
        var store = new ProjectStore(_storage);
        var project = Valid();
        await store.SaveAsync(project);
        var session = new ProjectSession(store);
        Assert.True(await session.OpenAsync(project.Id));
        return (session, session.Project!);
    }

    [Fact]
    public async Task Open_unknown_project_returns_false()
    {
        var session = new ProjectSession(new ProjectStore(_storage));

        Assert.False(await session.OpenAsync(Guid.NewGuid()));
        Assert.Null(session.Project);
    }

    [Fact]
    public async Task Change_revalidates_and_indexes_issues_by_target_and_board()
    {
        var (session, project) = await OpenValid();
        var question = project.Boards[0].Categories[0].Questions[0];

        session.Change(_ => question.QuestionText = "");

        Assert.Equal(1, session.ErrorCount);
        Assert.Equal("question.text.empty", Assert.Single(session.IssuesFor(question.Id)).Code);
        Assert.Single(session.IssuesInBoard(project.Boards[0].Id));
    }

    [Fact]
    public async Task Undo_restores_the_snapshot_and_selection()
    {
        var (session, project) = await OpenValid();
        var question = project.Boards[0].Categories[0].Questions[0];
        session.Select(EditorSelection.Question(question.Id));

        session.ChangeWithUndo("delete question", p => p.RemoveQuestion(question.Id));
        session.ClearSelection();
        Assert.Null(session.Project!.FindQuestion(question.Id));

        Assert.Equal("delete question", session.Undo());
        Assert.NotNull(session.Project!.FindQuestion(question.Id));
        Assert.Equal(EditorSelection.Question(question.Id), session.Selection);
        Assert.Null(session.Undo());
    }

    [Fact]
    public async Task Start_pixelated_is_saved_and_reloaded()
    {
        var (session, project) = await OpenValid();
        var question = new ImageQuestionDraft { Points = 500, QuestionText = "Which flag?" };
        session.Change(p => p.Boards[0].Categories[0].Questions.Add(question));
        Assert.False(question.StartPixelated);

        session.Change(_ => question.StartPixelated = true);
        await session.CloseAsync();

        var reloaded = await new ProjectStore(_storage).GetAsync(project.Id);
        Assert.True(reloaded!.AllQuestions().OfType<ImageQuestionDraft>().Single(q => q.Id == question.Id).StartPixelated);
    }

    [Fact]
    public async Task Close_flushes_pending_changes_to_the_store()
    {
        var (session, project) = await OpenValid();
        session.Change(p => p.Name = "Renamed");

        await session.CloseAsync();

        Assert.Equal("Renamed", (await new ProjectStore(_storage).GetAsync(project.Id))!.Name);
        Assert.Null(session.Project);
    }

    [Fact]
    public async Task Selected_board_follows_the_selection()
    {
        var (session, project) = await OpenValid();
        var board2 = project.AddBoard();
        var category = board2.AddCategory("Late");

        session.Select(EditorSelection.Category(category.Id), focus: true);

        Assert.Same(board2, session.SelectedBoard());
        Assert.Equal(1, session.FocusRequest);
    }
}

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Formats_recent_times_relatively()
    {
        Assert.Equal("just now", RelativeTime.Format(Now.AddSeconds(-20), Now));
        Assert.Equal("5 min ago", RelativeTime.Format(Now.AddMinutes(-5), Now));
    }

    [Fact]
    public void Formats_older_dates_absolutely()
    {
        Assert.Equal("Sep 1", RelativeTime.Format(Now.AddDays(-26), Now));
        Assert.Equal("Sep 1, 2025", RelativeTime.Format(Now.AddDays(-26).AddYears(-1), Now));
    }
}
