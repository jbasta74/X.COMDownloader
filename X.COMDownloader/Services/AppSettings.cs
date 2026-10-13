using System.IO;
using System.Text.Json;

namespace XComDownloader.Services;

/// <summary>Odkud gallery-dl dostane cookies (přihlášení k X).</summary>
public enum CookieSource
{
    /// <summary>Přihlášení ve vestavěném prohlížeči aplikace (WebView2).</summary>
    AppLogin,
    /// <summary>Ručně exportovaný soubor cookies.txt.</summary>
    CookiesFile,
    /// <summary>gallery-dl si je přečte z prohlížeče (--cookies-from-browser).</summary>
    Browser,
    /// <summary>Bez přihlášení.</summary>
    None
}

/// <summary>Nastavení aplikace, ukládá se do %AppData%\XComDownloader\settings.json.</summary>
public class AppSettings
{
    public static readonly string[] Browsers =
        ["firefox", "chrome", "edge", "brave", "opera", "vivaldi", "chromium"];

    public static readonly KeyValuePair<CookieSource, string>[] CookieSources =
    [
        new(CookieSource.AppLogin, "Přihlášení v aplikaci"),
        new(CookieSource.CookiesFile, "Soubor cookies.txt"),
        new(CookieSource.Browser, "Z prohlížeče (gallery-dl)"),
        new(CookieSource.None, "Bez přihlášení")
    ];

    public string GalleryDlPath { get; set; } = "gallery-dl";

    public string TargetFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "X.COM");

    public CookieSource CookieSource { get; set; } = CookieSource.AppLogin;

    public string Browser { get; set; } = "firefox";

    /// <summary>Soubor cookies.txt (formát Netscape) pro <see cref="CookieSource.CookiesFile"/>.</summary>
    public string CookiesFile { get; set; } = "";

    /// <summary>Stahovat jen nové soubory (gallery-dl --download-archive).</summary>
    public bool UseArchive { get; set; } = true;

    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XComDownloader");

    private static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    public static string ArchiveFile => Path.Combine(DataFolder, "archive.sqlite3");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
        }
        catch
        {
            // poškozený soubor -> výchozí nastavení
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(SettingsFile,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
