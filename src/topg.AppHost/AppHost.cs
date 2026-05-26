using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var registryEndpoint = builder.AddParameterFromConfiguration("registryEndpoint", "REGISTRY_ENDPOINT");
var registryRepository = builder.AddParameterFromConfiguration("registryRepository", "REGISTRY_REPOSITORY");

var registry = builder.AddContainerRegistry("kallisto", registryEndpoint, registryRepository);

var postgres = AddPostgres(builder);
var db = postgres.AddDatabase("topg");

var migrationService = builder.AddProject<topg_MigrationService>("migrationservice")
    .WithReference(db)
    .WithContainerRegistry(registry)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Networks = ["topg_internal"];
    });

if (builder.Environment.IsDevelopment())
{
    migrationService.WaitFor(db);
}

var web = builder.AddProject<topg_Web>("web")
    .WithReference(db)
    .WithReference(migrationService)
    .WithContainerRegistry(registry)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Networks = ["topg_internal, topg"];
        service.Expose = [];
    });

if (builder.Environment.IsDevelopment())
{
    web.WaitForCompletion(migrationService);
}

builder.AddDockerComposeEnvironment("docker-compose")
    .ConfigureComposeFile(file =>
    {
        file.Networks = new Dictionary<string, Network>
        {
            { "topg_internal", new Network() { Name = "topg_internal", Driver = "overlay", External = false } },
            { "topg", new Network() { Name = "topg", Driver = "overlay", External = true } }
        };
    })
    .WithDashboard(false);

builder.Build().Run();

IResourceBuilder<PostgresServerResource> AddPostgres(IDistributedApplicationBuilder distributedApplicationBuilder)
{
    var resourceBuilder = distributedApplicationBuilder.AddPostgres("db")
        .WithLifetime(ContainerLifetime.Persistent)
        .PublishAsDockerComposeService((resource, service) =>
        {
            service.Networks = ["topg_internal"];
        });

    if (distributedApplicationBuilder.Environment.IsDevelopment())
    {
        resourceBuilder.WithPgWeb();
    }
    else
    {
        var postgresDataPath = distributedApplicationBuilder.AddParameter("PostgresDataPath");
        resourceBuilder.WithDataBindMount(source: $"${{{postgresDataPath.Resource.Name}}}");
    }

    return resourceBuilder;
}