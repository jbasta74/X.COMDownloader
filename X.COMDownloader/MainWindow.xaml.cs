using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using XComDownloader.Models;
using XComDownloader.Services;

namespace XComDownloader;

public partial class MainWindow : Window
{
    private const string BookmarksUrl = "https://x.com/i/bookmarks";

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly ObservableCollection<DownloadItem> _queue = [];
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        BrowserCombo.ItemsSource = AppSettings.Browsers;
        CookieSourceCombo.ItemsSource = AppSettings.CookieSources;
        DataContext = _settings;
        QueueList.ItemsSource = _queue;
    }

    private IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCookieUi();
        await RefreshLoginStatusAsync();
    }

    // ---------- přihlášení / cookies ----------

    private CookieSource SelectedCookieSource =>
        CookieSourceCombo.SelectedValue is CookieSource cs ? cs : _settings.CookieSource;

    private async void CookieSourceCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCookieUi();
        await RefreshLoginStatusAsync();
    }

    /// <summary>Povolí jen ovládací prvky, které patří ke zvolenému zdroji cookies.</summary>
    private void UpdateCookieUi()
    {
        var source = SelectedCookieSource;
        AppLoginPanel.Visibility = source == CookieSource.AppLogin ? Visibility.Visible : Visibility.Collapsed;
        CookiesBox.IsEnabled = BrowseCookiesButton.IsEnabled = source == CookieSource.CookiesFile;
        BrowserCombo.IsEnabled = source == CookieSource.Browser;
    }

    private async Task RefreshLoginStatusAsync()
    {
        if (SelectedCookieSource != CookieSource.AppLogin) return;
        try
        {
            var cookies = await XSession.GetCookiesAsync(Hwnd);
            bool loggedIn = XSession.IsLoggedIn(cookies);
            LoginStatusText.Text = loggedIn ? "✔ Přihlášeno" : "Nepřihlášeno";
            LoginStatusText.Foreground = loggedIn ? Brushes.DarkGreen : Brushes.DarkRed;
            LogoutButton.IsEnabled = loggedIn;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            LoginStatusText.Text = "Chybí WebView2 Runtime";
            LoginStatusText.Foreground = Brushes.DarkRed;
        }
        catch (Exception ex)
        {
            LoginStatusText.Text = "Chyba";
            Log($"!!! Nepodařilo se zjistit stav přihlášení: {ex.Message}");
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow { Owner = this }.ShowDialog();
        await RefreshLoginStatusAsync();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Opravdu se odhlásit? Smažou se cookies uložené v aplikaci.",
                "Odhlásit", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            await XSession.LogoutAsync(Hwnd);
        }
        catch (Exception ex)
        {
            Log($"!!! Odhlášení selhalo: {ex.Message}");
        }
        await RefreshLoginStatusAsync();
    }

    /// <summary>
    /// Připraví soubor cookies podle zvoleného zdroje.
    /// Vrací (true, cesta | null) pokud lze pokračovat, (false, null) pokud ne.
    /// </summary>
    private async Task<(bool ok, string? cookiesFile)> PrepareCookiesAsync()
    {
        switch (_settings.CookieSource)
        {
            case CookieSource.AppLogin:
                try
                {
                    var file = await XSession.ExportCookiesAsync(Hwnd);
                    if (file != null)
                    {
                        Log("Cookies načteny z přihlášení v aplikaci.");
                        return (true, file);
                    }
                    var answer = MessageBox.Show(this,
                        "Nejste v aplikaci přihlášen k X.\n\nPřihlásit se teď?",
                        "Přihlášení", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Yes)
                    {
                        new LoginWindow { Owner = this }.ShowDialog();
                        await RefreshLoginStatusAsync();
                        file = await XSession.ExportCookiesAsync(Hwnd);
                        if (file != null) return (true, file);
                    }
                    else if (answer == MessageBoxResult.No)
                    {
                        Log("Pokračuji bez přihlášení.");
                        return (true, null);
                    }
                    return (false, null);
                }
                catch (Exception ex)
                {
                    Log($"!!! Cookies z přihlášení se nepodařilo načíst: {ex.Message}");
                    return (false, null);
                }

            case CookieSource.CookiesFile:
                var path = _settings.CookiesFile.Trim().Trim('"');
                if (File.Exists(path)) return (true, path);
                Log($"!!! Soubor cookies nebyl nalezen: {path}");
                return (false, null);

            default:
                return (true, null);
        }
    }

    // ---------- fronta ----------

    private void Add_Click(object sender, RoutedEventArgs e) => AddFromBox();

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddFromBox();
    }

    private void AddBookmarks_Click(object sender, RoutedEventArgs e) => Enqueue(BookmarksUrl);

    private void AddFromBox()
    {
        // více položek lze oddělit mezerou, čárkou nebo středníkem
        var parts = UrlBox.Text.Split([' ', ',', ';', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
            Enqueue(NormalizeUrl(part));
        UrlBox.Clear();
        UrlBox.Focus();
    }

    /// <summary>"@ucet" nebo "ucet" převede na URL médií profilu, hotové URL nechá být.</summary>
    private static string NormalizeUrl(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return input;
        if (input.StartsWith("x.com/", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("twitter.com/", StringComparison.OrdinalIgnoreCase))
            return "https://" + input;
        return $"https://x.com/{input.TrimStart('@')}/media";
    }

    private void Enqueue(string url)
    {
        if (_queue.Any(i => i.Url == url && i.Status is DownloadStatus.Pending or DownloadStatus.Running))
            return;
        _queue.Add(new DownloadItem(url));
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in QueueList.SelectedItems.Cast<DownloadItem>().ToList())
            if (item.Status != DownloadStatus.Running)
                _queue.Remove(item);
    }

    private void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _queue.Where(i => i.Status is not (DownloadStatus.Pending or DownloadStatus.Running)).ToList())
            _queue.Remove(item);
    }

    // ---------- stahování ----------

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return;

        _settings.Save();

        var (ok, cookiesFile) = await PrepareCookiesAsync();
        if (!ok) return;

        _cts = new CancellationTokenSource();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        Progress.IsIndeterminate = true;

        try
        {
            // položky přidané během běhu se zpracují také
            while (_queue.FirstOrDefault(i => i.Status == DownloadStatus.Pending) is { } item)
            {
                await RunItemAsync(item, cookiesFile, _cts.Token);
                if (_cts.IsCancellationRequested) break;
            }
        }
        finally
        {
            XSession.DeleteExportedCookies();
            _cts.Dispose();
            _cts = null;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            Progress.IsIndeterminate = false;
        }
    }

    private async Task RunItemAsync(DownloadItem item, string? cookiesFile, CancellationToken ct)
    {
        item.Status = DownloadStatus.Running;
        Log($"=== {item.Url} ===");

        // Progress<T> vytvořený v UI vlákně doručuje hlášení zpět do UI vlákna
        var output = new Progress<string>(line =>
        {
            if (line.StartsWith("# "))
            {
                item.Skipped++;
                return; // přeskočené soubory do logu nevypisujeme
            }
            item.Downloaded++;
            item.LastFile = Path.GetFileName(line);
            Log(line);
        });
        var error = new Progress<string>(Log);

        try
        {
            int exitCode = await GalleryDlRunner.RunAsync(item.Url, _settings, cookiesFile, output, error, ct);
            await Task.Yield(); // nechá doběhnout poslední hlášení průběhu
            item.Status = exitCode == 0 ? DownloadStatus.Done : DownloadStatus.Failed;
            Log($"--- dokončeno (kód {exitCode}): staženo {item.Downloaded}, přeskočeno {item.Skipped}");
        }
        catch (OperationCanceledException)
        {
            item.Status = DownloadStatus.Cancelled;
            Log("--- zrušeno uživatelem");
        }
        catch (Win32Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            Log($"!!! gallery-dl se nepodařilo spustit ({ex.Message}). Zkontrolujte cestu v nastavení.");
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            Log($"!!! {ex.Message}");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ---------- nastavení a ostatní ----------

    private void BrowseGalleryDl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Vyberte gallery-dl",
            Filter = "gallery-dl|gallery-dl*.exe|Programy (*.exe)|*.exe|Všechny soubory|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            GalleryDlBox.Text = dialog.FileName;
    }

    private void BrowseCookies_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Vyberte soubor cookies.txt",
            Filter = "Cookies (*.txt)|*.txt|Všechny soubory|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            CookiesBox.Text = dialog.FileName;
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Cílová složka pro stažená média" };
        if (Directory.Exists(_settings.TargetFolder))
            dialog.InitialDirectory = _settings.TargetFolder;
        if (dialog.ShowDialog(this) == true)
            TargetBox.Text = dialog.FolderName;
    }

    private void OpenTarget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetFullPath(_settings.TargetFolder);
            Directory.CreateDirectory(folder);
            // Složku otevíráme explicitně přes explorer.exe: přes ShellExecute by Windows
            // složku s koncovkou ".COM" (např. "X.COM") zkusily spustit jako program.
            var psi = new ProcessStartInfo("explorer.exe");
            psi.ArgumentList.Add(folder);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"!!! {ex.Message}");
        }
    }

    private void Log(string line)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _cts?.Cancel();
        XSession.DeleteExportedCookies();
        try { _settings.Save(); } catch { /* nastavení se nepodařilo uložit */ }
    }
}
