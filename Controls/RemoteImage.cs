using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Controls
{
    /// <summary>Rounded, asynchronously loaded network image (ImageBrush based so corners clip).</summary>
    public sealed class RemoteImage : Border
    {
        public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(
            nameof(Url), typeof(string), typeof(RemoteImage),
            new PropertyMetadata(null, (d, e) => ((RemoteImage)d).Reload()));

        public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(
            nameof(DecodeWidth), typeof(int), typeof(RemoteImage),
            new PropertyMetadata(480, (d, e) => ((RemoteImage)d).Reload()));

        public static readonly DependencyProperty StretchModeProperty = DependencyProperty.Register(
            nameof(StretchMode), typeof(Stretch), typeof(RemoteImage),
            new PropertyMetadata(Stretch.UniformToFill, (d, e) => ((RemoteImage)d).ApplyStretch()));

        private readonly TextBlock _placeholder;

        public string Url
        {
            get => (string)GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public int DecodeWidth
        {
            get => (int)GetValue(DecodeWidthProperty);
            set => SetValue(DecodeWidthProperty, value);
        }

        public Stretch StretchMode
        {
            get => (Stretch)GetValue(StretchModeProperty);
            set => SetValue(StretchModeProperty, value);
        }

        public RemoteImage()
        {
            CornerRadius = new CornerRadius(12);
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            ApplyStretch();
            SetResourceReference(BackgroundProperty, "SurfaceAlt");

            _placeholder = new TextBlock
            {
                Text = "\uE8B9",
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.35,
                Visibility = Visibility.Collapsed,
            };
            _placeholder.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
            Child = _placeholder;
        }

        private void ApplyStretch()
        {
            if (Background is ImageBrush brush) brush.Stretch = StretchMode;
        }

        private async void Reload()
        {
            var url = ImageLoader.Normalize(Url);
            if (string.IsNullOrEmpty(url) || (Svc.Settings != null && !Svc.Settings.ShowImages))
            {
                Background = null;
                SetResourceReference(BackgroundProperty, "SurfaceAlt");
                _placeholder.Visibility = Visibility.Visible;
                return;
            }
            SetResourceReference(BackgroundProperty, "SurfaceAlt");
            _placeholder.Visibility = Visibility.Visible;
            try
            {
                var image = await ImageLoader.GetAsync(url, DecodeWidth);
                if (image == null || !string.Equals(url, ImageLoader.Normalize(Url), StringComparison.Ordinal)) return;
                var brush = new ImageBrush(image)
                {
                    Stretch = StretchMode,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                };
                brush.Freeze();
                Background = brush;
                _placeholder.Visibility = Visibility.Collapsed;
                var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                BeginAnimation(OpacityProperty, animation);
            }
            catch
            {
                _placeholder.Visibility = Visibility.Visible;
            }
        }
    }
}
