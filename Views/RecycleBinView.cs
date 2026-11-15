using System;
using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    /// <summary>Threads deleted through this client; restore or remove from the local record.</summary>
    public sealed class RecycleBinView : UserControl, IRefreshable
    {
        private readonly StackPanel _list;

        public RecycleBinView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 960,
                Margin = new Thickness(30, 10, 30, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            root.Children.Add(Ui.Text("回收站", 20, FontWeights.SemiBold));
            var hint = Ui.Text("这里只记录通过本客户端删除的帖子。恢复会调用贴吧恢复接口；若客户端/网页端仍不显示，请到贴吧网页端「我的帖子 → 回收站」或 App 内对应吧务后台处理。缓存内容会把帖子保存到「缓存的帖子」，删除后仍可离线查看。", 12, null, "TextTertiary");
            hint.Margin = new Thickness(0, 6, 0, 14);
            root.Children.Add(hint);
            _list = new StackPanel();
            root.Children.Add(_list);
            scroll.Content = root;
            Content = scroll;
            Loaded += (_, _) => Render();
        }

        public System.Threading.Tasks.Task RefreshAsync()
        {
            Render();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void Render()
        {
            _list.Children.Clear();
            var posts = RecycleStore.All();
            if (posts.Count == 0)
            {
                _list.Children.Add(HomeView.EmptyCard("回收站是空的"));
                return;
            }
            foreach (var post in posts)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(16, 13, 16, 13),
                    Margin = new Thickness(0, 0, 0, 10),
                };
                card.SetResourceReference(BackgroundProperty, "Surface");
                card.SetResourceReference(BorderBrushProperty, "Border");
                card.BorderThickness = new Thickness(1);
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var info = new StackPanel();
                info.Children.Add(Ui.Text(string.IsNullOrWhiteSpace(post.Title) ? "无标题帖子" : post.Title,
                    14.5, FontWeights.Medium));
                info.Children.Add(Ui.Text(post.ForumName + " · 删除于 " +
                    DateTimeOffset.FromUnixTimeSeconds(post.DeletedAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                    11.5, null, "TextTertiary"));
                grid.Children.Add(info);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var restore = Ui.Button("恢复", true, 12);
                restore.Click += async (_, _) =>
                {
                    try
                    {
                        await Svc.Api.RecoverThreadAsync(post.ForumId, post.ThreadId);
                        RecycleStore.Remove(post.ThreadId);
                        Svc.Window?.ShowToast("已尝试恢复，请到吧内确认");
                        Render();
                    }
                    catch (Exception ex)
                    {
                        Svc.Window?.ShowToast("恢复失败：" + ex.Message);
                    }
                };
                actions.Children.Add(restore);
                var cache = Ui.Button("缓存内容", false, 12);
                cache.Margin = new Thickness(8, 0, 0, 0);
                cache.Click += async (_, _) =>
                {
                    try
                    {
                        var page = await Svc.Api.GetThreadAsync(post.ThreadId, 1, false, false, 0);
                        if (page?.Posts == null || page.Posts.Count == 0)
                        {
                            Svc.Window?.ShowToast("帖子已删除，无法再缓存内容");
                            return;
                        }
                        await CachedThreadStore.SaveAsync(page, false, 1);
                        Svc.Window?.ShowToast("已缓存到「缓存的帖子」");
                    }
                    catch (Exception ex)
                    {
                        Svc.Window?.ShowToast("缓存失败：" + ex.Message);
                    }
                };
                actions.Children.Add(cache);
                var remove = Ui.Button("移除记录", false, 12);
                remove.Margin = new Thickness(8, 0, 0, 0);
                remove.Click += (_, _) =>
                {
                    RecycleStore.Remove(post.ThreadId);
                    Render();
                };
                actions.Children.Add(remove);
                Grid.SetColumn(actions, 1);
                grid.Children.Add(actions);
                card.Child = grid;
                _list.Children.Add(card);
            }
        }
    }
}
