namespace topg.Web.Templating
{
    public static class ServiceCollectionExtenion
    {
        public static IServiceCollection AddTemplating(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ITemplateService, TemplateService>();

            return serviceCollection;
        }
    }
}
