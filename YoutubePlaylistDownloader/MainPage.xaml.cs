namespace YoutubePlaylistDownloader;

public partial class MainPage : UserControl
{
    private readonly YoutubeClient client;
    private FullPlaylist list = null;
    private IEnumerable<IVideo> VideoList;
    private Channel channel = null;
    private ObservableCollection<SelectableVideo> selectableVideos = [];
    private bool isUpdatingSelectAll = false;
    private readonly Dictionary<string, VideoQuality> Resolutions = new()
    {
        { "144p", YoutubeHelpers.Low144 },
        { "240p", YoutubeHelpers.Low240 },
        { "360p", YoutubeHelpers.Medium360 },
        { "480p", YoutubeHelpers.Medium480 },
        { "720p", YoutubeHelpers.High720 },
        { "1080p", YoutubeHelpers.High1080 },
        { "1440p", YoutubeHelpers.High1440 },
        { "2160p", YoutubeHelpers.High2160 },
        { "2880p", YoutubeHelpers.High2880 },
        { "3072p", YoutubeHelpers.High3072 },
        { "4320p", YoutubeHelpers.High4320 }
    };
    private readonly string[] VideoFileTypes = ["mp4", "mkv"];

    private readonly string[] FileTypes = ["mp3", "aac", "opus", "wav", "flac", "m4a", "ogg", "webm"];

    public MainPage()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(BulkLinksTextBox, BulkLinksTextBox_OnPaste);
        GlobalConsts.HideHomeButton();
        GlobalConsts.ShowSettingsButton();
        GlobalConsts.ShowAboutButton();
        GlobalConsts.ShowHelpButton();
        VideoList = new List<IVideo>();
        client = GlobalConsts.YoutubeClient;

