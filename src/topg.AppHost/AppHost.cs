using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("db")
    .WithPgWeb()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("topg");

var migrationService = builder.AddProject<topg_MigrationService>("migrationservice")
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<topg_Web>("web")
    .WithReference(db)
    .WithReference(migrationService)
    .WaitForCompletion(migrationService);

builder.Build().Run();