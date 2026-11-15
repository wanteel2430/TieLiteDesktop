using System;
using System.Collections.Generic;
using System.Linq;

namespace TieLiteDesktop.Core
{
    /// <summary>Local record of threads hidden from the profile page.</summary>
    public static class ThreadVisibilityStore
    {
        public static bool IsHidden(long threadId) =>
            threadId != 0 && (Svc.Settings?.HiddenThreads ?? Array.Empty<string>())
                .Any(value => value == threadId.ToString());

        public static void Set(long threadId, bool hidden)
        {
            if (threadId == 0 || Svc.Settings == null) return;
            var values = (Svc.Settings.HiddenThreads ?? Array.Empty<string>()).ToList();
            var key = threadId.ToString();
            values.RemoveAll(value => value == key);
            if (hidden) values.Add(key);
            Svc.Settings.HiddenThreads = values.ToArray();
            Svc.Settings.Save();
        }
    }
}
