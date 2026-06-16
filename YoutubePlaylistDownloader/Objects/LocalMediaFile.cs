namespace YoutubePlaylistDownloader.Objects;

public class LocalMediaFile : INotifyPropertyChanged
{
    private bool isSelected = true;
    private string artist;
    private string title;

    public string FilePath { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }
    public string SizeDisplay { get; set; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected != value)
            {
                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public string Artist
    {
        get => artist;
        set
        {
            if (artist != value)
            {
                artist = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Artist)));
            }
        }
    }

    public string Title
    {
        get => title;
        set
        {
            if (title != value)
            {
                title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
