using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using topg.Web.Client.Creator.Components;
using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Images;
using topg.Web.Client.Creator.Settings;
using topg.Web.Client.Creator.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
});
builder.Services.AddSingleton<ICreatorStorage, IndexedDbCreatorStorage>();
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<ImageIntake>();
builder.Services.AddSingleton<CreatorSettingsService>();
builder.Services.AddSingleton<ProjectSession>();
builder.Services.AddSingleton<ImagePreviewCache>();
builder.Services.AddScoped<UndoSnackbar>();
builder.Services.AddScoped<IssueNavigator>();
builder.Services.AddScoped<FileDownloader>();

await builder.Build().RunAsync();
