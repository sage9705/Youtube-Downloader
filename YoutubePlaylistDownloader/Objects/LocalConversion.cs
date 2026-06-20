namespace YoutubePlaylistDownloader.Objects;

public class LocalConversion : IDownload
{
    private readonly CancellationTokenSource cts = new();
    private readonly LocalMediaFile sourceFile;
    private readonly string outputFilePath;
    private readonly string bitrate;
    private Process ffmpegProcess;
    private bool disposedValue;

    private string title;
    private string totalDownloaded;
    private int currentProgressPercent;
    private string currentDownloadSpeed;
    private string currentTitle;
    private string currentStatus;

    public string ImageUrl => string.Empty;

    public string Title
    {
        get => title;
        set { title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); }
    }

    public string TotalDownloaded
    {
        get => totalDownloaded;
        set { totalDownloaded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDownloaded))); }
    }

    public int TotalVideos { get; set; } = 1;

    public int CurrentProgressPercent
    {
        get => currentProgressPercent;
        set { currentProgressPercent = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProgressPercent))); }
    }

    public string CurrentDownloadSpeed
    {
        get => currentDownloadSpeed;
        set { currentDownloadSpeed = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDownloadSpeed))); }
    }

    public string CurrentTitle
    {
        get => currentTitle;
        set { currentTitle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTitle))); }
    }

    public string CurrentStatus
    {
        get => currentStatus;
        set { currentStatus = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStatus))); }
    }

    private readonly bool embedThumbnail;
    private readonly string customThumbnailPath;

    public bool IsPaused => false;

    public event PropertyChangedEventHandler PropertyChanged;

    public LocalConversion(LocalMediaFile file, string outPath, string bitr, bool embedThumb, string customImagePath)
    {
        sourceFile = file;
        outputFilePath = outPath;
        bitrate = bitr;
        embedThumbnail = embedThumb;
        customThumbnailPath = customImagePath;

        Title = "Local Conversion";
        CurrentTitle = file.FileName;
        CurrentStatus = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("Pending") as string ?? "Pending");
        TotalDownloaded = "(0/1)";
        CurrentDownloadSpeed = "";
        CurrentProgressPercent = 0;

        _ = StartConversionAsync();
    }

    private async Task StartConversionAsync()
    {
        string tempThumbnailPath = null;
        
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFile.FileName);
        string metadataArgs = $"-id3v2_version 3 -metadata title=\"{sourceFile.Title}\"";
        if (!string.IsNullOrWhiteSpace(sourceFile.Artist))
        {
            metadataArgs += $" -metadata artist=\"{sourceFile.Artist}\"";
        }

        try
        {
            CurrentStatus = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("Converting") as string ?? "Converting...");

            if (GlobalConsts.settings?.LimitConversions == true)
            {
                await GlobalConsts.ConversionsLocker.WaitAsync(cts.Token);
            }

            string arguments;
            if (embedThumbnail)
            {
                if (!string.IsNullOrWhiteSpace(customThumbnailPath) && File.Exists(customThumbnailPath))
                {
                    arguments = $"-i \"{sourceFile.FilePath}\" -i \"{customThumbnailPath}\" -map 0:a -map 1:0 -c:a libmp3lame -b:a {bitrate}k -c:v mjpeg -vf \"scale='min(500,iw)':-1\" -pix_fmt yuvj420p -disposition:v attached_pic -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" {metadataArgs} -y \"{outputFilePath}\"";
                }
                else
                {
                    tempThumbnailPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
                    var extractProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = GlobalConsts.FFmpegFilePath,
                            Arguments = $"-y -ss 00:00:02 -i \"{sourceFile.FilePath}\" -vframes 1 -q:v 2 \"{tempThumbnailPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    extractProcess.Start();
                    await extractProcess.WaitForExitAsync(cts.Token);

                    if (File.Exists(tempThumbnailPath))
                    {
                        arguments = $"-i \"{sourceFile.FilePath}\" -i \"{tempThumbnailPath}\" -map 0:a -map 1:0 -c:a libmp3lame -b:a {bitrate}k -c:v mjpeg -vf \"scale='min(500,iw)':-1\" -pix_fmt yuvj420p -disposition:v attached_pic -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover (front)\" {metadataArgs} -y \"{outputFilePath}\"";
                    }
                    else
                    {
                        arguments = $"-i \"{sourceFile.FilePath}\" -vn -c:a libmp3lame -b:a {bitrate}k {metadataArgs} -y \"{outputFilePath}\"";
                    }
                }
            }
            else
            {
                arguments = $"-i \"{sourceFile.FilePath}\" -vn -c:a libmp3lame -b:a {bitrate}k {metadataArgs} -y \"{outputFilePath}\"";
            }

            ffmpegProcess = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalConsts.FFmpegFilePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var tcs = new TaskCompletionSource<bool>();
            TimeSpan totalDuration = TimeSpan.Zero;

            ffmpegProcess.Exited += (s, e) =>
            {
                tcs.TrySetResult(true);
            };

            ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    if (totalDuration == TimeSpan.Zero && e.Data.Contains("Duration:"))
                    {
                        var match = Regex.Match(e.Data, @"Duration:\s*(?<time>\d+:\d+:\d+\.\d+)");
                        if (match.Success)
                        {
                            TimeSpan.TryParse(match.Groups["time"].Value, out totalDuration);
                        }
                    }
                    else if (e.Data.Contains("time="))
                    {
                        var match = Regex.Match(e.Data, @"time=(?<time>\d+:\d+:\d+\.\d+)");
                        if (match.Success && TimeSpan.TryParse(match.Groups["time"].Value, out var currentTime) && totalDuration.TotalMilliseconds > 0)
                        {
                            var percent = (int)((currentTime.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100);
                            CurrentProgressPercent = percent > 100 ? 100 : percent;
                        }
                    }
                }
            };

            cts.Token.Register(() =>
            {
                try
                {
                    if (!ffmpegProcess.HasExited)
                    {
                        ffmpegProcess.Kill();
                    }
                    tcs.TrySetCanceled();
                }
                catch { }
            });

            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();

            await tcs.Task;

            CurrentProgressPercent = 100;
            TotalDownloaded = "(1/1)";
            CurrentStatus = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("AllDone") as string ?? "Done");

        }
        catch (OperationCanceledException)
        {
            CurrentStatus = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("Canceled") as string ?? "Canceled");
        }
        catch (Exception ex)
        {
            await GlobalConsts.Log(ex.ToString(), "LocalConversion");
            CurrentStatus = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("Error") as string ?? "Error");
        }
        finally
        {
            if (GlobalConsts.settings?.LimitConversions == true)
            {
                GlobalConsts.ConversionsLocker.Release();
            }
            if (tempThumbnailPath != null && File.Exists(tempThumbnailPath))
            {
                try { File.Delete(tempThumbnailPath); } catch { }
            }
        }
    }

    public async Task<bool> Cancel()
    {
        cts.Cancel();
        return await Task.FromResult(true);
    }

    public void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{outputFilePath}\"");
        }
        catch { }
    }

    public void TogglePause()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                cts.Cancel();
                cts.Dispose();
                ffmpegProcess?.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
