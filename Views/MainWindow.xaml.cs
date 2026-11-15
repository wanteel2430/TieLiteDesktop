using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;
using System.Windows.Media.Imaging;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    public partial class MainWindow : Window
    {
        private readonly ScaleTransform _rootScale;
        private bool _wasMinimized;
        private bool _animatingRestore;
        private bool _closing;
        private bool _forceClose;
        private Forms.NotifyIcon _trayIcon;
        private readonly List<Border> _pinDots = new();
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> _startupData =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                var iconPath = System.IO.Path.Combine(Svc.AssetsDir, "app.ico");
                if (System.IO.File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath));
            }
            catch
            {
            }
            Svc.Window = this;
            Svc.Nav.Navigated += (entry, back) => ShowPage(entry, back);
            Svc.ThemeChanged += OnThemeChanged;
            Loaded += MainWindow_Loaded;
            SetResourceReference(BackgroundProperty, "AppBackground");
            StateChanged += (_, _) => UpdateMaximizeGlyph();

            UserCard.RenderTransformOrigin = new Point(0.5, 0.5);
            var cardScale = new ScaleTransform(1, 1);
            UserCard.RenderTransform = cardScale;
            UserCard.MouseLeftButtonDown += (_, _) => AnimateScale(cardScale, 0.975, 70);
            UserCard.MouseLeftButtonUp += (_, _) => AnimateScale(cardScale, 1, 150);
            UserCard.MouseLeave += (_, _) => AnimateScale(cardScale, 1, 120);

            _rootScale = new ScaleTransform(1, 1);
            RootGrid.RenderTransformOrigin = new Point(0.5, 0.5);
            RootGrid.RenderTransform = _rootScale;
            SplashOverlay.Opacity = 0;
            try
            {
                SplashLogo.Source = ImageLoader.FromFile(System.IO.Path.Combine(Svc.AssetsDir, "logo.png"), 152);
            }
            catch
            {
            }
            StateChanged += (_, _) => OnWindowStateChanged();
            Activated += (_, _) =>
            {
                if (_wasMinimized && WindowState == WindowState.Normal &&
                    Svc.Settings.LockOnRestore && AppPassword.HasPassword)
                {
                    ShowLock();
                }
            };
            ApplySidebarState(Svc.Settings.SidebarCollapsed, false);
            for (var i = 0; i < 6; i++)
            {
                var dot = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(5, 0, 5, 0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1.5),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1, 1),
                };
                dot.SetResourceReference(Border.BorderBrushProperty, "TextTertiary");
                _pinDots.Add(dot);
                PinDots.Children.Add(dot);
            }
            LockBox.PasswordChanged += (_, _) =>
            {
                UpdatePinDots();
                if (Svc.Settings.PasswordType == "custom" &&
                    !string.IsNullOrEmpty(LockBox.Password) &&
                    AppPassword.Verify(LockBox.Password))
                {
                    HideLock();
                    LockError.Visibility = Visibility.Collapsed;
                }
            };
            foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "⌫" })
            {
                var button = Ui.Button(key, false, 15);
                button.Height = 46;
                button.Margin = new Thickness(4);
                button.RenderTransformOrigin = new Point(0.5, 0.5);
                var keyScale = new ScaleTransform(1, 1);
                button.RenderTransform = keyScale;
                button.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    keyScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
                    keyScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
                };
                button.PreviewMouseLeftButtonUp += (_, _) =>
                {
                    keyScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
                    keyScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
                };
                var value = key;
                button.Click += (_, _) =>
                {
                    if (value == "C") LockBox.Password = "";
                    else if (value == "⌫")
                    {
                        if (LockBox.Password.Length > 0)
                        {
                            LockBox.Password = LockBox.Password[..^1];
                        }
                    }
                    else if (LockBox.Password.Length < 6)
                    {
                        LockBox.Password += value;
                    }
                    LockBox.Focus();
                };
                PinPad.Children.Add(button);
            }
            LockContent.RenderTransformOrigin = new Point(0.5, 0.5);
            LockContent.RenderTransform = new ScaleTransform(0.94, 0.94);
            AnimateLockButton(MinimizeButton);
            AnimateLockButton(MaximizeButton);
            AnimateLockButton(CloseButton);
            AnimateLockButton(LockMinButton);
            AnimateLockButton(LockMaxButton);
            AnimateLockButton(LockCloseButton);
            RefreshLockButton();
            PreviewDragOver += (_, e) =>
            {
                long dragThreadId;
                if (TryGetDroppedText(e.Data, out var dropped) && TiebaLinkParser.TryGetThreadId(dropped, out dragThreadId))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
            };
            PreviewDrop += (_, e) =>
            {
                if (!TryGetDroppedText(e.Data, out var dropped) ||
                    !TiebaLinkParser.TryGetThreadId(dropped, out var threadId))
                {
                    return;
                }
                e.Handled = true;
                OpenThread(threadId, "", "");
                ShowToast("已打开拖入的帖子");
            };
        }

        private void OnWindowStateChanged()
        {
            UpdateMaximizeGlyph();
            if (WindowState == WindowState.Minimized)
            {
                _wasMinimized = true;
                return;
            }
            if (WindowState == WindowState.Normal && _wasMinimized && !_animatingRestore)
            {
                _wasMinimized = false;
                _animatingRestore = true;
                RootGrid.Opacity = 1;
                _rootScale.ScaleX = 1;
                _rootScale.ScaleY = 1;
                if (Svc.Settings.LockOnRestore && AppPassword.HasPassword)
                {
                    ShowLock();
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RootGrid.Opacity = 0;
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
                    fade.Completed += (_, _) =>
                    {
                        RootGrid.Opacity = 1;
                        _animatingRestore = false;
                    };
                    RootGrid.BeginAnimation(OpacityProperty, fade);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private async System.Threading.Tasks.Task AutoSignAsync()
        {
            if (!Svc.Settings.AutoSign || !Svc.Account.IsLoggedIn) return;
            try
            {
                var forums = await Svc.Api.GetFollowedForumsAsync();
                var targets = forums.Where(forum => !forum.IsSigned).ToList();
                if (targets.Count == 0) return;
                var success = 0;
                foreach (var forum in targets)
                {
                    try
                    {
                        await Svc.Api.SignForumAsync(forum.Id, forum.Name);
                        success++;
                    }
                    catch
                    {
                    }
                }
                if (success > 0) ShowToast("自动签到完成：" + success + " 个吧");
            }
            catch
            {
            }
        }

        private async System.Threading.Tasks.Task PlayStartupAnimationAsync()
        {
            SplashOverlay.Visibility = Visibility.Visible;
            SplashOverlay.Opacity = 0;
            SplashLogo.Opacity = 0;
            SplashTitle.Opacity = 0;
            SplashSubtitle.Opacity = 0;
            SplashBar.Opacity = 0;
            SplashStatus.Opacity = 0;
            RootGrid.Opacity = 0;
            _rootScale.ScaleX = 0.97;
            _rootScale.ScaleY = 0.97;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var soft = new QuarticEase { EasingMode = EasingMode.EaseOut };

            SplashBackdrop.Visibility = Visibility.Collapsed;
            SplashScrim.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(Svc.Settings.SplashImage) &&
                System.IO.File.Exists(Svc.Settings.SplashImage))
            {
                try
                {
                    SplashBackdrop.Source = ImageLoader.FromFile(Svc.Settings.SplashImage, 1920);
                    SplashBackdrop.Visibility = Visibility.Visible;
                    SplashScrim.Visibility = Visibility.Visible;
                    SplashBackdrop.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 0.62, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
                    SplashScrim.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 0.5, TimeSpan.FromMilliseconds(500)) { EasingFunction = ease });
                }
                catch
                {
                }
            }

            if (SplashGlow != null)
            {
                var accent = AccentTheme.GetColor(Svc.Settings.Accent, Svc.Settings.CustomAccent);
                SplashGlow.Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x2A, accent.R, accent.G, accent.B), 0),
                        new GradientStop(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 0.72),
                    },
                };
            }

            SplashLogo.RenderTransformOrigin = new Point(0.5, 0.5);
            var logoScale = new ScaleTransform(0.82, 0.82);
            SplashLogo.RenderTransform = logoScale;

            SplashOverlay.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = soft });
            SplashLogo.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            var grow = new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(720))
            {
                EasingFunction = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut },
            };
            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

            var titleShift = new TranslateTransform(0, 10);
            SplashTitle.RenderTransform = titleShift;
            var titleFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(340))
            {
                BeginTime = TimeSpan.FromMilliseconds(110),
                EasingFunction = ease,
            };
            SplashTitle.BeginAnimation(OpacityProperty, titleFade);
            titleShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(460))
                {
                    BeginTime = TimeSpan.FromMilliseconds(110),
                    EasingFunction = soft,
                });

            var subShift = new TranslateTransform(0, 8);
            SplashSubtitle.RenderTransform = subShift;
            SplashSubtitle.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(340))
                {
                    BeginTime = TimeSpan.FromMilliseconds(190),
                    EasingFunction = ease,
                });
            subShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(460))
                {
                    BeginTime = TimeSpan.FromMilliseconds(190),
                    EasingFunction = soft,
                });

            SplashBar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
                {
                    BeginTime = TimeSpan.FromMilliseconds(260),
                    EasingFunction = ease,
                });
            SplashStatus.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
                {
                    BeginTime = TimeSpan.FromMilliseconds(320),
                    EasingFunction = ease,
                });

            await System.Threading.Tasks.Task.WhenAll(
                System.Threading.Tasks.Task.Delay(800),
                System.Threading.Tasks.Task.WhenAny(_startupData.Task, System.Threading.Tasks.Task.Delay(1500)));

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            fadeOut.Completed += (_, _) =>
            {
                SplashOverlay.Visibility = Visibility.Collapsed;
                if (AppPassword.HasPassword) ShowLock();
            };
            SplashOverlay.BeginAnimation(OpacityProperty, fadeOut);

            RootGrid.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { EasingFunction = ease });
            var entrance = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(680)) { EasingFunction = soft };
            _rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, entrance);
            _rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, entrance);
        }

        private static void AnimateScale(ScaleTransform scale, double target, int durationMs)
        {
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = easing };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            var collapsed = !Svc.Settings.SidebarCollapsed;
            Svc.Settings.SidebarCollapsed = collapsed;
            Svc.Settings.Save();
            ApplySidebarState(collapsed, true);
        }

        private void ApplySidebarState(bool collapsed, bool animate = true)
        {
            var labels = new (RadioButton button, string label)[]
            {
                (NavHome, "发现"), (NavMessages, "消息"), (NavUserLookup, "查人"),
                (NavSettings, "设置"), (NavHistory, "浏览历史"), (NavFavorites, "收藏的帖子"),
                (NavCached, "缓存的帖子"), (NavFollowed, "我关注的吧"), (NavJump, "一键跳转"),
            };
            var targetWidth = collapsed ? 66d : 256d;
            if (animate)
            {
                var widthAnimation = new DoubleAnimation(SidebarBorder.ActualWidth, targetWidth,
                    TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                };
                SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
                SidebarArrowRotate.Angle = 0;
            }
            else
            {
                SidebarBorder.Width = targetWidth;
                SidebarArrowRotate.Angle = 0;
            }
            SidebarSearch.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            BrandText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            ForumsScroll.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            UserNameText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            UserSubText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            UserChevron.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            BrandStack.Margin = collapsed ? new Thickness(6, 0, 0, 0) : new Thickness(20, 0, 0, 0);
            BrandLogo.Width = collapsed ? 24 : 30;
            BrandLogo.Height = collapsed ? 24 : 30;
            SidebarNav.Margin = collapsed ? new Thickness(7, 4, 7, 6) : new Thickness(12, 0, 12, 6);
            UserCard.Padding = collapsed ? new Thickness(7) : new Thickness(10);
            UserCard.Margin = collapsed ? new Thickness(7, 6, 7, 14) : new Thickness(14, 6, 14, 14);
            foreach (var (button, label) in labels)
            {
                button.Content = collapsed ? "" : label;
                button.ToolTip = collapsed ? label : null;
                button.Padding = collapsed ? new Thickness(6, 9, 6, 9) : new Thickness(12, 9, 12, 9);
            }
            SidebarToggleGlyph.Text = collapsed ? "\u00BB" : "\u00AB";
            if (animate)
            {
                SidebarToggleGlyph.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(40),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    });
                SidebarArrowShift.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(collapsed ? -6 : 6, 0, TimeSpan.FromMilliseconds(220))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    });
            }
            else
            {
                SidebarToggleGlyph.Opacity = 1;
                SidebarArrowShift.X = 0;
            }
            SidebarToggle.ToolTip = collapsed ? "展开侧栏" : "收起侧栏";
        }

        private void OnThemeChanged()
        {
            UpdateThemeGlyph();
        }

        public void ApplyBackgroundImage()
        {
            var path = Svc.Settings.BackgroundImage;
            if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    AppBackgroundImage.Source = image;
                    AppBackgroundImage.Visibility = Visibility.Visible;
                    AppBackgroundScrim.Visibility = Visibility.Visible;
                    AppBackgroundImage.Stretch = Svc.Settings.BackgroundLetterbox
                        ? Stretch.Uniform
                        : Stretch.UniformToFill;
                    if (Svc.Settings.BackgroundLetterbox)
                    {
                        WindowRoot.Background = BrushFromHex(Svc.Settings.BackgroundFillColor);
                    }
                    else
                    {
                        WindowRoot.SetResourceReference(BackgroundProperty, "AppBackground");
                    }
                    RootGrid.Background = Brushes.Transparent;
                    PageContainer.Background = Brushes.Transparent;
                    BackgroundMaterial.Apply();
                    return;
                }
                catch
                {
                }
            }
            AppBackgroundImage.Visibility = Visibility.Collapsed;
            AppBackgroundScrim.Visibility = Visibility.Collapsed;
            AppBackgroundImage.Source = null;
            WindowRoot.SetResourceReference(BackgroundProperty, "AppBackground");
            RootGrid.SetResourceReference(BackgroundProperty, "AppBackground");
            PageContainer.SetResourceReference(BackgroundProperty, "AppBackground");
            BackgroundMaterial.Apply();
        }

        private static Brush BrushFromHex(string hex)
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    string.IsNullOrWhiteSpace(hex) ? "#121418" : hex));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x18));
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyBackgroundImage();
            _ = PlayStartupAnimationAsync();
            _ = AutoSignAsync();
            Anim.RoundWindowCorners(this);
            var work = SystemParameters.WorkArea;
            MaxWidth = work.Width;
            MaxHeight = work.Height;
            if (Svc.Settings.WindowWidth > MinWidth && Svc.Settings.WindowHeight > MinHeight)
            {
                Width = Math.Min(Svc.Settings.WindowWidth, work.Width);
                Height = Math.Min(Svc.Settings.WindowHeight, work.Height);
            }
            if (Width > work.Width) Width = work.Width;
            if (Height > work.Height) Height = work.Height;
            UpdateThemeGlyph();
            UpdateUserCard();
            if (Svc.Account.IsLoggedIn && (Svc.Account.Uid == 0 || string.IsNullOrWhiteSpace(Svc.Account.Portrait)))
            {
                _ = RefreshSelfAsync();
            }
            Svc.Nav.Reset(() => new HomeView(), "发现");
            try
            {
                await LoadSidebarForumsAsync();
            }
            finally
            {
                _startupData.TrySetResult(true);
            }
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon != null) return icon;
                }
            }
            catch
            {
            }
            return System.Drawing.SystemIcons.Application;
        }

        private void HideToTray()
        {
            if (!IsVisible)
            {
                Hide();
                EnsureTray();
                return;
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            fade.Completed += (_, _) =>
            {
                Hide();
                EnsureTray();
                RootGrid.Opacity = 1;
                ShowToast("已最小化到托盘，双击托盘图标恢复");
            };
            RootGrid.BeginAnimation(OpacityProperty, fade);
        }

        private void EnsureTray()
        {
            if (_trayIcon != null) return;
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "TieLiteDesktop",
                Visible = true,
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => RestoreFromTray());
            menu.Items.Add("退出", null, (_, _) =>
            {
                _forceClose = true;
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
                Close();
            });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            try
            {
                _trayIcon.ShowBalloonTip(2500, "TieLiteDesktop", "已最小化到托盘，双击图标恢复窗口", Forms.ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void RestoreFromTray()
        {
            Show();
            RootGrid.Opacity = 0;
            RootGrid.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
            WindowState = WindowState.Normal;
            if (Svc.Settings.LockOnRestore && AppPassword.HasPassword)
            {
                ShowLock();
            }
            Activate();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClose && !_closing)
            {
                if (!Svc.Settings.SkipExitPrompt)
                {
                    var dialog = new ExitChoiceWindow { Owner = this };
                    if (dialog.ShowDialog() != true)
                    {
                        e.Cancel = true;
                        return;
                    }
                    if (dialog.DoNotAsk)
                    {
                        if (dialog.ExitApplication)
                        {
                            Svc.Settings.MinimizeToTray = false;
                            Svc.Settings.SkipExitPrompt = false;
                        }
                        else
                        {
                            Svc.Settings.SkipExitPrompt = true;
                        }
                        Svc.Settings.Save();
                    }
                    if (dialog.MinimizeToTray)
                    {
                        Svc.Settings.MinimizeToTray = true;
                        Svc.Settings.Save();
                        e.Cancel = true;
                        HideToTray();
                        return;
                    }
                }
                else if (Svc.Settings.MinimizeToTray)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }
            }
            if (WindowState == WindowState.Normal)
            {
                Svc.Settings.WindowWidth = Width;
                Svc.Settings.WindowHeight = Height;
                Svc.Settings.Save();
            }
            if (!_closing)
            {
                e.Cancel = true;
                _closing = true;
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
                fade.Completed += (_, _) =>
                {
                    _closing = true;
                    Close();
                };
                RootGrid.BeginAnimation(OpacityProperty, fade);
                var shrink = new DoubleAnimation(1, 0.985, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
                _rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                _rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
                return;
            }
            base.OnClosing(e);
        }

        // ------------------------------------------------------------ navigation
        public void ShowPage(PageEntry entry, bool back)
        {
            try
            {
                var page = entry.Factory();
                PageHost.Content = page;
                PageTitleText.Text = entry.Title;
                switch (entry.Title)
                {
                    case "浏览历史": SelectNav("history"); break;
                    case "查人": SelectNav("lookup"); break;
                    case "缓存的帖子": SelectNav("cached"); break;
                    case "我关注的吧": SelectNav("followed"); break;
                    case "收藏的帖子": SelectNav("favorites"); break;
                    case "设置": SelectNav("settings"); break;
                    case "消息": SelectNav("messages"); break;
                    case "发现": SelectNav("home"); break;
                }
                BackButton.Visibility = Svc.Nav.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
                PageSubtitleChip.Visibility = Visibility.Collapsed;
                if (back) AnimateIn(-14);
                else AnimateIn(14);
            }
            catch (Exception ex)
            {
                ShowToast("页面加载失败：" + ex.Message);
            }
        }

        private void AnimateIn(double offset)
        {
            var transform = new TranslateTransform(0, 0);
            PageHost.RenderTransform = transform;
            var slide = new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            };
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            };
            PageHost.BeginAnimation(OpacityProperty, fade);
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        public void SetSubtitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                PageSubtitleChip.Visibility = Visibility.Collapsed;
                return;
            }
            PageSubtitleText.Text = text;
            PageSubtitleChip.Visibility = string.Equals(text, PageTitleText.Text, StringComparison.Ordinal) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void ClearNavSelection()
        {
            NavHome.IsChecked = false;
            NavMessages.IsChecked = false;
            NavFavorites.IsChecked = false;
            NavSettings.IsChecked = false;
            NavHistory.IsChecked = false;
            NavUserLookup.IsChecked = false;
            NavCached.IsChecked = false;
            NavFollowed.IsChecked = false;
            NavJump.IsChecked = false;
        }

        public void SelectNav(string name)
        {
            ClearNavSelection();
            switch (name)
            {
                case "home": NavHome.IsChecked = true; break;
                case "messages": NavMessages.IsChecked = true; break;
                case "favorites": NavFavorites.IsChecked = true; break;
                case "settings": NavSettings.IsChecked = true; break;
                case "history": NavHistory.IsChecked = true; break;
                case "lookup": NavUserLookup.IsChecked = true; break;
                case "cached": NavCached.IsChecked = true; break;
                case "followed": NavFollowed.IsChecked = true; break;
                case "jump": NavJump.IsChecked = true; break;
            }
        }

        public void OpenForum(string forumName)
        {
            if (string.IsNullOrWhiteSpace(forumName)) return;
            ClearNavSelection();
            RememberForum(forumName);
            Svc.Nav.Push(() => new ForumView(forumName), forumName);
        }

        public void OpenThread(long threadId, string title, string forumName, Core.Models.PollInfoModel poll = null)
        {
            HistoryStore.Add(threadId, title, forumName);
            ClearNavSelection();
            Svc.Nav.Push(() => new ThreadView(threadId, forumName, title, poll), string.IsNullOrWhiteSpace(title) ? "帖子" : title);
        }

        public void OpenImageViewer(System.Collections.Generic.List<string> urls, int index,
            System.Collections.Generic.List<string> fallbacks = null)
        {
            if (urls == null || urls.Count == 0) return;
            try
            {
                var viewer = new ImageViewerWindow(urls, Math.Min(index, urls.Count - 1), fallbacks)
                {
                    Owner = this,
                };
                viewer.Show();
                viewer.Activate();
            }
            catch (Exception ex)
            {
                ShowToast("打开图片失败：" + ex.Message);
            }
        }

        public void ShowImageOverlay(System.Collections.Generic.List<string> urls, int index,
            System.Collections.Generic.List<string> fallbacks = null)
        {
            if (urls == null || urls.Count == 0) return;
            index = Math.Clamp(index, 0, urls.Count - 1);
            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x0A, 0x0B, 0x0E)) };
            Panel.SetZIndex(overlay, 500);
            Grid.SetColumnSpan(overlay, Math.Max(1, RootGrid.ColumnDefinitions.Count));
            Grid.SetRowSpan(overlay, Math.Max(1, RootGrid.RowDefinitions.Count));

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(70, 60, 70, 90),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            image.RenderTransform = group;
            overlay.Children.Add(image);

            var counter = new TextBlock
            {
                Text = (index + 1) + " / " + urls.Count,
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 28),
            };
            overlay.Children.Add(counter);

            var zoom = 1.0;
            void ApplyZoom(double factor)
            {
                zoom = Math.Clamp(zoom * factor, 1, 6);
                scale.ScaleX = zoom;
                scale.ScaleY = zoom;
                if (zoom <= 1.01)
                {
                    zoom = 1;
                    translate.X = 0;
                    translate.Y = 0;
                }
            }
            void Reset()
            {
                zoom = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
                translate.X = 0;
                translate.Y = 0;
            }
            async void Show(int idx)
            {
                index = (idx + urls.Count) % urls.Count;
                counter.Text = (index + 1) + " / " + urls.Count;
                Reset();
                try
                {
                    var bitmap = await ImageLoader.GetAsync(urls[index], 2560);
                    if (bitmap == null && fallbacks != null && index < fallbacks.Count)
                    {
                        bitmap = await ImageLoader.GetAsync(fallbacks[index], 1280);
                    }
                    if (bitmap != null) image.Source = bitmap;
                    else counter.Text = "图片加载失败";
                }
                catch
                {
                    counter.Text = "图片加载失败";
                }
            }

            var close = Ui.IconButton("\uE8BB", "关闭", 38);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Margin = new Thickness(0, 18, 22, 0);
            close.Click += (_, _) => RootGrid.Children.Remove(overlay);
            if (close.Content is TextBlock closeGlyph) closeGlyph.Foreground = Brushes.White;
            overlay.Children.Add(close);

            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 18),
            };
            Button BarButton(string glyph, Action action)
            {
                var button = Ui.IconButton(glyph, null, 38);
                button.Margin = new Thickness(6, 0, 6, 0);
                if (button.Content is TextBlock tb) tb.Foreground = Brushes.White;
                button.Click += (_, _) => action();
                return button;
            }
            bar.Children.Add(BarButton("\uE71F", () => Show(index - 1)));
            bar.Children.Add(BarButton("\uE8A3", () => ApplyZoom(0.8)));
            bar.Children.Add(BarButton("\uE8A4", () => ApplyZoom(1.25)));
            bar.Children.Add(BarButton("\uE72C", Reset));
            bar.Children.Add(BarButton("\uE76C", () => Show(index + 1)));
            overlay.Children.Add(bar);

            overlay.MouseWheel += (_, e) =>
            {
                ApplyZoom(e.Delta > 0 ? 1.15 : 1 / 1.15);
                e.Handled = true;
            };
            image.MouseLeftButtonDown += (_, e) =>
            {
                if (zoom <= 1.01) return;
                image.CaptureMouse();
                var start = e.GetPosition(overlay);
                MouseEventHandler move = null;
                MouseButtonEventHandler up = null;
                move = (_, me) =>
                {
                    var p = me.GetPosition(overlay);
                    translate.X += p.X - start.X;
                    translate.Y += p.Y - start.Y;
                    start = p;
                };
                up = (_, _) =>
                {
                    image.ReleaseMouseCapture();
                    image.MouseMove -= move;
                    image.MouseLeftButtonUp -= up;
                };
                image.MouseMove += move;
                image.MouseLeftButtonUp += up;
                e.Handled = true;
            };
            overlay.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) RootGrid.Children.Remove(overlay);
                else if (e.Key == Key.Left) Show(index - 1);
                else if (e.Key == Key.Right) Show(index + 1);
            };
            RootGrid.Children.Add(overlay);
            overlay.Focusable = true;
            overlay.Focus();
            Show(index);
        }

        public void OpenProfile(long uid, string name)
        {
            ClearNavSelection();
            Svc.Nav.Push(() => new ProfileView(uid, name), string.IsNullOrWhiteSpace(name) ? "用户" : name);
        }

        public void OpenTopic(HotTopic topic)
        {
            ClearNavSelection();
            Svc.Nav.Push(() => new TopicView(topic), string.IsNullOrWhiteSpace(topic.Name) ? "话题" : topic.Name);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => Svc.Nav.Back();

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            NavHome.IsChecked = true;
            if (Svc.Nav.Current != null && Svc.Nav.Current.Title == "发现") return;
            Svc.Nav.Push(() => new HomeView(), "发现");
        }

        private void NavMessages_Click(object sender, RoutedEventArgs e)
        {
            NavMessages.IsChecked = true;
            Svc.Nav.Push(() => new MessagesView(), "消息");
        }

        private void NavFavorites_Click(object sender, RoutedEventArgs e)
        {
            NavFavorites.IsChecked = true;
            Svc.Nav.Push(() => new FavoritesView(), "收藏");
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            NavSettings.IsChecked = true;
            Svc.Nav.Push(() => new SettingsView(), "设置");
        }

        private void NavHistory_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            NavHistory.IsChecked = true;
            Svc.Nav.Push(() => new HistoryView(), "浏览历史");
        }

        private void NavUserLookup_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            NavUserLookup.IsChecked = true;
            Svc.Nav.Push(() => new UserLookupView(), "查人");
        }

        private void NavCached_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            NavCached.IsChecked = true;
            Svc.Nav.Push(() => new CachedPostsView(), "缓存的帖子");
        }

        private static bool TryGetDroppedText(IDataObject data, out string text)
        {
            text = "";
            if (data == null) return false;
            if (data.GetDataPresent(DataFormats.UnicodeText))
            {
                text = data.GetData(DataFormats.UnicodeText) as string;
            }
            if (string.IsNullOrWhiteSpace(text) && data.GetDataPresent(DataFormats.Text))
            {
                text = data.GetData(DataFormats.Text) as string;
            }
            if (string.IsNullOrWhiteSpace(text) && data.GetDataPresent(DataFormats.Html))
            {
                var html = data.GetData(DataFormats.Html) as string ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(html, "https?://[^\"'\\s<>]+");
                if (match.Success) text = match.Value;
            }
            return !string.IsNullOrWhiteSpace(text);
        }

        public void RefreshLockButton()
        {
            LockButton.Visibility = AppPassword.HasPassword ? Visibility.Visible : Visibility.Collapsed;
            var pinMode = AppPassword.HasPassword && Svc.Settings.PasswordType != "custom";
            PinPad.Visibility = pinMode ? Visibility.Visible : Visibility.Collapsed;
            PinDots.Visibility = pinMode ? Visibility.Visible : Visibility.Collapsed;
            LockBox.Visibility = pinMode ? Visibility.Collapsed : Visibility.Visible;
            LockBox.MaxLength = pinMode ? 6 : 64;
            UnlockButton.Content = "清除密码";
            ResetPasswordLink.Visibility = Visibility.Collapsed;
            if (!AppPassword.HasPassword) HideLock();
        }

        private void UpdatePinDots()
        {
            if (_pinDots.Count == 0) return;
            var count = Math.Min(LockBox.Password.Length, 6);
            for (var i = 0; i < _pinDots.Count; i++)
            {
                var filled = i < count;
                _pinDots[i].Background = filled ? Ui.Brush("TextPrimary") : Brushes.Transparent;
                if (filled)
                {
                    var scale = (ScaleTransform)_pinDots[i].RenderTransform;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(90))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        });
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(90))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        });
                }
            }
            if (count == 6 && AppPassword.Verify(LockBox.Password))
            {
                HideLock();
                LockError.Visibility = Visibility.Collapsed;
            }
        }

        private void HideLock()
        {
            if (LockOverlay.Visibility != Visibility.Visible) return;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            fade.Completed += (_, _) => LockOverlay.Visibility = Visibility.Collapsed;
            LockOverlay.BeginAnimation(OpacityProperty, fade);
            if (LockContent.RenderTransform is ScaleTransform lockScale)
            {
                lockScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1, 0.97, TimeSpan.FromMilliseconds(150)));
                lockScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1, 0.97, TimeSpan.FromMilliseconds(150)));
            }
        }

        private void AnimateLockButton(Button button)
        {
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
            button.PreviewMouseLeftButtonDown += (_, _) =>
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(70)));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(70)));
            };
            button.PreviewMouseLeftButtonUp += (_, _) =>
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            };
        }

        private void LockMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void LockMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void LockClose_Click(object sender, RoutedEventArgs e)
        {
            if (Svc.Settings.MinimizeToTray)
            {
                HideToTray();
                return;
            }
            _forceClose = true;
            Close();
        }

        private void ApplyLockBackground()
        {
            var path = Svc.Settings.SplashImage;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                LockBackdrop.Visibility = Visibility.Collapsed;
                LockScrim.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                LockBackdrop.Source = bitmap;
                LockBackdrop.Visibility = Visibility.Visible;
                LockScrim.Visibility = Visibility.Visible;
            }
            catch
            {
                LockBackdrop.Visibility = Visibility.Collapsed;
                LockScrim.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowLock()
        {
            LockOverlay.Visibility = Visibility.Visible;
            ApplyLockBackground();
            LockOverlay.Opacity = 0;
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            LockOverlay.BeginAnimation(OpacityProperty, fade);
            if (LockContent.RenderTransform is ScaleTransform lockScale)
            {
                var grow = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut },
                };
                lockScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                lockScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
            LockBox.Password = "";
            UpdatePinDots();
            LockError.Visibility = Visibility.Collapsed;
            LockBox.Focus();
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            if (AppPassword.HasPassword) ShowLock();
        }

        private void LockBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (AppPassword.Verify(LockBox.Password))
            {
                HideLock();
            }
        }

        private void Unlock_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ConfirmWindow("清除密码并退出登录",
                "清除应用密码后会同时退出当前账号，并删除已保存的账号，确定继续？", "清除并退出") { Owner = this };
            if (confirm.ShowDialog() != true)
            {
                return;
            }
            Svc.Account.Clear();
            AccountStore.Clear();
            Svc.RecreateClient();
            AppPassword.Clear();
            HideLock();
            RefreshLockButton();
            UpdateUserCard();
            ShowToast("应用密码已清除，已退出登录");
            RestartApplication();
        }

        private void RestartApplication()
        {
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                    });
                }
            }
            catch
            {
            }
            _forceClose = true;
            System.Windows.Application.Current.Shutdown();
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            Svc.Account.Clear();
            Svc.RecreateClient();
            AppPassword.Clear();
            HideLock();
            RefreshLockButton();
            UpdateUserCard();
            ShowToast("已重置密码并退出登录");
        }

        private void Recycle_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            Svc.Nav.Push(() => new RecycleBinView(), "回收站");
        }

        private async void NavJump_Click(object sender, RoutedEventArgs e)
        {
            var forum = string.IsNullOrWhiteSpace(Svc.Settings.JumpForumName)
                ? "孙笑川"
                : Svc.Settings.JumpForumName.Trim();
            ClearNavSelection();
            NavJump.IsChecked = true;
            try
            {
                var page = await Svc.Api.GetForumAsync(forum, 1);
                if (page?.Forum == null || string.IsNullOrWhiteSpace(page.Forum.Name))
                {
                    Svc.Window?.ShowToast("不存在该吧：" + forum);
                    NavJump.IsChecked = false;
                    return;
                }
                Svc.Nav.Push(() => new ForumView(forum), forum);
            }
            catch
            {
                Svc.Window?.ShowToast("不存在该吧：" + forum);
                NavJump.IsChecked = false;
            }
        }

        private void NavFollowed_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            NavFollowed.IsChecked = true;
            Svc.Nav.Push(() => new FollowedForumsView(), "我关注的吧");
        }

        private void SearchBox_Click(object sender, MouseButtonEventArgs e)
        {
            ClearNavSelection();
            Svc.Nav.Push(() => new SearchView(), "搜索");
        }

        // ------------------------------------------------------------ window chrome
        private void BrandArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d && FindAncestor<Button>(d) != null) return;
            // let the window chrome handle edge resizing instead of moving the window
            var position = e.GetPosition(this);
            if (position.Y < 10 || position.X < 10 || position.X > ActualWidth - 10) return;
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;
            Width = Math.Max(MinWidth, ActualWidth + e.HorizontalChange);
            Height = Math.Max(MinHeight, ActualHeight + e.VerticalChange);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Minimized) return;
            _wasMinimized = true;
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var scale = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(150)) { EasingFunction = easeIn };
            scale.Completed += (_, _) =>
            {
                WindowState = WindowState.Minimized;
                _rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _rootScale.ScaleX = 1;
                _rootScale.ScaleY = 1;
                RootGrid.BeginAnimation(OpacityProperty, null);
                RootGrid.Opacity = 1;
            };
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = easeIn };
            RootGrid.BeginAnimation(OpacityProperty, fade);
            _rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            _rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }

        private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void UpdateMaximizeGlyph()
        {
            var glyph = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            if (MaximizeButton.Content is TextBlock text) text.Text = glyph;
            else MaximizeButton.Content = glyph;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ------------------------------------------------------------ theme
        private void Theme_Click(object sender, RoutedEventArgs e)
        {
            Svc.Settings.ThemeMode = Svc.EffectiveTheme == "dark" ? "light" : "dark";
            Svc.ApplyTheme();
            Svc.Settings.Save();
            UpdateThemeGlyph();
        }

        private void UpdateThemeGlyph()
        {
            var glyph = Svc.EffectiveTheme == "dark" ? "\uE706" : "\uE793";
            if (ThemeButton.Content is TextBlock text) text.Text = glyph;
            else ThemeButton.Content = glyph;
            ThemeButton.ToolTip = Svc.Settings.ThemeMode == "system"
                ? "当前跟随系统，点击切换为" + (Svc.EffectiveTheme == "dark" ? "浅色" : "深色")
                : Svc.EffectiveTheme == "dark" ? "切换到浅色模式" : "切换到深色模式";
        }

        // ------------------------------------------------------------ refresh
        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (PageHost.Content is IRefreshable refreshable)
            {
                RefreshButton.IsEnabled = false;
                try
                {
                    await refreshable.RefreshAsync();
                }
                catch (Exception ex)
                {
                    ShowToast(ex.Message);
                }
                finally
                {
                    RefreshButton.IsEnabled = true;
                }
            }
            else if (PageHost.Content is MainWindow)
            {
            }
        }

        // ------------------------------------------------------------ account
        private async Task RefreshSelfAsync()
        {
            try
            {
                var me = await Svc.Api.GetSelfUserAsync();
                if (me == null) return;
                if (me.Id != 0) Svc.Account.Uid = me.Id;
                if (!string.IsNullOrWhiteSpace(me.ShowName)) Svc.Account.UserName = me.ShowName;
                if (!string.IsNullOrWhiteSpace(me.Portrait)) Svc.Account.Portrait = me.Portrait;
                Svc.Account.Save();
                UpdateUserCard();
            }
            catch
            {
            }
        }

        public void UpdateUserCard()
        {
            if (Svc.Account.IsLoggedIn)
            {
                UserNameText.Text = string.IsNullOrWhiteSpace(Svc.Account.UserName) ? "贴吧用户" : Svc.Account.UserName;
                UserSubText.Text = "UID " + Svc.Account.Uid;
                var image = ImageLoader.FromFile(System.IO.Path.Combine(Svc.AssetsDir, "logo.png"), 68);
                UserAvatar.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                if (!string.IsNullOrWhiteSpace(Svc.Account.Portrait))
                {
                    var portrait = Svc.Account.Portrait.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? Svc.Account.Portrait
                        : "https://himg.bdimg.com/sys/portrait/item/" + Svc.Account.Portrait;
                    _ = LoadUserAvatarAsync(portrait);
                }
            }
            else
            {
                UserNameText.Text = "未登录";
                UserSubText.Text = "登录后可收藏、回复";
                UserAvatar.Background = (Brush)FindResource("SurfaceAlt");
            }
        }

        private async System.Threading.Tasks.Task LoadUserAvatarAsync(string url)
        {
            try
            {
                var image = await ImageLoader.GetAsync(url, 68);
                if (image != null)
                {
                    UserAvatar.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                }
            }
            catch
            {
            }
        }

        private void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (Svc.Account.IsLoggedIn)
            {
                OpenProfile(Svc.Account.Uid, Svc.Account.UserName);
                return;
            }
            var login = new LoginWindow { Owner = this };
            if (login.ShowDialog() == true)
            {
                UpdateUserCard();
                _ = LoadSidebarForumsAsync();
                ShowToast("登录成功，欢迎回来");
            }
        }

        // ------------------------------------------------------------ sidebar forums
        private async Task LoadSidebarForumsAsync()
        {
            ForumsPanel.Children.Clear();
            TopicsPanel.Children.Clear();

            var forums = new List<RecommendedForum>();
            try
            {
                forums = await Data.ForumsAsync();
            }
            catch
            {
            }

            if (forums.Count < 8)
            {
                try
                {
                    var hot = await Data.HotAsync();
                    foreach (var name in hot.Threads.Select(t => t.ForumName)
                                 .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
                    {
                        if (forums.All(f => f.Name != name)) forums.Add(new RecommendedForum { Name = name });
                    }
                    if (!Svc.DemoMode)
                    {
                        foreach (var tab in hot.Tabs.Take(3))
                        {
                            try
                            {
                                var page = await Svc.Api.GetHotAsync(tab.Code);
                                foreach (var name in page.Threads.Select(t => t.ForumName)
                                             .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
                                {
                                    if (forums.All(f => f.Name != name)) forums.Add(new RecommendedForum { Name = name });
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            foreach (var recent in (Svc.Settings.RecentForums ?? Array.Empty<string>()))
            {
                if (forums.Count >= 10) break;
                if (forums.All(f => f.Name != recent)) forums.Add(new RecommendedForum { Name = recent });
            }

            forums = forums.Take(10).ToList();
            ForumsHeader.Text = "热门贴吧";
            if (forums.Count == 0)
            {
                ForumsHeader.Text = "暂无推荐";
            }
            var withAvatar = forums.Where(f => f.Id > 0).ToList();
            if (withAvatar.Count > 0)
            {
                await Task.WhenAll(withAvatar.Select(async f =>
                {
                    try
                    {
                        var detail = await Svc.Api.GetForumDetailAsync(f.Id);
                        if (detail != null && !string.IsNullOrWhiteSpace(detail.Avatar)) f.Avatar = detail.Avatar;
                    }
                    catch
                    {
                    }
                }));
            }
            foreach (var forum in forums)
            {
                ForumsPanel.Children.Add(BuildForumRow(forum));
            }

            try
            {
                var topics = await Data.TopicsAsync();
                var index = 0;
                foreach (var topic in topics.Take(6))
                {
                    TopicsPanel.Children.Add(BuildTopicRow(topic, index++));
                }
            }
            catch
            {
            }
            TopicsHeader.Visibility = Visibility.Collapsed;
            TopicsPanel.Visibility = Visibility.Collapsed;
        }

        private UIElement BuildForumRow(RecommendedForum forum)
        {
            var row = new Border
            {
                Width = 106,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 1, 3, 2),
                Cursor = Cursors.Hand,
                ToolTip = forum.Name,
            };
            Anim.Hover(row);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatar = Ui.Avatar(forum.AvatarUrl, 22, forum.Name);
            avatar.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(avatar);

            var label = Ui.Text(forum.Name, 12.3, FontWeights.Medium);
            label.Margin = new Thickness(7, 0, 0, 0);
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            row.Child = grid;
            row.MouseLeftButtonUp += (_, _) => OpenForum(forum.Name);
            return row;
        }

        private UIElement BuildTopicRow(HotTopic topic, int index)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                ToolTip = string.IsNullOrWhiteSpace(topic.Desc) ? topic.Name : topic.Desc,
            };
            Anim.Hover(row);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rank = Ui.Text((index + 1).ToString("D2"), 11.5, FontWeights.Bold,
                index < 3 ? "Accent" : "TextTertiary");
            rank.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(rank);

            var title = Ui.Text(topic.Name, 12.3);
            title.TextWrapping = TextWrapping.NoWrap;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Margin = new Thickness(0, 0, 6, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 1);
            grid.Children.Add(title);

            if (topic.DiscussNum > 0)
            {
                var heat = Ui.Text(Ui.ShortNum(topic.DiscussNum), 10.2, null, "TextTertiary");
                heat.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(heat, 2);
                grid.Children.Add(heat);
            }

            row.Child = grid;
            row.MouseLeftButtonUp += (_, _) => OpenTopic(topic);
            return row;
        }

        private void RememberForum(string forum)
        {
            var list = (Svc.Settings.RecentForums ?? Array.Empty<string>()).Where(f => f != forum).ToList();
            list.Insert(0, forum);
            Svc.Settings.RecentForums = list.Take(12).ToArray();
            Svc.Settings.LastForum = forum;
            Svc.Settings.Save();
        }

        private async void RefreshForums_Click(object sender, RoutedEventArgs e)
        {
            RefreshForumsButton.IsEnabled = false;
            await LoadSidebarForumsAsync();
            RefreshForumsButton.IsEnabled = true;
        }

        // ------------------------------------------------------------ toast
        private System.Windows.Threading.DispatcherTimer _toastTimer;

        public void ShowToast(string message)
        {
            ToastText.Text = message;
            Toast.Visibility = Visibility.Visible;
            Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            _toastTimer?.Stop();
            _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fade.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
                Toast.BeginAnimation(OpacityProperty, fade);
            };
            _toastTimer.Start();
        }
    }

    public interface IRefreshable
    {
        Task RefreshAsync();
    }
}
