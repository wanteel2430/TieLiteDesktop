using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Threading;
using System.Threading.Tasks;

namespace TieLiteDesktop.Core
{
    /// <summary>Win32 clipboard access with retries; avoids WPF Clipboard locking issues.</summary>
    public static class ClipboardHelper
    {
        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr newOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr handle);

        private static IntPtr OwnerHandle()
        {
            try
            {
                var window = Application.Current?.MainWindow;
                return window == null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static async Task<bool> SetTextAsync(string text, int timeoutMs = 1200)
        {
            if (text == null) return false;
            var owner = OwnerHandle();
            var task = Task.Run(() => SetTextWin32(text, owner));
            var finished = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(true);
            if (finished == task && task.Result) return true;
            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Synchronous compatibility wrapper; prefer <see cref="SetTextAsync"/>.</summary>
        public static bool TrySetText(string text) => SetTextWin32(text, OwnerHandle());

        public static string TryGetText()
        {
            var owner = OwnerHandle();
            for (var i = 0; i < 20; i++)
            {
                if (!OpenClipboard(owner))
                {
                    Thread.Sleep(25);
                    continue;
                }
                try
                {
                    if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
                    var handle = GetClipboardData(CF_UNICODETEXT);
                    if (handle == IntPtr.Zero) return null;
                    var pointer = GlobalLock(handle);
                    if (pointer == IntPtr.Zero) return null;
                    try
                    {
                        return Marshal.PtrToStringUni(pointer);
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            return null;
        }

        private static bool SetTextWin32(string text, IntPtr owner)
        {
            for (var i = 0; i < 20; i++)
            {
                if (!OpenClipboard(owner))
                {
                    Thread.Sleep(40);
                    continue;
                }
                try
                {
                    EmptyClipboard();
                    var handle = Marshal.StringToHGlobalUni(text);
                    if (SetClipboardData(CF_UNICODETEXT, handle) != IntPtr.Zero)
                    {
                        return true;
                    }
                    Marshal.FreeHGlobal(handle);
                }
                catch
                {
                }
                finally
                {
                    CloseClipboard();
                }
                Thread.Sleep(40);
            }
            return false;
        }
    }
}
