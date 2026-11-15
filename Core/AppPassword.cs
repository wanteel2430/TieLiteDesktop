using System;
using System.Security.Cryptography;
using System.Text;

namespace TieLiteDesktop.Core
{
    /// <summary>Local application lock password (PIN or custom), stored as a salted SHA-256 hash.</summary>
    public static class AppPassword
    {
        public static bool HasPassword =>
            Svc.Settings != null && !string.IsNullOrWhiteSpace(Svc.Settings.PasswordHash);

        public static void Set(string password, string type)
        {
            if (string.IsNullOrWhiteSpace(password)) return;
            var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
            Svc.Settings.PasswordSalt = salt;
            Svc.Settings.PasswordHash = Hash(salt + password);
            Svc.Settings.PasswordType = type;
            Svc.Settings.Save();
        }

        public static bool Verify(string password)
        {
            if (!HasPassword) return true;
            var salt = Svc.Settings.PasswordSalt ?? "";
            return Hash(salt + (password ?? "")) == Svc.Settings.PasswordHash;
        }

        public static void Clear()
        {
            Svc.Settings.PasswordHash = "";
            Svc.Settings.PasswordSalt = "";
            Svc.Settings.PasswordType = "";
            Svc.Settings.Save();
        }

        private static string Hash(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""));
            return Convert.ToHexString(bytes);
        }
    }
}
