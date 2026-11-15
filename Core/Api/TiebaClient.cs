using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TieLiteDesktop.Core.Proto;

namespace TieLiteDesktop.Core.Api
{
    /// <summary>
    /// Transport for the official Tieba client endpoints. Mirrors the Android app's
    /// OkHttp interceptor chain: common params -> st params -> multipart body -> md5 sign.
    /// </summary>
    public sealed class TiebaClient : IDisposable
    {
        public const string ClientVersion = "11.10.8.6";
        private const string Salt = "tiebaclient!!!";
        private const string Boundary = "--------7da3d81520810*";
        private const string UserAgent = "bdtb for Android " + ClientVersion;

        public HttpClient Http { get; }
        public DeviceProfile Device { get; }
        public Account Account { get; }
        public ProtoSchema Schema { get; }
        public string BaseUrl { get; set; } = "https://tiebac.baidu.com";
        public string JsonBaseUrl { get; set; } = "https://c.tieba.baidu.com";

        /// <summary>Last request/response pair, kept for diagnostics and the --capture harness.</summary>
        public string LastRequestPath { get; private set; }
        public byte[] LastRequestBytes { get; private set; }
        public byte[] LastResponseBytes { get; private set; }

        public TiebaClient(DeviceProfile device, Account account, ProtoSchema schema, AppSettings settings)
        {
            Device = device;
            Account = account;
            Schema = schema;
            Http = BuildHttpClient(settings);
        }

