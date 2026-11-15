using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Small password prompt used by privacy actions and entering the privacy page.</summary>
    public sealed class PasswordPromptWindow : Window
    {
        private string _buffer = "";
        private readonly System.Collections.Generic.List<Border> _dots = new();

        public bool ForgotPassword { get; private set; }

        public PasswordPromptWindow(string title, string message, bool allowForgot = false)
        {
            Width = 360;
            Height = 470;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            if (Svc.Window != null) Owner = Svc.Window;

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(22, 20, 22, 18),
                Margin = new Thickness(14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 4,
                    Direction = 270,
                    Opacity = 0.25,
                },
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            card.BorderThickness = new Thickness(1);
            var root = new StackPanel();
            var titleButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, -8, -8, 2),
            };
            var minimize = Ui.IconButton("\uE921", "最小化", 24);
            minimize.Click += (_, _) => WindowState = WindowState.Minimized;
            titleButtons.Children.Add(minimize);
            var maximize = Ui.IconButton("\uE922", "最大化", 24);
            maximize.Click += (_, _) =>
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };
            titleButtons.Children.Add(maximize);
            var close = Ui.IconButton("\uE8BB", "关闭", 24);
            close.Click += (_, _) => { DialogResult = false; Close(); };
            titleButtons.Children.Add(close);
            root.Children.Add(titleButtons);
            root.Children.Add(Ui.Text(title, 17, FontWeights.SemiBold));
            var hint = Ui.Text(message, 12, null, "TextSecondary");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 8, 0, 14);
            root.Children.Add(hint);

            var isPin = Svc.Settings.PasswordType != "custom";
            var dotsPanel = new UniformGrid { Columns = 6, Width = 220, Height = 30, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            var box = new PasswordBox
            {
                Height = 38,
                FontSize = 15,
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 8),
                MaxLength = isPin ? 6 : 64,
                Visibility = isPin ? Visibility.Collapsed : Visibility.Visible,
            };
            box.SetResourceReference(PasswordBox.BackgroundProperty, "SurfaceAlt");
            box.SetResourceReference(PasswordBox.ForegroundProperty, "TextPrimary");
            box.SetResourceReference(PasswordBox.BorderBrushProperty, "Border");
            box.SetResourceReference(PasswordBox.CaretBrushProperty, "TextPrimary");

            for (var i = 0; i < 6; i++)
            {
                var dot = new Border
                {
                    Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                    Margin = new Thickness(3, 0, 3, 0), BorderThickness = new Thickness(1.5),
                    Background = Brushes.Transparent,
                };
                dot.SetResourceReference(Border.BorderBrushProperty, "TextTertiary");
                _dots.Add(dot);
                dotsPanel.Children.Add(dot);
            }
            dotsPanel.Visibility = isPin ? Visibility.Visible : Visibility.Collapsed;
            root.Children.Add(dotsPanel);
            root.Children.Add(box);

            var error = Ui.Text("", 11.5, null, "Danger");
            error.Margin = new Thickness(0, 2, 0, 6);
            root.Children.Add(error);

            void UpdateDots()
            {
                for (var i = 0; i < _dots.Count; i++)
                {
                    _dots[i].Background = i < _buffer.Length ? Ui.Brush("TextPrimary") : Brushes.Transparent;
                }
            }

            var pad = new UniformGrid { Columns = 3, Width = 220, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "⌫" })
            {
                var button = Ui.Button(key, false, 14);
                button.Height = 36;
                button.Margin = new Thickness(2);
                var value = key;
                button.Click += (_, _) =>
                {
                    if (value == "C") _buffer = "";
                    else if (value == "⌫") { if (_buffer.Length > 0) _buffer = _buffer[..^1]; }
                    else if (_buffer.Length < 6) _buffer += value;
                    UpdateDots();
                    if (_buffer.Length == 6) TryUnlock();
                };
                pad.Children.Add(button);
            }
            pad.Visibility = isPin ? Visibility.Visible : Visibility.Collapsed;
            root.Children.Add(pad);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var cancel = Ui.Button("取消", false, 12);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            buttons.Children.Add(cancel);
            var unlock = Ui.Button("确认", true, 12);
            unlock.Click += (_, _) => TryUnlock();
            buttons.Children.Add(unlock);
            root.Children.Add(buttons);
            if (allowForgot)
            {
                var forgot = new TextBlock
                {
                    Text = "忘记密码？退出登录并清除应用密码",
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0),
                    Cursor = Cursors.Hand,
                };
                forgot.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
                forgot.MouseLeftButtonUp += (_, _) =>
                {
                    ForgotPassword = true;
                    DialogResult = false;
                    Close();
                };
                root.Children.Add(forgot);
            }
            card.Child = root;
            Content = card;

            void TryUnlock()
            {
                var value = isPin ? _buffer : box.Password;
                if (AppPassword.Verify(value))
                {
                    DialogResult = true;
                    Close();
                    return;
                }
                error.Text = "密码不正确";
                _buffer = "";
                box.Password = "";
                UpdateDots();
            }

            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) TryUnlock(); };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
            Loaded += (_, _) =>
            {
                Anim.PopIn(this);
                box.Focus();
            };
        }
    }
}
