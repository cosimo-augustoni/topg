using topg.Web.Client.Shared;

namespace topg.Web.Client.Creator.Model;

/// <summary>Where a question lives in the project.</summary>
public record QuestionLocation(BoardDraft Board, int BoardNumber, CategoryDraft Category, QuestionDraft Question);

/// <summary>Where a category lives in the project.</summary>
public record CategoryLocation(BoardDraft Board, int BoardNumber, CategoryDraft Category);

/// <summary>
/// Structural edits used by the editor (S-1, S-3). Everything is plain model manipulation so it can be unit tested;
/// saving, validation and undo are handled by <see cref="Storage.ProjectSession"/>.
/// </summary>
public static class ProjectEditing
{
    public const int StarterCategoryCount = 5;

    /// <summary>S-9 "1 board, 5 categories × 5": empty text questions so the validation shows what is left to do.</summary>
    public static void AddStarterBoard(this QuizProject project, IReadOnlyList<int> points)
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

    // Lookup

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

    // Boards

    public static BoardDraft AddBoard(this QuizProject project)
    {
        var board = new BoardDraft();
        project.Boards.Add(board);
        return board;
    }

    /// <summary>Inserts a copy (with new ids) right after the original.</summary>
    public static BoardDraft DuplicateBoard(this QuizProject project, BoardDraft board)
    {
        var copy = CreatorJson.Clone(board);
        RenewIds(copy);
        project.Boards.Insert(project.Boards.IndexOf(board) + 1, copy);
        return copy;
    }

    /// <summary>Moves the board one position; <paramref name="offset"/> is -1 (left) or +1 (right). Returns false at the edges.</summary>
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

    // Categories

    public static CategoryDraft AddCategory(this BoardDraft board, string? name = null)
    {
        var category = new CategoryDraft { Name = name ?? NextCategoryName(board) };
        board.Categories.Add(category);
        return category;
    }

    /// <summary>"Category n" with the lowest n not used on the board.</summary>
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

    /// <summary>Adds text questions for the default points the category doesn't have yet (S-3c). Returns how many were added.</summary>
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

    // Questions

    /// <summary>
    /// Points for a new question: the next unused value of the default list, otherwise the largest value plus the
    /// last step of the list (e.g. 600 after 100–500).
    /// </summary>
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

    /// <summary>Copy with a new id in the same category; images are shared (same hash).</summary>
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

    /// <summary>True if switching the type would drop filled fields (images), so the UI should ask first.</summary>
    public static bool WouldLoseData(QuestionDraft question, QuestionType newType) =>
        question.Type != newType && question is ImageQuestionDraft image && image.Images().Any();

    /// <summary>
    /// Replaces the question with one of <paramref name="newType"/> in place, keeping id, points, answer type and
    /// question text. The correct answer and the answer text are the same idea, so the value moves across.
    /// </summary>
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

    // Projects

    /// <summary>Copy of the whole project with new ids, named "… (copy)". Export timestamps are reset.</summary>
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
