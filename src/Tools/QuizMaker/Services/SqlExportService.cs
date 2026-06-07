using System.Text;
using Microsoft.EntityFrameworkCore;
using QuizMaker.Data;
using QuizMaker.Data.Entities;

namespace QuizMaker.Services;

public class SqlExportService
{
    private readonly QuizDbContext _db;

    public SqlExportService(QuizDbContext db)
    {
        _db = db;
    }

    public async Task<string> ExportTemplateSqlAsync(long templateId, string baseUrl)
    {
        var t = await _db.Templates.Include(x => x.Boards).ThenInclude(b => b.Categories).ThenInclude(c => c.Questions).FirstOrDefaultAsync(x => x.Id == templateId);
        if (t == null) throw new InvalidOperationException("Template not found");

        // We'll generate a PL/pgSQL DO block that captures generated IDs into variables
        var sb = new StringBuilder();
        var templateSafe = t.SafeFolderName;

        sb.AppendLine("DO $$");
        sb.AppendLine("DECLARE");
        sb.AppendLine("    template_id bigint;");
        // declare a variable for each board
        for (int i = 0; i < t.Boards.Count; i++)
        {
            sb.AppendLine($"    b{i} bigint;");
        }
        sb.AppendLine("BEGIN");

        // Insert template and capture id
        sb.AppendLine($"    INSERT INTO public.\"Templates\" (\"Name\") VALUES ('{Escape(t.Name)}') RETURNING \"Id\" INTO template_id;");

        // Insert boards and capture their ids into variables
        for (int i = 0; i < t.Boards.Count; i++)
        {
            var b = t.Boards[i];
            sb.AppendLine($"    INSERT INTO public.\"Boards\" (\"TemplateId\", \"Order\") VALUES (template_id, {b.Order}) RETURNING \"Id\" INTO b{i};");
        }

        // Insert questions referencing the corresponding board variable
        for (int bi = 0; bi < t.Boards.Count; bi++)
        {
            var b = t.Boards[bi];
            foreach (var c in b.Categories.OrderBy(c => c.Name))
            {
                foreach (var q in c.Questions)
                {
                    var qType = (int)q.QuestionType;
                    var aType = (int)q.AnswerType;
                    var points = q.Points;
                    var categoryVal = string.IsNullOrEmpty(c.Name) ? "NULL" : $"'{Escape(c.Name)}'";
                    var questionTextVal = string.IsNullOrEmpty(q.QuestionText) ? "NULL" : $"'{Escape(q.QuestionText)}'";

                    // Build a fully-qualified media URL (baseUrl/template/filename) from a stored filename.
                    string ImageUrlVal(string? filename)
                    {
                        if (string.IsNullOrEmpty(filename)) return "NULL";
                        var full = baseUrl.TrimEnd('/') + "/" + templateSafe + "/" + filename;
                        return $"'{Escape(full)}'";
                    }

                    // Build columns/values for the target TPH "Questions" table. Image and text
                    // questions populate disjoint sets of columns; the rest default to NULL.
                    var cols = new List<string>() { "\"QuestionType\"", "\"AnswerType\"", "\"Points\"", "\"Category\"", "\"BoardId\"" };
                    var vals = new List<string>() { qType.ToString(), aType.ToString(), points.ToString(), categoryVal, $"b{bi}" };

                    if (q.QuestionType == QuizMaker.DomainObjects.QuestionType.Image)
                    {
                        var answerTextVal = string.IsNullOrEmpty(q.AnswerText) ? "NULL" : $"'{Escape(q.AnswerText)}'";
                        var imageSizeVal = ((int)(q.ImageSize ?? QuizMaker.DomainObjects.ImageSize.Medium)).ToString();

                        cols.Add("\"QuestionText\"");
                        cols.Add("\"QuestionImageUri\"");
                        cols.Add("\"AnswerText\"");
                        cols.Add("\"AnswerImageUri\"");
                        cols.Add("\"ImageSize\"");

                        vals.Add(questionTextVal);
                        vals.Add(ImageUrlVal(q.QuestionImageUri));
                        vals.Add(answerTextVal);
                        vals.Add(ImageUrlVal(q.AnswerImageUri));
                        vals.Add(imageSizeVal);
                    }
                    else
                    {
                        // Text questions use TextQuestion_QuestionText + CorrectAnswer.
                        var correctVal = string.IsNullOrEmpty(q.CorrectAnswer) ? "NULL" : $"'{Escape(q.CorrectAnswer)}'";

                        cols.Add("\"TextQuestion_QuestionText\"");
                        cols.Add("\"CorrectAnswer\"");

                        vals.Add(questionTextVal);
                        vals.Add(correctVal);
                    }

                    sb.AppendLine($"    INSERT INTO public.\"Questions\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)});");
                }
            }
        }

        sb.AppendLine("END $$;");
        return sb.ToString();
    }

    private static string Escape(string? s)
    {
        if (s == null) return "NULL";
        return s.Replace("'", "''");
    }
}
