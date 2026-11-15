using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    /// <summary>Signed-in Tieba account (BDUSS/STOKEN based, same as the Android client).</summary>
    public sealed class Account
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string AccountPath => Path.Combine(AppSettings.ConfigDirectory, "account.json");

        public string Bduss { get; set; } = "";
        public string Stoken { get; set; } = "";
        public string RawCookie { get; set; } = "";
        public long Uid { get; set; }
        public string UserName { get; set; } = "";
        public string Portrait { get; set; } = "";
        public string Zid { get; set; } = "";

        public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Bduss);

        public static Account Load()
        {
            try
            {
                if (File.Exists(AccountPath))
                {
                    return JsonSerializer.Deserialize<Account>(File.ReadAllText(AccountPath)) ?? new Account();
                }
            }
            catch
            {
            }
            return new Account();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(AccountPath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch
            {
            }
        }

        public void Clear()
        {
            Bduss = "";
            Stoken = "";
            RawCookie = "";
            Uid = 0;
            UserName = "";
            Portrait = "";
            Zid = "";
            try
            {
                if (File.Exists(AccountPath)) File.Delete(AccountPath);
            }
            catch
            {
            }
        }

        /// <summary>Parses a "key=value; key=value" cookie header (also accepts line breaks or a bare BDUSS).</summary>
        public bool ApplyCookie(string cookie)
        {
            if (string.IsNullOrWhiteSpace(cookie)) return false;
            var raw = cookie.Replace("\r", ";").Replace("\n", ";").Trim();
            if (!raw.Contains('='))
            {
                // user pasted just the BDUSS value
                Bduss = raw;
                RawCookie = "BDUSS=" + raw;
                return IsLoggedIn;
            }

            var pairs = new List<string>();
            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                var value = kv[1].Trim().Trim('"');
                if (key.Length == 0 || value.Length == 0) continue;
                if (key.Equals("BDUSS", StringComparison.OrdinalIgnoreCase)) Bduss = value;
                else if (key.Equals("STOKEN", StringComparison.OrdinalIgnoreCase)) Stoken = value;
                else if (key.Equals("BAIDUZID", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("ZID", StringComparison.OrdinalIgnoreCase)) Zid = value;
                if (!pairs.Any(p => p.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)))
                {
                    pairs.Add(key + "=" + value);
                }
            }
            RawCookie = string.Join("; ", pairs);
            return IsLoggedIn;
        }

        private static readonly string[] EssentialCookies =
        {
            "BDUSS", "BDUSS_BFESS", "STOKEN", "PTOKEN", "BAIDUID", "BAIDUID_BFESS",
            "ZFY", "TIEBAUID", "TIEBA_USERTYPE", "AK",
        };

        /// <summary>Cookie header including all pasted cookies (some endpoints require BAIDUID etc.).</summary>
        public string BuildCookieHeader()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Bduss)) parts.Add("BDUSS=" + Bduss);
            if (!string.IsNullOrEmpty(Stoken)) parts.Add("STOKEN=" + Stoken);
            if (!string.IsNullOrEmpty(RawCookie))
            {
                foreach (var pair in RawCookie.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var key = kv[0].Trim();
                    if (key.Equals("BDUSS", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("STOKEN", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!EssentialCookies.Any(name => name.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                    parts.Add(key + "=" + kv[1].Trim().Trim('"'));
                }
            }
            return string.Join("; ", parts);
        }

        public override string ToString() => IsLoggedIn ? UserName : "未登录";
    }
}
