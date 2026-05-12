using topg.Web.Templating;

namespace topg.Web.Quiz
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddQuiz(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<SessionHandler>();

            return serviceCollection;
        }
    }
}
