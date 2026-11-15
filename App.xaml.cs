using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;
using TieLiteDesktop.Core.Proto;
using TieLiteDesktop.Views;

namespace TieLiteDesktop
{
    public partial class App : Application
    {
        private Window _loginWindow;

        protected override void OnExit(ExitEventArgs e)
        {
            if (Svc.Settings?.ClearCacheOnExit == true)
            {
                ImageLoader.ClearCache();
            }
            base.OnExit(e);
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (_, args) =>
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"),
                        DateTime.Now + " " + args.Exception + Environment.NewLine);
                }
                catch
                {
                }
                var headless = Environment.GetCommandLineArgs().Any(a => a == "--shot");
                if (!headless) MessageBox.Show(args.Exception.Message, "FluxTie 出错", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                if (headless) Shutdown(2);
            };

            Svc.DemoMode = HasArg(e.Args, "--demo");
            Anim.EnableSmoothScrolling();
            try
            {
                Svc.Initialize();
                Svc.StartThemeWatcher();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt"), ex.ToString());
                }
                catch
                {
                }
                MessageBox.Show("初始化失败：" + ex.Message, "TieLiteDesktop", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var probeUid = GetArg(e.Args, "--probe-user");
            if (!string.IsNullOrEmpty(probeUid) && long.TryParse(probeUid, out var probeUserId))
            {
                try
                {
                    var info = Svc.Api.GetUserInfoAsync(probeUserId).GetAwaiter().GetResult();
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-user.txt"),
                        info == null
                            ? "null " + (Core.Api.TiebaApi.LastUserInfoError ?? "")
                            : "id=" + info.Id + " name=" + info.Name + " show=" + info.ShowName +
                              " fans=" + info.FansNum + " follow=" + info.FollowNum + " post=" + info.PostNum +
                              " thread=" + info.ThreadNum + " agree=" + info.AgreeNum + " age=" + info.TbAge +
                              " portrait=" + info.Portrait);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-user.txt"), ex.ToString());
                }
                Shutdown(0);
                return;
            }

