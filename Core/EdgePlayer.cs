using System;
using System.Diagnostics;
using System.IO;

namespace TieLiteDesktop.Core
{
    /// <summary>
    /// Opens a URL in a chromeless Edge window. Used when the machine lacks the media
    /// codecs required by WPF's MediaElement (e.g. Windows N editions).
    /// </summary>
    public static class EdgePlayer
    {
        public static string FindEdgePath()
        {
            foreach (var path in new[]
                     {
                         @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                         @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                     })
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static string FindEdge() => FindEdgePath();

        public static bool OpenApp(string url, int width = 1024, int height = 660)
        {
            var edge = FindEdge();
            if (edge == null || string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                var info = new ProcessStartInfo(edge) { UseShellExecute = false };
                info.ArgumentList.Add("--app=" + url);
                info.ArgumentList.Add("--window-size=" + width + "," + height);
                Process.Start(info);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
