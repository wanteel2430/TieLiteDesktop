using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>发新主题：标题 + 正文 + 表情，提交到官方 AddThread 接口。</summary>
    public sealed class ComposeWindow : Window
    {
        private readonly ForumInfo _forum;
        private readonly TextBox _titleBox;
        private readonly TextBox _contentBox;
        private readonly TextBlock _error;
        private readonly TextBlock _count;
        private readonly Button _publish;
        private readonly ToggleSwitch _hideSwitch;
        private bool _busy;

        public long CreatedThreadId { get; private set; }

        public ComposeWindow(ForumInfo forum)
        {
            _forum = forum ?? new ForumInfo();
            Width = 640;
            Height = 590;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var shell = new Border
            {
                Margin = new Thickness(18),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(28, 22, 28, 22),
            };
            shell.SetResourceReference(BackgroundProperty, "Surface");
            shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 6,
                Opacity = 0.22,
                Color = Colors.Black,
            };

            var root = new StackPanel();

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(Ui.Text("发布新帖", 20, FontWeights.Bold));
            var subtitle = Ui.Text("发布到 " + _forum.Name + " 吧", 12, null, "TextTertiary");
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            titleStack.Children.Add(subtitle);
            header.Children.Add(titleStack);
            var close = Ui.IconButton("\uE8BB", "关闭", 30);
            close.Click += (_, _) => Close();
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            root.Children.Add(header);

            // title
            _titleBox = new TextBox
            {
                FontSize = 15,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxLength = 60,
            };
            _titleBox.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _titleBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _titleBox.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            _titleBox.SetResourceReference(TextBox.SelectionBrushProperty, "AccentSoft");
            var titleHost = MakeHost(_titleBox, 44, "标题（选填，最多 60 字）");
            root.Children.Add(titleHost);

            // content
            _contentBox = new TextBox
            {
                FontSize = 14,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0),
            };
            _contentBox.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _contentBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _contentBox.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            _contentBox.SetResourceReference(TextBox.SelectionBrushProperty, "AccentSoft");
            _contentBox.TextChanged += (_, _) => _count.Text = _contentBox.Text.Length + " 字";
            var contentHost = MakeHost(_contentBox, 190, "说点什么吧…");
            contentHost.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(contentHost);

            // emoji + counter
            var tools = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var emojiBar = new WrapPanel { VerticalAlignment = VerticalAlignment.Top };
            foreach (var pair in ContentParser.KnownEmoticons)
            {
                var image = new Image
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = pair.Key,
                    Source = ImageLoader.FromFile(ContentParser.EmoticonPath(pair.Value), 48),
                };
                var name = pair.Key;
                image.MouseLeftButtonUp += (_, _) => InsertEmoticon(name);
                emojiBar.Children.Add(image);
            }
            var emojiScroll = new ScrollViewer
            {
                Height = 84,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = emojiBar,
            };
            tools.Children.Add(emojiScroll);
            _count = Ui.Text("0 字", 11.5, null, "TextTertiary");
            _count.VerticalAlignment = VerticalAlignment.Center;
            _count.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(_count, 1);
            tools.Children.Add(_count);
            root.Children.Add(tools);

            // options
            var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            _hideSwitch = new ToggleSwitch { VerticalAlignment = VerticalAlignment.Center };
            options.Children.Add(_hideSwitch);
            var hideLabel = Ui.Text("仅自己可见", 12.5, null, "TextSecondary");
            hideLabel.Margin = new Thickness(9, 0, 0, 0);
            hideLabel.VerticalAlignment = VerticalAlignment.Center;
            options.Children.Add(hideLabel);
            root.Children.Add(options);

            _error = Ui.Text("", 12, null, "Danger");
            _error.Margin = new Thickness(0, 10, 0, 0);
            _error.Visibility = Visibility.Collapsed;
            root.Children.Add(_error);

            _publish = Ui.Button("发布", true, 14);
            _publish.Height = 42;
            _publish.Margin = new Thickness(0, 12, 0, 0);
            _publish.Click += async (_, _) => await PublishAsync();
            root.Children.Add(_publish);

            shell.Child = root;
            Content = shell;
            Loaded += (_, _) =>
            {
                Anim.PopIn(this);
                _contentBox.Focus();
            };
        }

        private static Border MakeHost(TextBox box, double height, string watermarkText)
        {
            var grid = new Grid();
            var watermark = Ui.Text(watermarkText, 13, null, "TextTertiary");
            watermark.Margin = new Thickness(2, 0, 0, 0);
            watermark.VerticalAlignment = VerticalAlignment.Center;
            watermark.IsHitTestVisible = false;
            grid.Children.Add(watermark);
            grid.Children.Add(box);
            var host = new Border
            {
                Height = height,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Child = grid,
            };
            host.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            host.SetResourceReference(BorderBrushProperty, "Border");
            host.BorderThickness = new Thickness(1);
            box.TextChanged += (_, _) =>
                watermark.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            return host;
        }

        private void InsertEmoticon(string name)
        {
            var token = "#(" + name + ")";
            var caret = _contentBox.CaretIndex;
            var text = _contentBox.Text ?? "";
            if (caret < 0 || caret > text.Length) caret = text.Length;
            _contentBox.Text = text.Insert(caret, token);
            _contentBox.CaretIndex = caret + token.Length;
            _contentBox.Focus();
        }

        private async System.Threading.Tasks.Task PublishAsync()
        {
            if (_busy) return;
            var content = _contentBox.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(Svc.Settings?.TailText))
            {
                content += "\n" + Svc.Settings.TailText.Trim();
            }
            if (content.Length == 0)
            {
                ShowError("正文不能为空");
                return;
            }
            if (!Svc.Account.IsLoggedIn)
            {
                ShowError("请先登录后再发帖");
                return;
            }
            _busy = true;
            _publish.IsEnabled = false;
            _publish.Content = "发布中…";
            ShowError("");
            try
            {
                if (!PostRiskGuard.Confirm()) return;
            var (threadId, _, message) = await Svc.Api.AddThreadAsync(content, _forum.Name, _forum.Id,
                    _titleBox.Text?.Trim() ?? "", _hideSwitch.IsChecked == true);
                CreatedThreadId = threadId;
                Svc.Window?.ShowToast(string.IsNullOrWhiteSpace(message) ? "发布成功" : message);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _busy = false;
                _publish.IsEnabled = true;
                _publish.Content = "发布";
            }
        }

        private void ShowError(string message)
        {
            _error.Text = message;
            _error.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
