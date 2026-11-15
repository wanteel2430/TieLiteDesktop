using System;

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    /// <summary>Persisted application preferences (settings.json under %APPDATA%).</summary>
    public sealed class AppSettings : INotifyPropertyChanged
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static bool _migrated;
        public static string ConfigDirectory
        {
            get
            {
                var custom = Environment.GetEnvironmentVariable("TIELITE_DATA_DIR")
                             ?? Environment.GetEnvironmentVariable("FLUXTIE_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(custom)) return custom;
                var portableMarker = Path.Combine(AppContext.BaseDirectory, "portable.txt");
                if (File.Exists(portableMarker)) return Path.Combine(AppContext.BaseDirectory, "data");
                var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TieLiteDesktop");
                if (!_migrated)
                {
                    _migrated = true;
                    try
                    {
                        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluxTieDesktop");
                        if (!Directory.Exists(target) && Directory.Exists(legacy))
                        {
                            Directory.Move(legacy, target);
                        }
                    }
                    catch
                    {
                    }
                }
                return target;
            }
        }

        public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

        private string _theme = "light";
        [System.Text.Json.Serialization.JsonIgnore]
        public string Theme { get => _theme; set => Set(ref _theme, value); }

        private string _postCacheDir = "";
        private bool _cacheOnlyOp;
        private int _cacheMaxPages = 50;
        private string _imageCacheDir = "";
        private string _jumpForumName = "孙笑川";
        private string[] _blockedWords = Array.Empty<string>();
        private bool _showRecentForums = true;
        private bool _sidebarCollapsed;
        private string _themeMode = "system";
        /// <summary>system | light | dark</summary>
        public string ThemeMode { get => _themeMode; set => Set(ref _themeMode, value); }

        private string _accent = "blue";
        private string _customAccent = "#4477E0";
        private string _splashImage = "";
        private string _backgroundImage = "";
        private bool _backgroundLetterbox;
        private string _backgroundFillColor = "#121418";
        private bool _hasCustomAccent;
        private string[] _hiddenThreads = Array.Empty<string>();
        private string[] _pinnedForums = Array.Empty<string>();
        private string _tailText = "";
        private bool _clearCacheOnExit;
        private bool _openTiebaLinks = true;
        private bool _showPostRiskWarning = true;
        private string _passwordHash = "";
        private string _passwordSalt = "";
        private string _passwordType = "";
        private bool _minimizeToTray;
        private bool _skipExitPrompt;
        private bool _lockOnRestore = true;
        private bool _autoSign;
        private double _cardOpacity = 0.5;
        private string[] _hiddenEmoticons = Array.Empty<string>();
        public string Accent { get => _accent; set => Set(ref _accent, value); }
        public string CustomAccent { get => _customAccent; set => Set(ref _customAccent, value); }
        public string SplashImage { get => _splashImage; set => Set(ref _splashImage, value); }
        public string BackgroundImage { get => _backgroundImage; set => Set(ref _backgroundImage, value); }
        public bool BackgroundLetterbox { get => _backgroundLetterbox; set => Set(ref _backgroundLetterbox, value); }
        public string BackgroundFillColor { get => _backgroundFillColor; set => Set(ref _backgroundFillColor, value); }
        public bool HasCustomAccent { get => _hasCustomAccent; set => Set(ref _hasCustomAccent, value); }
        public string[] HiddenThreads { get => _hiddenThreads; set => Set(ref _hiddenThreads, value ?? Array.Empty<string>()); }
        public string[] PinnedForums { get => _pinnedForums; set => Set(ref _pinnedForums, value ?? Array.Empty<string>()); }
        public string TailText { get => _tailText; set => Set(ref _tailText, value); }
        public bool ClearCacheOnExit { get => _clearCacheOnExit; set => Set(ref _clearCacheOnExit, value); }
        public bool OpenTiebaLinks { get => _openTiebaLinks; set => Set(ref _openTiebaLinks, value); }
        public bool ShowPostRiskWarning { get => _showPostRiskWarning; set => Set(ref _showPostRiskWarning, value); }
        public string[] HiddenEmoticons { get => _hiddenEmoticons; set => Set(ref _hiddenEmoticons, value ?? Array.Empty<string>()); }
        public string PasswordHash { get => _passwordHash; set => Set(ref _passwordHash, value ?? ""); }
        public string PasswordSalt { get => _passwordSalt; set => Set(ref _passwordSalt, value ?? ""); }
        public string PasswordType { get => _passwordType; set => Set(ref _passwordType, value ?? ""); }
        public bool MinimizeToTray { get => _minimizeToTray; set => Set(ref _minimizeToTray, value); }
        public bool SkipExitPrompt { get => _skipExitPrompt; set => Set(ref _skipExitPrompt, value); }
        public bool LockOnRestore { get => _lockOnRestore; set => Set(ref _lockOnRestore, value); }
        public bool AutoSign { get => _autoSign; set => Set(ref _autoSign, value); }
        public double CardOpacity { get => _cardOpacity; set => Set(ref _cardOpacity, Math.Clamp(value, 0.3, 1.0)); }

        private double _fontScale = 1.0;
        public double FontScale { get => _fontScale; set => Set(ref _fontScale, value); }

        private string _proxyMode = "system";
        /// <summary>system | none | custom</summary>
        public string ProxyMode { get => _proxyMode; set => Set(ref _proxyMode, value); }

        private string _proxyUrl = "";
        public string ProxyUrl { get => _proxyUrl; set => Set(ref _proxyUrl, value); }

        private bool _showImages = true;
        public bool ShowImages { get => _showImages; set => Set(ref _showImages, value); }

        private bool _hideVideoThreads;
        public bool HideVideoThreads { get => _hideVideoThreads; set => Set(ref _hideVideoThreads, value); }

        private string _videoCacheDir = "";
        /// <summary>Custom folder for cached videos; empty = default under the data directory.</summary>
        public string VideoCacheDir { get => _videoCacheDir; set => Set(ref _videoCacheDir, value); }
        public string PostCacheDir { get => _postCacheDir; set => Set(ref _postCacheDir, value); }
        public bool CacheOnlyOp { get => _cacheOnlyOp; set => Set(ref _cacheOnlyOp, value); }
        public int CacheMaxPages { get => _cacheMaxPages; set => Set(ref _cacheMaxPages, value); }
        public string ImageCacheDir { get => _imageCacheDir; set => Set(ref _imageCacheDir, value); }
        public string JumpForumName { get => _jumpForumName; set => Set(ref _jumpForumName, value); }
        public string[] BlockedWords { get => _blockedWords; set => Set(ref _blockedWords, value ?? Array.Empty<string>()); }
        public bool ShowRecentForums { get => _showRecentForums; set => Set(ref _showRecentForums, value); }
        public bool SidebarCollapsed { get => _sidebarCollapsed; set => Set(ref _sidebarCollapsed, value); }

        private bool _confirmOnClose = true;
        public bool ConfirmOnClose { get => _confirmOnClose; set => Set(ref _confirmOnClose, value); }

        private string _lastForum = "";
        public string LastForum { get => _lastForum; set => Set(ref _lastForum, value); }

        private string[] _recentForums = Array.Empty<string>();
        public string[] RecentForums { get => _recentForums; set => Set(ref _recentForums, value); }

        private double _windowWidth;
        public double WindowWidth { get => _windowWidth; set => Set(ref _windowWidth, value); }

        private double _windowHeight;
        public double WindowHeight { get => _windowHeight; set => Set(ref _windowHeight, value); }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath));
                    if (settings != null)
                    {
                        if (string.IsNullOrWhiteSpace(settings.ThemeMode)) settings.ThemeMode = "system";
                        return settings;
                    }
                }
            }
            catch
            {
                // corrupted config: fall back to defaults
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch
            {
                // never crash because settings could not be written
            }
        }
    }
}
