using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TieLiteDesktop.Core
{
    /// <summary>
    /// Loopback HTTP relay for MediaElement. Tieba video CDNs require a Referer/UA and
    /// Range requests; MediaElement cannot set headers, so it plays through this proxy.
    /// </summary>
    public sealed class MediaProxy : IDisposable
    {
        private static readonly Lazy<MediaProxy> Lazy = new(() => new MediaProxy());
        public static MediaProxy Instance => Lazy.Value;

        private readonly TcpListener _listener;
        private readonly HttpClient _http;
        private readonly string _token = Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource _cts = new();
        private readonly int _port;

        private MediaProxy()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                UseCookies = false,
            };
            if (Svc.Settings != null && Svc.Settings.ProxyMode == "none") handler.UseProxy = false;
            if (Svc.Settings != null && Svc.Settings.ProxyMode == "custom" &&
                Uri.TryCreate(Svc.Settings.ProxyUrl, UriKind.Absolute, out var proxy))
            {
                handler.Proxy = new WebProxy(proxy);
            }
            _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            _ = Task.Run(AcceptLoop);
        }

        public string BuildUrl(string target) =>
            "http://127.0.0.1:" + _port + "/media.mp4?u=" + Uri.EscapeDataString(target) + "&t=" + _token;

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    var stream = client.GetStream();
                    var request = await ReadRequestAsync(stream).ConfigureAwait(false);
                    if (request == null) return;

                    var (method, target, headers) = request.Value;
                    Log("request " + method + " " + target + (headers.TryGetValue("range", out var r0) ? " range=" + r0 : ""));
                    var query = ParseQuery(target);
                    if (!query.TryGetValue("t", out var token) || token != _token)
                    {
                        await WriteErrorAsync(stream, 403, "forbidden").ConfigureAwait(false);
                        return;
                    }
                    if (!query.TryGetValue("u", out var mediaUrl) || string.IsNullOrWhiteSpace(mediaUrl))
                    {
                        await WriteErrorAsync(stream, 400, "missing url").ConfigureAwait(false);
                        return;
                    }

                    using var upstreamRequest = new HttpRequestMessage(
                        method == "HEAD" ? HttpMethod.Head : HttpMethod.Get, mediaUrl);
                    upstreamRequest.Headers.TryAddWithoutValidation("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                    upstreamRequest.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                    if (headers.TryGetValue("range", out var range))
                    {
                        upstreamRequest.Headers.TryAddWithoutValidation("Range", range);
                    }

                    Log("upstream " + mediaUrl.Substring(0, Math.Min(80, mediaUrl.Length)));
                    using var upstream = await _http.SendAsync(upstreamRequest,
                        HttpCompletionOption.ResponseHeadersRead, _cts.Token).ConfigureAwait(false);
                    Log("status " + (int)upstream.StatusCode + " type=" + upstream.Content.Headers.ContentType + " len=" + upstream.Content.Headers.ContentLength);

                    var head = new StringBuilder();
                    head.Append("HTTP/1.1 ").Append((int)upstream.StatusCode).Append(' ')
                        .Append(upstream.ReasonPhrase ?? "OK").Append("\r\n");
                    AppendHeader(head, "Content-Type",
                        upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
                    if (upstream.Content.Headers.ContentLength.HasValue)
                    {
                        AppendHeader(head, "Content-Length", upstream.Content.Headers.ContentLength.Value.ToString());
                    }
                    var contentRange = upstream.Content.Headers.ContentRange?.ToString();
                    if (!string.IsNullOrEmpty(contentRange)) AppendHeader(head, "Content-Range", contentRange);
                    AppendHeader(head, "Accept-Ranges", upstream.Headers.AcceptRanges.Count > 0 ? "bytes" : "none");
                    AppendHeader(head, "Access-Control-Allow-Origin", "*");
                    AppendHeader(head, "Connection", "close");
                    head.Append("\r\n");

                    var headBytes = Encoding.ASCII.GetBytes(head.ToString());
                    await stream.WriteAsync(headBytes, 0, headBytes.Length, _cts.Token).ConfigureAwait(false);

                    if (method != "HEAD")
                    {
                        using var body = await upstream.Content.ReadAsStreamAsync(_cts.Token).ConfigureAwait(false);
                        await body.CopyToAsync(stream, 81920, _cts.Token).ConfigureAwait(false);
                    }
                    await stream.FlushAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log("error " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static void AppendHeader(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        private static async Task<(string method, string target, Dictionary<string, string> headers)?> ReadRequestAsync(
            NetworkStream stream)
        {
            var buffer = new byte[8192];
            var data = new MemoryStream();
            while (data.Length < 32768)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0) return null;
                data.Write(buffer, 0, read);
                var text = Encoding.ASCII.GetString(data.GetBuffer(), 0, (int)data.Length);
                if (text.Contains("\r\n\r\n")) return ParseRequest(text);
            }
            return null;
        }

        private static (string method, string target, Dictionary<string, string> headers) ParseRequest(string text)
        {
            var lines = text.Split("\r\n");
            var first = lines[0].Split(' ');
            var method = first.Length > 0 ? first[0].ToUpperInvariant() : "GET";
            var target = first.Length > 1 ? first[1] : "/";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) break;
                var idx = lines[i].IndexOf(':');
                if (idx <= 0) continue;
                headers[lines[i][..idx].Trim()] = lines[i][(idx + 1)..].Trim();
            }
            return (method, target, headers);
        }

        private static Dictionary<string, string> ParseQuery(string target)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var idx = target.IndexOf('?');
            if (idx < 0) return result;
            foreach (var part in target[(idx + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
            return result;
        }

        private static async Task WriteErrorAsync(NetworkStream stream, int code, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            var head = "HTTP/1.1 " + code + " Error\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length +
                       "\r\nConnection: close\r\n\r\n";
            var headBytes = Encoding.ASCII.GetBytes(head);
            await stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _http.Dispose();
        }
    }
}