        GlobalConsts.MainPage = this;
    }

    public MainPage Load()
    {
        GlobalConsts.HideHomeButton();
        GlobalConsts.ShowSettingsButton();
        GlobalConsts.ShowAboutButton();
        GlobalConsts.ShowHelpButton();
        return this;
    }

    private async void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (YoutubeHelpers.TryParsePlaylistId(PlaylistLinkTextBox.Text, out var playlistId))
            {
                _ = Task.Run(async () =>
                {
                    var basePlaylist = await client.Playlists.GetAsync(playlistId.Value).ConfigureAwait(false);
                    list = new FullPlaylist(basePlaylist, await client.Playlists.GetVideosAsync(basePlaylist.Id).CollectAsync().ConfigureAwait(false));
                    VideoList = new List<PlaylistVideo>();
                    await UpdatePlaylistInfo(Visibility.Visible, list.BasePlaylist.Title, list.BasePlaylist.Author?.ChannelTitle ?? "", "", list.Videos.Count().ToString(), $"https://img.youtube.com/vi/{list?.Videos?.FirstOrDefault()?.Id}/maxresdefault.jpg", true, true);
                    await PopulateVideoGrid(list.Videos);
                }).ConfigureAwait(false);
            }
            else if (YoutubeHelpers.TryParseChannelId(PlaylistLinkTextBox.Text, out var channelId))
            {
                _ = Task.Run(async () =>
                {
                    channel = await client.Channels.GetAsync(channelId).ConfigureAwait(false);
                    list = new FullPlaylist(null, null, channel.Title);
                    VideoList = await client.Channels.GetUploadsAsync(channel.Id).CollectAsync().ConfigureAwait(false);
                    await UpdatePlaylistInfo(Visibility.Visible, channel.Title, totalVideos: VideoList.Count().ToString(), imageUrl: channel.Thumbnails.FirstOrDefault()?.Url, downloadEnabled: true, showIndexes: true);
                    await PopulateVideoGrid(VideoList);
                }).ConfigureAwait(false);
            }
            else if (YoutubeHelpers.TryParseUsername(PlaylistLinkTextBox.Text, out var username))
            {
                _ = Task.Run(async () =>
                {
                    var channel = await client.Channels.GetByUserAsync(username).ConfigureAwait(false);
                    list = new FullPlaylist(null, null, channel.Title);
                    VideoList = await client.Channels.GetUploadsAsync(channel.Id).CollectAsync().ConfigureAwait(false);
                    await UpdatePlaylistInfo(Visibility.Visible, channel.Title, totalVideos: VideoList.Count().ToString(), imageUrl: channel.Thumbnails.FirstOrDefault()?.Url, downloadEnabled: true, showIndexes: true);
                    await PopulateVideoGrid(VideoList);
                }).ConfigureAwait(false);
            }
            else if (YoutubeHelpers.TryParseHandle(PlaylistLinkTextBox.Text, out var handle))
            {
                _ = Task.Run(async () =>
                {
                    var channel = await client.Channels.GetByHandleAsync(handle).ConfigureAwait(false);
                    list = new FullPlaylist(null, null, channel.Title);
                    VideoList = await client.Channels.GetUploadsAsync(channel.Id).CollectAsync().ConfigureAwait(false);
                    await UpdatePlaylistInfo(Visibility.Visible, channel.Title, totalVideos: VideoList.Count().ToString(), imageUrl: channel.Thumbnails.FirstOrDefault()?.Url, downloadEnabled: true, showIndexes: true);
                    await PopulateVideoGrid(VideoList);
                }).ConfigureAwait(false);
            }
            else if (YoutubeHelpers.TryParseVideoId(PlaylistLinkTextBox.Text, out var videoId))
            {
                _ = Task.Run(async () =>
                {
                    var video = await client.Videos.GetAsync(videoId);
                    VideoList = new List<Video> { video };
                    list = new FullPlaylist(null, null);
                    await UpdatePlaylistInfo(Visibility.Visible, video.Title, video.Author.ChannelTitle, video.Engagement.ViewCount.ToString(), string.Empty, $"https://img.youtube.com/vi/{video.Id}/maxresdefault.jpg", true, false);
                    await HideVideoGrid();
                }).ConfigureAwait(false);
            }
            else
            {
                await UpdatePlaylistInfo().ConfigureAwait(false);
                await HideVideoGrid();
            }
        }

        catch (Exception ex)
        {
            await GlobalConsts.Log(ex.ToString(), "MainPage TextBox_TextChanged");
            await GlobalConsts.ShowMessage((string)FindResource("Error"), ex.Message);
        }
    }

    private async Task PopulateVideoGrid(IEnumerable<IVideo> videos)
    {
        var items = videos.Select((v, i) => new SelectableVideo(v, i + 1)).ToList();
        await Dispatcher.InvokeAsync(() =>
        {
            selectableVideos = new ObservableCollection<SelectableVideo>(items);
            VideosDataGrid.ItemsSource = selectableVideos;
            VideosDataGrid.Visibility = Visibility.Visible;
            SelectionCountTextBlock.Visibility = Visibility.Visible;
            isUpdatingSelectAll = true;
            SelectAllCheckBox.IsChecked = true;
            isUpdatingSelectAll = false;
            UpdateSelectionCount();
        });
    }

    private async Task HideVideoGrid()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            VideosDataGrid.Visibility = Visibility.Collapsed;
            SelectionCountTextBlock.Visibility = Visibility.Collapsed;
            VideosDataGrid.ItemsSource = null;
            selectableVideos.Clear();
        });
    }

    private void UpdateSelectionCount()
    {
        if (selectableVideos == null || SelectionCountTextBlock == null) return;

        var selected = selectableVideos.Count(v => v.IsSelected);
        var total = selectableVideos.Count;
        SelectionCountTextBlock.Text = $"{selected} / {total} {FindResource("Selected")}";

        var hasSelection = selected > 0;
        if (DownloadButton != null) DownloadButton.IsEnabled = hasSelection;
        if (DownloadInBackgroundButton != null) DownloadInBackgroundButton.IsEnabled = hasSelection;
        if (SavePlaylistInfoButton != null) SavePlaylistInfoButton.IsEnabled = hasSelection;
    }

    private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingSelectAll || selectableVideos == null || selectableVideos.Count == 0) return;

        var isChecked = SelectAllCheckBox.IsChecked == true;
        foreach (var video in selectableVideos)
            video.IsSelected = isChecked;

        UpdateSelectionCount();
    }

    private void VideoCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingSelectAll) return;

        isUpdatingSelectAll = true;
        var allSelected = selectableVideos.All(v => v.IsSelected);
        var noneSelected = selectableVideos.All(v => !v.IsSelected);
        SelectAllCheckBox.IsChecked = allSelected ? true : noneSelected ? false : null;
        isUpdatingSelectAll = false;

        UpdateSelectionCount();
    }

    private List<SelectableVideo> GetSelectedSelectableVideos()
    {
        if (selectableVideos.Count == 0)
            return VideoList.Select((v, i) => new SelectableVideo(v, i + 1)).ToList();

        return selectableVideos.Where(v => v.IsSelected).ToList();
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (list != null || VideoList.Any())
        {
            if (!CanDownload())
            {
                GlobalConsts.ShowMessage((string)FindResource("Error"), $"{string.Format((string)FindResource("FileDoesNotExist"), GlobalConsts.FFmpegFilePath)}").ConfigureAwait(false);
                return;
            }

            var selected = GetSelectedSelectableVideos();
            if (!selected.Any())
            {
                GlobalConsts.ShowMessage((string)FindResource("Error"), (string)FindResource("NoVideosSelected")).ConfigureAwait(false);
                return;
            }

            GlobalConsts.LoadPage(new DownloadPage(list, GlobalConsts.DownloadSettings.Clone(), videos: selected.Select(s => s.Video), selectableVideos: selected));
            VideoList = new List<IVideo>();
            PlaylistLinkTextBox.Text = string.Empty;
        }
    }

    private async Task UpdatePlaylistInfo(Visibility vis = Visibility.Collapsed, string title = "", string author = "", string views = "", string totalVideos = "", string imageUrl = "", bool downloadEnabled = false, bool showIndexes = false)
        => await Dispatcher.InvokeAsync(() =>
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                PlaylistInfoImage.Source = new BitmapImage(new Uri(imageUrl));
                PlaylistInfoImage.Visibility = Visibility.Visible;
            }
            else
                PlaylistInfoImage.Visibility = Visibility.Collapsed;

            PlaylistInfoGrid.Visibility = vis;
            PlaylistTitleTextBlock.Text = title;
            PlaylistAuthorTextBlock.Text = author;
            PlaylistViewsTextBlock.Text = views;

            if (!string.IsNullOrWhiteSpace(totalVideos))
            {
                PlaylistTotalVideosTextBlockText.Visibility = Visibility.Visible;
                PlaylistTotalVideosTextBlock.Visibility = Visibility.Visible;
                PlaylistTotalVideosTextBlock.Text = totalVideos;
            }
            else
            {
                PlaylistTotalVideosTextBlockText.Visibility = Visibility.Collapsed;
                PlaylistTotalVideosTextBlock.Visibility = Visibility.Collapsed;
            }

            DownloadButton.IsEnabled = downloadEnabled;
            DownloadInBackgroundButton.IsEnabled = downloadEnabled;
            SavePlaylistInfoButton.IsEnabled = downloadEnabled;

        });

    private void DownloadInBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        if (list != null || VideoList.Any())
        {
            if (!CanDownload())
            {
                GlobalConsts.ShowMessage((string)FindResource("Error"), $"{string.Format((string)FindResource("FileDoesNotExist"), GlobalConsts.FFmpegFilePath)}").ConfigureAwait(false);
                return;
            }

            var selected = GetSelectedSelectableVideos();
            if (!selected.Any())
            {
                GlobalConsts.ShowMessage((string)FindResource("Error"), (string)FindResource("NoVideosSelected")).ConfigureAwait(false);
                return;
            }

            _ = new DownloadPage(list, GlobalConsts.DownloadSettings.Clone(), silent: true, videos: selected.Select(s => s.Video), selectableVideos: selected);
            VideoList = new List<IVideo>();
            PlaylistLinkTextBox.Text = string.Empty;
        }
    }

    private async void SavePlaylistInfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (list != null || VideoList.Any())
        {
            var selectedVideos = GetSelectedSelectableVideos().Select(s => s.Video).ToList();
            if (!selectedVideos.Any())
            {
                await GlobalConsts.ShowMessage((string)FindResource("Error"), (string)FindResource("NoVideosSelected")).ConfigureAwait(false);
                return;
            }

            try
            {
                var saveDirectory = GlobalConsts.settings.SaveDirectory;
                if (!Directory.Exists(saveDirectory))
                    Directory.CreateDirectory(saveDirectory);

                var titleText = list?.BasePlaylist?.Title ?? list?.Title ?? "Playlist";
                var cleanTitle = GlobalConsts.CleanFileName(titleText);
                var filePath = Path.Combine(saveDirectory, $"{cleanTitle}_Info.txt");

                using (var writer = new StreamWriter(filePath))
                {
                    await writer.WriteLineAsync($"Playlist: {titleText}");
                    await writer.WriteLineAsync($"Total Videos: {selectedVideos.Count}");
                    await writer.WriteLineAsync("--------------------------------------------------");
                    
                    for (int i = 0; i < selectedVideos.Count; i++)
                    {
                        var video = selectedVideos[i];
                        await writer.WriteLineAsync($"{i + 1}. {video.Title} ({video.Duration})");
                    }
                }

                await GlobalConsts.ShowMessage((string)FindResource("Success"), string.Format((string)FindResource("PlaylistInfoSavedSuccess"), filePath)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await GlobalConsts.Log(ex.ToString(), "MainPage SavePlaylistInfoButton_Click");
                await GlobalConsts.ShowMessage((string)FindResource("Error"), ex.Message).ConfigureAwait(false);
            }
        }
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        GlobalConsts.LoadFlyoutPage(new DownloadSettingsControl());
    }

    private void BulkDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var links = BulkLinksTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (!CanDownload())
        {
            GlobalConsts.ShowMessage((string)FindResource("Error"), $"{string.Format((string)FindResource("FileDoesNotExist"), GlobalConsts.FFmpegFilePath)}").ConfigureAwait(false);
            return;
        }

        _ = DownloadPage.SequenceDownload(links, GlobalConsts.DownloadSettings.Clone(), silent: true);
        BulkLinksTextBox.Text = string.Empty;
        MetroAnimatedTabControl.SelectedItem = QueueMetroTabItem;
    }

    public void ChangeToQueueTab()
    {
        MetroAnimatedTabControl.SelectedItem = QueueMetroTabItem;
    }

    private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
    {
        BulkDownloadButton.IsEnabled = !string.IsNullOrWhiteSpace(BulkLinksTextBox.Text);
    }

    private static bool CanDownload()
    {
        return GlobalConsts.DownloadSettings.AudioOnly || File.Exists(GlobalConsts.FFmpegFilePath);
    }

    private void BulkLinksTextBox_PreviewDrop(object sender, DragEventArgs e)
    {
        var data = e.Data.GetData(DataFormats.Text, true);
        if (data != null)
        {
            var dataAsString = (string)data;
            dataAsString += Environment.NewLine;
            BulkLinksTextBox.Text += dataAsString;
            BulkLinksTextBox.SelectionStart = BulkLinksTextBox.Text.Length;
            e.Handled = true;
        }
    }

    private void BulkLinksTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.SourceDataObject.GetData(DataFormats.Text, true);
        if (text != null)
        {
            var textAsString = (string)text;
            textAsString += Environment.NewLine;
            BulkLinksTextBox.Text += textAsString;
            BulkLinksTextBox.SelectionStart = BulkLinksTextBox.Text.Length;
            e.CancelCommand();
            e.Handled = true;
        }
    }

    private ObservableCollection<SiteDownloadFile> siteDownloadFiles = [];
    private ICollectionView siteDownloadFilesView;
    private bool isUpdatingSiteSelectAll = false;
    private string currentSiteUrl = string.Empty;

    private void SiteUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SiteScanButton.IsEnabled = !string.IsNullOrWhiteSpace(SiteUrlTextBox.Text) && 
            (SiteUrlTextBox.Text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
             SiteUrlTextBox.Text.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private async void SiteScanButton_Click(object sender, RoutedEventArgs e)
    {
        currentSiteUrl = SiteUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(currentSiteUrl)) return;

        SiteScanButton.IsEnabled = false;
        SiteScanStatusTextBlock.Visibility = Visibility.Visible;
        SiteScanStatusTextBlock.Text = "Scanning...";
        SiteFilesDataGrid.Visibility = Visibility.Collapsed;
        siteDownloadFiles.Clear();

        try
        {
            using var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(currentSiteUrl);
            
            var regex = new Regex(@"href\s*=\s*[""'](.*?\.([a-zA-Z0-9]+))[""']", RegexOptions.IgnoreCase);
            var matches = regex.Matches(html);
            
            var baseUri = new Uri(currentSiteUrl);
            var foundFiles = new List<SiteDownloadFile>();

            foreach (Match match in matches)
            {
                var link = match.Groups[1].Value;
                var extension = match.Groups[2].Value.ToLower();

                if (new[] { "mp3", "mp4", "pdf", "zip", "rar", "wav", "ogg", "mkv", "avi", "mov", "wmv", "flac" }.Contains(extension))
                {
                    if (Uri.TryCreate(baseUri, link, out Uri fileUri))
                    {
                        var url = fileUri.ToString();
                        if (!foundFiles.Any(f => f.Url == url))
                        {
                            var fileName = Path.GetFileName(fileUri.LocalPath);
                            if (string.IsNullOrWhiteSpace(fileName)) fileName = $"file.{extension}";

                            var subFolder = Path.GetDirectoryName(fileUri.LocalPath)?.TrimStart('\\', '/');
                            if (subFolder == null) subFolder = "";

                            foundFiles.Add(new SiteDownloadFile
                            {
                                Url = url,
                                FileName = Uri.UnescapeDataString(fileName),
                                Extension = extension,
                                SubFolder = Uri.UnescapeDataString(subFolder),
                                Size = 0
                            });
                        }
                    }
                }
            }

            _ = Task.Run(async () => 
            {
                var tasks = foundFiles.Select(async f =>
                {
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Head, f.Url);
                        using var res = await httpClient.SendAsync(req);
                        if (res.IsSuccessStatusCode && res.Content.Headers.ContentLength.HasValue)
                        {
                            f.Size = res.Content.Headers.ContentLength.Value;
                            await Dispatcher.InvokeAsync(() => f.IsSelected = f.IsSelected); 
                        }
                    }
                    catch { }
                });
                await Task.WhenAll(tasks);
            });

            siteDownloadFiles = new ObservableCollection<SiteDownloadFile>(foundFiles);
            siteDownloadFilesView = CollectionViewSource.GetDefaultView(siteDownloadFiles);
            siteDownloadFilesView.Filter = SiteFileFilter;
            SiteFilesDataGrid.ItemsSource = siteDownloadFilesView;
            
            if (siteDownloadFilesView.Cast<object>().Any())
            {
                SiteScanStatusTextBlock.Text = $"Found {siteDownloadFiles.Count} files.";
                SiteFilesDataGrid.Visibility = Visibility.Visible;
                SiteDownloadButton.IsEnabled = true;
                SiteSelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SiteScanStatusTextBlock.Text = "No downloadable files found.";
                SiteDownloadButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            SiteScanStatusTextBlock.Text = $"Error: {ex.Message}";
            SiteDownloadButton.IsEnabled = false;
        }
        finally
        {
            SiteScanButton.IsEnabled = true;
        }
    }

    private void SiteFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (siteDownloadFilesView != null)
        {
            siteDownloadFilesView.Refresh();
            UpdateSiteSelectionCount();
        }
    }

    private bool SiteFileFilter(object item)
    {
        if (item is SiteDownloadFile file)
        {
            if (SiteFilterComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                switch (tag)
                {
                    case "audio": return new[] { "mp3", "wav", "ogg", "flac", "aac", "m4a" }.Contains(file.Extension.ToLower());
                    case "video": return new[] { "mp4", "mkv", "avi", "mov", "wmv", "webm" }.Contains(file.Extension.ToLower());
                    case "doc": return new[] { "pdf", "doc", "docx", "txt" }.Contains(file.Extension.ToLower());
                    case "archive": return new[] { "zip", "rar", "7z", "tar", "gz" }.Contains(file.Extension.ToLower());
                    case "all":
                    default: return true;
                }
            }
        }
        return true;
    }

    private void SiteSelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingSiteSelectAll || siteDownloadFilesView == null || siteDownloadFilesView.IsEmpty) return;

        var isChecked = SiteSelectAllCheckBox.IsChecked == true;
        foreach (SiteDownloadFile file in siteDownloadFilesView)
            file.IsSelected = isChecked;

        UpdateSiteSelectionCount();
    }

    private void SiteFileCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingSiteSelectAll || siteDownloadFilesView == null) return;

        isUpdatingSiteSelectAll = true;
        var visibleFiles = siteDownloadFilesView.Cast<SiteDownloadFile>().ToList();
        var allSelected = visibleFiles.Any() && visibleFiles.All(v => v.IsSelected);
        var noneSelected = visibleFiles.All(v => !v.IsSelected);
        SiteSelectAllCheckBox.IsChecked = allSelected ? true : noneSelected ? false : null;
        isUpdatingSiteSelectAll = false;

        UpdateSiteSelectionCount();
    }

    private void UpdateSiteSelectionCount()
    {
        if (siteDownloadFilesView == null) return;
        var selected = siteDownloadFilesView.Cast<SiteDownloadFile>().Count(v => v.IsSelected);
        SiteDownloadButton.IsEnabled = selected > 0;
    }

    private void SiteDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (siteDownloadFilesView == null) return;
        var selectedFiles = siteDownloadFilesView.Cast<SiteDownloadFile>().Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            GlobalConsts.ShowMessage((string)FindResource("Error"), "No files selected").ConfigureAwait(false);
            return;
        }

        _ = new SiteDownloadPage(selectedFiles, currentSiteUrl);
        siteDownloadFiles.Clear();
        SiteFilesDataGrid.Visibility = Visibility.Collapsed;
        SiteScanStatusTextBlock.Visibility = Visibility.Collapsed;
        SiteUrlTextBox.Text = string.Empty;
        MetroAnimatedTabControl.SelectedItem = QueueMetroTabItem;
    }

    private ObservableCollection<LocalMediaFile> localMediaFiles = [];
    private ICollectionView localMediaFilesView;
    private bool isUpdatingLocalConverterSelectAll = false;

    private void LocalConverterBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files (*.*)|*.*",
            Title = "Select Video Files"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            AddLocalFiles(openFileDialog.FileNames);
        }
    }

    private void LocalConverter_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddLocalFiles(files);
        }
    }

    private void AddLocalFiles(string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            if (File.Exists(path) && !localMediaFiles.Any(f => f.FilePath == path))
            {
                var fileInfo = new FileInfo(path);
                
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
                string artist = "";
                string title = fileNameWithoutExt;

                var parts = fileNameWithoutExt.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    artist = parts[0].Trim();
                    title = parts[1].Trim();
                }

                localMediaFiles.Add(new LocalMediaFile
                {
                    FilePath = path,
                    FileName = fileInfo.Name,
                    Extension = fileInfo.Extension.TrimStart('.').ToLower(),
                    SizeDisplay = $"{(fileInfo.Length / 1048576.0):0.00} MB",
                    IsSelected = true,
                    Artist = artist,
                    Title = title
                });
            }
        }

        UpdateLocalConverterGrid();
    }

    private void UpdateLocalConverterGrid()
    {
        if (localMediaFilesView == null)
        {
            localMediaFilesView = CollectionViewSource.GetDefaultView(localMediaFiles);
            LocalConverterDataGrid.ItemsSource = localMediaFilesView;
        }

        if (localMediaFiles.Any())
        {
            LocalConverterDataGrid.Visibility = Visibility.Visible;
            LocalConverterSelectAllCheckBox.IsChecked = true;
        }
        else
        {
            LocalConverterDataGrid.Visibility = Visibility.Collapsed;
        }
        
        UpdateLocalConverterSelectionCount();
    }

    private void LocalConverterSelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingLocalConverterSelectAll || localMediaFilesView == null || localMediaFilesView.IsEmpty) return;

        var isChecked = LocalConverterSelectAllCheckBox.IsChecked == true;
        foreach (LocalMediaFile file in localMediaFilesView)
            file.IsSelected = isChecked;

        UpdateLocalConverterSelectionCount();
    }

    private void LocalConverterFileCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isUpdatingLocalConverterSelectAll || localMediaFilesView == null) return;

        isUpdatingLocalConverterSelectAll = true;
        var visibleFiles = localMediaFilesView.Cast<LocalMediaFile>().ToList();
        var allSelected = visibleFiles.Any() && visibleFiles.All(v => v.IsSelected);
        var noneSelected = visibleFiles.All(v => !v.IsSelected);
        LocalConverterSelectAllCheckBox.IsChecked = allSelected ? true : noneSelected ? false : null;
        isUpdatingLocalConverterSelectAll = false;

        UpdateLocalConverterSelectionCount();
    }

    private void UpdateLocalConverterSelectionCount()
    {
        if (localMediaFilesView == null) return;
        var selected = localMediaFilesView.Cast<LocalMediaFile>().Count(v => v.IsSelected);
        LocalConverterConvertButton.IsEnabled = selected > 0;
    }

    private void LocalConverterConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (localMediaFilesView == null) return;
        var selectedFiles = localMediaFilesView.Cast<LocalMediaFile>().Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0) return;

        var embedThumbnail = LocalEmbedThumbnailCheckBox.IsChecked == true;
        var customImagePath = LocalThumbnailCustomRadio.IsChecked == true ? LocalCustomThumbnailTextBox.Text : null;

        if (embedThumbnail && LocalThumbnailCustomRadio.IsChecked == true && (string.IsNullOrWhiteSpace(customImagePath) || !File.Exists(customImagePath)))
        {
            GlobalConsts.ShowMessage((string)FindResource("Error"), "Please select a valid custom thumbnail image or choose auto-extract.").ConfigureAwait(false);
            return;
        }

        if (!File.Exists(GlobalConsts.FFmpegFilePath))
        {
            GlobalConsts.ShowMessage((string)FindResource("Error"), $"{string.Format((string)FindResource("FileDoesNotExist"), GlobalConsts.FFmpegFilePath)}").ConfigureAwait(false);
            return;
        }

        var saveDir = GlobalConsts.settings?.SaveDirectory;
        if (string.IsNullOrWhiteSpace(saveDir) || !Directory.Exists(saveDir))
        {
            saveDir = Path.GetDirectoryName(selectedFiles.First().FilePath);
        }

        var bitrate = GlobalConsts.DownloadSettings?.Bitrate ?? "192";

        foreach (var file in selectedFiles)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
            var outputFilePath = Path.Combine(saveDir, $"{fileNameWithoutExt}.mp3");
            
            var copyFileLocCounter = 1;
            while (File.Exists(outputFilePath))
            {
                outputFilePath = Path.Combine(saveDir, $"{fileNameWithoutExt}-{copyFileLocCounter}.mp3");
                copyFileLocCounter++;
            }

            var conversion = new LocalConversion(file, outputFilePath, bitrate, embedThumbnail, customImagePath);
            GlobalConsts.Downloads.Add(new QueuedDownload(conversion));
        }

        var filesToRemove = selectedFiles.ToList();
        foreach (var f in filesToRemove)
        {
            localMediaFiles.Remove(f);
        }
        UpdateLocalConverterGrid();

        MetroAnimatedTabControl.SelectedItem = QueueMetroTabItem;
    }

    private void LocalBrowseCustomThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
            Title = "Select Custom Thumbnail"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LocalCustomThumbnailTextBox.Text = openFileDialog.FileName;
        }
    }
}
