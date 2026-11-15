using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>
    /// 帖内视频播放器：先把视频缓存到本地（带 Referer/UA），再用 MediaElement 播放，
    /// 避免 CDN 拒绝无 Referer 请求、也绕开 MediaElement 对 HTTP 流的挑剔。
    /// </summary>
    public sealed class VideoWindow : Window
    {
        private readonly MediaElement _player;
        private readonly Slider _progress;
        private readonly TextBlock _time;
        private readonly TextBlock _status;
        private readonly ProgressBar _loadingBar;
        private readonly DispatcherTimer _timer;
        private readonly CancellationTokenSource _cts = new();
        private string _localPath;
        private bool _dragging;
        private bool _playing;
        private bool _autoOpenedExternal;
        private TextBlock _playGlyph;
        private readonly long _threadId;

        public VideoWindow(VideoInfoModel video, long threadId = 0)
        {
            _threadId = threadId;
            Width = 960;
            Height = 620;
            MinWidth = 520;
            MinHeight = 360;
            WindowStyle = WindowStyle.None;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(12),
            });
            Anim.RoundWindowCorners(this);
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x10));
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Title = video?.Title ?? "视频";

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _player = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                ScrubbingEnabled = true,
                Stretch = Stretch.Uniform,
            };
            _player.MediaOpened += (_, _) =>
            {
                _status.Visibility = Visibility.Collapsed;
                _loadingBar.Visibility = Visibility.Collapsed;
                _player.Play();
                _playing = true;
                UpdatePlayGlyph();
            };
            _player.MediaFailed += (_, args) =>
            {
                _status.Text = "无法播放（" + (args.ErrorException?.Message ?? "格式不支持") + "）\n可尝试用浏览器打开";
                _status.Visibility = Visibility.Visible;
                _loadingBar.Visibility = Visibility.Collapsed;
                _playing = false;
                UpdatePlayGlyph();
                if (!_autoOpenedExternal)
                {
                    _autoOpenedExternal = true;
                    var target = !string.IsNullOrEmpty(video?.PageUrl)
                        ? video.PageUrl
                        : !string.IsNullOrEmpty(_localPath)
                            ? new Uri(_localPath).AbsoluteUri
                            : video?.Url;
                    if (EdgePlayer.OpenApp(target))
                    {
                        _status.Text = "系统缺少该视频的解码器，已用 Edge 播放窗口打开";
                    }
                }
            };
            _player.MediaEnded += (_, _) =>
            {
                _playing = false;
                UpdatePlayGlyph();
                _timer.Stop();
            };
            root.Children.Add(_player);

            _status = new TextBlock
            {
                Text = "正在加载视频…",
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            root.Children.Add(_status);

            _loadingBar = new ProgressBar
            {
                Width = 220,
                Height = 4,
                IsIndeterminate = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
                Foreground = (Brush)FindResource("Accent"),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            };
            root.Children.Add(_loadingBar);

            var close = new Button
            {
                Content = new TextBlock
                {
                    Text = "\uE8BB",
                    FontFamily = (FontFamily)FindResource("IconFont"),
                    FontSize = 12,
                    Foreground = Brushes.White,
                },
                Width = 40,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 12, 0),
                Cursor = Cursors.Hand,
            };
            close.SetResourceReference(FrameworkElement.StyleProperty, "WindowButton");
            close.Click += (_, _) => Anim.AnimateClose(this);
            Anim.HoverScale(close);
            root.Children.Add(close);

            var controls = new Border
            {
                Padding = new Thickness(16, 10, 16, 12),
                BorderThickness = new Thickness(0, 1, 0, 0),
            };
            controls.SetResourceReference(Border.BackgroundProperty, "Surface");
            controls.SetResourceReference(Border.BorderBrushProperty, "Border");
            var bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _playGlyph = Ui.Icon("\uE768", 15, "TextPrimary");
            _playGlyph.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            var play = new Button
            {
                Content = _playGlyph,
                Width = 36,
                Height = 36,
                ToolTip = "播放 / 暂停",
                Cursor = Cursors.Hand,
            };
            play.SetResourceReference(FrameworkElement.StyleProperty, "GhostButton");
            play.Click += (_, _) => TogglePlay();
            bar.Children.Add(play);

            _progress = new Slider
            {
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsMoveToPointEnabled = false,
                Minimum = 0,
                Maximum = 1,
                Style = (Style)FindResource("VideoSlider"),
            };
            void SeekFrom(double x)
            {
                if (!_player.NaturalDuration.HasTimeSpan || _progress.ActualWidth <= 1) return;
                var fraction = Math.Clamp(x / _progress.ActualWidth, 0, 1);
                _progress.Value = fraction;
                _player.Position = TimeSpan.FromSeconds(fraction * _player.NaturalDuration.TimeSpan.TotalSeconds);
            }
            _progress.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _dragging = true;
                SeekFrom(e.GetPosition(_progress).X);
                e.Handled = true;
            };
            _progress.PreviewMouseMove += (_, e) =>
            {
                if (_dragging) SeekFrom(e.GetPosition(_progress).X);
            };
            _progress.PreviewMouseLeftButtonUp += (_, e) =>
            {
                SeekFrom(e.GetPosition(_progress).X);
                _dragging = false;
                e.Handled = true;
            };
            _progress.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _dragging = true));
            _progress.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
            {
                _dragging = false;
                if (_player.NaturalDuration.HasTimeSpan)
                {
                    _player.Position = TimeSpan.FromSeconds(_progress.Value * _player.NaturalDuration.TimeSpan.TotalSeconds);
                }
            }));
            Grid.SetColumn(_progress, 1);
            bar.Children.Add(_progress);

            _time = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            _time.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            Grid.SetColumn(_time, 2);
            bar.Children.Add(_time);

            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var save = new Button
            {
                Content = "保存",
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
            };
            save.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
            save.Click += async (_, _) =>
            {
                if (string.IsNullOrEmpty(_localPath))
                {
                    Svc.Window?.ShowToast("视频还在加载，请稍后再保存");
                    return;
                }
                try
                {
                    var path = await MediaSaver.SaveAsync(_localPath,
                        "tieba-video-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".mp4",
                        "视频|*.mp4;*.flv;*.ts|所有文件|*.*");
                    if (!string.IsNullOrEmpty(path)) Svc.Window?.ShowToast("已保存到 " + path);
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("保存失败：" + ex.Message);
                }
            };
            actions.Children.Add(save);

            var browser = new Button
            {
                Content = "浏览器打开",
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = Cursors.Hand,
            };
            browser.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
            browser.Click += (_, _) =>
            {
                var target = !string.IsNullOrWhiteSpace(video?.PageUrl)
                    ? video.PageUrl
                    : threadId > 0
                        ? "https://tieba.baidu.com/p/" + threadId
                        : video?.Url;
                if (!Ui.OpenUrl(target)) Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
            };
            actions.Children.Add(browser);
            Grid.SetColumn(actions, 3);
            bar.Children.Add(actions);

            if (threadId > 0)
            {
                var page = new Button
                {
                    Content = "原帖",
                    Padding = new Thickness(12, 5, 12, 5),
                    Cursor = Cursors.Hand,
                };
                page.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
                page.Click += (_, _) => Ui.OpenUrl("https://tieba.baidu.com/p/" + threadId);
                Grid.SetColumn(page, 4);
                bar.Children.Add(page);
            }

            controls.Child = bar;
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);
            root.Background = Brushes.Transparent;
            root.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (controls.IsMouseOver || close.IsMouseOver) return;
                Anim.AnimateClose(this);
            };
            Content = root;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _timer.Tick += (_, _) =>
            {
                if (_dragging || !_player.NaturalDuration.HasTimeSpan) return;
                var duration = _player.NaturalDuration.TimeSpan;
                if (duration.TotalSeconds <= 0) return;
                _progress.Value = Math.Clamp(_player.Position.TotalSeconds / duration.TotalSeconds, 0, 1);
                _time.Text = Format(_player.Position) + " / " + Format(duration);
            };

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Space) TogglePlay();
            };

            Loaded += async (_, _) =>
            {
                Anim.PopIn(this);
                _timer.Start();
                await LoadVideoAsync(video);
            };
            Closed += (_, _) =>
            {
                _cts.Cancel();
                _timer.Stop();
                try
                {
                    _player.Stop();
                    _player.Close();
                }
                catch
                {
                }
            };
        }

        private async Task LoadVideoAsync(VideoInfoModel video)
        {
            if (video == null || string.IsNullOrWhiteSpace(video.Url))
            {
                _status.Text = "没有可用的视频地址";
                _loadingBar.Visibility = Visibility.Collapsed;
                return;
            }
            if (video.Url.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _status.Text = "该视频是 HLS 流，内置播放器不支持\n已尝试用 Edge 播放窗口打开";
                _loadingBar.Visibility = Visibility.Collapsed;
                if (_threadId > 0) EdgePlayer.OpenApp("https://tieba.baidu.com/p/" + _threadId);
                return;
            }

            try
            {
                var progress = new Progress<double>(p =>
                {
                    _loadingBar.IsIndeterminate = false;
                    _loadingBar.Value = p;
                    _status.Text = "正在缓存视频… " + (int)(p * 100) + "%";
                });
                _localPath = await VideoCache.DownloadAsync(video.Url, progress, _cts.Token);
                _status.Text = "正在打开…";
                _loadingBar.Visibility = Visibility.Collapsed;
                _player.Source = new Uri(_localPath);
                _player.Play();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _status.Text = "视频加载失败：" + ex.Message + "\n可尝试用浏览器打开";
                _loadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private void TogglePlay()
        {
            if (_player.NaturalDuration.HasTimeSpan && _player.Position >= _player.NaturalDuration.TimeSpan)
            {
                _player.Position = TimeSpan.Zero;
            }
            if (_playing)
            {
                _player.Pause();
                _playing = false;
                UpdatePlayGlyph();
            }
            else
            {
                _player.Play();
                _playing = true;
                UpdatePlayGlyph();
            }
        }

        private void UpdatePlayGlyph()
        {
            if (_playGlyph != null) _playGlyph.Text = _playing ? "\uE769" : "\uE768";
        }

        private static string Format(TimeSpan time) =>
            time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }
}
