using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SantasWorkshop.LandingPage;
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
    catch (Exception ex) when (ex is HttpRequestException or JsonException)
    {
        // There isn't one, or it isn't valid JSON. Neither is worth failing a
        // local run over — the committed appsettings.json still applies.
        Console.WriteLine($"appsettings.Development.json was ignored: {ex.Message}");
    }
}

builder.Services.AddSingleton(config);

await builder.Build().RunAsync();
