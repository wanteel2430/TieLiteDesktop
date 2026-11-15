using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TieLiteDesktop.Core
{
    /// <summary>
    /// Stable pseudo-device identity. The Android client derives CUID from ANDROID_ID;
    /// the desktop generates a random one once and keeps it in device.json.
    /// </summary>
    public sealed class DeviceProfile
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string AndroidId { get; set; }
        public string Cuid { get; set; }
        public string ClientId { get; set; }
        public string SampleId { get; set; }
        public string Uuid { get; set; }
        public string Imei { get; set; }
        public string Brand { get; set; } = "Xiaomi";
        public string Model { get; set; } = "M2012K11AC";

        private static string DevicePath => Path.Combine(AppSettings.ConfigDirectory, "device.json");

        public static DeviceProfile LoadOrCreate()
        {
            try
            {
                if (File.Exists(DevicePath))
                {
                    var loaded = JsonSerializer.Deserialize<DeviceProfile>(File.ReadAllText(DevicePath));
                    if (loaded != null && !string.IsNullOrEmpty(loaded.Cuid)) return loaded;
                }
            }
            catch
            {
            }

            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            var androidId = Convert.ToHexString(bytes).ToLowerInvariant();
            var uuid = Guid.NewGuid().ToString("N");
            var profile = new DeviceProfile
            {
                AndroidId = androidId,
                Cuid = Md5Hex("com.baidu" + androidId).ToUpperInvariant(),
                ClientId = "wappc_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "_" + Random.Shared.Next(0, 1000).ToString("D3"),
                SampleId = "sample_" + uuid[..8],
                Uuid = uuid,
                Imei = "0000000" + Random.Shared.Next(10000000, 99999999),
            };
            profile.Save();
            return profile;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(DevicePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch
            {
                // read-only environments: keep the in-memory profile
            }
        }

        public static string Md5Hex(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
