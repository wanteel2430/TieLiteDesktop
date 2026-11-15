using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Threading.Tasks;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>本地缓存的帖子列表。</summary>
    public sealed class CachedPostsView : UserControl, IRefreshable
    {
        private readonly StackPanel _list;
        private readonly ProgressBar _exportBar;
        private readonly TextBlock _exportStatus;

        public CachedPostsView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 900,
                MinWidth = 680,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var head = new Grid { Margin = new Thickness(2, 4, 2, 10) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(Ui.Text("缓存的帖子", 20, FontWeights.SemiBold));
            var clear = Ui.Button("清空", false, 11.5);
            clear.Click += (_, _) =>
            {
                foreach (var item in CachedThreadStore.All()) CachedThreadStore.Remove(item.Key);
                Render();
            };
            Grid.SetColumn(clear, 1);
            head.Children.Add(clear);
            root.Children.Add(head);
            _exportStatus = Ui.Text("导出 PDF 会下载并嵌入帖子图片，图片越多耗时越长", 11.5, null, "TextTertiary");
            _exportStatus.Margin = new Thickness(2, 0, 0, 6);
            root.Children.Add(_exportStatus);
            _exportBar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed, Margin = new Thickness(2, 0, 2, 10) };
            root.Children.Add(_exportBar);
            _list = new StackPanel();
            root.Children.Add(_list);
            scroll.Content = root;
            Content = scroll;
            Loaded += (_, _) => Render();
        }

        private static async Task OpenOriginalAsync(CachedThread item)
        {
            try
            {
                var page = await Svc.Api.GetThreadAsync(item.Id, 1);
                if (page?.Thread == null || page.Thread.Id == 0 || page.Posts.Count == 0)
                {
                    Svc.Window?.ShowToast("原帖不存在或已被删除");
                    return;
                }
                Svc.Window?.OpenThread(item.Id, item.Title, item.ForumName);
            }
            catch
            {
                Svc.Window?.ShowToast("原帖不存在或已被删除");
            }
        }

        private void ExportPdf(CachedThread item)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = (string.IsNullOrWhiteSpace(item.Title) ? "tieba-thread" : item.Title) + ".pdf",
                Filter = "PDF|*.pdf",
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true) return;
            _exportBar.Value = 0;
            _exportBar.Visibility = Visibility.Visible;
            _exportStatus.Text = "正在导出 PDF…";
            var progress = new Progress<double>(value =>
            {
                _exportBar.Value = Math.Clamp(value, 0, 1) * 100;
            });
            var worker = new System.Threading.Thread(() =>
            {
                var ok = PdfExporter.Export(item, dialog.FileName, out var error, progress);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _exportBar.Visibility = Visibility.Collapsed;
                    _exportStatus.Text = "导出 PDF 会下载并嵌入帖子图片，图片越多耗时越长";
                    Svc.Window?.ShowToast(ok ? "已导出到 " + dialog.FileName : "导出失败：" + error);
                }));
            });
            worker.SetApartmentState(System.Threading.ApartmentState.STA);
            worker.IsBackground = true;
            worker.Start();
        }

        public System.Threading.Tasks.Task RefreshAsync()
        {
            Render();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void Render()
        {
            _list.Children.Clear();
            var items = CachedThreadStore.All();
            if (items.Count == 0)
            {
                _list.Children.Add(HomeView.EmptyCard("还没有缓存的帖子"));
                return;
            }
            foreach (var item in items)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                card.SetResourceReference(Border.BackgroundProperty, "Surface");
                card.SetResourceReference(Border.BorderBrushProperty, "Border");
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var info = new StackPanel();
                info.Children.Add(Ui.Text(item.Title, 14.5, FontWeights.SemiBold));
                info.Children.Add(Ui.Text(item.ForumName + " · " + item.Posts.Count + " 楼 · " +
                                           (item.OnlyOp ? "只看楼主" : "全部楼层") + " · " +
                                           (item.MaxPages > 0 ? item.MaxPages + " 页" : "旧缓存") + " · " +
                                           DateTimeOffset.FromUnixTimeSeconds(item.SavedAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                    11.5, null, "TextTertiary"));
                row.Children.Add(info);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var original = Ui.Button("看原帖", false, 11.5);
                original.Margin = new Thickness(0, 0, 8, 0);
                original.Click += async (_, _) => await OpenOriginalAsync(item);
                actions.Children.Add(original);
                var export = Ui.Button("导出 PDF", false, 11.5);
                export.Margin = new Thickness(0, 0, 8, 0);
                export.Click += (_, _) => ExportPdf(item);
                actions.Children.Add(export);
                var open = Ui.Button("查看", false, 11.5);
                open.Click += (_, _) => new CachedThreadWindow(item) { Owner = Svc.Window }.Show();
                actions.Children.Add(open);
                var remove = Ui.Button("删除", false, 11.5);
                remove.Margin = new Thickness(8, 0, 0, 0);
                remove.Click += (_, _) =>
                {
                    CachedThreadStore.Remove(item.Key);
                    Render();
                };
                actions.Children.Add(remove);
                Grid.SetColumn(actions, 1);
                row.Children.Add(actions);
                card.Child = row;
                card.MouseLeftButtonUp += (_, _) => new CachedThreadWindow(item) { Owner = Svc.Window }.Show();
                _list.Children.Add(card);
            }
        }
    }

    /// <summary>离线阅读缓存帖子。</summary>
    public sealed class CachedThreadWindow : Window
    {
        public CachedThreadWindow(CachedThread thread)
        {
            Width = 900;
            Height = 760;
            MinWidth = 520;
            MinHeight = 400;
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

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = new Grid { Margin = new Thickness(20, 16, 16, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel();
            title.Children.Add(Ui.Text(thread.Title, 19, FontWeights.SemiBold));
            title.Children.Add(Ui.Text(thread.ForumName + " · 缓存于 " +
                                       DateTimeOffset.FromUnixTimeSeconds(thread.SavedAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                11.5, null, "TextTertiary"));
            header.Children.Add(title);
            var close = Ui.IconButton("\uE8BB", "关闭", 34);
            close.Click += (_, _) => Anim.AnimateClose(this);
            Anim.HoverScale(close);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            root.Children.Add(header);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(20, 0, 20, 16),
            };
            var list = new StackPanel();
            foreach (var post in thread.Posts)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                card.SetResourceReference(Border.BackgroundProperty, "Surface");
                card.SetResourceReference(Border.BorderBrushProperty, "Border");
                var body = new StackPanel();
                var author = Ui.Text(post.Author + " · " + post.Floor + "楼", 12, FontWeights.SemiBold, "TextSecondary");
                body.Children.Add(author);
                foreach (var segment in post.Segments)
                {
                    if (segment.Kind == "Image")
                    {
                        var url = string.IsNullOrWhiteSpace(segment.LocalImage) ? segment.Url : segment.LocalImage;
                        if (string.IsNullOrWhiteSpace(url)) continue;
                        if (File.Exists(url))
                        {
                            var local = new Image
                            {
                                Source = ImageLoader.FromFile(url, 960),
                                Width = 320,
                                Height = 220,
                                Stretch = System.Windows.Media.Stretch.UniformToFill,
                                Margin = new Thickness(0, 6, 0, 6),
                            };
                            var square = new Border
                            {
                                CornerRadius = new CornerRadius(10),
                                ClipToBounds = true,
                                Width = 320,
                                Height = 220,
                                Margin = new Thickness(0, 6, 0, 6),
                                Child = local,
                            };
                            square.MouseLeftButtonUp += (_, _) =>
                                Svc.Window?.OpenImageViewer(new System.Collections.Generic.List<string> { url }, 0);
                            body.Children.Add(square);
                        }
                        else
                        {
                            var image = new RemoteImage
                            {
                                Url = url,
                                Width = 320,
                                Height = 220,
                                DecodeWidth = 960,
                                CornerRadius = new CornerRadius(10),
                                Margin = new Thickness(0, 6, 0, 6),
                                Cursor = Cursors.Hand,
                                StretchMode = System.Windows.Media.Stretch.UniformToFill,
                            };
                            image.MouseLeftButtonUp += (_, _) =>
                                Svc.Window?.OpenImageViewer(new System.Collections.Generic.List<string> { url }, 0);
                            body.Children.Add(image);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        var text = Ui.Text(segment.Text, 13, null, "TextPrimary");
                        text.Margin = new Thickness(0, 4, 0, 0);
                        text.TextWrapping = TextWrapping.Wrap;
                        body.Children.Add(text);
                    }
                }
                card.Child = body;
                list.Children.Add(card);
            }
            scroll.Content = list;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            Content = root;
            Loaded += (_, _) => Anim.PopIn(this);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Anim.AnimateClose(this);
            };
        }
    }
}