            var dumpProfile = GetArg(e.Args, "--dump-profile");
            if (!string.IsNullOrEmpty(dumpProfile) && long.TryParse(dumpProfile, out var dumpProfileUid))
            {
                var output = "JSON:\n" + Svc.Api.GetProfileRawAsync(dumpProfileUid).GetAwaiter().GetResult() +
                             "\n\nPROTO:\n" + Svc.Api.GetProfileProtoDumpAsync(dumpProfileUid).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "FluxTie-profile-dump.txt"), output);
                Shutdown(0);
                return;
            }

            var probeProfile = GetArg(e.Args, "--probe-profile");
            if (!string.IsNullOrEmpty(probeProfile) && long.TryParse(probeProfile, out var probeProfileUid))
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-profile.txt"),
                    Svc.Api.GetProfileRawAsync(probeProfileUid).GetAwaiter().GetResult());
                Shutdown(0);
                return;
            }

            var probePosts = GetArg(e.Args, "--probe-userposts");
            if (!string.IsNullOrEmpty(probePosts) && long.TryParse(probePosts, out var probePostsUid))
            {
                var builder = new System.Text.StringBuilder();
                foreach (var variant in new[] { "0", "1", "2" })
                {
                    builder.AppendLine("===== is_view_card=" + variant + " =====");
                    builder.AppendLine(Svc.Api.GetUserPostsRawAsync(probePostsUid, 1, true, variant).GetAwaiter().GetResult());
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-userposts.txt"),
                    builder.ToString());
                Shutdown(0);
                return;
            }

            if (HasArg(e.Args, "--selftest"))
            {
                RunSelfTest();
                Shutdown(0);
                return;
            }

            var dumpDir = GetArg(e.Args, "--dump-request");
            if (!string.IsNullOrEmpty(dumpDir))
            {
                RunDumpRequests(dumpDir);
                Shutdown(0);
                return;
            }

            var liveDir = GetArg(e.Args, "--verify-live");
            if (!string.IsNullOrEmpty(liveDir))
            {
                RunVerifyLive(liveDir);
                Shutdown(0);
                return;
            }

            if (HasArg(e.Args, "--proxy-selftest"))
            {
                RunProxySelfTest();
                Shutdown(0);
                return;
            }

            if (HasArg(e.Args, "--browser-test"))
            {
                await RunBrowserTestAsync();
                Shutdown(0);
                return;
            }

            if (HasArg(e.Args, "--decode-pb"))
            {
                RunDecodePb();
                Shutdown(0);
                return;
            }

            var videoTest = GetArg(e.Args, "--video-test");
            if (!string.IsNullOrEmpty(videoTest))
            {
                await RunVideoTestAsync(videoTest);
                Shutdown(0);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            if (Application.Current.TryFindResource("AppBackground") is System.Windows.Media.Brush startBackground)
            {
                window.Background = startBackground;
            }
            window.Show();

            if (Svc.Settings.OpenTiebaLinks)
            {
                var link = e.Args.FirstOrDefault(arg => arg.Contains("tieba.baidu.com/p/", StringComparison.OrdinalIgnoreCase));
                var match = System.Text.RegularExpressions.Regex.Match(link ?? "", @"tieba\.baidu\.com/p/(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var linkedThread))
                {
                    await Task.Delay(700);
                    Svc.Window?.OpenThread(linkedThread, "", "");
                }
            }

            if (HasArg(e.Args, "--diag"))
            {
                await Task.Delay(1500);
                WriteDiagnostics(window);
                Shutdown(0);
                return;
            }

            var page = GetArg(e.Args, "--page");
            if (!string.IsNullOrEmpty(page))
            {
                await Task.Delay(500);
                NavigateForShot(page);
            }

            var shot = GetArg(e.Args, "--shot");
            if (!string.IsNullOrEmpty(shot))
            {
                await Task.Delay(int.TryParse(GetArg(e.Args, "--delay"), out var delay) ? delay : 3000);
                Capture(_loginWindow ?? window, shot);
                Shutdown(0);
            }
        }

        private static T FindDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
        {
            if (root == null) return null;
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T typed && predicate(typed)) return typed;
                var found = FindDescendant(child, predicate);
                if (found != null) return found;
            }
            return null;
        }

        private void NavigateForShot(string page)
        {
            switch (page)
            {
                case "forum":
                    Svc.Window?.OpenForum(Svc.DemoMode ? "孙笑川" : (string.IsNullOrEmpty(Svc.Settings.LastForum) ? "孙笑川" : Svc.Settings.LastForum));
                    break;
                case "thread":
                    if (Svc.DemoMode)
                    {
                        var demo = DemoData.Thread();
                        Svc.Window?.OpenThread(demo.Thread.Id, demo.Thread.Title, demo.Thread.ForumName);
                    }
                    else
                    {
                        Svc.Window?.OpenThread(11193062707, "帖子", "孙笑川");
                    }
                    break;
                case "search":
                {
                    Svc.Window?.ClearNavSelection();
                    var view = new SearchView();
                    Svc.Nav.Push(() => view, "搜索");
                    var query = GetArg(Environment.GetCommandLineArgs(), "--query");
                    if (!string.IsNullOrEmpty(query)) view.SetQuery(query);
                    break;
                }
                case "settings":
                    Svc.Window?.SelectNav("settings");
                    Svc.Nav.Push(() => new SettingsView(), "设置");
                    break;
                case "messages":
                    Svc.Window?.SelectNav("messages");
                    Svc.Nav.Push(() => new MessagesView(), "消息");
                    break;
                case "favorites":
                    Svc.Window?.SelectNav("favorites");
                    Svc.Nav.Push(() => new FavoritesView(), "收藏");
                    break;
                case "cached":
                    Svc.Window?.SelectNav("cachedposts");
                    Svc.Nav.Push(() => new CachedPostsView(), "缓存的帖子");
                    break;
                case "cachedwindow":
                {
                    var cachedItem = CachedThreadStore.All().FirstOrDefault();
                    if (cachedItem != null)
                    {
                        _loginWindow = new CachedThreadWindow(cachedItem);
                        _loginWindow.Show();
                    }
                    break;
                }
                case "login":
                    _loginWindow = new LoginWindow { Owner = Svc.Window };
                    _loginWindow.Show();
                    break;
                case "loginphone":
                {
                    var phoneLogin = new LoginWindow { Owner = Svc.Window };
                    _loginWindow = phoneLogin;
                    phoneLogin.Show();
                    phoneLogin.ShowPhoneTab();
                    break;
                }
                case "cookieguide":
                    _loginWindow = new CookieGuideWindow { Owner = Svc.Window };
                    _loginWindow.Show();
                    break;
                case "compose":
                    _loginWindow = new ComposeWindow(DemoData.Forum().Forum) { Owner = Svc.Window };
                    _loginWindow.Show();
                    break;
                case "imageclick":
                {
                    var demo = DemoData.Thread();
                    Svc.Window?.OpenThread(demo.Thread.Id, demo.Thread.Title, demo.Thread.ForumName);
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        var host = FindDescendant<System.Windows.Controls.Border>(Svc.Window,
                            b => (b.ToolTip as string) == "点击查看大图");
                        var log = "found=" + (host != null) + " windowsBefore=" + Application.Current.Windows.Count;
                        if (host != null)
                        {
                            var args = new System.Windows.Input.MouseButtonEventArgs(
                                System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                            {
                                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                            };
                            host.RaiseEvent(args);
                        }
                        log += " windowsAfter=" + Application.Current.Windows.Count;
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image-click.txt"), log);
                    };
                    timer.Start();
                    break;
                }
                case "imageoverlay":
                    Svc.Window?.ShowImageOverlay(new System.Collections.Generic.List<string>
                    {
                        System.IO.Path.Combine(Svc.AssetsDir, "logo.png"),
                    }, 0);
                    break;
                case "profile":
                    if (Svc.DemoMode)
                    {
                        var demo = DemoData.Thread();
                        var author = demo.Thread.Author ?? demo.Posts[0].Author;
                        Svc.Window?.OpenProfile(author.Id, author.DisplayName);
                    }
                    break;
                case "poll":
                    if (Svc.DemoMode)
                    {
                        var (pollPage, pollInfo) = DemoData.PollDemo();
                        Svc.Window?.OpenThread(pollPage.Thread.Id, pollPage.Thread.Title, pollPage.Thread.ForumName, pollInfo);
                    }
                    break;
                case "topic":
                    if (Svc.DemoMode)
                    {
                        Svc.Window?.OpenTopic(DemoData.Topics()[0]);
                    }
                    break;
            }
        }

        private static void Capture(Window window, string path)
        {
            try
            {
                if (window is LoginWindow)
                {
                    window.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF2, 0xF5));
                }
                window.UpdateLayout();
                var width = (int)Math.Max(1, window.ActualWidth);
                var height = (int)Math.Max(1, window.ActualHeight);
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capture.txt"),
                        "actual=" + window.ActualWidth + "x" + window.ActualHeight +
                        " width=" + window.Width + " height=" + window.Height +
                        " dpi=" + VisualTreeHelper.GetDpi(window).DpiScaleX +
                        " state=" + window.WindowState + " left=" + window.Left + " top=" + window.Top);
                }
                catch
                {
                }
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var full = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
                using var stream = File.Create(full);
                encoder.Save(stream);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shot-error.txt"), ex.ToString());
            }
        }

        private static void RunDumpRequests(string directory)
        {
            Directory.CreateDirectory(directory);
            var jobs = new (string file, string path, string requestType, Dictionary<string, object> data)[]
            {
                ("hot", "/c/f/forum/hotThreadList?cmd=309661", "tieba.hotThreadList.HotThreadListRequest",
                    new Dictionary<string, object> { ["tabId"] = "", ["tabCode"] = "" }),
                ("frs", "/c/f/frs/page?cmd=301001", "tieba.frsPage.FrsPageRequest",
                    new Dictionary<string, object>
                    {
                        ["kw"] = "孙笑川", ["rn"] = 30, ["rn_need"] = 30, ["pn"] = 1, ["q_type"] = 0,
                        ["scr_w"] = 1280, ["scr_h"] = 860, ["scr_dip"] = 1.0, ["load_type"] = 1,
                        ["st_type"] = "frs_page", ["sort_type"] = 0, ["is_good"] = 0, ["class_id"] = 0,
                    }),
                ("pb", "/c/f/pb/page?cmd=302001&format=protobuf", "tieba.pbPage.PbPageRequest",
                    new Dictionary<string, object>
                    {
                        ["kz"] = 11193062707L, ["pn"] = 1, ["rn"] = 30, ["with_floor"] = 1, ["floor_rn"] = 3,
                        ["lz"] = 0, ["r"] = 0, ["back"] = 0, ["scr_w"] = 1280, ["scr_h"] = 860, ["scr_dip"] = 1.0,
                        ["q_type"] = 0, ["st_type"] = "tb_frslist",
                    }),
            };
            var lines = new System.Text.StringBuilder();
            foreach (var job in jobs)
            {
                var body = new Dictionary<string, object> { ["data"] = job.data };
                job.data["common"] = Svc.Client.CommonParams();
                var proto = Core.Proto.ProtoCodec.Encode(Svc.Schema, job.requestType, body);
                var payload = Svc.Client.BuildMultipart(proto);
                File.WriteAllBytes(Path.Combine(directory, job.file + ".req"), payload);
                lines.AppendLine(job.file + "\t" + job.path);
            }
            lines.AppendLine("UA\tbdtb for Android " + Core.Api.TiebaClient.ClientVersion);
            File.WriteAllText(Path.Combine(directory, "meta.txt"), lines.ToString());
        }

        private static void WriteDiagnostics(Window window)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                sb.AppendLine("screen=" + SystemParameters.PrimaryScreenWidth + "x" + SystemParameters.PrimaryScreenHeight);
                sb.AppendLine("work=" + SystemParameters.WorkArea);
                sb.AppendLine("dpiScale=" + dpi.DpiScaleX + " ppi=" + dpi.PixelsPerInchX);
                sb.AppendLine("themeMode=" + Svc.Settings.ThemeMode + " effective=" + Svc.EffectiveTheme +
                              " systemDark=" + SystemTheme.IsDark());
                sb.AppendLine("window Left=" + window.Left + " Top=" + window.Top + " Width=" + window.Width +
                              " Height=" + window.Height + " Actual=" + window.ActualWidth + "x" + window.ActualHeight +
                              " state=" + window.WindowState + " resize=" + window.ResizeMode);
                sb.AppendLine("--- buttons");
                Walk(window, sb, 0);
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERR " + ex);
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diag.txt"), sb.ToString());
        }

        private static void Walk(DependencyObject node, System.Text.StringBuilder sb, int depth)
        {
            if (node == null || depth > 40) return;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is System.Windows.Controls.Button button)
                {
                    string point = "";
                    try
                    {
                        var p = button.TransformToAncestor(Svc.Window).Transform(new Point(0, 0));
                        point = " pos=" + (int)p.X + "," + (int)p.Y;
                    }
                    catch
                    {
                    }
                    sb.AppendLine("button '" + button.Content + "' vis=" + button.IsVisible + " size=" +
                                  button.ActualWidth + "x" + button.ActualHeight + point);
                }
                Walk(child, sb, depth + 1);
            }
        }

        private static void RunVerifyLive(string directory)
        {
            var lines = new System.Text.StringBuilder();
            try
            {
                ProtoMessage Load(string name, string type) =>
                    Core.Proto.ProtoCodec.Decode(Svc.Schema, type,
                        File.ReadAllBytes(Path.Combine(directory, name)));

                var hot = Core.Api.TiebaApi.ParseHot(Load("live-hot.bin", "tieba.hotThreadList.HotThreadListResponse"));
                lines.AppendLine("hot: threads=" + hot.Threads.Count + " tabs=" + hot.Tabs.Count);
                foreach (var t in hot.Threads.Take(3)) lines.AppendLine("  " + t.Id + " " + t.Title + " / " + t.ForumName);

                var forum = Core.Api.TiebaApi.ParseForum(Load("live-frs.bin", "tieba.frsPage.FrsPageResponse"));
                lines.AppendLine("forum: " + forum.Forum.Name + " members=" + forum.Forum.MemberNum +
                                 " threads=" + forum.Threads.Count + " tabs=" + forum.Tabs.Count);
                foreach (var t in forum.Threads.Take(3))
                {
                    lines.AppendLine("  " + t.Id + " " + t.Title + " replies=" + t.ReplyNum +
                                     " author=" + t.Author.DisplayName + " img=" + t.PreviewImage.Length);
                }

                var thread = Core.Api.TiebaApi.ParseThread(Load("live-pb.bin", "tieba.pbPage.PbPageResponse"));
                lines.AppendLine("thread: " + thread.Thread.Title + " posts=" + thread.Posts.Count +
                                 " totalPage=" + thread.Page.TotalPage);
                foreach (var p in thread.Posts.Take(3))
                {
                    lines.AppendLine("  #" + p.Floor + " " + p.Author.DisplayName + " imgs=" +
                                     p.Content.Count(c => c.Kind == ContentKind.Image) +
                                     " subs=" + p.SubPosts.Count + " text=" + (p.PlainText.Length > 40 ? p.PlainText[..40] : p.PlainText));
                }
            }
            catch (Exception ex)
            {
                lines.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verify-live.txt"), lines.ToString());
        }

        private async Task RunBrowserTestAsync()
        {
            var log = new System.Text.StringBuilder();
            BrowserLoginSession session = null;
            try
            {
                session = new BrowserLoginSession();
                session.Start();
                log.AppendLine("edge started");
                await Task.Delay(6000);
                var cookies = await session.ReadAllCookiesAsync(CancellationToken.None);
                log.AppendLine("cookies: " + cookies.Count + " baidu names: " +
                               string.Join(",", cookies.Keys.Take(12)));
            }
            catch (Exception ex)
            {
                log.AppendLine("ERROR: " + ex);
            }
            finally
            {
                session?.Dispose();
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "browser-test.txt"), log.ToString());
        }

        private static void RunDecodePb()
        {
            var log = new System.Text.StringBuilder();
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "pb.bin");
                var bytes = File.ReadAllBytes(path);
                var message = ProtoCodec.Decode(Svc.Schema, "tieba.pbPage.PbPageResponse", bytes);
                var data = message.GetMessage("data");
                log.AppendLine("thread=" + data.GetMessage("thread")?.GetLong("id") + " forum=" + data.GetMessage("forum")?.GetLong("id"));
                foreach (var post in data.GetMessages("post_list"))
                {
                    log.AppendLine("id=" + post.GetLong("id") + " floor=" + post.GetInt("floor") +
                                   " sub=" + post.GetInt("sub_post_number"));
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "decode-pb.txt"), log.ToString());
        }

        private static void RunProxySelfTest()
        {
            var log = new System.Text.StringBuilder();
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                var server = System.Threading.Tasks.Task.Run(async () =>
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    var buffer = new byte[4096];
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var request = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                    log.AppendLine("upstream saw Range: " + request.Contains("Range:", StringComparison.OrdinalIgnoreCase));
                    var body = System.Text.Encoding.UTF8.GetBytes("flux-media-ok");
                    var head = "HTTP/1.1 206 Partial Content\r\nContent-Type: video/mp4\r\nContent-Length: " +
                               body.Length + "\r\nContent-Range: bytes 0-12/13\r\n\r\n";
                    var headBytes = System.Text.Encoding.ASCII.GetBytes(head);
                    await stream.WriteAsync(headBytes);
                    await stream.WriteAsync(body);
                    listener.Stop();
                });

                var target = "http://127.0.0.1:" + port + "/video.mp4";
                using var http = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, MediaProxy.Instance.BuildUrl(target));
                request.Headers.TryAddWithoutValidation("Range", "bytes=0-");
                var response = http.Send(request);
                var text = response.Content.ReadAsStringAsync().Result;
                log.AppendLine("proxy status: " + (int)response.StatusCode);
                log.AppendLine("proxy body: " + text);
                server.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                log.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy-selftest.txt"), log.ToString());
        }

        private async Task RunVideoTestAsync(string url)
        {
            var log = new System.Text.StringBuilder();
            var window = new Window
            {
                Width = 640,
                Height = 360,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Opacity = 0.02,
                Left = -2000,
                Top = -2000,
            };
            var player = new MediaElement { LoadedBehavior = MediaState.Manual, ScrubbingEnabled = true };
            window.Content = player;
            window.Show();
            player.MediaOpened += (_, _) => log.AppendLine("opened duration=" + player.NaturalDuration);
            player.MediaFailed += (_, args) => log.AppendLine("failed: " + args.ErrorException?.Message);
            try
            {
                var local = VideoCache.DownloadAsync(url, new Progress<double>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
                log.AppendLine("downloaded " + new FileInfo(local).Length + " bytes");
                player.Source = new Uri(local);
                player.Play();
            }
            catch (Exception ex)
            {
                log.AppendLine("download error: " + ex.Message);
            }
            await Task.Delay(12000);
            log.AppendLine("position=" + player.Position + " playingState=" + player.LoadedBehavior);
            try
            {
                player.Stop();
                player.Close();
            }
            catch
            {
            }
            window.Close();
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "video-test.txt"), log.ToString());
        }

        private static void RunSelfTest()
        {
            var lines = new System.Text.StringBuilder();
            try
            {
                lines.AppendLine("schema messages: " + (Svc.Schema.All != null));
                var hot = DemoData.Hot();
                lines.AppendLine("hot threads: " + hot.Threads.Count + ", tabs: " + hot.Tabs.Count);
                var forum = DemoData.Forum();
                lines.AppendLine("forum: " + forum.Forum.Name + " threads=" + forum.Threads.Count + " tabs=" + forum.Tabs.Count);
                var thread = DemoData.Thread();
                lines.AppendLine("thread: " + thread.Thread.Title + " posts=" + thread.Posts.Count);
                var card = new PostCard(thread.Posts[0], thread.Thread.Id, thread.Forum?.Id ?? 0, thread.Forum?.Name, null);
                lines.AppendLine("post card built: " + (card != null));
                foreach (var p in thread.Posts.Take(4))
                {
                    lines.AppendLine("  post #" + p.Floor + " " + p.Author.DisplayName +
                                     " segments=" + p.Content.Count +
                                     " images=" + p.Content.Count(c => c.Kind == ContentKind.Image));
                }
            }
            catch (Exception ex)
            {
                lines.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), lines.ToString());
        }

        private static bool HasArg(string[] args, string name)
        {
            foreach (var arg in args) if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i][(name.Length + 1)..];
                }
            }
            return null;
        }
    }
}
