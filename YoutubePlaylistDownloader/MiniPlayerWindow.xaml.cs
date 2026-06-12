using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YoutubePlaylistDownloader.Objects;

namespace YoutubePlaylistDownloader
{
    public partial class MiniPlayerWindow : Window
    {
        private QueuedDownload currentActiveDownload = null;
        private DispatcherTimer updateTimer;

        public MiniPlayerWindow()
        {
            InitializeComponent();
            
            this.Loaded += (s, e) =>
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = desktopWorkingArea.Right - this.Width - 20;
                this.Top = desktopWorkingArea.Bottom - this.Height - 20;
            };

            UpdateActiveDownload();
            GlobalConsts.Downloads.CollectionChanged += Downloads_CollectionChanged;
            
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            updateTimer.Tick += (s, e) => UpdateUI();
            updateTimer.Start();
        }

        private void Downloads_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateActiveDownload());
        }

        private void UpdateActiveDownload()
        {
            var active = GlobalConsts.Downloads.FirstOrDefault();
            if (active != currentActiveDownload)
            {
                currentActiveDownload = active;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (currentActiveDownload == null)
            {
                TitleText.Text = "Idle";
                StatusText.Text = "Waiting for downloads...";
                ProgressBar.Value = 0;
                SpeedText.Text = "";
                ThumbnailImage.Source = null;
            }
            else
            {
                var item = currentActiveDownload.GetItem();
                if (item != null)
                {
                    TitleText.Text = item.CurrentTitle ?? item.Title;
                    StatusText.Text = item.CurrentStatus;
                    ProgressBar.Value = item.CurrentProgressPercent;
                    SpeedText.Text = item.CurrentDownloadSpeed;
                    
                    if (ThumbnailImage.Source == null && !string.IsNullOrEmpty(item.ImageUrl))
                    {
                        try
                        {
                            ThumbnailImage.Source = new BitmapImage(new Uri(item.ImageUrl));
                        }
                        catch { }
                    }
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var skeleton = Application.Current.MainWindow as Skeleton;
            if (skeleton != null)
            {
                skeleton.RestoreMainWindow();
            }
            this.Hide();
        }
    }
}
