using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using topg.Web.Components;
using topg.Web.Extensions;
using topg.Web.Quiz;
using topg.Web.Templating;
using topg.Web.Templating.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDataProtection().PersistKeysToDbContext<QuizContext>().SetApplicationName("topg");

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
