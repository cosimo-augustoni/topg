using topg.Web.Quiz.Management;

namespace topg.Web.Quiz
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddQuiz(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<SessionHandler>();

            serviceCollection.AddHostedService<SessionCleanupService>();

            return serviceCollection;
        }
    }
}
