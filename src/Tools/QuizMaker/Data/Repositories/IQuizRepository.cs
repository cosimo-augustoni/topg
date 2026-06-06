using QuizMaker.DomainObjects;

namespace QuizMaker.Data.Repositories;

public interface IQuizRepository
{
    Task<List<QuizTemplate>> GetAllTemplatesAsync();
    Task<QuizTemplate?> GetTemplateAsync(long id);
    Task<QuizTemplate> CreateTemplateAsync(QuizTemplate template);
    Task UpdateTemplateAsync(QuizTemplate template);
    Task DeleteTemplateAsync(long id);

    // Boards
    Task<QuizMaker.DomainObjects.Board> CreateBoardAsync(long templateId, int order);
    Task DeleteBoardAsync(long boardId);

    // Categories
    Task<QuizMaker.DomainObjects.Category> CreateCategoryAsync(long boardId, string name);
    Task DeleteCategoryAsync(long categoryId);
    Task<List<QuizMaker.DomainObjects.Category>> GetCategoriesByBoardAsync(long boardId);
    Task UpdateCategoryAsync(long categoryId, string newName);

    // Questions
    Task<QuizMaker.DomainObjects.Question> CreateQuestionAsync(long categoryId, QuizMaker.DomainObjects.Question question);
    Task UpdateQuestionAsync(QuizMaker.DomainObjects.Question question);
    Task<QuizMaker.DomainObjects.Question?> GetQuestionAsync(long questionId);
    Task DeleteQuestionAsync(long questionId);
    Task<string> GetTemplateSafeFolderNameAsync(long templateId);
}
