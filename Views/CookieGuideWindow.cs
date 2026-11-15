using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Detailed Cookie login guide.</summary>
    public sealed class CookieGuideWindow : Window
    {
        public CookieGuideWindow()
        {
            Width = 600;
            Height = 640;
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
                Padding = new Thickness(26, 20, 26, 22),
            };
            shell.SetResourceReference(Border.BackgroundProperty, "Surface");
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
            var title = new StackPanel();
            title.Children.Add(Ui.Text("如何获取贴吧 Cookie", 20, FontWeights.Bold));
            var subtitle = Ui.Text("只需要 BDUSS，回帖/签到建议同时提供 STOKEN", 12, null, "TextTertiary");
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            title.Children.Add(subtitle);
            header.Children.Add(title);
            var close = Ui.IconButton("\uE8BB", "关闭", 30);
            close.Click += (_, _) => Close();
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            root.Children.Add(header);

            var steps = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            AddStep(steps, 1, "点击下面的「打开贴吧网页」，在浏览器里登录你的百度账号。",
                "如果已经登录，直接进入下一步。");
            AddStep(steps, 2, "在贴吧页面按 F12 打开开发者工具，选择「Application / 应用」标签。",
                "如果没有 F12，可右键页面 → 检查。");
            AddStep(steps, 3, "左侧展开 Cookies → https://tieba.baidu.com。",
                "Edge 在「存储 → Cookie」下，Chrome 在「应用 → Cookie」下。");
            AddStep(steps, 4, "在列表中找到名为 BDUSS 的行，双击 Value 全选并复制。",
                "BDUSS 很长，注意不要漏字符或带空格。");
            AddStep(steps, 5, "回到登录窗口，把内容粘贴成：BDUSS=你复制的值",
                "需要回帖、签到时再追加：; STOKEN=另一行的值");
            AddStep(steps, 6, "点击「登录」。如果提示失效，请重新登录网页版后再复制一次。",
                "BDUSS 等同登录态，请勿发给他人。");
            root.Children.Add(steps);

            var tip = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 14, 0, 0),
            };
            tip.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
            tip.Child = Ui.Text("提示：只用 Cookie 登录后，应用会自动校验登录态；名字和头像缺失不影响使用。",
                12, null, "Accent");
            root.Children.Add(tip);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
            var open = Ui.Button("打开贴吧网页", true, 12.5);
            open.Click += (_, _) =>
            {
                if (!Ui.OpenUrl("https://tieba.baidu.com"))
                {
                    Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
                }
            };
            buttons.Children.Add(open);
            var ok = Ui.Button("知道了", false, 12.5);
            ok.Margin = new Thickness(8, 0, 0, 0);
            ok.Click += (_, _) => Close();
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            shell.Child = root;
            Content = shell;
            Loaded += (_, _) => Anim.PopIn(this);
        }

        private static void AddStep(StackPanel parent, int index, string title, string detail)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var number = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                VerticalAlignment = VerticalAlignment.Top,
                Child = Ui.Text(index.ToString(), 11, FontWeights.Bold, "Accent"),
            };
            number.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
            ((TextBlock)number.Child).HorizontalAlignment = HorizontalAlignment.Center;
            row.Children.Add(number);

            var text = new StackPanel();
            var bold = Ui.Text(title, 13, FontWeights.Medium);
            bold.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(bold);
            var sub = Ui.Text(detail, 11.5, null, "TextTertiary");
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 3, 0, 0);
            text.Children.Add(sub);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            parent.Children.Add(row);
        }
    }
}
