namespace YoutubePlaylistDownloader.Objects;

public class SelectableVideo : INotifyPropertyChanged
{
    private bool isSelected;

    public SelectableVideo(IVideo video, int index)
    {
        Video = video;
        Index = index;
        isSelected = true;
    }

    public IVideo Video { get; }
    public int Index { get; }
    public string Title => Video.Title;
    public string Channel => Video.Author.ChannelTitle;
    public string Duration => Video.Duration.HasValue
        ? Video.Duration.Value.ToString(Video.Duration.Value.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss")
        : "";

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

    public event PropertyChangedEventHandler PropertyChanged;
}
