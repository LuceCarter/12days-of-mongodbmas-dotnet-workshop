using Part4B.Starter;

namespace Part4B.Tests;

/// <summary>Not a step: <c>ElfLlm</c> is already written for you. These check that it points at the right place, so that
/// the Google AI Studio route and the Google Cloud route can't quietly drift. They pass from the start. No network, no keys.</summary>
public sealed class ElfLlmTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;

    [Fact(DisplayName = "Already written — a Gemini key means Google AI Studio, with its default model")]
    public void AiStudioIsTheDefault()
    {
        var (url, credential, model) = ElfLlm.ResolveFromEnvironment(Env(("GEMINI_API_KEY", "studio-key")));

        Assert.Equal(ElfLlm.DefaultBaseUrl, url);
        Assert.Equal("studio-key", credential);
        Assert.Equal(ElfLlm.DefaultModel, model);
    }

    [Fact(DisplayName = "Already written — with no key but a Google Cloud project it uses Vertex AI, and its model name has the google/ prefix")]
    public void GoogleCloudIsUsedWhenThereIsAProjectAndNoKey()
    {
        var (url, credential, model) = ElfLlm.ResolveFromEnvironment(Env(("GOOGLE_CLOUD_PROJECT", "my-project")), accessToken: () => "token-from-gcloud");

        Assert.Equal("https://aiplatform.googleapis.com/v1/projects/my-project/locations/global/endpoints/openapi/", url);
        Assert.Equal("token-from-gcloud", credential);
        Assert.Equal($"google/{ElfLlm.DefaultModel}", model);
    }

    [Fact(DisplayName = "Already written — the Google Cloud location, model and a ready-made token can all be chosen")]
    public void GoogleCloudCanBeConfigured()
    {
        var (url, credential, model) = ElfLlm.ResolveFromEnvironment(
            Env(("GOOGLE_CLOUD_PROJECT", "p"), ("GOOGLE_CLOUD_LOCATION", "europe-west2"), ("GEMINI_MODEL", "google/some-model"), ("GOOGLE_CLOUD_ACCESS_TOKEN", "my-token")),
            accessToken: () => throw new InvalidOperationException("gcloud shouldn't be asked when a token was supplied"));

        Assert.StartsWith("https://europe-west2-aiplatform.googleapis.com/v1/projects/p/locations/europe-west2/", url);
        Assert.Equal("my-token", credential);
        Assert.Equal("google/some-model", model);
    }

    [Fact(DisplayName = "Already written — the global location uses the hostname with no region in it")]
    public void GlobalHasNoRegionInTheHostname()
    {
        var (url, _, _) = ElfLlm.ResolveFromEnvironment(Env(("GOOGLE_CLOUD_PROJECT", "p"), ("GOOGLE_CLOUD_LOCATION", "global")), accessToken: () => "t");

        Assert.Equal("https://aiplatform.googleapis.com/v1/projects/p/locations/global/endpoints/openapi/", url);
    }

    [Fact(DisplayName = "Already written — a Gemini key wins over a Google Cloud project, and with neither it explains both routes")]
    public void KeyWinsAndNothingExplains()
    {
        var both = ElfLlm.ResolveFromEnvironment(Env(("GEMINI_API_KEY", "k"), ("GOOGLE_CLOUD_PROJECT", "p")), accessToken: () => "unused");
        Assert.Equal(ElfLlm.DefaultBaseUrl, both.BaseUrl);

        var error = Assert.Throws<InvalidOperationException>(() => ElfLlm.ResolveFromEnvironment(Env()));
        Assert.Contains("GEMINI_API_KEY", error.Message);
        Assert.Contains("GOOGLE_CLOUD_PROJECT", error.Message);
    }
}
