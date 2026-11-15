using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Controls
{
    /// <summary>Small imperative UI helpers so pages can be built without heavy XAML.</summary>
    public static class Ui
    {
        public static object Res(string key) => Application.Current.TryFindResource(key);

        public static Brush Brush(string key) => Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

        public static TextBlock Text(string text, double size = 13.5, FontWeight? weight = null, string color = "TextPrimary")
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            tb.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
            tb.SetResourceReference(TextBlock.ForegroundProperty, color);
            if (weight.HasValue) tb.FontWeight = weight.Value;
            return tb;
        }

        public static TextBlock Icon(string glyph, double size = 14, string color = "TextSecondary")
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            tb.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            tb.SetResourceReference(TextBlock.ForegroundProperty, color);
            return tb;
        }

        public static StackPanel Row(params UIElement[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var child in children) panel.Children.Add(child);
            return panel;
        }

        public static StackPanel Col(params UIElement[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var child in children) panel.Children.Add(child);
            return panel;
        }

        public static Border Card(UIElement child, double padding = 18, double radius = 18)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(radius),
                Padding = new Thickness(padding),
                Margin = new Thickness(0, 0, 0, 10),
            };
            border.SetResourceReference(Border.BackgroundProperty, "CardSurface");
            border.SetResourceReference(Border.BorderBrushProperty, "Border");
            border.BorderThickness = new Thickness(1);
            if (child is ThreadRow)
            {
                var content = new Grid();
                var hover = new Border
                {
                    CornerRadius = new CornerRadius(radius),
                    Opacity = 0,
                    IsHitTestVisible = false,
                    Margin = new Thickness(-padding),
                };
                hover.SetResourceReference(Border.BackgroundProperty, "SurfaceHover");
                content.Children.Add(hover);
                content.Children.Add(child);
                border.Child = content;
                border.MouseEnter += (_, _) =>
                {
                    hover.BeginAnimation(UIElement.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0.55,
                            TimeSpan.FromMilliseconds(140)));
                };
                border.MouseLeave += (_, _) =>
                {
                    hover.BeginAnimation(UIElement.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0,
                            TimeSpan.FromMilliseconds(180)));
                };
            }
            else
            {
                border.Child = child;
            }
            return border;
        }

        public static Border Chip(string text, string foreground = "TextSecondary", string background = "SurfaceAlt")
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Text(text, 11, null, foreground),
            };
            border.SetResourceReference(Border.BackgroundProperty, background);
            return border;
        }

        public static Button Button(string text, bool primary = false, double fontSize = 13)
        {
            var button = new Button
            {
                Content = text,
                FontSize = fontSize,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, primary ? "PrimaryButton" : "SecondaryButton");
            return button;
        }

        public static Button IconButton(string glyph, string tooltip = null, double size = 34)
        {
            var button = new Button
            {
                Width = size,
                Height = size,
                ToolTip = tooltip,
                Content = new TextBlock
                {
                    Text = glyph,
                    FontSize = size * 0.44,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            if (button.Content is TextBlock icon)
            {
                icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
                icon.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            }
            button.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
            return button;
        }

        /// <summary>Rounded avatar backed by a network image; draws a letter placeholder first.</summary>
        public static Border Avatar(string url, double size = 38, string fallback = "吧")
        {
            var border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                ClipToBounds = true,
            };
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            var letter = Text(string.IsNullOrEmpty(fallback) ? "吧" : fallback.Substring(0, 1), size * 0.4, FontWeights.SemiBold, "TextSecondary");
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            border.Child = letter;
            if (string.IsNullOrWhiteSpace(url)) return border;

            _ = LoadAvatarAsync(border, url, (int)(size * 2));
            return border;
        }

        private static async System.Threading.Tasks.Task LoadAvatarAsync(Border border, string url, int width)
        {
            try
            {
                var image = await Core.ImageLoader.GetAsync(url, width);
                if (image == null) return;
                var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                border.Background = brush;
                border.Child = null;
            }
            catch
            {
            }
        }

        public static string ShortNum(long value)
        {
            if (value >= 100000000) return (value / 100000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "亿";
            if (value >= 10000) return (value / 10000.0).ToString("0.#", CultureInfo.InvariantCulture) + "万";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string TimeAgo(long unixSeconds)
        {
            if (unixSeconds <= 0) return "";
            if (unixSeconds > 1000000000000) unixSeconds /= 1000;
            var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
            var now = DateTimeOffset.Now;
            var span = now - time;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + "分钟前";
            if (span.TotalHours < 24 && time.Date == now.Date) return (int)span.TotalHours + "小时前";
            if (time.Date == now.Date.AddDays(-1)) return "昨天 " + time.ToString("HH:mm");
            if (now.Year == time.Year) return time.ToString("MM-dd");
            return time.ToString("yyyy-MM-dd");
        }

        public static string TimeFull(long unixSeconds)
        {
            if (unixSeconds <= 0) return "";
            if (unixSeconds > 1000000000000) unixSeconds /= 1000;
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        /// <summary>Opens a URL with the default browser, with several fallbacks. Returns false when all fail.</summary>
        public static bool OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var edgePath = EdgePlayer.FindEdgePath();
            if (!string.IsNullOrEmpty(edgePath))
            {
                try
                {
                    var edgeInfo = new System.Diagnostics.ProcessStartInfo(edgePath)
                    {
                        UseShellExecute = false,
                    };
                    edgeInfo.ArgumentList.Add(url);
                    System.Diagnostics.Process.Start(edgeInfo);
                    return true;
                }
                catch
                {
                }
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch
            {
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "rundll32.exe", "url.dll,FileProtocolHandler \"" + url + "\"") { UseShellExecute = false });
                return true;
            }
            catch
            {
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "cmd.exe", "/c start \"\" \"" + url + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                return true;
            }
            catch
            {
            }
            // last resort: launch Edge directly (present on every Windows 10/11 install)
            foreach (var edge in new[]
                     {
                         @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                         @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                     })
            {
                try
                {
                    if (!System.IO.File.Exists(edge)) continue;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(edge, url)
                    {
                        UseShellExecute = false,
                    });
                    return true;
                }
                catch
                {
                }
            }
            try
            {
                if (!ClipboardHelper.TrySetText(url)) throw new InvalidOperationException("clipboard unavailable");
            }
            catch
            {
            }
            return false;
        }
    }
}
