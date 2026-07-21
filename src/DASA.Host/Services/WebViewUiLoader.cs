using System.Net.Http;
using Microsoft.Web.WebView2.Core;

namespace DASA.Host.Services;

/// <summary>
/// Resolves and navigates WebView2 to the React UI (Vite dev server or packaged dist).
/// </summary>
public static class WebViewUiLoader
{
    public const string VirtualHostName = "dasa.local";
    private const string DefaultDevUrl = "http://localhost:5173/";

    public static async Task<UiLoadPlan> ResolveAsync()
    {
        var envUrl = Environment.GetEnvironmentVariable("DASA_UI_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            return UiLoadPlan.Navigate(envUrl.Trim(), "environment variable");
        }

#if DEBUG
        if (await IsDevServerAvailableAsync(DefaultDevUrl).ConfigureAwait(false))
        {
            return UiLoadPlan.Navigate(DefaultDevUrl, "Vite dev server");
        }
#endif

        var distFolder = Path.Combine(AppContext.BaseDirectory, "ui");
        var distIndex = Path.Combine(distFolder, "index.html");
        if (File.Exists(distIndex))
        {
            return UiLoadPlan.VirtualHost(distFolder, $"https://{VirtualHostName}/index.html", "packaged UI");
        }

#if DEBUG
        return UiLoadPlan.Navigate(DefaultDevUrl, "Vite dev server (fallback)");
#else
        throw new InvalidOperationException(
            "UI not found. Build the frontend (npm run build in src/dasa-ui) or set DASA_UI_URL.");
#endif
    }

    public static void ApplyVirtualHostMapping(CoreWebView2 core, string distFolder)
    {
        core.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            distFolder,
            CoreWebView2HostResourceAccessKind.Allow);
    }

    private static async Task<bool> IsDevServerAvailableAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(800) };
            using var response = await client.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UiLoadPlan
{
    public required string NavigateUrl { get; init; }
    public string? DistFolder { get; init; }
    public required string Source { get; init; }

    public static UiLoadPlan Navigate(string url, string source) =>
        new() { NavigateUrl = url, Source = source };

    public static UiLoadPlan VirtualHost(string distFolder, string url, string source) =>
        new() { NavigateUrl = url, DistFolder = distFolder, Source = source };
}
