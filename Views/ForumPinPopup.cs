using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    public static class ForumPinPopup
    {
        public static void Show(FrameworkElement target, bool pinned, Action toggle)
        {
            var popup = new Popup
            {
                PlacementTarget = target,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                HorizontalOffset = -150,
                VerticalOffset = 4,
            };
            var panel = new StackPanel { Width = 170 };
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
            var button = new Button
            {
                Content = pinned ? "取消置顶" : "置顶",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 12.5,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.SetResourceReference(Control.FontFamilyProperty, "UiFont");
            button.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
            button.Click += (_, _) =>
            {
                popup.IsOpen = false;
                toggle();
            };
            panel.Children.Add(button);
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
}
