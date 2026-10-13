using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace XComDownloader.Services;

/// <summary>
/// Přihlášení k X přes vestavěný prohlížeč (WebView2) s vlastním profilem v %AppData%.
/// Cookies se z profilu čtou oficiálním API WebView2 a ukládají do cookies.txt pro gallery-dl.
/// </summary>
public static class XSession
{
    public const string XUrl = "https://x.com";

    public static string UserDataFolder => Path.Combine(AppSettings.DataFolder, "WebView2");

    /// <summary>Dočasný cookies.txt pro gallery-dl; maže se po dokončení fronty.</summary>
    public static string ExportedCookiesFile => Path.Combine(AppSettings.DataFolder, "x-cookies.txt");

    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>Jedno sdílené prostředí pro přihlašovací okno i skryté čtení cookies.</summary>
    public static Task<CoreWebView2Environment> GetEnvironmentAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(null, UserDataFolder);

    public static bool IsLoggedIn(IEnumerable<CoreWebView2Cookie> cookies) =>
        cookies.Any(c => c.Name == "auth_token" && !string.IsNullOrEmpty(c.Value));

    /// <summary>Přečte cookies x.com ze skryté instance WebView2 (bez zobrazení okna).</summary>
    /// <param name="ownerHwnd">Handle libovolného okna aplikace (WebView2 ho potřebuje jako rodiče).</param>
    public static async Task<List<CoreWebView2Cookie>> GetCookiesAsync(IntPtr ownerHwnd)
    {
        var env = await GetEnvironmentAsync();
        var controller = await env.CreateCoreWebView2ControllerAsync(ownerHwnd);
        try
        {
            controller.IsVisible = false;
            return await controller.CoreWebView2.CookieManager.GetCookiesAsync(XUrl);
        }
        finally
        {
            controller.Close();
        }
    }

    /// <summary>
    /// Uloží cookies x.com do <see cref="ExportedCookiesFile"/> ve formátu Netscape.
    /// Vrací cestu k souboru, nebo null, pokud uživatel není přihlášen.
    /// </summary>
    public static async Task<string?> ExportCookiesAsync(IntPtr ownerHwnd)
    {
        var cookies = await GetCookiesAsync(ownerHwnd);
        if (!IsLoggedIn(cookies))
            return null;

        var sb = new StringBuilder();
        sb.Append("# Netscape HTTP Cookie File\n");
        foreach (var c in cookies)
        {
            string domain = c.Domain;
            bool includeSubdomains = domain.StartsWith('.');
            // Expires je ve WPF wrapperu DateTime -> převod na Unix čas (sekundy)
            long expires = c.IsSession
                ? 0
                : new DateTimeOffset(c.Expires.ToUniversalTime()).ToUnixTimeSeconds();
            sb.Append(domain).Append('\t')
              .Append(includeSubdomains ? "TRUE" : "FALSE").Append('\t')
              .Append(string.IsNullOrEmpty(c.Path) ? "/" : c.Path).Append('\t')
              .Append(c.IsSecure ? "TRUE" : "FALSE").Append('\t')
              .Append(expires).Append('\t')
              .Append(c.Name).Append('\t')
              .Append(c.Value).Append('\n');
        }

        Directory.CreateDirectory(AppSettings.DataFolder);
        await File.WriteAllTextAsync(ExportedCookiesFile, sb.ToString(), new UTF8Encoding(false));
        return ExportedCookiesFile;
    }

    /// <summary>Odhlášení = smazání všech cookies v profilu aplikace.</summary>
    public static async Task LogoutAsync(IntPtr ownerHwnd)
    {
        var env = await GetEnvironmentAsync();
        var controller = await env.CreateCoreWebView2ControllerAsync(ownerHwnd);
        try
        {
            controller.IsVisible = false;
            controller.CoreWebView2.CookieManager.DeleteAllCookies();
        }
        finally
        {
            controller.Close();
        }
        DeleteExportedCookies();
    }

    public static void DeleteExportedCookies()
    {
        try { File.Delete(ExportedCookiesFile); } catch { /* nevadí */ }
    }
}
