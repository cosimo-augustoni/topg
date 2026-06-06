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
                    var correctVal = string.IsNullOrEmpty(q.CorrectAnswer) ? "NULL" : $"'{Escape(q.CorrectAnswer)}'";
                    string imageVal = "NULL";
                    if (q.QuestionType == QuizMaker.DomainObjects.QuestionType.Image)
                    {
                        var fname = q.ImageUri ?? string.Empty;
                        if (!string.IsNullOrEmpty(fname))
                        {
                            var full = baseUrl.TrimEnd('/') + "/" + templateSafe + "/" + fname;
                            imageVal = $"'{Escape(full)}'";
                        }
                    }

                    // Build columns/values depending on question type (TPH + TextQuestion_* for text questions)
                    var cols = new List<string>() { "\"QuestionType\"", "\"AnswerType\"", "\"Points\"", "\"Category\"", "\"BoardId\"" };
                    var vals = new List<string>() { qType.ToString(), aType.ToString(), points.ToString(), categoryVal, $"b{bi}" };

                    if (q.QuestionType == QuizMaker.DomainObjects.QuestionType.Image)
                    {
                        // Image questions: populate QuestionText, CorrectAnswer, ImageUri; TextQuestion_* = NULL
                        cols.Add("\"QuestionText\"");
                        cols.Add("\"CorrectAnswer\"");
                        cols.Add("\"ImageUri\"");

                        vals.Add(questionTextVal == "NULL" ? "NULL" : questionTextVal);
                        vals.Add(correctVal == "NULL" ? "NULL" : correctVal);
                        vals.Add(imageVal == "NULL" ? "NULL" : imageVal);

                        // Explicitly add NULLs for TextQuestion_* columns to be explicit (optional)
                        cols.Add("\"TextQuestion_QuestionText\"");
                        cols.Add("\"TextQuestion_CorrectAnswer\"");
                        vals.Add("NULL");
                        vals.Add("NULL");
                    }
                    else
                    {
                        // Text questions: set QuestionText/CorrectAnswer/ImageUri = NULL; use TextQuestion_* columns
                        cols.Add("\"QuestionText\"");
                        cols.Add("\"CorrectAnswer\"");
                        cols.Add("\"ImageUri\"");
                        vals.Add("NULL");
                        vals.Add("NULL");
                        vals.Add("NULL");

                        cols.Add("\"TextQuestion_QuestionText\"");
                        cols.Add("\"TextQuestion_CorrectAnswer\"");
                        var tQuestionTextVal = string.IsNullOrEmpty(q.QuestionText) ? "NULL" : $"'{Escape(q.QuestionText)}'";
                        var tCorrectVal = string.IsNullOrEmpty(q.CorrectAnswer) ? "NULL" : $"'{Escape(q.CorrectAnswer)}'";
                        vals.Add(tQuestionTextVal);
                        vals.Add(tCorrectVal);
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
