using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XComDownloader.Models;

public enum DownloadStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Cancelled
}

/// <summary>Jedna položka fronty (URL profilu, tweetu, záložek...).</summary>
public class DownloadItem : INotifyPropertyChanged
{
    private DownloadStatus _status = DownloadStatus.Pending;
    private int _downloaded;
    private int _skipped;
    private string _lastFile = "";

    public DownloadItem(string url) => Url = url;

    public string Url { get; }

    public DownloadStatus Status
    {
        get => _status;
        set { _status = value; OnChanged(); OnChanged(nameof(StatusText)); }
    }

    public string StatusText => Status switch
    {
        DownloadStatus.Pending => "Čeká",
        DownloadStatus.Running => "Stahuje se…",
        DownloadStatus.Done => "Hotovo",
        DownloadStatus.Failed => "Chyba",
        DownloadStatus.Cancelled => "Zrušeno",
        _ => ""
    };

    /// <summary>Počet nově stažených souborů.</summary>
    public int Downloaded
    {
        get => _downloaded;
        set { _downloaded = value; OnChanged(); }
    }

    /// <summary>Počet přeskočených (už dříve stažených) souborů.</summary>
    public int Skipped
    {
        get => _skipped;
        set { _skipped = value; OnChanged(); }
    }

    public string LastFile
    {
        get => _lastFile;
        set { _lastFile = value; OnChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
