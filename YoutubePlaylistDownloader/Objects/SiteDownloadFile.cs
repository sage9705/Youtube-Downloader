using System.ComponentModel;

namespace YoutubePlaylistDownloader.Objects;

public class SiteDownloadFile : INotifyPropertyChanged
{
    private bool isSelected = true;

    public string Url { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }
    public string SubFolder { get; set; }
    public long Size { get; set; }

    public string SizeDisplay => Size > 0
        ? Size >= 1048576 ? $"{Size / 1048576.0:F1} MB" : $"{Size / 1024.0:F1} KB"
        : "\u2014";

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
