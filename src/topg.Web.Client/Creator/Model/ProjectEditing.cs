using topg.Web.Client.Shared;

namespace topg.Web.Client.Creator.Model;

public record QuestionLocation(BoardDraft Board, int BoardNumber, CategoryDraft Category, QuestionDraft Question);

public record CategoryLocation(BoardDraft Board, int BoardNumber, CategoryDraft Category);

/// <summary>
/// Structural edits used by the editor (S-1, S-3). Everything is plain model manipulation so it can be unit tested;
/// saving, validation and undo are handled by <see cref="Storage.ProjectSession"/>.
/// </summary>
public static class ProjectEditing
{
    public const int StarterCategoryCount = 5;

    // Mirrors the Jeopardy / Double Jeopardy rounds. The questions stay empty so the validation shows what is left to do.
    public static void AddStarterBoards(this QuizProject project, IReadOnlyList<int> points)
    {
        project.AddStarterBoard(points);
        project.AddStarterBoard(points.Select(p => p * 2).ToList());
    }

    private static void AddStarterBoard(this QuizProject project, IReadOnlyList<int> points)
    {
        var board = project.AddBoard();
        for (var i = 1; i <= StarterCategoryCount; i++)
        {
            var category = board.AddCategory($"Category {i}");
            foreach (var value in points)
            {
                category.Questions.Add(new TextQuestionDraft { Points = value });
            }
        }
    }

    public static int BoardNumber(this QuizProject project, BoardDraft board) => project.Boards.IndexOf(board) + 1;

    public static BoardDraft? FindBoard(this QuizProject project, Guid boardId) => project.Boards.Find(b => b.Id == boardId);

    public static CategoryLocation? FindCategory(this QuizProject project, Guid categoryId)
    {
        for (var i = 0; i < project.Boards.Count; i++)
        {
            var category = project.Boards[i].Categories.Find(c => c.Id == categoryId);
            if (category is not null)
            {
                return new CategoryLocation(project.Boards[i], i + 1, category);
            }
        }

        return null;
    }

    public static QuestionLocation? FindQuestion(this QuizProject project, Guid questionId)
    {
        for (var i = 0; i < project.Boards.Count; i++)
        {
            foreach (var category in project.Boards[i].Categories)
            {
                var question = category.Questions.Find(q => q.Id == questionId);
                if (question is not null)
                {
                    return new QuestionLocation(project.Boards[i], i + 1, category, question);
                }
            }
        }

        return null;
    }

    public static BoardDraft AddBoard(this QuizProject project)
    {
        var board = new BoardDraft();
        project.Boards.Add(board);
        return board;
    }

    public static BoardDraft DuplicateBoard(this QuizProject project, BoardDraft board)
    {
        var copy = CreatorJson.Clone(board);
        RenewIds(copy);
        project.Boards.Insert(project.Boards.IndexOf(board) + 1, copy);
        return copy;
    }

    public static bool MoveBoard(this QuizProject project, BoardDraft board, int offset)
    {
        var index = project.Boards.IndexOf(board);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= project.Boards.Count)
        {
            return false;
        }

