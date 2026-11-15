using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TieLiteDesktop.Views
{
    /// <summary>Full-screen eyedropper: click anywhere to sample a screen pixel.</summary>
    public sealed class ScreenColorPickerWindow : Window
    {
        public Color PickedColor { get; private set; } = Colors.Transparent;
        public bool HasColor { get; private set; }

        public ScreenColorPickerWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            ResizeMode = ResizeMode.NoResize;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            MouseLeftButtonUp += (_, _) =>
            {
                Pick();
                Close();
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }

        private void Pick()
        {
            try
            {
                var point = PointToScreen(Mouse.GetPosition(this));
                using var bitmap = new System.Drawing.Bitmap(1, 1);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                graphics.CopyFromScreen((int)point.X, (int)point.Y, 0, 0, new System.Drawing.Size(1, 1));
                var color = bitmap.GetPixel(0, 0);
                PickedColor = Color.FromRgb(color.R, color.G, color.B);
                HasColor = true;
            }
            catch
            {
                HasColor = false;
            }
        }
    }
}
