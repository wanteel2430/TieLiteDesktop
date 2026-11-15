using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>收藏页：贴吧云端收藏（需登录）。</summary>
    public sealed class FavoritesView : UserControl, IRefreshable
    {
        private readonly StackPanel _list;
        private readonly TextBlock _status;
        private bool _loading;

        public FavoritesView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 900,
                MinWidth = 720,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.Margin = new Thickness(2, 0, 0, 8);
            _list = new StackPanel();
            root.Children.Add(_status);
            root.Children.Add(_list);
            scroll.Content = root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _list.Children.Clear();
            if (!Svc.Account.IsLoggedIn)
            {
                _status.Text = "";
                _list.Children.Add(MessagesView.LoginPrompt("登录后查看你的收藏"));
                return;
            }
            _loading = true;
            _status.Text = "加载中…";
            _list.Children.Add(HomeView.Skeleton(3));
            try
            {
                var items = await Svc.Api.GetFavoritesAsync();
                _list.Children.Clear();
                _status.Text = items.Count == 0 ? "还没有收藏的帖子" : "共 " + items.Count + " 个收藏";
                if (items.Count == 0) _list.Children.Add(HomeView.EmptyCard("在帖子里点「收藏」即可加入这里"));
                foreach (var thread in items)
                {
                    if (string.IsNullOrWhiteSpace(thread.Title))
                    {
                        thread.Title = "收藏的帖子";
                    }
                    _list.Children.Add(Ui.Card(new ThreadRow(thread, true), 4, 16));
                }
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(HomeView.ErrorCard(ex.Message, LoadAsync));
                _status.Text = "";
            }
            finally
            {
                _loading = false;
            }
        }

        public async Task RefreshAsync() => await LoadAsync();
    }
}
