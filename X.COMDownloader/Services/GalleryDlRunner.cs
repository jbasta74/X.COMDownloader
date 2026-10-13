using System.Diagnostics;
using System.IO;
using System.Text;

namespace XComDownloader.Services;

/// <summary>Spouští gallery-dl pro jednu URL a předává jeho výstup.</summary>
public static class GalleryDlRunner
{
    /// <param name="cookiesFile">Soubor cookies.txt, který se má použít (null = podle nastavení / bez cookies).</param>
    /// <param name="onOutput">Řádek stdout: cesta staženého souboru; přeskočené soubory začínají "# ".</param>
    /// <param name="onError">Řádek stderr: varování, chyby a informace gallery-dl.</param>
    /// <returns>Návratový kód gallery-dl (0 = v pořádku).</returns>
    public static async Task<int> RunAsync(
        string url,
        AppSettings settings,
        string? cookiesFile,
        IProgress<string> onOutput,
        IProgress<string> onError,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = settings.GalleryDlPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        if (cookiesFile != null)
        {
            psi.ArgumentList.Add("--cookies");
            psi.ArgumentList.Add(cookiesFile);
        }
        else if (settings.CookieSource == CookieSource.Browser && !string.IsNullOrWhiteSpace(settings.Browser))
        {
            psi.ArgumentList.Add("--cookies-from-browser");
            psi.ArgumentList.Add(settings.Browser);
        }

        Directory.CreateDirectory(settings.TargetFolder);
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(settings.TargetFolder);

        if (settings.UseArchive)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppSettings.ArchiveFile)!);
            psi.ArgumentList.Add("--download-archive");
            psi.ArgumentList.Add(AppSettings.ArchiveFile);
        }

        psi.ArgumentList.Add(url);

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onOutput.Report(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onError.Report(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* už skončil */ }
            throw;
        }

        return process.ExitCode;
    }
}
