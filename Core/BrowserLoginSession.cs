using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TieLiteDesktop.Core
{
    /// <summary>
    /// Opens Baidu's official login page in a dedicated Edge profile (phone/SMS, QR or password),
    /// then reads the resulting cookies over the DevTools protocol. This avoids reverse-engineering
    /// the passport SMS API and works with whatever verification Baidu requires.
    /// </summary>
    public sealed class BrowserLoginSession : IDisposable
    {
        private readonly int _port;
        private readonly string _profileDir;
        private Process _edge;
        private static readonly HttpClient LocalHttp = new(new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(5),
        };

        public BrowserLoginSession()
        {
            _port = FindFreePort();
            _profileDir = Path.Combine(AppSettings.ConfigDirectory, "edge-login-profile");
        }

        private static int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Start()
        {
            var edge = EdgePlayer.FindEdgePath();
            if (edge == null) throw new InvalidOperationException("没有找到 Microsoft Edge，无法使用浏览器登录");
            Directory.CreateDirectory(_profileDir);
            var info = new ProcessStartInfo(edge) { UseShellExecute = false };
            info.ArgumentList.Add("--remote-debugging-port=" + _port);
            info.ArgumentList.Add("--user-data-dir=" + _profileDir);
            if (Environment.GetEnvironmentVariable("FLUXTIE_EDGE_NO_SANDBOX") == "1")
            {
                info.ArgumentList.Add("--no-sandbox");
            }
            info.ArgumentList.Add("--no-first-run");
            info.ArgumentList.Add("--no-default-browser-check");
            info.ArgumentList.Add("--new-window");
            info.ArgumentList.Add("https://passport.baidu.com/v2/?login&tpl=mn&u=https%3A%2F%2Ftieba.baidu.com%2F");
            _edge = Process.Start(info);
        }

        /// <summary>Polls the browser until BDUSS appears; returns null when cancelled/timed out.</summary>
        public async Task<(string bduss, string stoken, string cookieHeader)?> WaitForLoginAsync(CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddMinutes(6);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var cookies = await ReadCookiesAsync(ct).ConfigureAwait(false);
                    if (cookies.TryGetValue("BDUSS", out var bduss) && !string.IsNullOrWhiteSpace(bduss))
                    {
                        cookies.TryGetValue("STOKEN", out var stoken);
                        return (bduss, stoken ?? "", BuildCookieHeader(cookies));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // browser still starting / page not ready
                }
                await Task.Delay(1500, ct).ConfigureAwait(false);
            }
            return null;
        }

        private async Task<Dictionary<string, string>> ReadCookiesAsync(CancellationToken ct)
        {
            var targets = await LocalHttp.GetStringAsync("http://127.0.0.1:" + _port + "/json/list", ct)
                .ConfigureAwait(false);
            string wsUrl = null;
            using (var doc = JsonDocument.Parse(targets))
            {
                foreach (var target in doc.RootElement.EnumerateArray())
                {
                    if (!target.TryGetProperty("type", out var type) || type.GetString() != "page") continue;
                    if (!target.TryGetProperty("webSocketDebuggerUrl", out var ws)) continue;
                    wsUrl = ws.GetString();
                    if (target.TryGetProperty("url", out var url) &&
                        (url.GetString() ?? "").Contains("baidu", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(wsUrl)) return new Dictionary<string, string>();

            using var socket = new ClientWebSocket();
            socket.Options.Proxy = null;
            await socket.ConnectAsync(new Uri(wsUrl), ct).ConfigureAwait(false);
            var payload = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Storage.getCookies\",\"params\":{}}");
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);

            var buffer = new byte[64 * 1024];
            var builder = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                builder.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    builder.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (builder.Length == 0) continue;
                var text = Encoding.UTF8.GetString(builder.GetBuffer(), 0, (int)builder.Length);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
                var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (!doc.RootElement.TryGetProperty("result", out var res) ||
                    !res.TryGetProperty("cookies", out var list) || list.ValueKind != JsonValueKind.Array)
                {
                    return cookies;
                }
                foreach (var cookie in list.EnumerateArray())
                {
                    var name = cookie.GetProperty("name").GetString() ?? "";
                    var domain = cookie.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
                    if (!domain.Contains("baidu.com", StringComparison.OrdinalIgnoreCase)) continue;
                    var rank = domain.Contains("tieba.baidu.com", StringComparison.OrdinalIgnoreCase) ? 3
                        : domain.Equals(".baidu.com", StringComparison.OrdinalIgnoreCase) ? 2
                        : 1;
                    if (cookies.ContainsKey(name) && ranks.TryGetValue(name, out var existingRank) && existingRank > rank)
                    {
                        continue;
                    }
                    cookies[name] = cookie.GetProperty("value").GetString() ?? "";
                    ranks[name] = rank;
                }
                return cookies;
            }
            return new Dictionary<string, string>();
        }

        public async Task<Dictionary<string, string>> ReadAllCookiesAsync(CancellationToken ct) =>
            await ReadCookiesAsync(ct).ConfigureAwait(false);

        public async Task<(string bduss, string stoken, string cookieHeader)?> TryReadOnceAsync(CancellationToken ct)
        {
            try
            {
                var cookies = await ReadCookiesAsync(ct).ConfigureAwait(false);
                if (cookies.TryGetValue("BDUSS", out var bduss) && !string.IsNullOrWhiteSpace(bduss))
                {
                    cookies.TryGetValue("STOKEN", out var stoken);
                    return (bduss, stoken ?? "", BuildCookieHeader(cookies));
                }
            }
            catch
            {
            }
            return null;
        }

        private static string BuildCookieHeader(Dictionary<string, string> cookies)
        {
            var parts = new List<string>();
            foreach (var pair in cookies)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value)) continue;
                parts.Add(pair.Key + "=" + pair.Value.Trim().Trim('"'));
            }
            return string.Join("; ", parts);
        }

        public void Dispose()
        {
            try
            {
                if (_edge != null && !_edge.HasExited)
                {
                    _edge.Kill(true);
                }
            }
            catch
            {
            }
            _edge = null;
        }
    }
}
