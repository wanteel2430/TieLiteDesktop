using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Controls
{
    /// <summary>Minimal iOS-style toggle used on the settings page.</summary>
    public sealed class ToggleSwitch : System.Windows.Controls.Primitives.ToggleButton
    {
        private readonly Border _track;
        private readonly Ellipse _knob;

        public ToggleSwitch()
        {
            Width = 44;
            Height = 26;
            Cursor = System.Windows.Input.Cursors.Hand;
            Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToggleButton'>" +
                "<ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/></ControlTemplate>");
            _track = new Border
            {
                Width = 44,
                Height = 26,
                CornerRadius = new CornerRadius(13),
            };
            _track.SetResourceReference(Border.BackgroundProperty, "SurfaceHover");

            _knob = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0),
            };
            var grid = new Grid();
            grid.Children.Add(_track);
            grid.Children.Add(_knob);
            Content = grid;

            Checked += (_, _) => Animate(true);
            Unchecked += (_, _) => Animate(false);
            Loaded += (_, _) => Apply(IsChecked == true);
        }

        private void Animate(bool on)
        {
            Apply(on);
        }

        private void Apply(bool on)
        {
            _track.SetResourceReference(Border.BackgroundProperty, on ? "Accent" : "SurfaceHover");
            var margin = on ? new Thickness(21, 0, 0, 0) : new Thickness(3, 0, 0, 0);
            var anim = new ThicknessAnimation(margin, TimeSpan.FromMilliseconds(210))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            _knob.BeginAnimation(FrameworkElement.MarginProperty, anim);
        }
    }
}
