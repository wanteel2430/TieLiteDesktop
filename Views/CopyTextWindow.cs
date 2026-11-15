using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Selectable copy dialog: copy all content or just the selected part.</summary>
    public sealed class CopyTextWindow : Window
    {
        private readonly TextBox _box;
        private readonly TextBlock _status;

        public CopyTextWindow(string title, string text)
        {
            Width = 680;
            Height = 520;
            MinWidth = 460;
            MinHeight = 320;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (System.Windows.Media.Brush)FindResource("AppBackground");
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(12),
            });

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(18, 14, 14, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(Ui.Text(title, 15, FontWeights.SemiBold));
            var close = Ui.IconButton("\uE8BB", "关闭", 34);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Click += (_, _) => Anim.AnimateClose(this);
            Anim.HoverScale(close);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            root.Children.Add(header);

            var host = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(18, 4, 18, 10),
            };
            host.SetResourceReference(Border.BackgroundProperty, "Surface");
            host.SetResourceReference(Border.BorderBrushProperty, "Border");
            _box = new TextBox
            {
                Text = text ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            _box.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _box.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            host.Child = _box;
            Grid.SetRow(host, 1);
            root.Children.Add(host);

            var footer = new Grid { Margin = new Thickness(18, 0, 18, 14) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status = Ui.Text("", 11.5, null, "TextTertiary");
            _status.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(_status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var copySelection = Ui.Button("复制选中", false, 12);
            copySelection.Margin = new Thickness(0, 0, 8, 0);
            copySelection.Click += (_, _) => Copy(selectedOnly: true);
            buttons.Children.Add(copySelection);
            var copyAll = Ui.Button("复制全部", true, 12);
            copyAll.Margin = new Thickness(0, 0, 8, 0);
            copyAll.Click += (_, _) => Copy(selectedOnly: false);
            buttons.Children.Add(copyAll);
            var cancel = Ui.Button("关闭", false, 12);
            cancel.Click += (_, _) => Anim.AnimateClose(this);
            buttons.Children.Add(cancel);
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
            Loaded += (_, _) =>
            {
                Anim.PopIn(this);
                _box.Focus();
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Anim.AnimateClose(this);
            };
            Deactivated += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsActive) Anim.AnimateClose(this);
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        private async void Copy(bool selectedOnly)
        {
            var text = selectedOnly ? _box.SelectedText : _box.Text;
            if (string.IsNullOrEmpty(text))
            {
                _status.Text = selectedOnly ? "先选中要复制的内容" : "没有可复制的内容";
                return;
            }
            _status.Text = await ClipboardHelper.SetTextAsync(text) ? "已复制" : "复制失败，请稍后再试";
        }
    }
}
