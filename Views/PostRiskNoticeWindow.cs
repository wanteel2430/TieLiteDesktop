using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Risk control reminder shown before creating a thread or reply.</summary>
    public sealed class PostRiskNoticeWindow : Window
    {
        public PostRiskNoticeWindow()
        {
            Width = 460;
            Height = 430;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            if (Svc.Window != null) Owner = Svc.Window;

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24, 22, 24, 20),
                Margin = new Thickness(14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 26,
                    ShadowDepth = 5,
                    Direction = 270,
                    Opacity = 0.28,
                },
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);
            var panel = new StackPanel();
            panel.Children.Add(Ui.Text("建议使用官方途径回帖", 19, FontWeights.SemiBold));
            var body = Ui.Text(
                "使用本客户端回帖可能会遇到回帖失败、需要验证码等问题，甚至可能导致账号被永久封禁。\n建议你通过官方途径回帖。",
                13, null, "TextSecondary");
            body.TextWrapping = TextWrapping.Wrap;
            body.Margin = new Thickness(0, 12, 0, 0);
            panel.Children.Add(body);
            var hint = Ui.Text("可在 设置 → 其他 中关闭此提醒。", 11.5, null, "TextTertiary");
            hint.Margin = new Thickness(0, 10, 0, 16);
            panel.Children.Add(hint);
            var buttons = new StackPanel();
            var official = Ui.Button("使用官方客户端回帖", true, 13);
            official.Height = 42;
            official.Margin = new Thickness(0, 0, 0, 8);
            official.Click += (_, _) =>
            {
                Ui.OpenUrl("https://tieba.baidu.com");
                DialogResult = false;
                Close();
            };
            buttons.Children.Add(official);
            var ignore = Ui.Button("无视风险，继续回帖", false, 13);
            ignore.Height = 42;
            ignore.Margin = new Thickness(0, 0, 0, 8);
            ignore.Click += (_, _) => { DialogResult = true; Close(); };
            buttons.Children.Add(ignore);
            var abandon = Ui.Button("放弃回帖", false, 13);
            abandon.Height = 42;
            abandon.Click += (_, _) => { DialogResult = false; Close(); };
            buttons.Children.Add(abandon);
            panel.Children.Add(buttons);
            card.Child = panel;
            Content = card;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }

    public static class PostRiskGuard
    {
        public static bool Confirm()
        {
            if (Svc.Settings?.ShowPostRiskWarning != true) return true;
            var window = new PostRiskNoticeWindow { Owner = Svc.Window };
            return window.ShowDialog() == true;
        }
    }
}
