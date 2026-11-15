using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>本地浏览历史。</summary>
    public sealed class HistoryView : UserControl, IRefreshable
    {
        private readonly StackPanel _list;
        private readonly TextBlock _status;

        public HistoryView()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var root = new StackPanel
            {
                MaxWidth = 880,
                MinWidth = 640,
                Margin = new Thickness(30, 10, 30, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var head = new Grid { Margin = new Thickness(2, 4, 2, 10) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status = Ui.Text("", 12, null, "TextTertiary");
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            head.Children.Add(Ui.Text("浏览历史", 20, FontWeights.SemiBold));
            head.Children.Add(_status);
            root.Children.Add(head);
            _list = new StackPanel();
            root.Children.Add(_list);
            scroll.Content = root;
            Content = scroll;
            Loaded += (_, _) => Render();
        }

        private void Render()
        {
            _list.Children.Clear();
            var entries = HistoryStore.All();
            _status.Text = entries.Count == 0 ? "" : "共 " + entries.Count + " 条";
            if (entries.Count == 0)
            {
                _list.Children.Add(HomeView.EmptyCard("还没有浏览记录"));
                return;
            }
            var index = 0;
            foreach (var entry in entries)
            {
                var thread = new ThreadSummary
                {
                    Id = entry.Id,
                    Title = string.IsNullOrWhiteSpace(entry.Title) ? "帖子 " + entry.Id : entry.Title,
                    ForumName = entry.ForumName,
                    LastTimeInt = entry.Time,
                };
                var card = Ui.Card(new ThreadRow(thread, true), 4, 16);
                _list.Children.Add(card);
                Anim.FadeInUp(card, Math.Min(index++ * 26, 260), 12, 280);
            }
        }

        public Task RefreshAsync()
        {
            Render();
            return Task.CompletedTask;
        }
    }
}
