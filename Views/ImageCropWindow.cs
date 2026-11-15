using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Lets the user drag/resize a 16:9 crop box, keeping the original source resolution.</summary>
    public sealed class ImageCropWindow : Window
    {
        private readonly string _sourcePath;
        private readonly string _destination;
        private readonly bool _allowFill;
        private readonly StackPanel _fillPanel;
        private bool _limitToImage;
        public bool Letterbox { get; private set; }
        public string FillColorHex { get; private set; } = "#121418";
        private readonly double _aspect;
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly Border _crop;
        private readonly Thumb _mover;
        private BitmapSource _source;
        private double _imageX, _imageY, _imageWidth, _imageHeight;
        private Rect _startRect;
        private Point _startPoint;
        private string _mode;
        private Color _backgroundColor = Color.FromRgb(0x12, 0x14, 0x18);

        public ImageCropWindow(string sourcePath, string destination, double aspect, bool allowFill = true)
        {
            _sourcePath = sourcePath;
            _destination = destination;
            _allowFill = allowFill;
            Letterbox = allowFill && Svc.Settings != null && Svc.Settings.BackgroundLetterbox;
            _limitToImage = !Letterbox;
            if (Svc.Settings != null && !string.IsNullOrWhiteSpace(Svc.Settings.BackgroundFillColor))
            {
                FillColorHex = Svc.Settings.BackgroundFillColor;
                try { _backgroundColor = (Color)ColorConverter.ConvertFromString(FillColorHex); } catch { }
            }
            _aspect = aspect <= 0 ? 16.0 / 9.0 : aspect;
            Title = "裁剪图片";
            Width = 920;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "AppBackground");
            if (Svc.Window != null) Owner = Svc.Window;

            _source = Load(sourcePath);
            var root = new Grid { Margin = new Thickness(18, 14, 18, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel();
            header.Children.Add(Ui.Text("裁剪图片", 18, FontWeights.SemiBold));
            var hint = Ui.Text("拖动选框移动，拖四角缩放；输出保持原图分辨率。", 12, null, "TextTertiary");
            hint.Margin = new Thickness(0, 5, 0, 10);
            header.Children.Add(hint);
            root.Children.Add(header);

            _canvas = new Canvas { ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x18)) };
            _canvas.SizeChanged += (_, _) => LayoutImage();
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            _canvas.PreviewMouseLeftButtonDown += Canvas_PreviewMouseLeftButtonDown;

            _image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
            _canvas.Children.Add(_image);

            _crop = new Border
            {
                BorderBrush = Ui.Brush("Accent"),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                Cursor = Cursors.SizeAll,
            };
            _crop.MouseLeftButtonDown += (_, e) =>
            {
                StartDrag("move", e);
                e.Handled = true;
            };
            AddHandle("tl", HorizontalAlignment.Left, VerticalAlignment.Top);
            AddHandle("tr", HorizontalAlignment.Right, VerticalAlignment.Top);
            AddHandle("bl", HorizontalAlignment.Left, VerticalAlignment.Bottom);
            AddHandle("br", HorizontalAlignment.Right, VerticalAlignment.Bottom);
            Panel.SetZIndex(_crop, 10);
            _canvas.Children.Add(_crop);

            _mover = new Thumb
            {
                Cursor = Cursors.SizeAll,
                Background = Brushes.Transparent,
                Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                    "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='Transparent'/></ControlTemplate>"),
            };
            _mover.DragDelta += (_, e) =>
            {
                var rect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
                rect.X = Clamp(rect.X + e.HorizontalChange, _imageX, _imageX + _imageWidth - rect.Width);
                rect.Y = Clamp(rect.Y + e.VerticalChange, _imageY, _imageY + _imageHeight - rect.Height);
                ApplyRect(rect);
            };
            Panel.SetZIndex(_mover, 20);
            _canvas.Children.Add(_mover);
            root.Children.Add(_canvas);
            Grid.SetRow(_canvas, 1);

            var colorBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var colorLabel = Ui.Text("黑边颜色", 11.5, null, "TextTertiary");
            colorLabel.VerticalAlignment = VerticalAlignment.Center;
            colorLabel.Margin = new Thickness(0, 0, 10, 0);
            colorBar.Children.Add(colorLabel);
            void AddColor(string label, Color color)
            {
                var dot = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(color),
                    BorderBrush = Ui.Brush("Border"),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = label,
                };
                dot.MouseLeftButtonUp += (_, _) => SetFillColor(color);
                colorBar.Children.Add(dot);
            }
            AddColor("黑色", Color.FromRgb(0x12, 0x14, 0x18));
            AddColor("白色", Colors.White);
            AddColor("灰色", Color.FromRgb(0x80, 0x80, 0x80));
            var autoColor = Ui.Button("", false, 11.5);
            autoColor.Content = DropperContent("自动取色");
            autoColor.Margin = new Thickness(0, 0, 6, 0);
            autoColor.Click += (_, _) => SetFillColor(BackgroundSampler.Average(_sourcePath));
            colorBar.Children.Add(autoColor);
            var screenColor = Ui.Button("", false, 11.5);
            screenColor.Content = DropperContent("屏幕取色");
            screenColor.Click += (_, _) =>
            {
                var picker = new ScreenColorPickerWindow { Owner = this };
                picker.ShowDialog();
                if (picker.HasColor) SetFillColor(picker.PickedColor);
            };
            colorBar.Children.Add(screenColor);
            _fillPanel = colorBar;
            _fillPanel.Visibility = Letterbox ? Visibility.Visible : Visibility.Collapsed;

            var fillCheck = new CheckBox
            {
                Content = "超出黑区填充",
                IsChecked = Letterbox,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12.5,
            };
            fillCheck.SetResourceReference(Control.ForegroundProperty, "TextSecondary");
            fillCheck.Checked += (_, _) =>
            {
                Letterbox = true;
                _limitToImage = false;
                _fillPanel.Visibility = Visibility.Visible;
            };
            fillCheck.Unchecked += (_, _) =>
            {
                Letterbox = false;
                _limitToImage = true;
                _fillPanel.Visibility = Visibility.Collapsed;
                ConstrainCropToImage();
            };
            fillCheck.Visibility = _allowFill ? Visibility.Visible : Visibility.Collapsed;

            var options = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
            options.Children.Add(fillCheck);
            options.Children.Add(_fillPanel);
            Grid.SetRow(options, 2);
            root.Children.Add(options);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
            };
            var cancel = Ui.Button("取消", false, 13);
            cancel.Margin = new Thickness(0, 0, 10, 0);
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            footer.Children.Add(cancel);
            var ok = Ui.Button("确定裁剪", true, 13);
            ok.Click += (_, _) => SaveAndClose();
            footer.Children.Add(ok);
            root.Children.Add(footer);
            Grid.SetRow(footer, 4);

            Content = root;
            Loaded += (_, _) => LayoutImage();
            PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
            PreviewMouseMove += Window_PreviewMouseMove;
            PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
        }

        private static BitmapSource Load(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void AddHandle(string mode, HorizontalAlignment horizontal, VerticalAlignment vertical)
        {
            var handle = new Border
            {
                Width = 14,
                Height = 14,
                Background = Brushes.White,
                BorderBrush = Ui.Brush("Accent"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = new Thickness(-6),
                Cursor = mode == "tl" || mode == "br" ? Cursors.SizeNWSE : Cursors.SizeNESW,
                Tag = "crop-handle",
            };
            handle.MouseLeftButtonDown += (_, e) =>
            {
                StartDrag(mode, e);
                e.Handled = true;
            };
            if (_crop.Child == null)
            {
                _crop.Child = new Grid();
            }
            ((Grid)_crop.Child).Children.Add(handle);
        }

        private void Canvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_crop.Width <= 0) return;
            if (e.OriginalSource is FrameworkElement source && string.Equals(source.Tag as string, "crop-handle"))
            {
                return;
            }
            var point = e.GetPosition(_canvas);
            var rect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
            if (rect.Contains(point))
            {
                StartDrag("move", e);
                e.Handled = true;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_crop.Width <= 0) return;
            var point = e.GetPosition(_canvas);
            var rect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
            if (rect.Contains(point))
            {
                StartDrag("move", e);
                e.Handled = true;
            }
        }

        private void StartDrag(string mode, MouseButtonEventArgs e)
        {
            _mode = mode;
            _startPoint = e.GetPosition(_canvas);
            _startRect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
            _canvas.CaptureMouse();
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_crop.Width <= 0) return;
            var point = e.GetPosition(_canvas);
            var rect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
            if (!rect.Contains(point)) return;
            var nearLeft = Math.Abs(point.X - rect.Left) <= 22;
            var nearRight = Math.Abs(point.X - rect.Right) <= 22;
            var nearTop = Math.Abs(point.Y - rect.Top) <= 22;
            var nearBottom = Math.Abs(point.Y - rect.Bottom) <= 22;
            _mode = nearTop && nearLeft ? "tl"
                : nearTop && nearRight ? "tr"
                : nearBottom && nearLeft ? "bl"
                : nearBottom && nearRight ? "br"
                : "move";
            _startPoint = point;
            _startRect = rect;
            _canvas.CaptureMouse();
            e.Handled = true;
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_mode == null || e.LeftButton != MouseButtonState.Pressed) return;
            UpdateDrag(e.GetPosition(_canvas));
            e.Handled = true;
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mode == null) return;
            _mode = null;
            _canvas.ReleaseMouseCapture();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mode == null || e.LeftButton != MouseButtonState.Pressed) return;
            UpdateDrag(e.GetPosition(_canvas));
        }

        private void UpdateDrag(Point point)
        {
            var dx = point.X - _startPoint.X;
            var dy = point.Y - _startPoint.Y;
            var rect = _startRect;
            if (_mode == "move")
            {
                var minX = _limitToImage ? _imageX : 0;
                var minY = _limitToImage ? _imageY : 0;
                var maxX = _limitToImage ? _imageX + _imageWidth : _canvas.ActualWidth;
                var maxY = _limitToImage ? _imageY + _imageHeight : _canvas.ActualHeight;
                rect.X = Clamp(_startRect.X + dx, minX, Math.Max(minX, maxX - rect.Width));
                rect.Y = Clamp(_startRect.Y + dy, minY, Math.Max(minY, maxY - rect.Height));
            }
            else
            {
                var width = Math.Max(80, _startRect.Width + (_mode.Contains("r") ? dx : -dx));
                var maxWidth = _limitToImage ? _imageWidth : _canvas.ActualWidth;
                var maxHeight = _limitToImage ? _imageHeight : _canvas.ActualHeight;
                width = Math.Min(width, maxWidth);
                var height = width / _aspect;
                if (height > maxHeight)
                {
                    height = maxHeight;
                    width = height * _aspect;
                }
                rect.Width = width;
                rect.Height = height;
                rect.X = _mode.Contains("l") ? _startRect.Right - width : _startRect.X;
                rect.Y = _mode.Contains("t") ? _startRect.Bottom - height : _startRect.Y;
                var boundX = _limitToImage ? _imageX : 0;
                var boundY = _limitToImage ? _imageY : 0;
                var boundW = _limitToImage ? _imageWidth : _canvas.ActualWidth;
                var boundH = _limitToImage ? _imageHeight : _canvas.ActualHeight;
                rect.X = Clamp(rect.X, boundX, Math.Max(boundX, boundX + boundW - width));
                rect.Y = Clamp(rect.Y, boundY, Math.Max(boundY, boundY + boundH - height));
            }
            ApplyRect(rect);
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mode == null) return;
            _mode = null;
            _canvas.ReleaseMouseCapture();
        }

        private void LayoutImage()
        {
            if (_source == null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0) return;
            var scale = Math.Min(_canvas.ActualWidth / _source.PixelWidth, _canvas.ActualHeight / _source.PixelHeight);
            _imageWidth = _source.PixelWidth * scale;
            _imageHeight = _source.PixelHeight * scale;
            _imageX = (_canvas.ActualWidth - _imageWidth) / 2;
            _imageY = (_canvas.ActualHeight - _imageHeight) / 2;
            _image.Source = _source;
            _image.Width = _imageWidth;
            _image.Height = _imageHeight;
            Canvas.SetLeft(_image, _imageX);
            Canvas.SetTop(_image, _imageY);

            if (double.IsNaN(_crop.Width) || _crop.Width <= 0)
            {
                var width = _imageWidth * 0.82;
                var height = width / _aspect;
                if (height > _imageHeight * 0.82)
                {
                    height = _imageHeight * 0.82;
                    width = height * _aspect;
                }
                ApplyRect(new Rect(_imageX + (_imageWidth - width) / 2, _imageY + (_imageHeight - height) / 2, width, height));
            }
            else
            {
                ApplyRect(new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height));
            }
        }

        private void ApplyRect(Rect rect)
        {
            _crop.Width = rect.Width;
            _crop.Height = rect.Height;
            Canvas.SetLeft(_crop, rect.X);
            Canvas.SetTop(_crop, rect.Y);
            if (_mover != null)
            {
                _mover.Width = Math.Max(1, rect.Width - 26);
                _mover.Height = Math.Max(1, rect.Height - 26);
                Canvas.SetLeft(_mover, rect.X + 13);
                Canvas.SetTop(_mover, rect.Y + 13);
            }
        }

        private static double Clamp(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));

        private static UIElement DropperContent(string label)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new TextBlock
            {
                Text = "\uE790",
                FontSize = 15,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            panel.Children.Add(icon);
            panel.Children.Add(Ui.Text(label, 11.5, null, "TextSecondary"));
            return panel;
        }

        private void SetFillColor(Color color)
        {
            _backgroundColor = color;
            FillColorHex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            _canvas.Background = new SolidColorBrush(color);
        }

        private void ConstrainCropToImage()
        {
            if (_imageWidth <= 0 || double.IsNaN(_crop.Width)) return;
            var rect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
            var width = Math.Min(rect.Width, _imageWidth);
            var height = width / _aspect;
            if (height > _imageHeight) { height = _imageHeight; width = height * _aspect; }
            rect = new Rect(
                Clamp(rect.X, _imageX, _imageX + _imageWidth - width),
                Clamp(rect.Y, _imageY, _imageY + _imageHeight - height),
                width, height);
            ApplyRect(rect);
        }

        private Color SampleImageColor()
        {
            try
            {
                var formatted = _source.Format == PixelFormats.Bgra32
                    ? _source
                    : new FormatConvertedBitmap(_source, PixelFormats.Bgra32, null, 0);
                var points = new[]
                {
                    (formatted.PixelWidth / 2, formatted.PixelHeight / 8),
                    (formatted.PixelWidth / 8, formatted.PixelHeight / 2),
                    (formatted.PixelWidth * 7 / 8, formatted.PixelHeight / 2),
                    (formatted.PixelWidth / 2, formatted.PixelHeight * 7 / 8),
                    (formatted.PixelWidth / 2, formatted.PixelHeight / 2),
                };
                long r = 0, g = 0, b = 0;
                var buffer = new byte[4];
                foreach (var (x, y) in points)
                {
                    formatted.CopyPixels(new Int32Rect(
                        Math.Min(x, formatted.PixelWidth - 1),
                        Math.Min(y, formatted.PixelHeight - 1), 1, 1), buffer, 4, 0);
                    b += buffer[0];
                    g += buffer[1];
                    r += buffer[2];
                }
                return Color.FromRgb((byte)(r / points.Length), (byte)(g / points.Length), (byte)(b / points.Length));
            }
            catch
            {
                return _backgroundColor;
            }
        }

        private void SaveAndClose()
        {
            if (_source == null || _imageWidth <= 0) return;
            try
            {
                var cropRect = new Rect(Canvas.GetLeft(_crop), Canvas.GetTop(_crop), _crop.Width, _crop.Height);
                var scaleX = _source.PixelWidth / _imageWidth;
                var scaleY = _source.PixelHeight / _imageHeight;
                var outputWidth = Math.Max(1, (int)Math.Round(cropRect.Width * scaleX));
                var outputHeight = Math.Max(1, (int)Math.Round(cropRect.Height * scaleY));

                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawRectangle(new SolidColorBrush(_backgroundColor), null,
                        new Rect(0, 0, outputWidth, outputHeight));
                    var intersection = Rect.Intersect(cropRect,
                        new Rect(_imageX, _imageY, _imageWidth, _imageHeight));
                    if (!intersection.IsEmpty)
                    {
                        var sx = (int)Math.Round((intersection.X - _imageX) * scaleX);
                        var sy = (int)Math.Round((intersection.Y - _imageY) * scaleY);
                        var sw = Math.Max(1, Math.Min((int)Math.Round(intersection.Width * scaleX), _source.PixelWidth - sx));
                        var sh = Math.Max(1, Math.Min((int)Math.Round(intersection.Height * scaleY), _source.PixelHeight - sy));
                        var image = new CroppedBitmap(_source, new Int32Rect(sx, sy, sw, sh));
                        context.DrawImage(image, new Rect(
                            (intersection.X - cropRect.X) * scaleX,
                            (intersection.Y - cropRect.Y) * scaleY,
                            intersection.Width * scaleX,
                            intersection.Height * scaleY));
                    }
                }
                var bitmap = new RenderTargetBitmap(outputWidth, outputHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var directory = Path.GetDirectoryName(_destination);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                using (var stream = File.Create(_destination))
                {
                    encoder.Save(stream);
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Svc.Window?.ShowToast("裁剪失败：" + ex.Message);
            }
        }
    }
}
