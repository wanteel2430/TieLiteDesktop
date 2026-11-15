using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>图片查看窗口（与视频播放器同一种窗口形态）。</summary>
    public sealed class ImageViewerWindow : Window
    {
        private readonly List<string> _urls;
        private readonly List<string> _fallbacks;
        private readonly Image _image;
        private readonly TextBlock _counter;
        private readonly TextBlock _status;
        private readonly ScaleTransform _scale = new(1, 1);
        private readonly TranslateTransform _translate = new(0, 0);
        private int _index;
        private double _zoom = 1;
        private bool _dragging;
        private bool _suppressDeactivate;
        private Point _dragStart;

        public ImageViewerWindow(List<string> urls, int index, List<string> fallbacks = null)
        {
            _urls = urls ?? new List<string>();
            _fallbacks = fallbacks;
            _index = Math.Clamp(index, 0, Math.Max(0, _urls.Count - 1));

            Width = 1000;
            Height = 760;
            MinWidth = 420;
            MinHeight = 320;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(12),
            });
            Anim.RoundWindowCorners(this);
            SetResourceReference(BackgroundProperty, "AppBackground");
            Title = "查看图片";

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imageHost = new Grid { ClipToBounds = true, Background = Brushes.Transparent };
            _image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(26, 20, 26, 20),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var group = new TransformGroup();
            group.Children.Add(_scale);
            group.Children.Add(_translate);
            _image.RenderTransform = group;
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
            imageHost.Children.Add(_image);

            _status = new TextBlock
            {
                Text = "正在加载…",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _status.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            imageHost.Children.Add(_status);
            root.Children.Add(imageHost);

            var controls = new Border
            {
                Padding = new Thickness(14, 8, 14, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
            };
            controls.SetResourceReference(Border.BackgroundProperty, "Surface");
            controls.SetResourceReference(Border.BorderBrushProperty, "Border");
            var bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _counter = new TextBlock
            {
                Text = "1 / " + Math.Max(1, _urls.Count),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 14, 0),
            };
            _counter.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            bar.Children.Add(_counter);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var prev = NavButton("\uE76B", "上一张", () => ShowAt(_index - 1));
            var next = NavButton("\uE76C", "下一张", () => ShowAt(_index + 1));
            buttons.Children.Add(prev);
            buttons.Children.Add(TextButton("−", "缩小", () => ZoomAt(0.8)));
            buttons.Children.Add(TextButton("+", "放大", () => ZoomAt(1.25)));
            buttons.Children.Add(NavButton("\uE72C", "复位", ResetZoom));
            buttons.Children.Add(next);
            buttons.Children.Add(TextButton("保存", "保存图片", async () =>
            {
                _suppressDeactivate = true;
                try
                {
                    var name = "tieba-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ExtOf(_urls[_index]);
                    var path = await MediaSaver.SaveAsync(_urls[_index], name,
                        "图片|*.jpg;*.jpeg;*.png;*.gif;*.webp|所有文件|*.*");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _status.Visibility = Visibility.Visible;
                        _status.Text = "已保存到 " + path;
                    }
                }
                catch (Exception ex)
                {
                    _status.Visibility = Visibility.Visible;
                    _status.Text = "保存失败：" + ex.Message;
                }
                finally
                {
                    _suppressDeactivate = false;
                }
            }));
            if (_urls.Count <= 1)
            {
                prev.IsEnabled = false;
                next.IsEnabled = false;
                prev.Opacity = 0.35;
                next.Opacity = 0.35;
                prev.ToolTip = "只有一张图片";
                next.ToolTip = "只有一张图片";
            }
            Grid.SetColumn(buttons, 1);
            bar.Children.Add(buttons);

            var close = Ui.IconButton("\uE8BB", "关闭", 34);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Click += (_, _) => Anim.AnimateClose(this);
            Anim.HoverScale(close);
            Grid.SetColumn(close, 2);
            bar.Children.Add(close);
            controls.Child = bar;
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);

            Content = root;
            MouseWheel += (_, e) =>
            {
                ZoomAt(e.Delta > 0 ? 1.15 : 1 / 1.15);
                e.Handled = true;
            };
            _image.MouseLeftButtonDown += (_, e) =>
            {
                if (_zoom <= 1.01) return;
                _dragging = true;
                _dragStart = e.GetPosition(this);
                _image.CaptureMouse();
                Cursor = Cursors.SizeAll;
                e.Handled = true;
            };
            _image.MouseMove += (_, e) =>
            {
                if (!_dragging) return;
                var pos = e.GetPosition(this);
                _translate.X += pos.X - _dragStart.X;
                _translate.Y += pos.Y - _dragStart.Y;
                _dragStart = pos;
            };
            _image.MouseLeftButtonUp += (_, _) =>
            {
                _dragging = false;
                _image.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            };
            KeyDown += (_, e) =>
            {
                switch (e.Key)
                {
                    case Key.Escape: Close(); break;
                    case Key.Left: ShowAt(_index - 1); break;
                    case Key.Right: ShowAt(_index + 1); break;
                    case Key.Add:
                    case Key.OemPlus: ZoomAt(1.2); break;
                    case Key.Subtract:
                    case Key.OemMinus: ZoomAt(0.8); break;
                }
            };
            Loaded += (_, _) =>
            {
                Anim.PopIn(this);
                ShowAt(_index);
            };
            Deactivated += (_, _) =>
            {
                if (_suppressDeactivate) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsActive) Anim.AnimateClose(this);
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        private static string ExtOf(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var dot = path.LastIndexOf('.');
                return dot >= 0 && path.Length - dot <= 6 ? path[dot..] : ".jpg";
            }
            catch
            {
                return ".jpg";
            }
        }

        private Button TextButton(string text, string tip, Action action)
        {
            var button = Ui.Button(text, false, 13);
            button.ToolTip = tip;
            button.MinWidth = 34;
            button.Height = 34;
            button.Padding = new Thickness(6, 0, 6, 0);
            button.Margin = new Thickness(3, 0, 3, 0);
            button.Click += (_, _) => action();
            return button;
        }

        private Button NavButton(string glyph, string tip, Action action)
        {
            var button = Ui.IconButton(glyph, tip, 34);
            button.Margin = new Thickness(3, 0, 3, 0);

            button.Click += (_, _) => action();
            return button;
        }

        private void ZoomAt(double factor)
        {
            _zoom = Math.Clamp(_zoom * factor, 1, 6);
            _scale.ScaleX = _zoom;
            _scale.ScaleY = _zoom;
            if (_zoom <= 1.01)
            {
                _zoom = 1;
                _translate.X = 0;
                _translate.Y = 0;
            }
        }

        private void ResetZoom()
        {
            _zoom = 1;
            _scale.ScaleX = 1;
            _scale.ScaleY = 1;
            _translate.X = 0;
            _translate.Y = 0;
        }

        private async void ShowAt(int index)
        {
            if (_urls.Count == 0) return;
            if (index < 0) index = _urls.Count - 1;
            if (index >= _urls.Count) index = 0;
            _index = index;
            _counter.Text = (_index + 1) + " / " + _urls.Count;
            _status.Visibility = Visibility.Visible;
            _status.Text = "正在加载…";
            ResetZoom();
            try
            {
                var image = await ImageLoader.GetAsync(_urls[index], 2560);
                if (image == null && _fallbacks != null && index < _fallbacks.Count)
                {
                    image = await ImageLoader.GetAsync(_fallbacks[index], 1280);
                }
                if (_index != index) return;
                if (image != null)
                {
                    _image.Source = image;
                    _status.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _status.Text = "图片加载失败";
                }
            }
            catch (Exception ex)
            {
                _status.Text = "图片加载失败：" + ex.Message;
            }
        }
    }
}
