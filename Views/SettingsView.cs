using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>设置页：外观、网络、内容与关于。</summary>
    public sealed class SettingsView : UserControl
    {
        private string _proxyMode = "system";
        private readonly System.Collections.Generic.List<(string mode, RadioButton radio)> _themeRadios = new();
        private bool _syncingTheme;
        private bool _privacyUnlocked;

        public SettingsView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var generalGroup = new WrapPanel { Orientation = Orientation.Horizontal };
            var cacheGroup = new WrapPanel { Orientation = Orientation.Horizontal };
            var accountGroup = new WrapPanel { Orientation = Orientation.Horizontal };
            var root = new StackPanel
            {
                Width = 860,
                MaxWidth = 860,
                Margin = new Thickness(32, 14, 32, 40),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            // appearance
            var themePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var options = new (string mode, string label)[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") };
            foreach (var option in options)
            {
                var radio = new RadioButton
                {
                    Content = option.label,
                    GroupName = "ThemePick",
                    IsChecked = Svc.Settings.ThemeMode == option.mode,
                };
                radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                var mode = option.mode;
                radio.Checked += (_, _) =>
                {
                    if (!_syncingTheme) SetTheme(mode);
                };
                radio.Click += (_, _) =>
                {
                    if (!_syncingTheme) SetTheme(mode);
                };
                _themeRadios.Add((mode, radio));
                themePanel.Children.Add(radio);
            }

            Svc.ThemeChanged += SyncThemeRadios;
            Unloaded += (_, _) => Svc.ThemeChanged -= SyncThemeRadios;
            SyncThemeRadios();

            var images = new ToggleSwitch { IsChecked = Svc.Settings.ShowImages, VerticalAlignment = VerticalAlignment.Center };
            images.Checked += (_, _) => { Svc.Settings.ShowImages = true; Svc.Settings.Save(); };
            images.Unchecked += (_, _) => { Svc.Settings.ShowImages = false; Svc.Settings.Save(); };

            var hideVideo = new ToggleSwitch { IsChecked = Svc.Settings.HideVideoThreads, VerticalAlignment = VerticalAlignment.Center };
            hideVideo.Checked += (_, _) => { Svc.Settings.HideVideoThreads = true; Svc.Settings.Save(); };
            hideVideo.Unchecked += (_, _) => { Svc.Settings.HideVideoThreads = false; Svc.Settings.Save(); };

            var appearance = new StackPanel();
            appearance.Children.Add(SettingRow("界面主题", "跟随系统，或手动指定", themePanel));

            var accentOptions = new (string code, string label)[]
            {
                ("blue", "贴吧蓝"), ("green", "抹茶绿"), ("purple", "葡萄紫"),
                ("orange", "日落橙"), ("pink", "樱花粉"), ("red", "珊瑚红"), ("cyan", "青蓝"),
            };
            var accentPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var swatches = new List<(string code, Border dot)>();
            void RefreshSwatches()
            {
                foreach (var (code, dot) in swatches)
                {
                    var selected = string.Equals(Svc.Settings.Accent, code, StringComparison.OrdinalIgnoreCase);
                    dot.BorderThickness = new Thickness(selected ? 3 : 1);
                    dot.BorderBrush = selected ? Ui.Brush("TextPrimary") : Ui.Brush("Border");
                }
            }
            foreach (var option in accentOptions)
            {
                var dot = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = option.label,
                    Background = new SolidColorBrush(AccentTheme.GetColor(option.code)),
                };
                var code = option.code;
                dot.MouseLeftButtonUp += (_, _) =>
                {
                    if (string.Equals(Svc.Settings.Accent, code, StringComparison.OrdinalIgnoreCase)) return;
                    Svc.Settings.Accent = code;
                    Svc.Settings.Save();
                    RefreshSwatches();
                    Svc.ApplyTheme();
                };
                swatches.Add((code, dot));
                accentPanel.Children.Add(dot);
            }
            var customDot = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                ToolTip = "自定义颜色",
                Background = Svc.Settings.HasCustomAccent
                    ? new SolidColorBrush(AccentTheme.GetColor("custom", Svc.Settings.CustomAccent))
                    : Brushes.Transparent,
                Child = Svc.Settings.HasCustomAccent
                    ? null
                    : Ui.Text("+", 15, FontWeights.Medium, "TextSecondary"),
            };
            customDot.MouseLeftButtonUp += (_, _) =>
            {
                try
                {
                    using var dialog = new System.Windows.Forms.ColorDialog
                    {
                        FullOpen = true,
                        Color = System.Drawing.ColorTranslator.FromHtml(Svc.Settings.CustomAccent),
                    };
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    var picked = dialog.Color;
                    var hex = "#" + picked.R.ToString("X2") + picked.G.ToString("X2") + picked.B.ToString("X2");
                    Svc.Settings.CustomAccent = hex;
                    Svc.Settings.Accent = "custom";
                    Svc.Settings.HasCustomAccent = true;
                    Svc.Settings.Save();
                    customDot.Child = null;
                    customDot.Background = new SolidColorBrush(AccentTheme.GetColor("custom", hex));
                    RefreshSwatches();
                    Svc.ApplyTheme();
                }
                catch
                {
                }
            };
            swatches.Add(("custom", customDot));
            accentPanel.Children.Add(customDot);
            RefreshSwatches();
            appearance.Children.Add(SettingRow("主题色", "选择强调色", accentPanel));
            appearance.Children.Add(SettingRow("显示图片", "关闭后帖子内图片只显示占位框，节省流量", images));
            appearance.Children.Add(SettingRow("隐藏视频贴", "列表与话题页不再显示带视频的帖子", hideVideo));
            var recentSwitch = new ToggleSwitch { IsChecked = Svc.Settings.ShowRecentForums, VerticalAlignment = VerticalAlignment.Center };
            recentSwitch.Checked += (_, _) => { Svc.Settings.ShowRecentForums = true; Svc.Settings.Save(); };
            recentSwitch.Unchecked += (_, _) => { Svc.Settings.ShowRecentForums = false; Svc.Settings.Save(); };
            appearance.Children.Add(SettingRow("最近浏览的吧", "在发现页显示最近访问过的贴吧", recentSwitch));

            var splashPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Action refreshSplash = null;
            var chooseSplash = Ui.Button("选择图片", false, 12);
            chooseSplash.Click += (_, _) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择启动界面图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                };
                if (dialog.ShowDialog() != true) return;
                try
                {
                    var target = Path.Combine(AppSettings.ConfigDirectory, "splash-custom.png");
                    var cropper = new ImageCropWindow(dialog.FileName, target, 16.0 / 9.0) { Owner = Svc.Window };
                    if (cropper.ShowDialog() != true) return;
                    Svc.Settings.SplashImage = target;
                    Svc.Settings.Save();
                    refreshSplash?.Invoke();
                    Svc.Window?.ShowToast("启动图片已保存，下次启动生效");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("设置失败：" + ex.Message);
                }
            };
            splashPanel.Children.Add(chooseSplash);
            var clearSplash = Ui.Button("恢复默认", false, 12);
            clearSplash.Margin = new Thickness(8, 0, 0, 0);
            clearSplash.Click += (_, _) =>
            {
                Svc.Settings.SplashImage = "";
                Svc.Settings.Save();
                refreshSplash?.Invoke();
                Svc.Window?.ShowToast("启动图片已恢复默认");
            };
            splashPanel.Children.Add(clearSplash);

            var backgroundPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var chooseBackground = Ui.Button("选择图片", false, 12);
            chooseBackground.Click += (_, _) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择应用背景图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                };
                if (dialog.ShowDialog() != true) return;
                try
                {
                    var target = Path.Combine(AppSettings.ConfigDirectory, "background-custom.png");
                    var cropper = new ImageCropWindow(dialog.FileName, target, 16.0 / 9.0) { Owner = Svc.Window };
                    if (cropper.ShowDialog() != true) return;
                    Svc.Settings.BackgroundImage = target;
                    Svc.Settings.BackgroundLetterbox = cropper.Letterbox;
                    Svc.Settings.BackgroundFillColor = cropper.FillColorHex;
                    Svc.Settings.Save();
                    Svc.Window?.ApplyBackgroundImage();
                    Svc.Window?.ShowToast("自定义背景已应用");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("设置失败：" + ex.Message);
                }
            };
            backgroundPanel.Children.Add(chooseBackground);
            var clearBackground = Ui.Button("恢复默认", false, 12);
            clearBackground.Margin = new Thickness(8, 0, 0, 0);
            clearBackground.Click += (_, _) =>
            {
                Svc.Settings.BackgroundImage = "";
                Svc.Settings.Save();
                Svc.Window?.ApplyBackgroundImage();
                Svc.Window?.ShowToast("已恢复默认背景");
            };
            backgroundPanel.Children.Add(clearBackground);
            appearance.Children.Add(SettingRow("自定义背景", "选择后手动裁剪为 16:9", backgroundPanel));

            var opacityPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var opacitySlider = new Slider
            {
                Width = 150,
                Minimum = 0.3,
                Maximum = 1.0,
                Value = Svc.Settings.CardOpacity,
                VerticalAlignment = VerticalAlignment.Center,
            };
            opacitySlider.SetResourceReference(FrameworkElement.StyleProperty, "RoundSlider");
            var opacityLabel = Ui.Text(Math.Round(Svc.Settings.CardOpacity * 100) + "%", 12, null, "TextSecondary");
            opacityLabel.Width = 42;
            opacityLabel.TextAlignment = TextAlignment.Right;
            opacityLabel.Margin = new Thickness(8, 0, 0, 0);
            opacitySlider.ValueChanged += (_, _) =>
            {
                Svc.Settings.CardOpacity = opacitySlider.Value;
                opacityLabel.Text = Math.Round(opacitySlider.Value * 100) + "%";
                BackgroundMaterial.Apply();
                Svc.Settings.Save();
            };
            opacityPanel.Children.Add(opacitySlider);
            opacityPanel.Children.Add(opacityLabel);
            appearance.Children.Add(SettingRow("卡片透明度", "仅在自定义背景下生效，数值越低背景越明显", opacityPanel));

            generalGroup.Children.Add(Section("外观", appearance));

            var splashBody = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
            var splashPreview = new Border
            {
                Height = 150,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 10),
                ClipToBounds = true,
            };
            splashPreview.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            splashPreview.SetResourceReference(Border.BorderBrushProperty, "Border");
            splashPreview.BorderThickness = new Thickness(1);
            refreshSplash = () =>
            {
                var path = Svc.Settings.SplashImage;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        var image = new System.Windows.Media.Imaging.BitmapImage();
                        image.BeginInit();
                        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 720;
                        image.UriSource = new Uri(path, UriKind.Absolute);
                        image.EndInit();
                        image.Freeze();
                        splashPreview.Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
                        return;
                    }
                    catch
                    {
                    }
                }
                splashPreview.Child = new TextBlock
                {
                    Text = "默认主题色启动界面",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Ui.Brush("TextTertiary"),
                };
            };
            refreshSplash();
            splashBody.Children.Add(splashPreview);
            splashPanel.Orientation = Orientation.Horizontal;
            splashBody.Children.Add(splashPanel);
            generalGroup.Children.Add(Section("启动界面", splashBody));

            // network
            var network = new StackPanel();
            _proxyMode = Svc.Settings.ProxyMode;
            var modePanel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var option in new (string code, string label)[] { ("system", "跟随系统"), ("none", "直连"), ("custom", "自定义") })
            {
                var radio = new RadioButton
                {
                    Content = option.label,
                    GroupName = "ProxyMode",
                    IsChecked = _proxyMode == option.code,
                };
                radio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                var code = option.code;
                radio.Checked += (_, _) => _proxyMode = code;
                modePanel.Children.Add(radio);
            }
            network.Children.Add(SettingRow("网络代理", "访问贴吧接口时使用的代理方式", null));
            network.Children.Add(SettingRow("", "", modePanel, false));

            var proxyBox = new TextBox
            {
                Width = 230,
                Height = 32,
                FontSize = 12.5,
                Text = Svc.Settings.ProxyUrl,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            proxyBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            proxyBox.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            var proxyHost = new Border
            {
                Width = 244,
                Height = 34,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 0, 10, 0),
                Child = proxyBox,
            };
            proxyHost.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            proxyHost.SetResourceReference(BorderBrushProperty, "Border");
            proxyHost.BorderThickness = new Thickness(1);
            network.Children.Add(SettingRow("代理地址", "例如 http://127.0.0.1:7890", proxyHost));

            var apply = Ui.Button("应用网络设置", false, 12.5);
            apply.Click += (_, _) =>
            {
                Svc.Settings.ProxyMode = _proxyMode;
                Svc.Settings.ProxyUrl = proxyBox.Text?.Trim() ?? "";
                Svc.Settings.Save();
                Svc.RecreateClient();
                Svc.Window?.ShowToast("网络设置已应用");
            };
            network.Children.Add(SettingRow("", "", apply, false));
            generalGroup.Children.Add(Section("网络", network));

            // cache
            var cache = new StackPanel();
            var clear = Ui.Button("清除图片缓存", false, 12.5);
            clear.Click += (_, _) =>
            {
                try
                {
                    if (Directory.Exists(ImageLoader.CacheDirectory))
                    {
                        foreach (var file in Directory.GetFiles(ImageLoader.CacheDirectory)) File.Delete(file);
                    }
                    Svc.Window?.ShowToast("图片缓存已清除");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("清除失败：" + ex.Message);
                }
            };
            cache.Children.Add(SettingRow("图片缓存", CacheSizeText(), clear));

            var imagePath = Ui.Text(ImageLoader.CacheDirectory, 11.5, null, "TextTertiary");
            imagePath.TextTrimming = TextTrimming.CharacterEllipsis;
            imagePath.MaxWidth = 320;
            imagePath.VerticalAlignment = VerticalAlignment.Center;
            var imageActions = new StackPanel { Orientation = Orientation.Horizontal };
            var chooseImage = Ui.Button("更改位置", false, 12);
            chooseImage.Margin = new Thickness(0, 0, 6, 0);
            chooseImage.Click += (_, _) =>
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择图片缓存保存位置",
                    UseDescriptionForTitle = true,
                    SelectedPath = ImageLoader.CacheDirectory,
                    ShowNewFolderButton = true,
                };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                try
                {
                    System.IO.Directory.CreateDirectory(dialog.SelectedPath);
                    Svc.Settings.ImageCacheDir = dialog.SelectedPath;
                    Svc.Settings.Save();
                    imagePath.Text = ImageLoader.CacheDirectory;
                    Svc.Window?.ShowToast("图片缓存位置已更新");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("无法使用该目录：" + ex.Message);
                }
            };
            imageActions.Children.Add(chooseImage);
            var openImage = Ui.Button("打开", false, 12);
            openImage.Click += (_, _) =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(ImageLoader.CacheDirectory);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ImageLoader.CacheDirectory,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("打开缓存目录失败：" + ex.Message);
                }
            };
            imageActions.Children.Add(openImage);
            var imageRow = new StackPanel();
            imageRow.Children.Add(imagePath);
            imageRow.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6) });
            imageRow.Children.Add(imageActions);
            cache.Children.Add(SettingRow("图片缓存位置", "默认存放在数据目录，可改到其他磁盘", null));
            cache.Children.Add(SettingRow("", "", imageRow, false));

            var videoPath = Ui.Text(VideoCache.CacheDirectory, 11.5, null, "TextTertiary");
            videoPath.TextTrimming = TextTrimming.CharacterEllipsis;
            videoPath.MaxWidth = 320;
            videoPath.VerticalAlignment = VerticalAlignment.Center;
            var videoActions = new StackPanel { Orientation = Orientation.Horizontal };
            var chooseVideo = Ui.Button("更改位置", false, 12);
            chooseVideo.Margin = new Thickness(0, 0, 6, 0);
            chooseVideo.Click += (_, _) =>
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择视频缓存保存位置",
                    UseDescriptionForTitle = true,
                    SelectedPath = VideoCache.CacheDirectory,
                    ShowNewFolderButton = true,
                };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                try
                {
                    System.IO.Directory.CreateDirectory(dialog.SelectedPath);
                    Svc.Settings.VideoCacheDir = dialog.SelectedPath;
                    Svc.Settings.Save();
                    videoPath.Text = VideoCache.CacheDirectory;
                    Svc.Window?.ShowToast("视频缓存位置已更新");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("无法使用该目录：" + ex.Message);
                }
            };
            videoActions.Children.Add(chooseVideo);
            var openVideo = Ui.Button("打开", false, 12);
            openVideo.Margin = new Thickness(0, 0, 6, 0);
            openVideo.Click += (_, _) =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(VideoCache.CacheDirectory);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = VideoCache.CacheDirectory,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("打开缓存目录失败：" + ex.Message);
                }
            };
            videoActions.Children.Add(openVideo);
            var clearVideo = Ui.Button("清除", false, 12);
            clearVideo.Click += (_, _) =>
            {
                try
                {
                    var removed = 0;
                    var failed = 0;
                    if (System.IO.Directory.Exists(VideoCache.CacheDirectory))
                    {
                        foreach (var file in System.IO.Directory.GetFiles(VideoCache.CacheDirectory))
                        {
                            try
                            {
                                File.Delete(file);
                                removed++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    }
                    Svc.Window?.ShowToast(failed > 0
                        ? "已清理 " + removed + " 个，另有 " + failed + " 个正在使用"
                        : "视频缓存已清除");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("清除失败：" + ex.Message);
                }
            };
            videoActions.Children.Add(clearVideo);

            var videoRow = new StackPanel();
            videoRow.Children.Add(videoPath);
            videoRow.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6) });
            videoRow.Children.Add(videoActions);
            cache.Children.Add(SettingRow("视频缓存位置", "默认存放在数据目录，可改到其他磁盘", null));
            cache.Children.Add(SettingRow("", "", videoRow, false));

            var postPath = Ui.Text(CachedThreadStore.CacheDirectory, 11.5, null, "TextTertiary");
            postPath.TextTrimming = TextTrimming.CharacterEllipsis;
            postPath.MaxWidth = 320;
            postPath.VerticalAlignment = VerticalAlignment.Center;
            var postActions = new StackPanel { Orientation = Orientation.Horizontal };
            var choosePost = Ui.Button("更改位置", false, 12);
            choosePost.Margin = new Thickness(0, 0, 6, 0);
            choosePost.Click += (_, _) =>
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择帖子缓存保存位置",
                    UseDescriptionForTitle = true,
                    SelectedPath = CachedThreadStore.CacheDirectory,
                    ShowNewFolderButton = true,
                };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                try
                {
                    System.IO.Directory.CreateDirectory(dialog.SelectedPath);
                    Svc.Settings.PostCacheDir = dialog.SelectedPath;
                    Svc.Settings.Save();
                    CachedThreadStore.Reload();
                    postPath.Text = CachedThreadStore.CacheDirectory;
                    Svc.Window?.ShowToast("帖子缓存位置已更新");
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("无法使用该目录：" + ex.Message);
                }
            };
            postActions.Children.Add(choosePost);
            var openPost = Ui.Button("打开", false, 12);
            openPost.Click += (_, _) =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(CachedThreadStore.CacheDirectory);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = CachedThreadStore.CacheDirectory,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Svc.Window?.ShowToast("打开缓存目录失败：" + ex.Message);
                }
            };
            postActions.Children.Add(openPost);
            var postRow = new StackPanel();
            postRow.Children.Add(postPath);
            postRow.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6) });
            postRow.Children.Add(postActions);
            cache.Children.Add(SettingRow("帖子缓存位置", "缓存帖子的 JSON 保存目录，数量无上限", null));
            cache.Children.Add(SettingRow("", "", postRow, false));

            var cacheModePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var onlyOpRadio = new RadioButton
            {
                Content = "只缓存楼主",
                GroupName = "CacheMode",
                IsChecked = Svc.Settings.CacheOnlyOp,
                Margin = new Thickness(0, 0, 8, 0),
            };
            onlyOpRadio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            onlyOpRadio.Checked += (_, _) =>
            {
                Svc.Settings.CacheOnlyOp = true;
                Svc.Settings.Save();
            };
            cacheModePanel.Children.Add(onlyOpRadio);
            var allRadio = new RadioButton
            {
                Content = "缓存全部",
                GroupName = "CacheMode",
                IsChecked = !Svc.Settings.CacheOnlyOp,
            };
            allRadio.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            allRadio.Checked += (_, _) =>
            {
                Svc.Settings.CacheOnlyOp = false;
                Svc.Settings.Save();
            };
            cacheModePanel.Children.Add(allRadio);
            cache.Children.Add(SettingRow("缓存内容", "缓存整帖时只保存楼主楼层，或保存全部楼层", cacheModePanel));

            var pageValue = Math.Clamp(Svc.Settings.CacheMaxPages, 1, 200);
            var syncingPage = false;
            var pageLabel = Ui.Text(pageValue + " 页", 12, FontWeights.SemiBold, "Accent");
            pageLabel.Margin = new Thickness(10, 0, 0, 0);
            pageLabel.VerticalAlignment = VerticalAlignment.Center;
            pageLabel.Width = 44;
            pageLabel.TextAlignment = TextAlignment.Right;
            var pageInput = new TextBox
            {
                Width = 58,
                Text = pageValue.ToString(),
                FontSize = 12.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 2, 5, 2),
            };
            pageInput.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            pageInput.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            pageInput.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            pageInput.SetResourceReference(TextBox.BackgroundProperty, "SurfaceAlt");
            pageInput.SetResourceReference(TextBox.BorderBrushProperty, "Border");
            var pageSlider = new Slider
            {
                Minimum = 1,
                Maximum = 200,
                Value = pageValue,
                Width = 220,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("VideoSlider"),
            };
            void ApplyPage(int value)
            {
                value = Math.Clamp(value, 1, 200);
                Svc.Settings.CacheMaxPages = value;
                Svc.Settings.Save();
                pageLabel.Text = value + " 页";
                if (!syncingPage && pageInput.Text != value.ToString())
                {
                    syncingPage = true;
                    pageInput.Text = value.ToString();
                    syncingPage = false;
                }
            }
            pageSlider.ValueChanged += (_, e) => ApplyPage((int)Math.Round(e.NewValue));
            void ParsePageInput()
            {
                if (int.TryParse(pageInput.Text?.Trim(), out var value))
                {
                    value = Math.Clamp(value, 1, 200);
                    pageSlider.Value = value;
                    ApplyPage(value);
                }
                else
                {
                    pageInput.Text = Svc.Settings.CacheMaxPages.ToString();
                }
            }
            pageInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) ParsePageInput();
            };
            pageInput.LostFocus += (_, _) => ParsePageInput();
            var pagePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            pagePanel.Children.Add(pageSlider);
            pagePanel.Children.Add(pageLabel);
            pagePanel.Children.Add(pageInput);
            var pageBlock = new StackPanel { Margin = new Thickness(18, 12, 18, 10) };
            pageBlock.Children.Add(Ui.Text("最大缓存页数", 15, FontWeights.Medium));
            pagePanel.Margin = new Thickness(0, 10, 0, 0);
            pageBlock.Children.Add(pagePanel);
            var pageDesc = Ui.Text("1–200 页，可拖动滑块或直接输入页数", 12.5, null, "TextTertiary");
            pageDesc.Margin = new Thickness(0, 8, 0, 0);
            pageDesc.TextWrapping = TextWrapping.Wrap;
            pageBlock.Children.Add(pageDesc);
            cache.Children.Add(pageBlock);
            var riskButton = Ui.Button("风控说明", false, 11.5);
            riskButton.HorizontalAlignment = HorizontalAlignment.Left;
            riskButton.Margin = new Thickness(2, 0, 0, 2);
            riskButton.Click += (_, _) =>
            {
                var window = new RiskNoticeWindow { Owner = Svc.Window };
                window.Show();
                window.Activate();
            };
            cache.Children.Add(riskButton);
            cacheGroup.Children.Add(Section("存储", cache));

            var blockBody = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
            blockBody.Children.Add(Ui.Text("屏蔽词管理", 14, FontWeights.Medium));
            var blockDesc = Ui.Text("标题或正文包含屏蔽词的帖子会被自动隐藏。", 12.5, null, "TextTertiary");
            blockDesc.TextWrapping = TextWrapping.Wrap;
            blockDesc.Margin = new Thickness(0, 5, 0, 10);
            blockBody.Children.Add(blockDesc);
            var blockButton = Ui.Button("管理屏蔽词 (" + BlockWords.All.Count + ")", false, 12.5);
            blockButton.HorizontalAlignment = HorizontalAlignment.Left;
            blockButton.Click += (_, _) =>
            {
                var window = new BlockWordsWindow { Owner = Svc.Window };
                window.ShowDialog();
                blockButton.Content = "管理屏蔽词 (" + BlockWords.All.Count + ")";
            };
            blockBody.Children.Add(blockButton);
            cacheGroup.Children.Add(Section("屏蔽", blockBody));

            // account
            if (Svc.Account.IsLoggedIn)
            {
                AccountStore.Upsert(Svc.Account);
                var account = new StackPanel();
                var switchButton = Ui.Button("切换账号", false, 12.5);
                switchButton.Click += (_, _) =>
                {
                    var window = new AccountSwitchWindow { Owner = Svc.Window };
                    window.ShowDialog();
                };
                var currentName = string.IsNullOrWhiteSpace(Svc.Account.UserName)
                    ? "ID " + Svc.Account.Uid
                    : Svc.Account.UserName;
                account.Children.Add(SettingRow("当前账号", currentName, switchButton));

                var addButton = Ui.Button("添加账号", false, 12.5);
                addButton.Click += (_, _) =>
                {
                    var login = new LoginWindow { Owner = Svc.Window };
                    if (login.ShowDialog() == true)
                    {
                        AccountStore.Upsert(Svc.Account);
                        Svc.Window?.UpdateUserCard();
                        Svc.Window?.ShowToast("账号已添加");
                    }
                };
                account.Children.Add(SettingRow("添加账号", "登录另一个贴吧账号", addButton));

                var logout = Ui.Button("退出登录", false, 12.5);
                logout.Click += (_, _) =>
                {
                    Svc.Account.Clear();
                    Svc.RecreateClient();
                    Svc.Window?.UpdateUserCard();
                    Svc.Window?.ShowToast("已退出登录");
                };
                account.Children.Add(SettingRow("退出登录", "清除当前登录状态", logout));

                var rename = Ui.Button("修改用户名", false, 12.5);
                rename.Click += (_, _) => Ui.OpenUrl("https://tieba.baidu.com/home/main");
                account.Children.Add(SettingRow("修改用户名", "打开贴吧个人主页进行修改", rename));

                var copyBduss = Ui.Button("复制 BDUSS", false, 12.5);
                copyBduss.Click += async (_, _) =>
                {
                    try
                    {
                        await ClipboardHelper.SetTextAsync(Svc.Account.Bduss ?? "");
                        Svc.Window?.ShowToast("BDUSS 已复制，请勿泄露");
                    }
                    catch
                    {
                        Svc.Window?.ShowToast("复制失败，剪贴板被占用");
                    }
                };
                account.Children.Add(SettingRow("复制 BDUSS", "请勿泄露，否则可能导致账号被盗用", copyBduss));

                var tail = new TextBox
                {
                    Text = Svc.Settings.TailText ?? "",
                    Width = 200,
                    FontSize = 12.5,
                    Padding = new Thickness(6, 3, 6, 3),
                    BorderThickness = new Thickness(1),
                };
                tail.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
                tail.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
                tail.SetResourceReference(TextBox.BackgroundProperty, "SurfaceAlt");
                tail.SetResourceReference(TextBox.BorderBrushProperty, "Border");
                tail.LostFocus += (_, _) =>
                {
                    Svc.Settings.TailText = tail.Text.Trim();
                    Svc.Settings.Save();
                };
                account.Children.Add(SettingRow("我的小尾巴", "保存在本地的回复签名", tail));
                accountGroup.Children.Add(Section("账号", account));
            }
            // about
            var others = new StackPanel();
            var jumpInput = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(Svc.Settings.JumpForumName) ? "孙笑川" : Svc.Settings.JumpForumName,
                Width = 200,
                FontSize = 12.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 3, 6, 3),
            };
            jumpInput.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            jumpInput.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            jumpInput.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            jumpInput.SetResourceReference(TextBox.BackgroundProperty, "SurfaceAlt");
            jumpInput.SetResourceReference(TextBox.BorderBrushProperty, "Border");
            jumpInput.LostFocus += (_, _) =>
            {
                Svc.Settings.JumpForumName = string.IsNullOrWhiteSpace(jumpInput.Text) ? "孙笑川" : jumpInput.Text.Trim();
                Svc.Settings.Save();
            };
            others.Children.Add(SettingRow("一键跳转吧", "侧栏「一键跳转」打开的吧，默认孙笑川吧", jumpInput));

            var clearOnExit = new ToggleSwitch { IsChecked = Svc.Settings.ClearCacheOnExit, VerticalAlignment = VerticalAlignment.Center };
            clearOnExit.Checked += (_, _) => { Svc.Settings.ClearCacheOnExit = true; Svc.Settings.Save(); };
            clearOnExit.Unchecked += (_, _) => { Svc.Settings.ClearCacheOnExit = false; Svc.Settings.Save(); };
            others.Children.Add(SettingRow("退出时清理缓存", "退出应用时自动清理图片缓存，避免缓存无限膨胀", clearOnExit));

            var openLinks = new ToggleSwitch { IsChecked = Svc.Settings.OpenTiebaLinks, VerticalAlignment = VerticalAlignment.Center };
            openLinks.Checked += (_, _) => { Svc.Settings.OpenTiebaLinks = true; Svc.Settings.Save(); };
            openLinks.Unchecked += (_, _) => { Svc.Settings.OpenTiebaLinks = false; Svc.Settings.Save(); };
            others.Children.Add(SettingRow("默认打开", "允许在此应用中打开贴吧网页链接", openLinks));

            var traySwitch = new ToggleSwitch { IsChecked = Svc.Settings.MinimizeToTray, VerticalAlignment = VerticalAlignment.Center };
            traySwitch.Checked += (_, _) => { Svc.Settings.MinimizeToTray = true; Svc.Settings.Save(); };
            traySwitch.Unchecked += (_, _) => { Svc.Settings.MinimizeToTray = false; Svc.Settings.Save(); };
            others.Children.Add(SettingRow("最小化到托盘", "关闭窗口时隐藏到托盘并保留图标", traySwitch));

            var autoSign = new ToggleSwitch { IsChecked = Svc.Settings.AutoSign, VerticalAlignment = VerticalAlignment.Center };
            autoSign.Checked += (_, _) => { Svc.Settings.AutoSign = true; Svc.Settings.Save(); };
            autoSign.Unchecked += (_, _) => { Svc.Settings.AutoSign = false; Svc.Settings.Save(); };
            others.Children.Add(SettingRow("自动签到", "启动时自动签到所有已关注的吧", autoSign));

            var riskSwitch = new ToggleSwitch { IsChecked = Svc.Settings.ShowPostRiskWarning, VerticalAlignment = VerticalAlignment.Center };
            riskSwitch.Checked += (_, _) => { Svc.Settings.ShowPostRiskWarning = true; Svc.Settings.Save(); };
            riskSwitch.Unchecked += (_, _) => { Svc.Settings.ShowPostRiskWarning = false; Svc.Settings.Save(); };
            others.Children.Add(SettingRow("发帖风控提醒", "发帖或回复前显示频率限制提醒", riskSwitch));

            var emojiButton = Ui.Button("管理表情", false, 12.5);
            emojiButton.Click += (_, _) =>
            {
                var window = new EmojiManagerWindow { Owner = Svc.Window };
                window.ShowDialog();
            };
            others.Children.Add(SettingRow("表情包管理", "选择发帖面板中显示的表情", emojiButton));

            var save = Ui.Button("保存设置", true, 12.5);
            save.Click += (_, _) =>
            {
                Svc.Settings.Save();
                Svc.ApplyTheme();
                Svc.Window?.ShowToast("设置已保存");
            };
            others.Children.Add(SettingRow("保存设置", "立即保存当前主题、网络与缓存设置", save, false));
            accountGroup.Children.Add(Section("其他", others));

            var about = new StackPanel();
            about.Children.Add(SettingRow("TieLiteDesktop", "版本 1.0.1 · 非官方贴吧客户端", null));
            about.Children.Add(SettingRow("发布者", "wanteel2430", null));
            var bilibili = Ui.Button("打开 B 站空间", false, 12.5);
            bilibili.Click += (_, _) =>
            {
                if (!Ui.OpenUrl("https://b23.tv/H8UP6z2"))
                {
                    Svc.Window?.ShowToast("无法打开浏览器");
                }
            };
            about.Children.Add(SettingRow("开发者", "国王的新宰相 · 哔哩哔哩", bilibili));
            about.Children.Add(SettingRow("开源协议", "GPL-3.0，与上游 FluxTie 一致", null));
            var link = Ui.Button("打开项目主页", false, 12.5);
            link.Click += (_, _) =>
            {
                if (!Ui.OpenUrl("https://github.com/makise2060/FluxTie"))
                {
                    Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
                }
            };
            about.Children.Add(SettingRow("项目地址", "桌面版基于 FluxTie 源码移植", link));
            var help = Ui.Button("打开帮助中心", false, 12.5);
            help.Click += (_, _) => Ui.OpenUrl("https://help.baidu.com/question?prod_id=3");
            about.Children.Add(SettingRow("帮助中心", "百度贴吧官方帮助与问题反馈", help));
            accountGroup.Children.Add(Section("关于", about));

            var disclaimer = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
            var disclaimerText = Ui.Text(
                "TieLiteDesktop 是非官方第三方贴吧客户端，与百度贴吧官方无隶属关系。\n\n" +
                "本程序仅供学习、研究与个人交流使用，不得用于商业用途或违反法律法规的用途。\n\n" +
                "贴吧内容、用户信息及相关商标版权归百度贴吧及原作者所有。使用本程序产生的账号风险、内容风险由使用者自行承担。\n\n" +
                "登录信息、应用密码与缓存仅保存在本机，不会上传到本程序作者的服务器。",
                12, null, "TextSecondary");
            disclaimerText.TextWrapping = TextWrapping.Wrap;
            disclaimerText.LineHeight = 20;
            disclaimer.Children.Add(disclaimerText);
            accountGroup.Children.Add(Section("免责声明", disclaimer));

            var footer = Ui.Text("TieLiteDesktop · 为兴趣而生", 11.5, null, "TextTertiary");
            footer.HorizontalAlignment = HorizontalAlignment.Center;
            footer.Margin = new Thickness(0, 18, 0, 0);

            var privacy = new StackPanel();
            var passwordDescription = Ui.Text(
                AppPassword.HasPassword
                    ? (Svc.Settings.PasswordType == "custom" ? "已设置自定义密码" : "已设置六位 PIN")
                    : "未设置，锁定按钮隐藏",
                11.5, null, "TextTertiary");
            passwordDescription.Margin = new Thickness(0, 4, 0, 0);
            var passwordButton = Ui.Button(AppPassword.HasPassword ? "更改密码" : "启用密码", false, 12.5);
            passwordButton.Click += (_, _) =>
            {
                if (AppPassword.HasPassword)
                {
                    var prompt = new PasswordPromptWindow("更改密码", "先输入当前密码，验证后才能设置新密码。") { Owner = Svc.Window };
                    if (prompt.ShowDialog() != true) return;
                }
                var window = new PasswordSetupWindow(AppPassword.HasPassword) { Owner = Svc.Window };
                window.ShowDialog();
                Svc.Window?.RefreshLockButton();
                passwordButton.Content = AppPassword.HasPassword ? "更改密码" : "启用密码";
                passwordDescription.Text = AppPassword.HasPassword
                    ? (Svc.Settings.PasswordType == "custom" ? "已设置自定义密码" : "已设置六位 PIN")
                    : "未设置，锁定按钮隐藏";
            };
            var lockOnRestore = new ToggleSwitch { IsChecked = Svc.Settings.LockOnRestore, VerticalAlignment = VerticalAlignment.Center };
            lockOnRestore.Checked += (_, _) => { Svc.Settings.LockOnRestore = true; Svc.Settings.Save(); };
            lockOnRestore.Unchecked += (_, _) => { Svc.Settings.LockOnRestore = false; Svc.Settings.Save(); };
            privacy.Children.Add(SettingRow("恢复时锁定", "从任务栏或托盘恢复窗口时要求输入密码", lockOnRestore));

            var passwordTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            passwordTexts.Children.Add(Ui.Text("应用密码", 15, FontWeights.Medium));
            passwordTexts.Children.Add(passwordDescription);
            var passwordRow = new Grid { Margin = new Thickness(18, 14, 18, 14) };
            passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            passwordRow.Children.Add(passwordTexts);
            Grid.SetColumn(passwordButton, 1);
            passwordRow.Children.Add(passwordButton);
            privacy.Children.Add(passwordRow);

            var removePassword = Ui.Button("取消密码", false, 12.5);
            removePassword.Click += (_, _) =>
            {
                if (!AppPassword.HasPassword)
                {
                    Svc.Window?.ShowToast("未设置密码");
                    return;
                }
                var prompt = new PasswordPromptWindow("取消密码", "输入当前密码后取消应用密码。") { Owner = Svc.Window };
                if (prompt.ShowDialog() != true) return;
                AppPassword.Clear();
                Svc.Window?.RefreshLockButton();
                passwordButton.Content = "启用密码";
                passwordDescription.Text = "未设置，锁定按钮隐藏";
                Svc.Window?.ShowToast("应用密码已取消");
            };
            privacy.Children.Add(SettingRow("取消密码", "验证当前密码后关闭应用锁", removePassword));

            var privacyGroup = new WrapPanel { Orientation = Orientation.Horizontal };
            privacyGroup.Children.Add(Section("隐私", privacy));
            var privacyPage = new StackPanel { Visibility = Visibility.Collapsed };
            privacyPage.Children.Add(privacyGroup);

            var generalPage = new StackPanel();
            generalPage.Children.Add(generalGroup);
            var cachePage = new StackPanel { Visibility = Visibility.Collapsed };
            cachePage.Children.Add(cacheGroup);
            var accountPage = new StackPanel { Visibility = Visibility.Collapsed };
            accountPage.Children.Add(accountGroup);

            var tabBar = new StackPanel { Orientation = Orientation.Horizontal, Width = 840, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 2, 16) };
            var generalTab = new RadioButton { Content = "常规", GroupName = "SettingsSection", IsChecked = true };
            var cacheTab = new RadioButton { Content = "内容与缓存", GroupName = "SettingsSection" };
            var accountTab = new RadioButton { Content = "账号与关于", GroupName = "SettingsSection" };
            var privacyTab = new RadioButton { Content = "隐私", GroupName = "SettingsSection" };
            foreach (var tab in new[] { generalTab, cacheTab, accountTab, privacyTab })
            {
                tab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
                tab.Margin = new Thickness(0, 0, 8, 0);
                tabBar.Children.Add(tab);
            }
            void AnimatePage(FrameworkElement page)
            {
                if (page == null) return;
                page.Opacity = 0;
                var shift = new System.Windows.Media.TranslateTransform(0, 7);
                page.RenderTransform = shift;
                var ease = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                };
                page.BeginAnimation(UIElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                    {
                        EasingFunction = ease,
                    });
                shift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(220))
                    {
                        EasingFunction = ease,
                    });
            }

            void ShowSection(int index)
            {
                generalPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
                cachePage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
                accountPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
                privacyPage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
                if (index == 0) generalTab.IsChecked = true;
                else if (index == 1) cacheTab.IsChecked = true;
                else if (index == 2) accountTab.IsChecked = true;
                else privacyTab.IsChecked = true;
                if (index == 0) AnimatePage(generalPage);
                else if (index == 1) AnimatePage(cachePage);
                else if (index == 2) AnimatePage(accountPage);
                else AnimatePage(privacyPage);
            }
            generalTab.Checked += (_, _) => { if (generalTab.IsChecked == true) ShowSection(0); };
            cacheTab.Checked += (_, _) => { if (cacheTab.IsChecked == true) ShowSection(1); };
            accountTab.Checked += (_, _) => { if (accountTab.IsChecked == true) ShowSection(2); };
            privacyTab.Checked += (_, _) =>
            {
                if (privacyTab.IsChecked != true) return;
                if (AppPassword.HasPassword && !_privacyUnlocked)
                {
                    var prompt = new PasswordPromptWindow("隐私设置", "输入应用密码后才能查看隐私设置。", true) { Owner = Svc.Window };
                    if (prompt.ShowDialog() != true)
                    {
                        accountTab.IsChecked = true;
                        ShowSection(2);
                        return;
                    }
                    _privacyUnlocked = true;
                }
                ShowSection(3);
            };
            root.Children.Add(tabBar);
            root.Children.Add(generalPage);
            root.Children.Add(cachePage);
            root.Children.Add(accountPage);
            root.Children.Add(privacyPage);
            root.Children.Add(footer);
            scroll.Content = root;
            Content = scroll;
        }

        private static UIElement GroupHeader(string title)
        {
            var border = new Border
            {
                Margin = new Thickness(2, 6, 2, 10),
                Padding = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            border.SetResourceReference(Border.BorderBrushProperty, "Border");
            var text = Ui.Text(title, 13, FontWeights.SemiBold, "Accent");
            border.Child = text;
            return border;
        }

        private static UIElement Section(string title, UIElement body)
        {
            var stack = new StackPanel { Width = 410, Margin = new Thickness(0, 0, 14, 24) };
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 0, 0, 8),
            };
            var bar = new Border { Width = 3, Height = 14, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
            bar.SetResourceReference(BackgroundProperty, "Accent");
            header.Children.Add(bar);
            var label = Ui.Text("  " + title, 14, FontWeights.SemiBold, "TextSecondary");
            label.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(label);
            stack.Children.Add(header);
            stack.Children.Add(Ui.Card(body, 4, 16));
            return stack;
        }

        private static UIElement SettingRow(string title, string description, UIElement control, bool showDivider = true)
        {
            var grid = new Grid { Margin = new Thickness(18, 14, 18, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(title)) texts.Children.Add(Ui.Text(title, 15, FontWeights.Medium));
            if (!string.IsNullOrEmpty(description))
            {
                var desc = Ui.Text(description, 12.5, null, "TextTertiary");
                desc.Margin = new Thickness(0, 3, 0, 0);
                texts.Children.Add(desc);
            }
            grid.Children.Add(texts);
            if (control != null)
            {
                if (control is FrameworkElement element) element.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
            }
            if (!showDivider) return grid;
            var host = new StackPanel();
            host.Children.Add(grid);
            var divider = new Border { Height = 1, Margin = new Thickness(16, 0, 16, 0) };
            divider.SetResourceReference(BackgroundProperty, "Border");
            host.Children.Add(divider);
            return host;
        }

        private void SyncThemeRadios()
        {
            _syncingTheme = true;
            try
            {
                foreach (var (mode, radio) in _themeRadios)
                {
                    radio.IsChecked = Svc.Settings.ThemeMode == mode;
                }
            }
            finally
            {
                _syncingTheme = false;
            }
        }

        private void SetTheme(string mode)
        {
            Svc.Settings.ThemeMode = mode;
            Svc.ApplyTheme();
            Svc.Settings.Save();
        }

        private static string CacheSizeText()
        {
            try
            {
                if (!Directory.Exists(ImageLoader.CacheDirectory)) return "暂无缓存";
                var size = Directory.GetFiles(ImageLoader.CacheDirectory).Sum(f => new FileInfo(f).Length);
                return (size / 1024.0 / 1024.0).ToString("0.0") + " MB";
            }
            catch
            {
                return "";
            }
        }
    }
}
