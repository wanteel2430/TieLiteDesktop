using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Animated menu for a profile thread (visibility and delete).</summary>
    public static class ThreadActionPopup
    {
        public static void Show(FrameworkElement target, bool hidden, Action<bool> setVisibility, Action delete)
        {
            var popup = new Popup
            {
                PlacementTarget = target,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                HorizontalOffset = -170,
                VerticalOffset = 4,
            };
            var panel = new StackPanel { Width = 200 };
            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(6),
                Child = panel,
                RenderTransformOrigin = new Point(0.9, 0),
                Opacity = 0,
            };
            border.SetResourceReference(Border.BackgroundProperty, "Surface");
            border.SetResourceReference(Border.BorderBrushProperty, "Border");
            border.BorderThickness = new Thickness(1);
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.22,
            };

            AddItem(hidden ? "设为主页可见" : "设为主页不可见", () =>
            {
                popup.IsOpen = false;
                setVisibility(!hidden);
            });
            AddItem("删除", () =>
            {
                popup.IsOpen = false;
                delete();
            }, true);

            void AddItem(string text, Action action, bool danger = false)
            {
                var button = new Button
                {
                    Content = text,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 8, 12, 8),
                    FontSize = 12.5,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                button.SetResourceReference(Control.FontFamilyProperty, "UiFont");
                button.SetResourceReference(Control.ForegroundProperty, danger ? "Danger" : "TextPrimary");
                button.Click += (_, _) => action();
                panel.Children.Add(button);
            }

            popup.Child = border;
            popup.Opened += (_, _) =>
            {
                var scale = new ScaleTransform(0.94, 0.94);
                border.RenderTransform = scale;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                border.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            };
            popup.IsOpen = true;
        }
    }

    /// <summary>Small animated confirmation dialog used for destructive profile actions.</summary>
    public sealed class ConfirmWindow : Window
    {
        public ConfirmWindow(string title, string message, string confirmText = "删除")
        {
            Width = 420;
            Height = 230;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            if (Svc.Window != null) Owner = Svc.Window;

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24, 22, 24, 20),
                Margin = new Thickness(16),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 28,
                    ShadowDepth = 6,
                    Direction = 270,
                    Opacity = 0.28,
                },
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);

            var stack = new StackPanel();
            stack.Children.Add(Ui.Text(title, 17, FontWeights.SemiBold));
            var body = Ui.Text(message, 12.5, null, "TextSecondary");
            body.Margin = new Thickness(0, 10, 0, 20);
            body.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(body);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var cancel = Ui.Button("取消", false, 12.5);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            buttons.Children.Add(cancel);
            var confirm = Ui.Button(confirmText, true, 12.5);
            confirm.Click += (_, _) => { DialogResult = true; Close(); };
            buttons.Children.Add(confirm);
            stack.Children.Add(buttons);
            card.Child = stack;
            Content = card;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }
}
