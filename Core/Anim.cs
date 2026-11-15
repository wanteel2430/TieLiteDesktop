using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TieLiteDesktop.Core
{
    /// <summary>Shared easing / entrance / hover helpers so the UI does not feel abrupt.</summary>
    public static class Anim
    {
        public static readonly IEasingFunction EaseOut = new QuarticEase { EasingMode = EasingMode.EaseOut };
        public static readonly IEasingFunction EaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };
        public static readonly IEasingFunction EaseInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

        public static void FadeInUp(UIElement element, int delayMs = 0, double distance = 12, int duration = 280)
        {
            if (element == null) return;
            element.Opacity = 0;
            var transform = new TranslateTransform(0, distance);
            element.RenderTransform = transform;
            var begin = TimeSpan.FromMilliseconds(delayMs);

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration))
            {
                BeginTime = begin,
                EasingFunction = EaseOut,
            };
            var slide = new DoubleAnimation(distance, 0, TimeSpan.FromMilliseconds(duration))
            {
                BeginTime = begin,
                EasingFunction = EaseOut,
            };
            element.BeginAnimation(UIElement.OpacityProperty, fade);
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        public static void FadeIn(UIElement element, int duration = 220, int delayMs = 0)
        {
            if (element == null) return;
            element.Opacity = 0;
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = EaseOut,
            };
            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        /// <summary>Fade + slight scale used when dialogs open.</summary>
        public static void PopIn(Window window)
        {
            if (window == null) return;
            window.Opacity = 0;
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut };
            window.BeginAnimation(UIElement.OpacityProperty, fade);
            if (window.Content is FrameworkElement content)
            {
                content.RenderTransformOrigin = new Point(0.5, 0.5);
                var scale = new ScaleTransform(0.97, 0.97);
                content.RenderTransform = scale;
                var grow = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = EaseOut };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
        }

        /// <summary>Gives a Border a smooth hover cross-fade (no layout impact).</summary>
        public static void Hover(Border border, string hoverBrush = "SurfaceHover")
        {
            var brush = new SolidColorBrush(Colors.Transparent);
            border.Background = brush;
            border.MouseEnter += (_, _) => AnimateColor(brush, UiColors(hoverBrush), 140);
            border.MouseLeave += (_, _) => AnimateColor(brush, Colors.Transparent, 180);
        }

        private static Color UiColors(string key) =>
            (Application.Current.TryFindResource(key) as SolidColorBrush)?.Color ?? Colors.Transparent;

        public static void AnimateColor(SolidColorBrush brush, Color target, int durationMs)
        {
            var animation = new ColorAnimation(target, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = EaseOut,
                FillBehavior = FillBehavior.HoldEnd,
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        // ---------------------------------------------------------------- smooth wheel scrolling
        public static readonly DependencyProperty ScrollOffsetProperty = DependencyProperty.RegisterAttached(
            "ScrollOffset", typeof(double), typeof(Anim),
            new PropertyMetadata(0.0, OnScrollOffsetChanged));

        private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv) sv.ScrollToVerticalOffset((double)e.NewValue);
        }

        private static bool _scrollHooked;

        public static void EnableSmoothScrolling()
        {
            if (_scrollHooked) return;
            _scrollHooked = true;
            EventManager.RegisterClassHandler(typeof(ScrollViewer), Mouse.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewWheel));
        }

        private static void OnPreviewWheel(object sender, MouseWheelEventArgs e)
        {
            if (!(sender is ScrollViewer sv) || sv.ScrollableHeight <= 0) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            e.Handled = true;
            var from = sv.VerticalOffset;
            var target = Math.Clamp(from - e.Delta * 1.1, 0, sv.ScrollableHeight);
            sv.BeginAnimation(ScrollOffsetProperty, null);
            var animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = EaseOut,
            };
            sv.BeginAnimation(ScrollOffsetProperty, animation);
        }

        /// <summary>Fade + shrink a window, then close it.</summary>
        public static void AnimateClose(Window window)
        {
            if (window == null) return;
            try
            {
                var duration = TimeSpan.FromMilliseconds(150);
                var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = EaseIn };
                fade.Completed += (_, _) => window.Close();
                if (window.Content is FrameworkElement root)
                {
                    var scale = new ScaleTransform(1, 1);
                    root.RenderTransformOrigin = new Point(0.5, 0.5);
                    root.RenderTransform = scale;
                    var sx = new DoubleAnimation(1, 0.96, duration) { EasingFunction = EaseIn };
                    var sy = new DoubleAnimation(1, 0.96, duration) { EasingFunction = EaseIn };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
                }
                window.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch
            {
                window.Close();
            }
        }

        public static void HoverScale(FrameworkElement element, double scale = 1.12)
        {
            if (element == null) return;
            var transform = new ScaleTransform(1, 1);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = transform;
            element.MouseEnter += (_, _) =>
            {
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(scale, TimeSpan.FromMilliseconds(130)) { EasingFunction = EaseOut });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(scale, TimeSpan.FromMilliseconds(130)) { EasingFunction = EaseOut });
            };
            element.MouseLeave += (_, _) =>
            {
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut });
            };
        }

        // ---------------------------------------------------------------- window corners
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Windows 11 rounded window corners (no-op on older systems).</summary>
        public static void RoundWindowCorners(Window window)
        {
            void Apply()
            {
                try
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    if (handle == IntPtr.Zero) return;
                    var preference = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int));
                }
                catch
                {
                }
            }

            if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Apply();
            else window.SourceInitialized += (_, _) => Apply();
        }
    }
}
