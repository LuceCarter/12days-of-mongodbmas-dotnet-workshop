using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SantasWorkshop.LandingPage;
using SantasWorkshop.Content;
using SantasWorkshop.LandingPage.Config;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => http);

// The whole site is driven by this one file. Load it before the first render so
// no component ever has to cope with a half-configured world.
var config = await http.GetFromJsonAsync("appsettings.json", ChallengeConfigContext.Default.ChallengeConfig)
             ?? throw new InvalidOperationException("wwwroot/appsettings.json could not be read.");

// A git-ignored appsettings.Development.json can point a local run at a repo that
// exists, so the GitHub links are clickable before the real repo is published.
if (builder.HostEnvironment.IsDevelopment())
{
    try
    {
        var overrides = await http.GetFromJsonAsync(
            "appsettings.Development.json", ChallengeConfigContext.Default.DevelopmentOverrides);
        config = config.With(overrides);
    }
    catch (HttpRequestException)
    {
        // There isn't one. That is the normal case, so say nothing.
    }
    catch (JsonException ex)
    {
        // There is one, but it isn't valid JSON. Worth a note; not worth failing a local
        // run over — the committed appsettings.json still applies.
        Console.WriteLine($"appsettings.Development.json was ignored: {ex.Message}");
    }
}

builder.Services.AddSingleton(config);

// Which repository markdown files the site renders itself, and where. Releasing a part
// (Available: true in appsettings.json) is what makes its README and SOLUTIONS appear.
var catalog = ContentCatalog.Create(config.Parts.Select(p => new PartSource(p.Id, p.Folder, p.Title, p.Available)));
builder.Services.AddSingleton(catalog);
builder.Services.AddSingleton(new MarkdownRenderer(catalog, config.Repo));

await builder.Build().RunAsync();
