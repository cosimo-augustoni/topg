using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.Data;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Templating
{
    public class TemplateService(QuizContext quizContext) : ITemplateService
    {
        public async Task<List<QuizTemplate>> GetAllTemplatesAsync()
        {
            return await quizContext.Templates
                .Include(x => x.Boards)
                .ThenInclude(x => x.Questions)
                .ToListAsync();
        }
    }
}
