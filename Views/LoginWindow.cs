using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>登录窗口：Cookie 登录 / 百度扫码登录。</summary>
    public sealed class LoginWindow : Window
    {
        private readonly StackPanel _body;
        private readonly TextBlock _error;
        private readonly TextBox _cookieBox;
        private readonly Image _qrImage;
        private readonly TextBlock _qrStatus;
        private readonly Button _qrRefresh;
        private CancellationTokenSource _qrCts;
        private readonly StackPanel _cookiePanel;
        private readonly StackPanel _qrPanel;
        private readonly StackPanel _phonePanel;
        private readonly RadioButton _cookieTab;
        private readonly RadioButton _qrTab;
        private readonly RadioButton _phoneTab;
        private readonly TextBlock _phoneStatus;
        private BrowserLoginSession _browserSession;
        private CancellationTokenSource _browserCts;
        private bool _busy;

        public LoginWindow()
        {
            Width = 560;
            Height = 575;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var shell = new Border
            {
                Margin = new Thickness(18),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(28, 22, 28, 24),
            };
            shell.SetResourceReference(BackgroundProperty, "Surface");
            shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 6,
                Opacity = 0.22,
                Color = Colors.Black,
            };

            var root = new StackPanel();
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(Ui.Text("登录百度贴吧", 20, FontWeights.Bold));
            var subtitle = Ui.Text("登录后可回复、收藏、关注与查看消息", 12, null, "TextTertiary");
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            titleStack.Children.Add(subtitle);
            header.Children.Add(titleStack);
            var close = Ui.IconButton("\uE8BB", "关闭", 30);
            close.Click += (_, _) => Close();
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            root.Children.Add(header);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 14) };
            _cookieTab = new RadioButton { Content = "Cookie 登录", GroupName = "LoginTabs" };
            _cookieTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            _qrTab = new RadioButton { Content = "扫码登录", GroupName = "LoginTabs" };
            _qrTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            _phoneTab = new RadioButton { Content = "手机号登录", GroupName = "LoginTabs", IsChecked = true };
            _phoneTab.SetResourceReference(FrameworkElement.StyleProperty, "PillRadio");
            tabs.Children.Add(_phoneTab);
            tabs.Children.Add(_qrTab);
            tabs.Children.Add(_cookieTab);
            root.Children.Add(tabs);

            _error = Ui.Text("", 12, null, "Danger");
            _error.Margin = new Thickness(2, 0, 0, 8);
            _error.Visibility = Visibility.Collapsed;

            // cookie panel
            _cookiePanel = new StackPanel { Visibility = Visibility.Collapsed };
            var hint = Ui.Text("在浏览器登录 tieba.baidu.com，打开开发者工具 → Application → Cookies，复制包含 BDUSS 的整段 Cookie 粘贴到下方。", 12, null, "TextSecondary");
            hint.Margin = new Thickness(0, 0, 0, 10);
            hint.TextWrapping = TextWrapping.Wrap;
            _cookiePanel.Children.Add(hint);

            var boxHost = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Height = 150 };
            boxHost.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            boxHost.SetResourceReference(BorderBrushProperty, "Border");
            boxHost.BorderThickness = new Thickness(1);
            _cookieBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            _cookieBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _cookieBox.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            boxHost.Child = _cookieBox;
            _cookiePanel.Children.Add(boxHost);

            var cookieHelp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var openTieba = Ui.Button("打开贴吧网页", false, 12);
            openTieba.Click += (_, _) =>
            {
                if (!Ui.OpenUrl("https://tieba.baidu.com"))
                {
                    Svc.Window?.ShowToast("无法打开浏览器，链接已复制到剪贴板");
                }
            };
            cookieHelp.Children.Add(openTieba);
            var guide = Ui.Button("Cookie 获取教程", false, 12);
            guide.Margin = new Thickness(8, 0, 0, 0);
            guide.Click += (_, _) =>
            {
                var window = new CookieGuideWindow { Owner = this };
                window.ShowDialog();
            };
            cookieHelp.Children.Add(guide);
            var tip = Ui.Text("  BDUSS 是登录态凭据，请勿分享给他人", 11.5, null, "TextTertiary");
            tip.VerticalAlignment = VerticalAlignment.Center;
            cookieHelp.Children.Add(tip);
            _cookiePanel.Children.Add(cookieHelp);

            var loginButton = Ui.Button("登录", true, 13.5);
            loginButton.Height = 40;
            loginButton.Margin = new Thickness(0, 18, 0, 0);
            loginButton.Click += async (_, _) => await CookieLoginAsync();
            _cookiePanel.Children.Add(loginButton);

            var diagnose = Ui.Button("测试登录状态", false, 12);
            diagnose.Margin = new Thickness(0, 8, 0, 0);
            diagnose.HorizontalAlignment = HorizontalAlignment.Center;
            diagnose.Click += async (_, _) =>
            {
                var raw = _cookieBox.Text?.Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    ShowError("请先粘贴 Cookie 或 BDUSS");
                    return;
                }
                diagnose.IsEnabled = false;
                try
                {
                    var result = await DiagnoseAsync(raw);
                    System.Windows.MessageBox.Show(result, "登录状态测试", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                finally
                {
                    diagnose.IsEnabled = true;
                }
            };
            _cookiePanel.Children.Add(diagnose);

            // qr panel
            _qrPanel = new StackPanel { Visibility = Visibility.Collapsed };
            var qrHost = new Border
            {
                Width = 220,
                Height = 220,
                CornerRadius = new CornerRadius(14),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.White,
            };
            _qrImage = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
            qrHost.Child = _qrImage;
            _qrPanel.Children.Add(qrHost);
            _qrStatus = Ui.Text("正在获取二维码…", 12.5, null, "TextSecondary");
            _qrStatus.HorizontalAlignment = HorizontalAlignment.Center;
            _qrStatus.Margin = new Thickness(0, 14, 0, 0);
            _qrPanel.Children.Add(_qrStatus);
            var qrHint = Ui.Text("使用百度 App 或贴吧 App 扫码登录", 11.5, null, "TextTertiary");
            qrHint.HorizontalAlignment = HorizontalAlignment.Center;
            qrHint.Margin = new Thickness(0, 4, 0, 0);
            _qrPanel.Children.Add(qrHint);
            _qrRefresh = Ui.Button("刷新二维码", false, 12.5);
            _qrRefresh.HorizontalAlignment = HorizontalAlignment.Center;
            _qrRefresh.Margin = new Thickness(0, 12, 0, 0);
            _qrRefresh.Click += async (_, _) => await StartQrAsync();
            _qrPanel.Children.Add(_qrRefresh);

            // phone / browser panel
            _phonePanel = new StackPanel();
            var phoneHint = Ui.Text(
                "点击下方按钮会打开百度官方登录页（独立 Edge 窗口）。在网页里输入手机号、获取短信验证码并登录，成功后这里会自动读取登录状态；也支持账号密码登录。",
                12, null, "TextSecondary");
            phoneHint.TextWrapping = TextWrapping.Wrap;
            phoneHint.Margin = new Thickness(0, 0, 0, 14);
            _phonePanel.Children.Add(phoneHint);

            var phoneOpen = Ui.Button("打开百度登录页", true, 13.5);
            phoneOpen.Height = 42;
            phoneOpen.Click += async (_, _) => await StartBrowserLoginAsync();
            _phonePanel.Children.Add(phoneOpen);

            _phoneStatus = Ui.Text("尚未打开登录页", 12, null, "TextTertiary");
            _phoneStatus.TextWrapping = TextWrapping.Wrap;
            _phoneStatus.Margin = new Thickness(0, 12, 0, 0);
            _phonePanel.Children.Add(_phoneStatus);

            var phoneCheck = Ui.Button("我已完成登录，读取状态", false, 12.5);
            phoneCheck.Margin = new Thickness(0, 12, 0, 0);
            phoneCheck.HorizontalAlignment = HorizontalAlignment.Left;
            phoneCheck.Click += async (_, _) => await CheckBrowserLoginAsync();
            _phonePanel.Children.Add(phoneCheck);

            _body = new StackPanel();
            _body.Children.Add(_error);
            _body.Children.Add(_cookiePanel);
            _body.Children.Add(_qrPanel);
            _body.Children.Add(_phonePanel);
            root.Children.Add(_body);

            _cookieTab.Checked += (_, _) =>
            {
                _cookiePanel.Visibility = Visibility.Visible;
                _qrPanel.Visibility = Visibility.Collapsed;
                _phonePanel.Visibility = Visibility.Collapsed;
                _qrCts?.Cancel();
            };
            _qrTab.Checked += async (_, _) =>
            {
                _cookiePanel.Visibility = Visibility.Collapsed;
                _phonePanel.Visibility = Visibility.Collapsed;
                _qrPanel.Visibility = Visibility.Visible;
                await StartQrAsync();
            };
            _phoneTab.Checked += (_, _) =>
            {
                _cookiePanel.Visibility = Visibility.Collapsed;
                _qrPanel.Visibility = Visibility.Collapsed;
                _phonePanel.Visibility = Visibility.Visible;
                _qrCts?.Cancel();
            };

            shell.Child = root;
            Content = shell;
            Closed += (_, _) =>
            {
                _qrCts?.Cancel();
                _browserCts?.Cancel();
                _browserSession?.Dispose();
            };
            Loaded += (_, _) => Anim.PopIn(this);
        }

        public void ShowPhoneTab()
        {
            _phoneTab.IsChecked = true;
        }

        private async Task StartBrowserLoginAsync()
        {
            try
            {
                if (_browserSession == null)
                {
                    _browserSession = new BrowserLoginSession();
                    _browserSession.Start();
                }
                _browserCts?.Cancel();
                _browserCts = new CancellationTokenSource();
                _phoneStatus.Text = "已打开登录窗口，请在浏览器中完成手机号 / 验证码登录…";
                var result = await _browserSession.WaitForLoginAsync(_browserCts.Token);
                if (result == null)
                {
                    _phoneStatus.Text = "等待超时，请重试或改用 Cookie 登录";
                    return;
                }
                await FinishBrowserLoginAsync(result.Value.bduss, result.Value.stoken, result.Value.cookieHeader);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _phoneStatus.Text = "浏览器登录失败：" + ex.Message;
            }
        }

        private async Task CheckBrowserLoginAsync()
        {
            if (_browserSession == null)
            {
                _phoneStatus.Text = "请先点击「打开百度登录页」";
                return;
            }
            try
            {
                var result = await _browserSession.TryReadOnceAsync(CancellationToken.None);
                if (result == null)
                {
                    _phoneStatus.Text = "还没有检测到登录状态，请先在浏览器里完成登录";
                    return;
                }
                await FinishBrowserLoginAsync(result.Value.bduss, result.Value.stoken, result.Value.cookieHeader);
            }
            catch (Exception ex)
            {
                _phoneStatus.Text = "读取登录状态失败：" + ex.Message;
            }
        }

        private async Task FinishBrowserLoginAsync(string bduss, string stoken, string cookieHeader)
        {
            _phoneStatus.Text = "已获取登录信息，正在校验…";
            if (await CompleteLoginAsync(bduss, stoken, cookieHeader, trustBrowser: true))
            {
                _browserSession?.Dispose();
                _browserSession = null;
                DialogResult = true;
            }
            else
            {
                _phoneStatus.Text = "已拿到 Cookie 但校验失败，正在生成诊断…";
                var diag = await DiagnoseAsync(cookieHeader);
                _phoneStatus.Text = "已拿到 Cookie 但校验失败，详见诊断窗口";
                System.Windows.MessageBox.Show(diag, "登录校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowError(string message)        {
            _error.Text = message;
            _error.Visibility = Visibility.Collapsed;
            if (string.IsNullOrEmpty(message)) return;
            _error.Visibility = Visibility.Visible;
            _error.Opacity = 0;
            _error.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private async Task CookieLoginAsync()
        {
            if (_busy) return;
            var raw = _cookieBox.Text?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                ShowError("请粘贴 Cookie 或 BDUSS");
                return;
            }
            _busy = true;
            ShowError("");
            try
            {
                var account = new Account();
                if (!account.ApplyCookie(raw))
                {
                    ShowError("未能解析出 BDUSS，请检查复制内容");
                    return;
                }
                if (await CompleteLoginAsync(account.Bduss, account.Stoken, account.RawCookie)) DialogResult = true;
                else ShowError("Cookie 已失效或网络不可用，请重新获取");
            }
            catch (Exception ex)
            {
                ShowError("登录失败：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Validates the cookie against Tieba, then stores the signed-in account.</summary>
        private static async Task<bool> CompleteLoginAsync(string bduss, string stoken, string rawCookie = "", bool trustBrowser = false)
        {
            if (string.IsNullOrWhiteSpace(bduss)) return false;
            var account = new Account { Bduss = bduss, Stoken = stoken, RawCookie = rawCookie ?? "" };
            await EnsureBaiduIdAsync(account);
            if (!string.IsNullOrEmpty(account.Stoken)) stoken = account.Stoken;
            var info = await FetchUserInfoAsync(account);
            // json_userinfo can be empty for some accounts; fall back to the tbs endpoint,
            // which reliably reports is_login=1 for a valid BDUSS.
            if (info == null && !await CheckTbsLoginAsync(account) && !await CheckMoIndexLoginAsync(account) && !trustBrowser)
            {
                return false;
            }

            Svc.Account.Bduss = bduss;
            Svc.Account.Stoken = stoken;
            Svc.Account.RawCookie = account.RawCookie;
            if (account.Uid != 0) Svc.Account.Uid = account.Uid;
            if (info != null)
            {
                Svc.Account.Uid = info.Value.uid;
                Svc.Account.UserName = info.Value.name;
                Svc.Account.Portrait = info.Value.portrait;
            }
            else if (string.IsNullOrEmpty(Svc.Account.UserName))
            {
                Svc.Account.UserName = "贴吧用户";
            }
            Svc.Account.Save();
            Svc.RecreateClient();
            try
            {
                var me = await Svc.Api.GetSelfUserAsync();
                if (me != null && (me.Id != 0 || !string.IsNullOrWhiteSpace(me.ShowName)))
                {
                    if (me.Id != 0) Svc.Account.Uid = me.Id;
                    if (!string.IsNullOrWhiteSpace(me.ShowName)) Svc.Account.UserName = me.ShowName;
                    if (!string.IsNullOrWhiteSpace(me.Portrait)) Svc.Account.Portrait = me.Portrait;
                    Svc.Account.Save();
                }
            }
            catch
            {
            }
            return true;
        }

        private static async Task<string> DiagnoseAsync(string raw)
        {
            var account = new Account();
            if (!account.ApplyCookie(raw))
            {
                return "没有解析出 BDUSS。\n请粘贴完整 Cookie（含 BDUSS=），或只粘贴 BDUSS 的值。";
            }
            var cookie = account.BuildCookieHeader();
            var report = new System.Text.StringBuilder();
            report.AppendLine("BDUSS 长度：" + account.Bduss.Length);
            report.AppendLine("STOKEN：" + (string.IsNullOrEmpty(account.Stoken) ? "未提供" : "已提供"));
            report.AppendLine("额外 Cookie：" + (account.RawCookie.Length > account.Bduss.Length + 8 ? "有" : "无"));
            report.AppendLine();
            report.AppendLine("tbs 接口：" + await ProbeAsync("https://tieba.baidu.com/dc/common/tbs", cookie));
            report.AppendLine("用户信息接口：" + await ProbeAsync("https://tieba.baidu.com/f/user/json_userinfo", cookie));
            report.AppendLine("贴吧首页接口：" + await ProbeAsync("https://tieba.baidu.com/mo/q/newmoindex?need_user=1", cookie));
            return report.ToString();
        }

        private static async Task<string> ProbeAsync(string url, string cookie)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
                request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                using var response = await Svc.Client.Http.SendAsync(request).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (text.Length > 260) text = text[..260] + "…";
                return "HTTP " + (int)response.StatusCode + " " + text;
            }
            catch (Exception ex)
            {
                return "请求失败：" + ex.Message;
            }
        }

        private static async Task<bool> CheckMoIndexLoginAsync(Account account)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://tieba.baidu.com/mo/q/newmoindex?need_user=1");
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Cookie", account.BuildCookieHeader());
                request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                using var response = await Svc.Client.Http.SendAsync(request).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.TryGetProperty("data", out var data) &&
                       data.TryGetProperty("is_login", out var login) &&
                       login.ValueKind == JsonValueKind.Number && login.GetInt32() == 1;
            }
            catch
            {
                return false;
            }
        }

        private static async Task EnsureBaiduIdAsync(Account account)
        {
            if (account.RawCookie.Contains("BAIDUID", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://tieba.baidu.com/");
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Cookie", account.BuildCookieHeader());
                using var response = await Svc.Client.Http.SendAsync(request).ConfigureAwait(false);
                if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
                var extras = new List<string>();
                foreach (var value in values)
                {
                    var pair = value.Split(';')[0];
                    if (pair.StartsWith("BAIDUID", StringComparison.OrdinalIgnoreCase)) extras.Add(pair);
                    if (pair.StartsWith("STOKEN=", StringComparison.OrdinalIgnoreCase))
                    {
                        account.Stoken = pair.Substring("STOKEN=".Length);
                        extras.Add(pair);
                    }
                }
                if (extras.Count > 0)
                {
                    account.RawCookie = string.Join("; ", extras) +
                                        (string.IsNullOrEmpty(account.RawCookie) ? "" : "; " + account.RawCookie);
                }
            }
            catch
            {
            }
        }

        private static async Task<bool> CheckTbsLoginAsync(Account account)
        {
            foreach (var url in new[] { "https://tieba.baidu.com/dc/common/tbs", "http://tieba.baidu.com/dc/common/tbs" })
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                    var cookie = account.BuildCookieHeader();
                    request.Headers.TryAddWithoutValidation("Cookie", cookie);
                    request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                    using var response = await Svc.Client.Http.SendAsync(request).ConfigureAwait(false);
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("is_login", out var login) && login.GetInt32() == 1)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        private static async Task<(long uid, string name, string portrait)?> FetchUserInfoAsync(Account account)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://tieba.baidu.com/f/user/json_userinfo");
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                var cookie = account.BuildCookieHeader();
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
                request.Headers.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
                using var response = await Svc.Client.Http.SendAsync(request).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text) || text.Trim() == "null") return null;

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;
                long uid = 0;
                if (data.TryGetProperty("user_id", out var idElement))
                {
                    long.TryParse(idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : idElement.ToString(), out uid);
                }
                var name = data.TryGetProperty("user_name_show", out var nameShow) ? nameShow.GetString() : null;
                if (string.IsNullOrEmpty(name) && data.TryGetProperty("user_name", out var userName)) name = userName.GetString();
                var portrait = data.TryGetProperty("portrait", out var p) ? p.GetString() : "";
                if (uid <= 0) return null;
                return (uid, name ?? ("贴吧用户" + uid), portrait ?? "");
            }
            catch
            {
                return null;
            }
        }

        private async Task StartQrAsync()
        {
            _qrCts?.Cancel();
            _qrCts = new CancellationTokenSource();
            var token = _qrCts.Token;
            _qrRefresh.IsEnabled = false;
            _qrStatus.Text = "正在获取二维码…";
            try
            {
                var session = await QrLoginSession.CreateAsync(token);
                session.Status = message => Dispatcher.Invoke(() => _qrStatus.Text = message);
                _qrImage.Source = await session.LoadImageAsync(token);
                _qrStatus.Text = "请使用手机百度 / 贴吧 App 扫描二维码";
                _qrRefresh.IsEnabled = true;
                var result = await session.WaitForLoginAsync(token);
                if (result == null)
                {
                    _qrStatus.Text = "二维码已过期，请刷新后重试";
                    return;
                }
                _qrStatus.Text = "扫码成功，正在登录…";
                if (await CompleteLoginAsync(result.Value.bduss, result.Value.stoken, trustBrowser: true))
                {
                    DialogResult = true;
                }
                else
                {
                    _qrStatus.Text = "登录失败，请改用 Cookie 登录";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _qrStatus.Text = "二维码获取失败：" + ex.Message;
                _qrRefresh.IsEnabled = true;
            }
        }
    }

    /// <summary>Baidu passport QR-code login session.</summary>
    public sealed class QrLoginSession
    {
        public string Sign { get; private set; }
        public string ImageUrl { get; private set; }

        public static async Task<QrLoginSession> CreateAsync(CancellationToken ct)
        {
            var gid = Guid.NewGuid().ToString("N");
            var url = "https://passport.baidu.com/v2/api/getqrcode?lp=pc&qrloginfrom=pc&gid=" + gid +
                      "&callback=cb&_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var text = await GetTextAsync(url, ct).ConfigureAwait(false);
            var json = JsonpPayload(text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sign = root.GetProperty("sign").GetString();
            var img = root.GetProperty("imgurl").GetString();
            if (string.IsNullOrEmpty(sign) || string.IsNullOrEmpty(img)) throw new InvalidOperationException("二维码接口返回异常");
            return new QrLoginSession
            {
                Sign = sign,
                ImageUrl = img.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? img
                    : "https://" + img.TrimStart('/'),
            };
        }

        public async Task<ImageSource> LoadImageAsync(CancellationToken ct)
        {
            var bytes = await Svc.Client.Http.GetByteArrayAsync(ImageUrl, ct).ConfigureAwait(false);
            var image = ImageLoader.Decode(bytes, 320);
            return image;
        }

        public Action<string> Status;

        private static HttpClient _directHttp;

        private static HttpClient DirectHttp
        {
            get
            {
                if (_directHttp != null) return _directHttp;
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                };
                if (Svc.Settings != null && Svc.Settings.ProxyMode == "none") handler.UseProxy = false;
                if (Svc.Settings != null && Svc.Settings.ProxyMode == "custom" &&
                    Uri.TryCreate(Svc.Settings.ProxyUrl, UriKind.Absolute, out var proxy))
                {
                    handler.Proxy = new System.Net.WebProxy(proxy);
                }
                _directHttp = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                _directHttp.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
                _directHttp.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://passport.baidu.com/");
                return _directHttp;
            }
        }

        public async Task<(string bduss, string stoken)?> WaitForLoginAsync(CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(2000, ct).ConfigureAwait(false);
                var url = "https://passport.baidu.com/channel/unicast?channel_id=" + Sign +
                          "&tpl=pc&callback=cb&_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string text;
                try
                {
                    text = await GetTextAsync(url, ct).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }
                var json = JsonpPayload(text);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("channel_v", out var channel)) continue;
                using var inner = JsonDocument.Parse(channel.GetString() ?? "{}");
                var status = 0;
                if (inner.RootElement.TryGetProperty("status", out var s))
                {
                    status = s.ValueKind == JsonValueKind.Number ? s.GetInt32()
                        : s.ValueKind == JsonValueKind.String && int.TryParse(s.GetString(), out var parsed) ? parsed : 0;
                }
                var v = inner.RootElement.TryGetProperty("v", out var valueElement) ? valueElement.GetString() : null;
                if (string.IsNullOrEmpty(v))
                {
                    if (status == 1) Status?.Invoke("已扫描，请在手机上确认登录");
                    continue;
                }
                Status?.Invoke("手机已确认，正在获取登录状态…");
                try { v = Uri.UnescapeDataString(v); } catch { }
                var bduss = ExtractParam(v, "bduss");
                if (string.IsNullOrEmpty(bduss)) bduss = v;

                var loginUrl = "https://passport.baidu.com/v3/login/main/qrbdusslogin?bduss=" +
                               Uri.EscapeDataString(bduss) +
                               "&qrcode=1&tpl=pp&apiver=v3&tt=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                               "&traceid=&time=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                               "&alg=v3&elapsed=1";

                var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var current = loginUrl;
                for (var hop = 0; hop < 4 && !string.IsNullOrEmpty(current); hop++)
                {
                    using var response = await DirectHttp.GetAsync(current, ct).ConfigureAwait(false);
                    CollectCookies(response, collected);
                    var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    // passport may answer with JSON instead of Set-Cookie
                    if (TryParseSession(body, out var jsonBduss, out var jsonStoken))
                    {
                        collected["BDUSS"] = jsonBduss;
                        if (!string.IsNullOrEmpty(jsonStoken)) collected["STOKEN"] = jsonStoken;
                        break;
                    }
                    if (response.StatusCode is System.Net.HttpStatusCode.MovedPermanently
                        or System.Net.HttpStatusCode.Found
                        or System.Net.HttpStatusCode.SeeOther
                        or System.Net.HttpStatusCode.TemporaryRedirect)
                    {
                        var location = response.Headers.Location;
                        if (location == null) break;
                        current = new Uri(new Uri(current), location).ToString();
                        continue;
                    }
                    break;
                }

                collected.TryGetValue("BDUSS", out var bdussCookie);
                collected.TryGetValue("STOKEN", out var stoken);
                if (string.IsNullOrEmpty(bdussCookie)) bdussCookie = bduss;
                if (string.IsNullOrEmpty(bdussCookie))
                {
                    Status?.Invoke("未获取到 BDUSS，请重新扫码");
                    continue;
                }
                return (bdussCookie, stoken ?? "");
            }
            return null;
        }

        private static bool TryParseSession(string body, out string bduss, out string stoken)
        {
            bduss = "";
            stoken = "";
            if (string.IsNullOrWhiteSpace(body)) return false;
            var start = body.IndexOf('{');
            var end = body.LastIndexOf('}');
            if (start < 0 || end <= start) return false;
            try
            {
                using var doc = JsonDocument.Parse(body.Substring(start, end - start + 1));
                if (!doc.RootElement.TryGetProperty("data", out var data)) return false;
                if (!data.TryGetProperty("session", out var session)) return false;
                if (session.TryGetProperty("bduss", out var b) && b.ValueKind == JsonValueKind.String)
                {
                    bduss = b.GetString() ?? "";
                }
                if (session.TryGetProperty("stoken", out var s) && s.ValueKind == JsonValueKind.String)
                {
                    stoken = s.GetString() ?? "";
                }
                return bduss.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void CollectCookies(HttpResponseMessage response, Dictionary<string, string> target)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
            foreach (var header in values)
            {
                var pair = header.Split(';')[0];
                var kv = pair.Split('=', 2);
                if (kv.Length != 2) continue;
                var name = kv[0].Trim();
                if (name.Equals("BDUSS", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("STOKEN", StringComparison.OrdinalIgnoreCase))
                {
                    target[name] = kv[1].Trim();
                }
            }
        }

        private static async Task<string> GetTextAsync(string url, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Referer", "https://passport.baidu.com/");
            using var response = await Svc.Client.Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        private static string JsonpPayload(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";
            var start = text.IndexOf('(');
            var end = text.LastIndexOf(')');
            if (start >= 0 && end > start) return text.Substring(start + 1, end - start - 1);
            return text;
        }

        private static string ExtractParam(string query, string name)
        {
            foreach (var part in query.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            return null;
        }
    }
}
