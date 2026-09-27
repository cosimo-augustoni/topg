using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using topg.Web.Components;
using topg.Web.Extensions;
using topg.Web.Quiz;
using topg.Web.Templating;
using topg.Web.Templating.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDataProtection().PersistKeysToDbContext<QuizContext>().SetApplicationName("topg");

builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.AddNpgsqlDbContext<QuizContext>("topg");
builder.Services.AddTemplating();
builder.Services.AddQuiz();

var app = builder.Build();

// In non-development environments (e.g. Docker Swarm), depends_on is not supported.
// Wait here until the migration service has applied all pending migrations before serving traffic.
if (!app.Environment.IsDevelopment())
{
    await app.WaitForMigrationsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(topg.Web.Client._Imports).Assembly);

app.Run();
