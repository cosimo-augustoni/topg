using System.Globalization;
using System.Text;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Shared;

namespace topg.Web.Client.Creator.Export;

/// <summary>
/// Turns a project into a PostgreSQL script for the live database (WI-14). The script is one <c>DO</c> block, so it
/// runs atomically, and it captures the generated ids with <c>RETURNING … INTO</c>.
/// Table and column names follow <c>QuizContextModelSnapshot.cs</c>:
/// Templates(Name) → Boards(TemplateId, Order) → Questions (TPH, discriminator <c>QuestionType</c>).
/// </summary>
public static class SqlExport
{
    public const string FileName = "import.sql";

    /// <param name="generatedAt">Written into the header comment; passed in so the output is reproducible.</param>
    public static string Generate(QuizProject project, DateTimeOffset generatedAt)
    {
        if (project.AllQuestions().Any(q => q is not (TextQuestionDraft or ImageQuestionDraft)))
        {
            throw new NotSupportedException("Only text and image questions can be exported.");
        }

        var name = project.Name.Trim();
        var questionCount = project.AllQuestions().Count();
        var tag = DollarQuoteTag(project);

        var sql = new StringBuilder();
        sql.Line("-- topg quiz import");
        sql.Line($"-- Quiz:      {SingleLine(name)}");
        sql.Line($"-- Generated: {generatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}");
        sql.Line($"-- Boards: {project.Boards.Count}, questions: {questionCount}, replace existing: {(project.ReplaceExisting ? "yes" : "no")}");
        sql.Line($"-- Images are expected at {SingleLine(ImageRef.UrlPrefix(project.BaseUrl, project.Folder))}");
        sql.Line($"DO {tag}");
        sql.Line("DECLARE");
        sql.Line("    template_id bigint;");
        sql.Line("    board_id bigint;");
        sql.Line("BEGIN");

        if (project.ReplaceExisting)
        {
            sql.Line($"    -- Replace existing templates with this name. Questions don't cascade from boards, so delete them first.");
            sql.Line("    DELETE FROM \"Questions\" WHERE \"BoardId\" IN (");
            sql.Line($"        SELECT b.\"Id\" FROM \"Boards\" b JOIN \"Templates\" t ON t.\"Id\" = b.\"TemplateId\" WHERE t.\"Name\" = {Literal(name)});");
            sql.Line($"    DELETE FROM \"Templates\" WHERE \"Name\" = {Literal(name)};");
            sql.Line();
        }

        sql.Line($"    INSERT INTO \"Templates\" (\"Name\") VALUES ({Literal(name)}) RETURNING \"Id\" INTO template_id;");

        for (var order = 0; order < project.Boards.Count; order++)
        {
            var board = project.Boards[order];
            sql.Line();
            sql.Line($"    -- Board {order + 1}");
            sql.Line($"    INSERT INTO \"Boards\" (\"TemplateId\", \"Order\") VALUES (template_id, {order}) RETURNING \"Id\" INTO board_id;");

            // Game order, so the script reads like the board.
            var questions = GameOrder.Categories(board)
                .SelectMany(c => GameOrder.Questions(c).Select(q => (Category: c.Name.Trim(), Question: q)))
                .ToList();

            AppendInsert(sql, "\"TextQuestion_QuestionText\", \"CorrectAnswer\"",
                questions.Where(x => x.Question is TextQuestionDraft).Select(x =>
                {
                    var q = (TextQuestionDraft)x.Question;
                    return $"{Common(q, x.Category)}, {Literal(q.QuestionText)}, {Literal(q.CorrectAnswer)}";
                }));

            AppendInsert(sql, "\"QuestionText\", \"QuestionImageUri\", \"AnswerText\", \"AnswerImageUri\", \"ImageSize\"",
                questions.Where(x => x.Question is ImageQuestionDraft).Select(x =>
                {
                    var q = (ImageQuestionDraft)x.Question;
                    var questionImage = q.QuestionImage?.Url(project.BaseUrl, project.Folder)
                        ?? throw new InvalidOperationException("Image question without image – validate the project before exporting.");
                    // The game treats an empty string as "no answer image" and the column is mapped as non-nullable.
                    var answerImage = q.AnswerImage?.Url(project.BaseUrl, project.Folder) ?? "";
                    return $"{Common(q, x.Category)}, {Literal(q.QuestionText)}, {Literal(questionImage)}, {Literal(q.AnswerText)}, {Literal(answerImage)}, {(int)q.ImageSize}";
                }));

        }

        sql.Line("END");
        sql.Line($"{tag};");
        return sql.ToString();
    }

    /// <summary>Values of the columns every question has, in the order of <see cref="CommonColumns"/>.</summary>
    private static string Common(QuestionDraft q, string category) =>
        string.Create(CultureInfo.InvariantCulture, $"board_id, {(int)q.Type}, {(int)q.AnswerType}, {q.Points}, {Literal(category)}");

    private const string CommonColumns = "\"BoardId\", \"QuestionType\", \"AnswerType\", \"Points\", \"Category\"";

    private static void AppendInsert(StringBuilder sql, string typeColumns, IEnumerable<string> rows)
    {
        var values = rows.ToList();
        if (values.Count == 0)
        {
            return;
        }

        sql.Line($"    INSERT INTO \"Questions\" ({CommonColumns}, {typeColumns}) VALUES");
        for (var i = 0; i < values.Count; i++)
        {
            sql.Append("        (").Append(values[i]).Line(i == values.Count - 1 ? ");" : "),");
        }
    }

    /// <summary>
    /// A standard SQL string literal. With <c>standard_conforming_strings</c> (the PostgreSQL default) only quotes need
    /// escaping; backslashes, newlines and unicode are kept as they are. NUL can't be stored in text and is removed.
    /// </summary>
    public static string Literal(string? value) => $"'{(value ?? "").Replace("\0", "").Replace("'", "''")}'";

    /// <summary>
    /// The body is dollar-quoted, which is purely lexical: a string containing the tag would end the block.
    /// Pick a tag that doesn't occur in any text of the project.
    /// </summary>
    private static string DollarQuoteTag(QuizProject project)
    {
        var texts = project.AllQuestions()
            .SelectMany(q => new[] { q.QuestionText, (q as TextQuestionDraft)?.CorrectAnswer, (q as ImageQuestionDraft)?.AnswerText })
            .Concat(project.Boards.SelectMany(b => b.Categories).Select(c => c.Name))
            .Append(project.Name)
            .Append(project.BaseUrl)
            .Append(project.Folder)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var tag = "$topg$";
        for (var i = 1; texts.Any(t => t!.Contains(tag, StringComparison.Ordinal)); i++)
        {
            tag = $"$topg{i}$";
        }

        return tag;
    }

    /// <summary>Always LF, so the script is the same in the browser and in tests on Windows.</summary>
    private static StringBuilder Line(this StringBuilder sql, string text = "") => sql.Append(text).Append('\n');

    /// <summary>For comments: a line break would end the comment and turn the rest into SQL.</summary>
    /// <remarks>ReplaceLineEndings covers CR, LF, CRLF, NEL, LS, PS and FF.</remarks>
    private static string SingleLine(string value) => value.ReplaceLineEndings(" ");
}
