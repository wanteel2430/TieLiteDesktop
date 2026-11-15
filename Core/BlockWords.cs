using System;
using System.Collections.Generic;
using System.Linq;

namespace TieLiteDesktop.Core
{
    /// <summary>User-configured keyword blocking for titles and post content.</summary>
    public static class BlockWords
    {
        public static IReadOnlyList<string> All =>
            (Svc.Settings?.BlockedWords ?? Array.Empty<string>())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        public static bool Match(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var word in All)
            {
                if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool Match(IEnumerable<string> texts) =>
            texts != null && texts.Any(Match);
    }
}
