using System;
using Microsoft.Win32;

namespace TieLiteDesktop.Core
{
    /// <summary>Reads the Windows "app theme" preference (Settings → Personalization → Colors).</summary>
    public static class SystemTheme
    {
        public static bool IsDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var raw = key?.GetValue("AppsUseLightTheme");
            if (raw != null)
            {
                try
                {
                    return Convert.ToInt32(raw) == 0;
                }
                catch
                {
                }
            }
            }
            catch
            {
            }
            return false;
        }
    }
}
