using System;
using System.Text.RegularExpressions;

namespace TieLiteDesktop.Core
{
    public static class TiebaLinkParser
    {
        private static readonly Regex ThreadPattern = new(
            @"(?:tieba\.baidu\.com/p/|/p/|^p/)(\d{5,})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex KzPattern = new(
            @"[?&]kz=(\d{5,})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryGetThreadId(string text, out long threadId)
        {
            threadId = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var match = ThreadPattern.Match(text);
            if (!match.Success) match = KzPattern.Match(text);
            return match.Success && long.TryParse(match.Groups[1].Value, out threadId) && threadId > 0;
        }
    }
}
