using System;
using System.Collections.Generic;
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
    public sealed class PasswordSetupWindow : Window
    {
        private readonly List<Border> _dots = new();
        private string _buffer = "";
        private string _firstPin = "";
        private string _secondPin = "";
        private int _stage;

        private readonly bool _resetMode;

        public PasswordSetupWindow(bool resetMode = false)
        {
            _resetMode = resetMode;
            Width = 470;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Title = resetMode ? "重置密码" : AppPassword.HasPassword ? "更改密码" : "设置密码";
            SetResourceReference(BackgroundProperty, "AppBackground");
            if (Svc.Window != null) Owner = Svc.Window;

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            root.Children.Add(Ui.Text(resetMode ? "重置应用密码" : AppPassword.HasPassword ? "更改应用密码" : "设置应用密码", 18, FontWeights.SemiBold));
            var hint = Ui.Text("密码用于锁定本应用，忘记后只能重置密码（会退出登录）。", 12, null, "TextTertiary");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 6, 0, 14);
            root.Children.Add(hint);

            PasswordBox MakeBox(string placeholder)
            {
                var box = new PasswordBox
                {
                    Height = 38,
                    FontSize = 14,
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 10),
                };
                box.SetResourceReference(PasswordBox.BackgroundProperty, "SurfaceAlt");
                box.SetResourceReference(PasswordBox.ForegroundProperty, "TextPrimary");
                box.SetResourceReference(PasswordBox.BorderBrushProperty, "Border");
                box.SetResourceReference(PasswordBox.CaretBrushProperty, "TextPrimary");
                return box;
            }

            var current = MakeBox("当前密码");
            if (AppPassword.HasPassword && !resetMode) root.Children.Add(current);

            var typePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 12) };
            var pinRadio = new RadioButton
            {
                Content = "六位 PIN",
                GroupName = "PasswordType",
                IsChecked = (Svc.Settings.PasswordType ?? "pin") != "custom",
                Margin = new Thickness(0, 0, 8, 0),
            };
            pinRadio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            var customRadio = new RadioButton
            {
                Content = "自定义密码",
                GroupName = "PasswordType",
                IsChecked = Svc.Settings.PasswordType == "custom",
            };
            customRadio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            typePanel.Children.Add(pinRadio);
            typePanel.Children.Add(customRadio);
            root.Children.Add(typePanel);

            var error = Ui.Text("", 12, null, "Danger");
            error.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(error);

            // ---------------- custom password panel
            var customPanel = new StackPanel();
            var first = MakeBox("新密码");
            var firstPlain = new TextBox
            {
                Height = 38,
                FontSize = 14,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 6, 10, 6),
            };
            firstPlain.SetResourceReference(TextBox.BackgroundProperty, "SurfaceAlt");
            firstPlain.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            firstPlain.SetResourceReference(TextBox.BorderBrushProperty, "Border");
            firstPlain.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            first.PasswordChanged += (_, _) =>
            {
                if (firstPlain.Visibility != Visibility.Visible) firstPlain.Text = first.Password;
            };
            firstPlain.TextChanged += (_, _) =>
            {
                if (firstPlain.Visibility == Visibility.Visible) first.Password = firstPlain.Text;
            };
            var second = MakeBox("确认新密码");
            var show = new CheckBox
            {
                Content = "显示密码",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
            };
            show.SetResourceReference(Control.ForegroundProperty, "TextSecondary");
            show.Checked += (_, _) =>
            {
                firstPlain.Text = first.Password;
                first.Visibility = Visibility.Collapsed;
                firstPlain.Visibility = Visibility.Visible;
                firstPlain.Focus();
            };
            show.Unchecked += (_, _) =>
            {
                first.Password = firstPlain.Text;
                firstPlain.Visibility = Visibility.Collapsed;
                first.Visibility = Visibility.Visible;
                first.Focus();
            };
            customPanel.Children.Add(first);
            customPanel.Children.Add(firstPlain);
            customPanel.Children.Add(second);
            customPanel.Children.Add(show);
            root.Children.Add(customPanel);

            // ---------------- PIN panel
            var pinPanel = new StackPanel { Visibility = Visibility.Collapsed };
            var pinHint = Ui.Text("输入 6 位 PIN", 12, null, "TextTertiary");
            pinHint.HorizontalAlignment = HorizontalAlignment.Center;
            pinHint.Margin = new Thickness(0, 0, 0, 10);
            pinPanel.Children.Add(pinHint);
            var dots = new UniformGrid
            {
                Columns = 6,
                Width = 220,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12),
            };
            for (var i = 0; i < 6; i++)
            {
                var dot = new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Margin = new Thickness(3, 0, 3, 0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1.5),
                };
                dot.SetResourceReference(Border.BorderBrushProperty, "TextTertiary");
                _dots.Add(dot);
                dots.Children.Add(dot);
            }
            pinPanel.Children.Add(dots);

            void RefreshDots()
            {
                for (var i = 0; i < _dots.Count; i++)
                {
                    _dots[i].Background = i < _buffer.Length ? Ui.Brush("TextPrimary") : Brushes.Transparent;
                }
            }

            void SavePin()
            {
                if (!_resetMode && AppPassword.HasPassword && !AppPassword.Verify(current.Password))
                {
                    error.Text = "当前密码不正确";
                    return;
                }
                AppPassword.Set(_firstPin, "pin");
                if (!AppPassword.HasPassword)
                {
                    error.Text = "保存失败，请重试";
                    return;
                }
                DialogResult = true;
                Close();
            }

            void PinInput(string value)
            {
                if (value == "C") _buffer = "";
                else if (value == "⌫")
                {
                    if (_buffer.Length > 0) _buffer = _buffer[..^1];
                }
                else if (_buffer.Length < 6) _buffer += value;

                RefreshDots();
                if (_buffer.Length < 6) return;

                if (_stage == 0)
                {
                    _firstPin = _buffer;
                    _buffer = "";
                    _stage = 1;
                    pinHint.Text = "再次输入 6 位 PIN";
                    RefreshDots();
                    return;
                }

                _secondPin = _buffer;
                if (_firstPin == _secondPin)
                {
                    error.Text = "";
                    SavePin();
                }
                else
                {
                    error.Text = "两次 PIN 不一致，请重新输入";
                    pinHint.Text = "两次 PIN 不一致，请重新输入";
                    _firstPin = "";
                    _secondPin = "";
                    _buffer = "";
                    _stage = 0;
                    RefreshDots();
                }
            }

            var pad = new UniformGrid { Columns = 3, Width = 230, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "⌫" })
            {
                var button = Ui.Button(key, false, 15);
                button.Height = 40;
                button.Margin = new Thickness(2);
                var value = key;
                button.Click += (_, _) => PinInput(value);
                pad.Children.Add(button);
            }
            pinPanel.Children.Add(pad);
            root.Children.Add(pinPanel);

            void ApplyMode()
            {
                var pin = pinRadio.IsChecked == true;
                pinPanel.Visibility = pin ? Visibility.Visible : Visibility.Collapsed;
                customPanel.Visibility = pin ? Visibility.Collapsed : Visibility.Visible;
                if (!pin)
                {
                    _buffer = "";
                    _firstPin = "";
                    _secondPin = "";
                    _stage = 0;
                    pinHint.Text = "输入 6 位 PIN";
                    RefreshDots();
                }
            }
            pinRadio.Checked += (_, _) => ApplyMode();
            customRadio.Checked += (_, _) => ApplyMode();
            ApplyMode();

            // ---------------- actions
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            if (AppPassword.HasPassword && !resetMode)
            {
                var remove = Ui.Button("取消密码", false, 12.5);
                remove.Margin = new Thickness(0, 0, 8, 0);
                remove.Click += (_, _) =>
                {
                    if (!AppPassword.Verify(current.Password))
                    {
                        error.Text = "当前密码不正确";
                        return;
                    }
                    AppPassword.Clear();
                    DialogResult = true;
                    Close();
                };
                actions.Children.Add(remove);
            }
            var save = Ui.Button("保存", true, 12.5);
            save.Click += (_, _) =>
            {
                if (pinRadio.IsChecked == true)
                {
                    if (_firstPin.Length != 6 || _firstPin != _secondPin)
                    {
                        error.Text = "请连续输入两次相同的 6 位 PIN";
                        return;
                    }
                    SavePin();
                    return;
                }
                if (!_resetMode && AppPassword.HasPassword && !AppPassword.Verify(current.Password))
                {
                    error.Text = "当前密码不正确";
                    return;
                }
                var password = first.Password ?? "";
                if (password.Length < 4)
                {
                    error.Text = "自定义密码至少 4 位";
                    return;
                }
                if (password != second.Password)
                {
                    error.Text = "两次输入的密码不一致";
                    return;
                }
                AppPassword.Set(password, "custom");
                if (!AppPassword.HasPassword)
                {
                    error.Text = "保存失败，请重试";
                    return;
                }
                DialogResult = true;
                Close();
            };
            actions.Children.Add(save);
            root.Children.Add(actions);
            Content = root;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }
}