        (project.Boards[index], project.Boards[target]) = (project.Boards[target], project.Boards[index]);
        return true;
    }

    public static CategoryDraft AddCategory(this BoardDraft board, string? name = null)
    {
        var category = new CategoryDraft { Name = name ?? NextCategoryName(board) };
        board.Categories.Add(category);
        return category;
    }

    public static string NextCategoryName(BoardDraft board)
    {
        var names = board.Categories.Select(c => c.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = board.Categories.Count + 1;
        for (var i = 1; i <= n; i++)
        {
            if (!names.Contains($"Category {i}"))
            {
                return $"Category {i}";
            }
        }

        return $"Category {n}";
    }

    public static void MoveCategory(this QuizProject project, CategoryDraft category, BoardDraft target)
    {
        var source = project.FindCategory(category.Id)?.Board ?? throw new InvalidOperationException("Category not in project.");
        if (source == target)
        {
            return;
        }

        source.Categories.Remove(category);
        target.Categories.Add(category);
    }

    public static int FillDefaultPoints(this CategoryDraft category, IEnumerable<int> points)
    {
        var existing = category.Questions.Select(q => q.Points).ToHashSet();
        var added = 0;
        foreach (var value in points.Where(p => !existing.Contains(p)).Distinct())
        {
            category.Questions.Add(new TextQuestionDraft { Points = value });
            added++;
        }

        return added;
    }

    public static int NextPoints(this CategoryDraft category, IReadOnlyList<int> defaultPoints)
    {
        var points = defaultPoints.Count > 0 ? defaultPoints : Settings.CreatorSettings.StandardPoints;
        var existing = category.Questions.Select(q => q.Points).ToHashSet();
        var next = points.FirstOrDefault(p => !existing.Contains(p));
        if (next > 0)
        {
            return next;
        }

        var step = points.Count > 1 ? points[^1] - points[^2] : points[^1];
        return Math.Max(points[^1], existing.Max()) + Math.Max(step, 1);
    }

    public static TextQuestionDraft AddQuestion(this CategoryDraft category, IReadOnlyList<int> defaultPoints)
    {
        var question = new TextQuestionDraft { Points = category.NextPoints(defaultPoints) };
        category.Questions.Add(question);
        return question;
    }

    public static QuestionDraft DuplicateQuestion(this CategoryDraft category, QuestionDraft question)
    {
        var copy = CreatorJson.Clone(question);
        copy.Id = Guid.NewGuid();
        category.Questions.Insert(category.Questions.IndexOf(question) + 1, copy);
        return copy;
    }

    public static void MoveQuestion(this QuizProject project, QuestionDraft question, CategoryDraft target)
    {
        var source = project.FindQuestion(question.Id)?.Category ?? throw new InvalidOperationException("Question not in project.");
        if (source == target)
        {
            return;
        }

        source.Questions.Remove(question);
        target.Questions.Add(question);
    }

    public static bool RemoveQuestion(this QuizProject project, Guid questionId) =>
        project.FindQuestion(questionId) is { } location && location.Category.Questions.Remove(location.Question);

    public static bool WouldLoseData(QuestionDraft question, QuestionType newType) =>
        question.Type != newType && question switch
        {
            ImageQuestionDraft image => image.Images().Any(),
            TextQuestionDraft text => text.Hints.Count > 0,
            _ => false,
        };

    public static HintDraft? AddHint(this TextQuestionDraft question, HintType type)
    {
        if (question.Hints.Count >= TextQuestionDraft.MaxHints)
        {
            return null;
        }

        if (question.Hints.Count > 0 && question.HintType != type)
        {
            throw new InvalidOperationException("All hints of a question have the same type – switch the hint type first.");
        }

        var hint = new HintDraft();
        question.HintType = type;
        question.Hints.Add(hint);
        return hint;
    }

    public static bool MoveHint(this TextQuestionDraft question, HintDraft hint, int offset)
    {
        var index = question.Hints.IndexOf(hint);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= question.Hints.Count)
        {
            return false;
        }

        (question.Hints[index], question.Hints[target]) = (question.Hints[target], question.Hints[index]);
        return true;
    }

    public static bool RemoveHint(this TextQuestionDraft question, HintDraft hint)
    {
        if (!question.Hints.Remove(hint))
        {
            return false;
        }

        // Without hints there is no type to keep, so the inspector offers both kinds again.
        if (question.Hints.Count == 0)
        {
            question.HintType = null;
        }

        return true;
    }

    // A text hint can't become an image hint, so the old hints go and one empty hint of the new type takes their place.
    public static void ChangeHintType(this TextQuestionDraft question, HintType type)
    {
        if (question.HintType == type)
        {
            return;
        }

        question.Hints = [new HintDraft()];
        question.HintType = type;
    }

    // The correct answer and the answer text are the same idea, so the value moves across.
    public static QuestionDraft ChangeQuestionType(this QuizProject project, QuestionDraft question, QuestionType newType)
    {
        if (question.Type == newType)
        {
            return question;
        }

        var location = project.FindQuestion(question.Id) ?? throw new InvalidOperationException("Question not in project.");
        QuestionDraft replacement = newType switch
        {
            QuestionType.Text => new TextQuestionDraft
            {
                CorrectAnswer = (question as ImageQuestionDraft)?.AnswerText ?? "",
            },
            QuestionType.Image => new ImageQuestionDraft
            {
                AnswerText = (question as TextQuestionDraft)?.CorrectAnswer ?? "",
            },
            _ => throw new NotSupportedException($"{newType} questions are not supported yet."),
        };

        replacement.Id = question.Id;
        replacement.Points = question.Points;
        replacement.AnswerType = question.AnswerType;
        replacement.QuestionText = question.QuestionText;

        var index = location.Category.Questions.IndexOf(question);
        location.Category.Questions[index] = replacement;
        return replacement;
    }

    public static QuizProject Duplicate(this QuizProject project)
    {
        var copy = CreatorJson.Clone(project);
        copy.Id = Guid.NewGuid();
        copy.Name = $"{project.Name} (copy)";
        copy.CreatedAt = copy.UpdatedAt = DateTimeOffset.UtcNow;
        copy.LastExportedAt = null;
        copy.LastProjectFileExportAt = null;
        foreach (var board in copy.Boards)
        {
            RenewIds(board);
        }

        return copy;
    }

    private static void RenewIds(BoardDraft board)
    {
        board.Id = Guid.NewGuid();
        foreach (var category in board.Categories)
        {
            category.Id = Guid.NewGuid();
            foreach (var question in category.Questions)
            {
                question.Id = Guid.NewGuid();
            }
        }
    }
}