        private static HttpClient BuildHttpClient(AppSettings settings)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                UseCookies = false,
                AllowAutoRedirect = false,
            };
            switch ((settings.ProxyMode ?? "system").ToLowerInvariant())
            {
                case "none":
                    handler.UseProxy = false;
                    break;
                case "custom":
                    if (Uri.TryCreate(settings.ProxyUrl, UriKind.Absolute, out var proxyUri))
                    {
                        handler.Proxy = new WebProxy(proxyUri);
                        handler.UseProxy = true;
                    }
                    else
                    {
                        handler.UseProxy = false;
                    }
                    break;
                default:
                    handler.UseProxy = true;
                    handler.Proxy = WebRequest.GetSystemWebProxy();
                    break;
            }
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(40),
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            return client;
        }

        public void ApplyNetworkSettings(AppSettings settings)
        {
            // network settings changes require a fresh window; handled by app restart prompt
        }

        /// <summary>Builds the __COMMON__ request block used by every protobuf request.</summary>
        public Dictionary<string, object> CommonParams()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var common = new Dictionary<string, object>
            {
                ["_client_type"] = 2,
                ["_client_version"] = VersionOverride ?? ClientVersion,
                ["_client_id"] = Device.ClientId,
                ["_phone_imei"] = Device.Imei,
                ["_timestamp"] = now,
                ["_os_version"] = "13",
                ["from"] = "1024324o",
                ["cuid"] = Device.Cuid,
                ["cuid_galaxy2"] = Device.Cuid,
                ["cuid_gid"] = "",
                ["pversion"] = "1.0.3",
                ["lego_lib_version"] = "3.0.0",
                ["model"] = Device.Model,
                ["brand"] = Device.Brand,
                ["net_type"] = 1,
                ["is_teenager"] = 0,
                ["oaid"] = "",
                ["sample_id"] = Device.SampleId,
            };
            if (!string.IsNullOrEmpty(Tbs)) common["tbs"] = Tbs;
            if (Account.IsLoggedIn)
            {
                common["BDUSS"] = Account.Bduss;
                if (!string.IsNullOrEmpty(Account.Stoken)) common["stoken"] = Account.Stoken;
                if (!string.IsNullOrEmpty(Account.Zid)) common["z_id"] = Account.Zid;
            }
            return common;
        }

        private string CookieHeader()
        {
            var sb = new StringBuilder();
            sb.Append("CUID=").Append(Device.Cuid).Append(";ka=open;TBBRAND=").Append(Device.Brand);
            if (Account.IsLoggedIn)
            {
                var extra = Account.BuildCookieHeader();
                if (!string.IsNullOrEmpty(extra)) sb.Append(';').Append(extra);
            }
            return sb.ToString();
        }

        public async Task<ProtoMessage> CallProtoAsync(
            string path,
            string requestType,
            string responseType,
            IDictionary<string, object> data,
            CancellationToken ct = default)
        {
            var body = ProtoCodec.Encode(Schema, requestType, new Dictionary<string, object> { ["data"] = data });
            var raw = await PostProtoRawAsync(path, body, ct).ConfigureAwait(false);
            var message = ProtoCodec.Decode(Schema, responseType, raw);
            ThrowIfError(message);
            return message;
        }

        private void ThrowIfError(ProtoMessage message)
        {
            var error = message.GetMessage("error");
            if (error == null) return;
            var code = error.GetInt("error_code");
            if (code == 0) return;
            var userMsg = error.GetString("user_msg");
            var errMsg = error.GetString("error_msg");
            var text = !string.IsNullOrWhiteSpace(userMsg) ? userMsg : errMsg;
            if (string.IsNullOrWhiteSpace(text)) text = "请求失败（错误码 " + code + "）";
            throw new TiebaException(code, text);
        }

        public async Task<byte[]> PostProtoRawAsync(string path, byte[] protoBody, CancellationToken ct = default)
        {
            var payload = BuildMultipart(protoBody);
            LastRequestPath = path;
            LastRequestBytes = payload;

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
            var content = new ByteArrayContent(payload);
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + Boundary);
            request.Content = content;
            ApplyHeaders(request);

            using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            LastResponseBytes = bytes;
            if (!response.IsSuccessStatusCode)
            {
                throw new TiebaException((int)response.StatusCode, "服务器返回 HTTP " + (int)response.StatusCode);
            }
            return bytes;
        }

        /// <summary>Form POST against a full web URL (uses the logged-in cookie).</summary>
        public async Task<string> PostWebFormAsync(string url, IDictionary<string, string> form,
            CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form),
            };
            ApplyHeaders(request);
            using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
            var text = await ReadTextAsync(response.Content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new TiebaException((int)response.StatusCode, "服务器返回 HTTP " + (int)response.StatusCode);
            }
            return text;
        }

        /// <summary>Reads a response as UTF-8 regardless of the (sometimes invalid) charset header.</summary>
        private static async Task<string> ReadTextAsync(HttpContent content, CancellationToken ct)
        {
            var bytes = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption option, CancellationToken ct)
        {
            try
            {
                // follow redirects manually so POST bodies survive http→https / host changes
                var body = request.Content == null
                    ? null
                    : await request.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                var contentHeaders = request.Content?.Headers
                    .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
                var uri = request.RequestUri;
                for (var hop = 0; hop < 4; hop++)
                {
                    using var attempt = new HttpRequestMessage(request.Method, uri);
                    foreach (var header in request.Headers)
                    {
                        attempt.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    if (body != null)
                    {
                        attempt.Content = new ByteArrayContent(body);
                        if (contentHeaders != null)
                        {
                            foreach (var header in contentHeaders)
                            {
                                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                                attempt.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }
                        }
                    }

                    var response = await Http.SendAsync(attempt, option, ct).ConfigureAwait(false);
                    if (hop < 3 && response.StatusCode is System.Net.HttpStatusCode.MovedPermanently
                        or System.Net.HttpStatusCode.Found
                        or System.Net.HttpStatusCode.SeeOther
                        or System.Net.HttpStatusCode.TemporaryRedirect
                        or System.Net.HttpStatusCode.PermanentRedirect)
                    {
                        var location = response.Headers.Location;
                        if (location != null)
                        {
                            uri = new Uri(uri, location);
                            response.Dispose();
                            continue;
                        }
                    }
                    return response;
                }
                throw new TiebaException(-1, "请求重定向次数过多");
            }
            catch (Exception ex)
            {
                throw Friendly(ex);
            }
        }

        private static TiebaException Friendly(Exception ex)
        {
            if (ex is TiebaException tieba) return tieba;
            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                return new TiebaException(-1, "请求超时，请检查网络后重试");
            }
            return new TiebaException(-1, "网络连接失败，请检查网络或在设置中配置代理");
        }

        /// <summary>tbs from /dc/common/tbs; included in multipart common params for write APIs.</summary>
        public string Tbs { get; set; } = "";

        /// <summary>Optional per-request client version override (some write/generalTab APIs require 12.x).</summary>
        public string VersionOverride { get; set; }

        public void ApplyHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("x_bd_data_type", "protobuf");
            request.Headers.TryAddWithoutValidation("client_type", "2");
            request.Headers.TryAddWithoutValidation("charset", "UTF-8");
            request.Headers.TryAddWithoutValidation("Cookie", CookieHeader());
            if (Account.IsLoggedIn) request.Headers.TryAddWithoutValidation("client_user_token", Account.Uid.ToString());
        }

        /// <summary>Builds the exact multipart payload (used by the offline request-validation harness).</summary>
        public byte[] BuildMultipart(byte[] protoBody)
        {
            var common = CommonParams();

            // text form parts (signed)
            var parts = new List<KeyValuePair<string, string>>();
            foreach (var pair in common)
            {
                if (pair.Value == null) continue;
                parts.Add(new KeyValuePair<string, string>(pair.Key, Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture)));
            }
            parts.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            var signInput = new StringBuilder();
            foreach (var part in parts) signInput.Append(part.Key).Append('=').Append(part.Value);
            var sign = DeviceProfile.Md5Hex(signInput + Salt);

            var ms = new MemoryStream();
            void WriteString(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                ms.Write(bytes, 0, bytes.Length);
            }

            foreach (var part in parts)
            {
                WriteString("--" + Boundary + "\r\nContent-Disposition: form-data; name=\"" + part.Key + "\"\r\n\r\n");
                WriteString(part.Value);
                WriteString("\r\n");
            }
            WriteString("--" + Boundary + "\r\nContent-Disposition: form-data; name=\"sign\"\r\n\r\n" + sign + "\r\n");
            WriteString("--" + Boundary + "\r\nContent-Disposition: form-data; name=\"data\"; filename=\"file\"\r\nContent-Type: application/octet-stream\r\n\r\n");
            ms.Write(protoBody, 0, protoBody.Length);
            WriteString("\r\n--" + Boundary + "--\r\n");
            return ms.ToArray();
        }

        /// <summary>Signed form POST against the JSON ("c.tieba.baidu.com") app API.</summary>
        public async Task<string> PostFormAsync(string path, IDictionary<string, string> form, CancellationToken ct = default)
        {
            var all = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in CommonParams())
            {
                if (pair.Value != null) all[pair.Key] = Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (form != null)
            {
                foreach (var pair in form) all[pair.Key] = pair.Value ?? "";
            }

            var sorted = new List<KeyValuePair<string, string>>(all);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder();
            foreach (var pair in sorted) sb.Append(pair.Key).Append('=').Append(pair.Value);
            all["sign"] = DeviceProfile.Md5Hex(sb + Salt);

            using var request = new HttpRequestMessage(HttpMethod.Post, JsonBaseUrl + path)
            {
                Content = new FormUrlEncodedContent(all),
            };
            request.Headers.TryAddWithoutValidation("User-Agent", "bdtb for Android " + ClientVersion);
            request.Headers.TryAddWithoutValidation("Cookie", CookieHeader());
            if (Account.IsLoggedIn) request.Headers.TryAddWithoutValidation("client_user_token", Account.Uid.ToString());
            using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            var text = await ReadTextAsync(response.Content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new TiebaException((int)response.StatusCode, "服务器返回 HTTP " + (int)response.StatusCode);
            }
            return text;
        }

        public async Task<string> GetStringAsync(string url, string referer = null, CancellationToken ct = default,
            string userAgent = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrEmpty(userAgent)
                ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36"
                : userAgent);
            if (!string.IsNullOrEmpty(referer)) request.Headers.TryAddWithoutValidation("Referer", referer);
            request.Headers.TryAddWithoutValidation("Cookie", CookieHeader());
            using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await ReadTextAsync(response.Content, ct).ConfigureAwait(false);
        }

        public void Dispose() => Http.Dispose();
    }

    public sealed class TiebaException : Exception
    {
        public int Code { get; }

        public TiebaException(int code, string message) : base(message) => Code = code;
    }
}
