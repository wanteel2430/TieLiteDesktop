using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>缓存风控说明公告窗口。</summary>
    public sealed class RiskNoticeWindow : Window
    {
        public RiskNoticeWindow()
        {
            Width = 560;
            Height = 430;
            MinWidth = 420;
            MinHeight = 300;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = "缓存风控说明";
            SetResourceReference(BackgroundProperty, "AppBackground");
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
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(20, 16, 16, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel();
            title.Children.Add(Ui.Text("缓存风控说明", 18, FontWeights.SemiBold));
            title.Children.Add(Ui.Text("关于连续请求与图片下载的注意事项", 11.5, null, "TextTertiary"));
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
                Margin = new Thickness(20, 0, 20, 10),
            };
            var body = new StackPanel();
            body.Children.Add(Paragraph("缓存整帖时，客户端会按页依次请求帖子内容，并逐张下载帖子中的图片。请求越密集、页数越多，越容易触发贴吧的访问频率限制（风控）。"));
            body.Children.Add(Paragraph("触发风控后可能出现："));
            body.Children.Add(Bullet("后续页面请求失败或返回空内容；"));
            body.Children.Add(Bullet("图片下载失败，缓存中显示占位；"));
            body.Children.Add(Bullet("短时间内无法签到、发帖、回复或查看资料；"));
            body.Children.Add(Bullet("严重时需要等待较长时间才能恢复。"));
            body.Children.Add(Paragraph("建议："));
            body.Children.Add(Bullet("把最大缓存页数控制在 50 页以内；"));
            body.Children.Add(Bullet("不要短时间连续缓存大量长帖；"));
            body.Children.Add(Bullet("网络不稳定或提示请求失败时，先暂停缓存并等待一段时间；"));
            body.Children.Add(Bullet("只缓存楼主内容可以显著减少请求量与图片数量。"));
            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 16),
            };
            var ok = Ui.Button("我知道了", true, 12.5);
            ok.Click += (_, _) => Anim.AnimateClose(this);
            footer.Children.Add(ok);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
            Loaded += (_, _) => Anim.PopIn(this);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Anim.AnimateClose(this);
            };
        }

        private static UIElement Paragraph(string text)
        {
            var block = Ui.Text(text, 12.5, null, "TextSecondary");
            block.TextWrapping = TextWrapping.Wrap;
            block.Margin = new Thickness(0, 0, 0, 10);
            return block;
        }

        private static UIElement Bullet(string text)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            row.Children.Add(Ui.Text("·", 12.5, FontWeights.Bold, "Accent"));
            var block = Ui.Text(text, 12.5, null, "TextSecondary");
            block.TextWrapping = TextWrapping.Wrap;
            block.Margin = new Thickness(6, 0, 0, 0);
            block.MaxWidth = 460;
            row.Children.Add(block);
            return row;
        }
    }
}
