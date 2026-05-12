using topg.Web.Templating.DomainObjects;

namespace topg.Web.Templating;

public interface ITemplateService
{
    Task<List<QuizTemplate>> GetAllTemplatesAsync();
}