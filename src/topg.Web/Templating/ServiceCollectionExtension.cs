namespace topg.Web.Templating
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddTemplating(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ITemplateService, TemplateService>();

            return serviceCollection;
        }
    }
}
