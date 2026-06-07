using Microsoft.EntityFrameworkCore;
using QuizMaker.Data.Entities;
using QuizMaker.DomainObjects;

namespace QuizMaker.Data.Repositories;

public class QuizRepository : IQuizRepository
{
    private readonly QuizDbContext _db;

    public QuizRepository(QuizDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetTemplateSafeFolderNameAsync(long templateId)
    {
        var t = await _db.Templates.FindAsync(templateId);
        return t?.SafeFolderName ?? string.Empty;
    }

    public async Task<List<QuizTemplate>> GetAllTemplatesAsync()
    {
        var templates = await _db.Templates.Include(t => t.Boards).ThenInclude(b => b.Categories).ThenInclude(c => c.Questions).ToListAsync();
        return templates.Select(ToDomain).ToList();
    }

    public async Task<QuizTemplate?> GetTemplateAsync(long id)
    {
        var t = await _db.Templates.Include(t => t.Boards).ThenInclude(b => b.Categories).ThenInclude(c => c.Questions).FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? null : ToDomain(t);
    }

    public async Task<QuizTemplate> CreateTemplateAsync(QuizTemplate template)
    {
        var entity = new TemplateEntity
        {
            Name = template.Name,
            SafeFolderName = Slugify(template.Name),
            MediaCounter = 0
        };
        _db.Templates.Add(entity);
        await _db.SaveChangesAsync();
        return template with { Id = entity.Id };
    }

    public async Task UpdateTemplateAsync(QuizTemplate template)
    {
        var e = await _db.Templates.FindAsync(template.Id);
        if (e == null) return;
        var oldSafe = e.SafeFolderName;
        e.Name = template.Name;
        e.SafeFolderName = Slugify(template.Name);
        if (oldSafe != e.SafeFolderName)
        {
            // folder rename will be handled by MediaService externally
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTemplateAsync(long id)
    {
        var e = await _db.Templates.FindAsync(id);
        if (e == null) return;
        _db.Templates.Remove(e);
        await _db.SaveChangesAsync();
    }

    public async Task<QuizMaker.DomainObjects.Board> CreateBoardAsync(long templateId, int order)
    {
        var be = new BoardEntity { TemplateId = templateId, Order = order };
        _db.Boards.Add(be);
        await _db.SaveChangesAsync();
        return new QuizMaker.DomainObjects.Board
        {
            Id = be.Id,
            Template = new QuizMaker.DomainObjects.QuizTemplate { Id = templateId, Name = string.Empty, Boards = new List<QuizMaker.DomainObjects.Board>() },
            Order = be.Order,
            Questions = new List<QuizMaker.DomainObjects.Question>(),
            Categories = new List<QuizMaker.DomainObjects.Category>()
        };
    }

    public async Task DeleteBoardAsync(long boardId)
    {
        var b = await _db.Boards.FindAsync(boardId);
        if (b == null) return;
        _db.Boards.Remove(b);
        await _db.SaveChangesAsync();
    }

    public async Task<QuizMaker.DomainObjects.Category> CreateCategoryAsync(long boardId, string name)
    {
        var ce = new CategoryEntity { BoardId = boardId, Name = name };
        _db.Categories.Add(ce);
        await _db.SaveChangesAsync();
        return new QuizMaker.DomainObjects.Category { Name = name };
    }

    public async Task<List<QuizMaker.DomainObjects.Category>> GetCategoriesByBoardAsync(long boardId)
    {
        var cats = await _db.Categories.Where(c => c.BoardId == boardId).ToListAsync();
        return cats.Select(c => new QuizMaker.DomainObjects.Category { Id = c.Id, Name = c.Name }).OrderBy(c => c.Name).ToList();
    }

    public async Task UpdateCategoryAsync(long categoryId, string newName)
    {
        var c = await _db.Categories.FindAsync(categoryId);
        if (c == null) return;
        c.Name = newName;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteCategoryAsync(long categoryId)
    {
        var c = await _db.Categories.FindAsync(categoryId);
        if (c == null) return;
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
    }

    public async Task<QuizMaker.DomainObjects.Question> CreateQuestionAsync(long categoryId, QuizMaker.DomainObjects.Question question)
    {
        var qe = new QuestionEntity
        {
            CategoryId = categoryId,
            QuestionType = question.QuestionType,
            AnswerType = question.AnswerType,
            Points = question.Points,
            QuestionText = (question as QuizMaker.DomainObjects.TextQuestion)?.QuestionText ?? (question as QuizMaker.DomainObjects.ImageQuestion)?.QuestionText,
            CorrectAnswer = (question as QuizMaker.DomainObjects.TextQuestion)?.CorrectAnswer,
            QuestionImageUri = (question as QuizMaker.DomainObjects.ImageQuestion)?.QuestionImageUri,
            AnswerText = (question as QuizMaker.DomainObjects.ImageQuestion)?.AnswerText,
            AnswerImageUri = (question as QuizMaker.DomainObjects.ImageQuestion)?.AnswerImageUri,
            ImageSize = (question as QuizMaker.DomainObjects.ImageQuestion)?.ImageSize
        };
        _db.Questions.Add(qe);
        await _db.SaveChangesAsync();
        return question with { Id = qe.Id };
    }

    public async Task UpdateQuestionAsync(QuizMaker.DomainObjects.Question question)
    {
        var qe = await _db.Questions.FindAsync(question.Id);
        if (qe == null) return;
        qe.QuestionType = question.QuestionType;
        qe.AnswerType = question.AnswerType;
        qe.Points = question.Points;
        qe.QuestionText = (question as QuizMaker.DomainObjects.TextQuestion)?.QuestionText ?? (question as QuizMaker.DomainObjects.ImageQuestion)?.QuestionText;
        qe.CorrectAnswer = (question as QuizMaker.DomainObjects.TextQuestion)?.CorrectAnswer;
        qe.QuestionImageUri = (question as QuizMaker.DomainObjects.ImageQuestion)?.QuestionImageUri;
        qe.AnswerText = (question as QuizMaker.DomainObjects.ImageQuestion)?.AnswerText;
        qe.AnswerImageUri = (question as QuizMaker.DomainObjects.ImageQuestion)?.AnswerImageUri;
        qe.ImageSize = (question as QuizMaker.DomainObjects.ImageQuestion)?.ImageSize;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteQuestionAsync(long questionId)
    {
        var q = await _db.Questions.FindAsync(questionId);
        if (q == null) return;
        _db.Questions.Remove(q);
        await _db.SaveChangesAsync();
    }

    public async Task<QuizMaker.DomainObjects.Question?> GetQuestionAsync(long questionId)
    {
        var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == questionId);
        if (q == null) return null;

        // Need category name: find category entity
        var c = await _db.Categories.FindAsync(q.CategoryId);
        var categoryName = c?.Name ?? string.Empty;

        return MapQuestion(q, categoryName, 0);
    }

    private static QuizTemplate ToDomain(TemplateEntity e)
    {
        var boards = e.Boards.OrderBy(b => b.Order).Select(b => {
            var questions = b.Categories.SelectMany(c => c.Questions.Select(q => MapQuestion(q, c.Name, b.Id))).ToList();
            var categories = b.Categories.Select(c => new Category { Id = c.Id, Name = c.Name }).ToList();
            return new Board
            {
                Id = b.Id,
                Template = new QuizTemplate { Id = e.Id, Name = e.Name, Boards = new List<Board>() },
                Order = b.Order,
                Questions = questions,
                Categories = categories
            };
        }).ToList();

        return new QuizTemplate { Id = e.Id, Name = e.Name, Boards = boards };
    }

    private static Question MapQuestion(QuestionEntity q, string categoryName, long boardId)
    {
        if (q.QuestionType == QuestionType.Image)
        {
            return new ImageQuestion
            {
                Id = q.Id,
                QuestionType = q.QuestionType,
                AnswerType = q.AnswerType,
                Points = q.Points,
                Category = categoryName,
                QuestionText = q.QuestionText ?? string.Empty,
                QuestionImageUri = q.QuestionImageUri ?? string.Empty,
                AnswerText = q.AnswerText ?? string.Empty,
                AnswerImageUri = q.AnswerImageUri ?? string.Empty,
                ImageSize = q.ImageSize ?? QuizMaker.DomainObjects.ImageSize.Small
            };
        }

        return new TextQuestion
        {
            Id = q.Id,
            QuestionType = q.QuestionType,
            AnswerType = q.AnswerType,
            Points = q.Points,
            Category = categoryName,
            QuestionText = q.QuestionText ?? string.Empty,
            CorrectAnswer = q.CorrectAnswer ?? string.Empty
        };
    }

    private static string Slugify(string name)
    {
        var s = name.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else sb.Append('-');
        }
        return sb.ToString();
    }
}
