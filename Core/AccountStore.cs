using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    /// <summary>Saved Tieba accounts for quick switching.</summary>
    public static class AccountStore
    {
        private static readonly string FilePath =
            Path.Combine(AppSettings.ConfigDirectory, "accounts.json");

        public static List<Account> All()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<Account>();
                return JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(FilePath)) ?? new List<Account>();
            }
            catch
            {
                return new List<Account>();
            }
        }

        public static void Upsert(Account account)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.Bduss)) return;
            var list = All();
            list.RemoveAll(item => item.Uid == account.Uid || item.Bduss == account.Bduss);
            list.Insert(0, new Account
            {
                Bduss = account.Bduss,
                Stoken = account.Stoken,
                RawCookie = account.RawCookie,
                Uid = account.Uid,
                UserName = account.UserName,
                Portrait = account.Portrait,
                Zid = account.Zid,
            });
            Save(list.Take(10).ToList());
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch
            {
            }
        }

        public static void Remove(long uid)
        {
            var list = All();
            list.RemoveAll(item => item.Uid == uid);
            Save(list);
        }

        private static void Save(List<Account> list)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }
    }
}
