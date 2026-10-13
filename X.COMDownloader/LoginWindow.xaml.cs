using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using XComDownloader.Services;

namespace XComDownloader;

/// <summary>Okno s vestavěným prohlížečem pro jednorázové přihlášení k X.</summary>
public partial class LoginWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _checking;

    public LoginWindow()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await CheckLoginAsync();
    }

    /// <summary>True, pokud byl uživatel při zavření okna přihlášen.</summary>
    public bool LoggedIn { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await XSession.GetEnvironmentAsync();
            await Browser.EnsureCoreWebView2Async(env);

            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(XSession.XUrl);
            Browser.CoreWebView2.Navigate(XSession.IsLoggedIn(cookies)
                ? XSession.XUrl + "/home"
                : XSession.XUrl + "/i/flow/login");

            // X je single-page aplikace, po přihlášení se nemusí vyvolat klasická navigace -> kontrolujeme průběžně
            _timer.Start();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                "Na počítači chybí Microsoft Edge WebView2 Runtime.\nStáhněte ho z https://developer.microsoft.com/microsoft-edge/webview2/",
                "Přihlášení k X", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Přihlášení k X", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async Task CheckLoginAsync()
    {
        if (_checking || Browser.CoreWebView2 == null) return;
        _checking = true;
        try
        {
            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(XSession.XUrl);
            bool loggedIn = XSession.IsLoggedIn(cookies);
            if (loggedIn && !LoggedIn)
            {
                StatusText.Text = "✔ Přihlášeno. Okno můžete zavřít tlačítkem Hotovo.";
                StatusText.Foreground = Brushes.DarkGreen;
                DoneButton.IsDefault = true;
            }
            LoggedIn = loggedIn;
        }
        catch
        {
            // okno se právě zavírá
        }
        finally
        {
            _checking = false;
        }
    }

    private async void Done_Click(object sender, RoutedEventArgs e)
    {
        await CheckLoginAsync();
        Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        Browser.Dispose();
    }
}
