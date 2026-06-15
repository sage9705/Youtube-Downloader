namespace YoutubePlaylistDownloader;

public partial class SiteDownloadPage : UserControl, IDownload
{
    private List<SiteDownloadFile> files;
    private string siteUrl;
    private CancellationTokenSource cts;
    private ManualResetEventSlim pauseGate;
    private bool disposedValue = false;
    private int downloadedCount = 0;
    private long totalBytesDownloaded = 0;
    private DateTime lastUpdate = DateTime.Now;

    public event PropertyChangedEventHandler PropertyChanged;

    public SiteDownloadPage(List<SiteDownloadFile> files, string siteUrl)
    {
        InitializeComponent();
        this.files = files;
        this.siteUrl = siteUrl;
        
        Title = $"Site Download: {new Uri(siteUrl).Host}";
        ImageUrl = string.Empty;
        TotalVideos = files.Count;
        TotalDownloaded = $"0 / {TotalVideos}";
        CurrentStatus = "Starting...";
        
        cts = new CancellationTokenSource();
        pauseGate = new ManualResetEventSlim(true);

        GlobalConsts.Downloads.Add(new QueuedDownload(this));

        _ = StartDownloadAsync();
    }

    public string ImageUrl { get; }
    
    private string title;
    public string Title
    {
        get => title;
        set { title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); }
    }

    private string totalDownloaded;
    public string TotalDownloaded
    {
        get => totalDownloaded;
        set { totalDownloaded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDownloaded))); }
    }

    private int totalVideos;
    public int TotalVideos
    {
        get => totalVideos;
        set { totalVideos = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalVideos))); }
    }

    private int currentProgressPercent;
    public int CurrentProgressPercent
    {
        get => currentProgressPercent;
        set { currentProgressPercent = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProgressPercent))); }
    }

    private string currentDownloadSpeed;
    public string CurrentDownloadSpeed
    {
        get => currentDownloadSpeed;
        set { currentDownloadSpeed = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDownloadSpeed))); }
    }

    private string currentTitle;
    public string CurrentTitle
    {
        get => currentTitle;
        set { currentTitle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTitle))); }
    }

    private string currentStatus;
    public string CurrentStatus
    {
        get => currentStatus;
        set { currentStatus = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStatus))); }
    }

    public bool IsPaused => !pauseGate.IsSet;

    public void TogglePause()
    {
        if (IsPaused) pauseGate.Set();
        else pauseGate.Reset();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
    }

    public void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = GlobalConsts.settings.SaveDirectory;
            Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", path);
        }
        catch { }
    }

    public Task<bool> Cancel()
    {
        if (!disposedValue)
        {
            pauseGate.Set();
            cts?.Cancel();
            CurrentStatus = "Cancelled";
            Dispose();
        }
        return Task.FromResult(true);
    }

    private async Task StartDownloadAsync()
    {
        var semaphore = new SemaphoreSlim(3); // Concurrency limit
        var tasks = new List<Task>();

        var saveDir = GlobalConsts.settings.SaveDirectory;
        if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

        using var client = new HttpClient();
        var startTime = DateTime.Now;

        foreach (var file in files)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    pauseGate.Wait(cts.Token);
                    if (cts.IsCancellationRequested) return;

                    var folder = Path.Combine(saveDir, file.SubFolder);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    var savePath = Path.Combine(folder, file.FileName);
                    CurrentTitle = file.FileName;
                    CurrentStatus = "Downloading...";

                    using var response = await client.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    var isMoreToRead = true;
                    var fileDownloadedBytes = 0L;

                    do
                    {
                        pauseGate.Wait(cts.Token);
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, read, cts.Token);
                            fileDownloadedBytes += read;
                            Interlocked.Add(ref totalBytesDownloaded, read);
                            
                            if (totalBytes > 0)
                            {
                                CurrentProgressPercent = (int)((fileDownloadedBytes * 100) / totalBytes);
                            }

                            if ((DateTime.Now - lastUpdate).TotalMilliseconds > 500)
                            {
                                lastUpdate = DateTime.Now;
                                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                if (elapsed > 0)
                                {
                                    var speed = totalBytesDownloaded / elapsed;
                                    CurrentDownloadSpeed = speed > 1048576 ? $"{speed / 1048576.0:F1} MB/s" : $"{speed / 1024.0:F1} KB/s";
                                }
                            }
                        }
                    }
                    while (isMoreToRead);

                    Interlocked.Increment(ref downloadedCount);
                    TotalDownloaded = $"{downloadedCount} / {TotalVideos}";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    CurrentStatus = $"Error: {ex.Message}";
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        if (!cts.IsCancellationRequested)
        {
            CurrentStatus = "Completed";
            CurrentTitle = "";
            CurrentDownloadSpeed = "";
            CurrentProgressPercent = 100;
        }

        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                cts?.Cancel();
                cts?.Dispose();
                pauseGate?.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }
}
