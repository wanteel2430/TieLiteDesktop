using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    public sealed class ExitChoiceWindow : Window
    {
        public bool MinimizeToTray { get; private set; }
        public bool ExitApplication { get; private set; }
        public bool DoNotAsk { get; private set; }

        public ExitChoiceWindow()
        {
            Width = 420;
            Height = 300;
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
                    BlurRadius = 24,
                    ShadowDepth = 4,
                    Direction = 270,
                    Opacity = 0.26,
                },
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);
            var panel = new StackPanel();
            panel.Children.Add(Ui.Text("退出 TieLiteDesktop", 18, FontWeights.SemiBold));
            var hint = Ui.Text("要最小化到托盘继续接收消息，还是直接退出应用？", 12.5, null, "TextSecondary");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 8, 0, 14);
            panel.Children.Add(hint);

            var buttons = new StackPanel();
            var tray = Ui.Button("最小化到托盘", true, 13);
            tray.Height = 40;
            tray.Margin = new Thickness(0, 0, 0, 8);
            tray.Click += (_, _) =>
            {
                MinimizeToTray = true;
                DialogResult = true;
                Close();
            };
            buttons.Children.Add(tray);
            var exit = Ui.Button("退出应用", false, 13);
            exit.Height = 40;
            exit.Margin = new Thickness(0, 0, 0, 8);
            exit.Click += (_, _) =>
            {
                ExitApplication = true;
                DialogResult = true;
                Close();
            };
            buttons.Children.Add(exit);
            var cancel = Ui.Button("取消", false, 13);
            cancel.Height = 40;
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            var doNotAsk = new CheckBox
            {
                Content = "不再询问",
                FontSize = 11.5,
                Margin = new Thickness(0, 12, 0, 0),
            };
            doNotAsk.SetResourceReference(Control.ForegroundProperty, "TextTertiary");
            doNotAsk.Click += (_, _) => DoNotAsk = doNotAsk.IsChecked == true;
            panel.Children.Add(doNotAsk);

            card.Child = panel;
            Content = card;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }
}
