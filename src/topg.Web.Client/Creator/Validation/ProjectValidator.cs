using topg.Web.Client.Creator.Model;

namespace topg.Web.Client.Creator.Validation;

/// <summary>
/// Checks a project against the rules of the game and the database (see "Implicit rules" in work-items.md).
/// Errors block the export, warnings have to be acknowledged. Editing is never blocked.
/// </summary>
public static class ProjectValidator
{
    /// <summary>The game board renders every category as a 20% wide column.</summary>
    public const int MaxCategoriesPerBoard = 5;

    public static IReadOnlyList<ValidationIssue> Validate(QuizProject project)
    {
        var issues = new List<ValidationIssue>();

        void Project(string code, string message) =>
            issues.Add(new(code, ValidationSeverity.Error, ValidationTargetKind.Project, project.Id, null, message));

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            Project("project.name.empty", "Quiz name is required.");
        }

        if (!Slug.IsValid(project.Folder))
        {
            Project("project.folder.invalid", "Folder may only contain lowercase letters, digits and dashes.");
        }

        if (!IsAbsoluteHttpUrl(project.BaseUrl))
        {
            Project("project.baseUrl.invalid", "Base URL must be an absolute http(s) URL.");
        }

        if (project.Boards.Count == 0)
        {
            Project("project.noBoards", "The quiz needs at least one board.");
        }

        for (var i = 0; i < project.Boards.Count; i++)
        {
            ValidateBoard(project.Boards[i], boardNumber: i + 1, issues);
        }

        return issues;
    }

    public static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    private static void ValidateBoard(BoardDraft board, int boardNumber, List<ValidationIssue> issues)
    {
        if (board.Categories.Count == 0)
        {
            issues.Add(new("board.noCategories", ValidationSeverity.Error, ValidationTargetKind.Board, board.Id, board.Id,
                $"Board {boardNumber} has no categories."));
        }
        else if (board.Categories.Count > MaxCategoriesPerBoard)
        {
            issues.Add(new("board.tooManyCategories", ValidationSeverity.Warning, ValidationTargetKind.Board, board.Id, board.Id,
                $"Board {boardNumber} has {board.Categories.Count} categories – only {MaxCategoriesPerBoard} fit on the game board."));
        }

        // The game groups questions by the exact category string, so duplicates would merge into one column.
        var duplicateNames = board.Categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name.Trim())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var category in board.Categories)
        {
            var name = category.Name.Trim();
            if (name.Length == 0)
            {
                issues.Add(new("category.name.empty", ValidationSeverity.Error, ValidationTargetKind.Category, category.Id, board.Id,
                    "Category name is required."));
            }
            else if (duplicateNames.Contains(name))
            {
                issues.Add(new("category.name.duplicate", ValidationSeverity.Error, ValidationTargetKind.Category, category.Id, board.Id,
                    $"Category \"{name}\" exists twice on board {boardNumber}."));
            }

            if (category.Questions.Count == 0)
            {
                issues.Add(new("category.noQuestions", ValidationSeverity.Warning, ValidationTargetKind.Category, category.Id, board.Id,
                    $"Category \"{name}\" has no questions."));
            }

            ValidateQuestions(category, board, issues);
        }
    }

    private static void ValidateQuestions(CategoryDraft category, BoardDraft board, List<ValidationIssue> issues)
    {
        void Add(QuestionDraft question, string code, ValidationSeverity severity, string message) =>
            issues.Add(new(code, severity, ValidationTargetKind.Question, question.Id, board.Id, message));

        var duplicatePoints = category.Questions
            .GroupBy(q => q.Points)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var question in category.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionText))
            {
                Add(question, "question.text.empty", ValidationSeverity.Error, "Question text is required.");
            }

            switch (question)
            {
                case TextQuestionDraft text when string.IsNullOrWhiteSpace(text.CorrectAnswer):
                    Add(question, "question.answer.empty", ValidationSeverity.Error, "Correct answer is required.");
                    break;
                case ImageQuestionDraft { QuestionImage: null }:
                    Add(question, "question.image.missing", ValidationSeverity.Error, "Question image is required.");
                    break;
            }

            if (question.Points <= 0)
            {
                Add(question, "question.points.invalid", ValidationSeverity.Error, "Points must be greater than 0.");
            }
            else if (duplicatePoints.Contains(question.Points))
            {
                Add(question, "question.points.duplicate", ValidationSeverity.Warning,
                    $"Another question in \"{category.Name.Trim()}\" also has {question.Points} points – order is undefined.");
            }
        }
    }
}
