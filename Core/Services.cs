using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;
using TieLiteDesktop.Core.Api;
using TieLiteDesktop.Core.Proto;
using TieLiteDesktop.Views;

namespace TieLiteDesktop.Core
{
    /// <summary>Application-wide service locator (small app, no DI container needed).</summary>
    public static class Svc
    {
        public static AppSettings Settings { get; private set; }
        public static Account Account { get; private set; }
        public static DeviceProfile Device { get; private set; }
        public static ProtoSchema Schema { get; private set; }
        public static TiebaClient Client { get; private set; }
        public static TiebaApi Api { get; private set; }
        public static Navigator Nav { get; } = new();
        public static MainWindow Window { get; set; }
        public static bool DemoMode { get; set; }
        public static string EffectiveTheme { get; private set; } = "light";

        public static event Action ThemeChanged;

        public static string AssetsDir => Path.Combine(AppContext.BaseDirectory, "Assets");

        public static void Initialize()
        {
            Settings = AppSettings.Load();
            Account = Account.Load();
            Device = DeviceProfile.LoadOrCreate();
            Schema = ProtoSchema.Load(Path.Combine(AssetsDir, "schema.json"));
            ContentParser.Initialize(AssetsDir);
            Client = new TiebaClient(Device, Account, Schema, Settings);
            Api = new TiebaApi(Client);
            ApplyTheme();
        }

        /// <summary>Resolves "system" against the Windows app theme.</summary>
        public static string ResolveTheme(string mode)
        {
            if (mode == "light" || mode == "dark") return mode;
            return SystemTheme.IsDark() ? "dark" : "light";
        }

        public static void ApplyTheme()
        {
            var resolved = ResolveTheme(Settings.ThemeMode);
            var dark = resolved == "dark";
            EffectiveTheme = dark ? "dark" : "light";

            void Swap()
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(dark ? "Theme/Dark.xaml" : "Theme/Light.xaml", UriKind.Relative),
                };
                var app = Application.Current;
                if (app == null) return;
                if (app.Resources.MergedDictionaries.Count == 0) app.Resources.MergedDictionaries.Add(dict);
                else app.Resources.MergedDictionaries[0] = dict;
                AccentTheme.Apply(Settings.Accent, Settings.CustomAccent);
                BackgroundMaterial.Apply();
                ThemeChanged?.Invoke();
            }

            var window = Application.Current?.MainWindow;
            if (window != null && window.IsLoaded && window.Content is Panel panel)
            {
                var veil = new Border
                {
                    Background = new SolidColorBrush(dark
                        ? Color.FromRgb(0x0A, 0x0B, 0x0E)
                        : Color.FromRgb(0x80, 0x80, 0x80)),
                    Opacity = 0,
                    IsHitTestVisible = false,
                };
                Panel.SetZIndex(veil, 9999);
                panel.Children.Add(veil);
                var fadeIn = new DoubleAnimation(0, 0.72, TimeSpan.FromMilliseconds(110))
                {
                    EasingFunction = Anim.EaseInOut,
                };
                fadeIn.Completed += (_, _) =>
                {
                    Swap();
                    var fadeOut = new DoubleAnimation(0.72, 0, TimeSpan.FromMilliseconds(240))
                    {
                        EasingFunction = Anim.EaseOut,
                    };
                    fadeOut.Completed += (_, _) => panel.Children.Remove(veil);
                    veil.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                veil.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                Swap();
            }
        }

        private static System.Windows.Threading.DispatcherTimer _themeTimer;

        /// <summary>Watches the Windows setting so "跟随系统" reacts without a restart.</summary>
        public static void StartThemeWatcher()
        {
            if (_themeTimer != null) return;
            _themeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _themeTimer.Tick += (_, _) =>
            {
                if (Settings == null || Settings.ThemeMode != "system") return;
                var resolved = ResolveTheme("system");
                if (resolved != EffectiveTheme) ApplyTheme();
            };
            _themeTimer.Start();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) =>
            {
                try
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (Settings?.ThemeMode == "system")
                        {
                            var resolved = ResolveTheme("system");
                            if (resolved != EffectiveTheme) ApplyTheme();
                        }
                    }));
                }
                catch
                {
                }
            };
        }

        public static void UseAccount(Account account)
        {
            if (account == null) return;
            Account = account;
            Account.Save();
            RecreateClient();
        }

        public static void RecreateClient()
        {
            Client?.Dispose();
            Client = new TiebaClient(Device, Account, Schema, Settings);
            Api = new TiebaApi(Client);
        }
    }

    public sealed class PageEntry
    {
        public Func<UserControl> Factory { get; init; }
        public string Title { get; init; }
    }

    /// <summary>Simple stack navigation for the content area.</summary>
    public sealed class Navigator
    {
        private readonly List<PageEntry> _stack = new();

        public event Action<PageEntry, bool> Navigated;

        public bool CanGoBack => _stack.Count > 1;
        public PageEntry Current => _stack.Count > 0 ? _stack[^1] : null;

        public void Push(Func<UserControl> factory, string title)
        {
            if (_stack.Count > 0 && _stack[^1].Title == title && title == "设置")
            {
                // avoid stacking duplicate settings pages
                Navigated?.Invoke(_stack[^1], false);
                return;
            }
            _stack.Add(new PageEntry { Factory = factory, Title = title });
            Navigated?.Invoke(_stack[^1], false);
        }

        public void Reset(Func<UserControl> factory, string title)
        {
            _stack.Clear();
            _stack.Add(new PageEntry { Factory = factory, Title = title });
            Navigated?.Invoke(_stack[^1], false);
        }

        public void Back()
        {
            if (_stack.Count <= 1) return;
            _stack.RemoveAt(_stack.Count - 1);
            Navigated?.Invoke(_stack[^1], true);
        }
    }
}
