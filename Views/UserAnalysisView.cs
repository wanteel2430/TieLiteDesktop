using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>简单的用户发言分析：概览、按月、活跃时段、常逛吧、高频词。</summary>
    public sealed class UserAnalysisView : UserControl
    {
        private readonly long _uid;
        private readonly string _name;
        private readonly StackPanel _root;

        public UserAnalysisView(long uid, string name)
        {
            _uid = uid;
            _name = name;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _root = new StackPanel
            {
                MaxWidth = 820,
                MinWidth = 640,
                Margin = new Thickness(28, 10, 28, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _root.Children.Add(Ui.Text(name + " 的发言分析", 20, FontWeights.SemiBold));
            _root.Children.Add(Ui.Text("正在统计…", 12, null, "TextTertiary"));
            scroll.Content = _root;
            Content = scroll;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            List<ThreadSummary> threads;
            List<ThreadSummary> replies;
            try
            {
                threads = await Svc.Api.GetUserPostsAsync(_uid, 1, true);
                replies = await Svc.Api.GetUserPostsAsync(_uid, 1, false);
            }
            catch (Exception ex)
            {
                _root.Children.Clear();
                _root.Children.Add(Ui.Text("统计失败：" + ex.Message, 12.5, null, "TextTertiary"));
                return;
            }
            var all = threads.Concat(replies).ToList();
            _root.Children.Clear();
            _root.Children.Add(Ui.Text(_name + " 的发言分析", 20, FontWeights.SemiBold));

            var forums = all.Where(t => !string.IsNullOrWhiteSpace(t.ForumName))
                .GroupBy(t => t.ForumName)
                .OrderByDescending(g => g.Count())
                .ToList();
            var times = all.Select(t => t.LastTimeInt).Where(t => t > 0).ToList();
            var span = times.Count == 0
                ? "暂无数据"
                : DateTimeOffset.FromUnixTimeSeconds(times.Min()).LocalDateTime.ToString("yyyy-MM-dd") + " ~ " +
                  DateTimeOffset.FromUnixTimeSeconds(times.Max()).LocalDateTime.ToString("yyyy-MM-dd");

            var summary = new StackPanel();
            summary.Children.Add(Row("发言总数", all.Count.ToString()));
            summary.Children.Add(Row("主题帖", threads.Count.ToString()));
            summary.Children.Add(Row("回帖", replies.Count.ToString()));
            summary.Children.Add(Row("涉及贴吧", forums.Count.ToString()));
            summary.Children.Add(Row("时间跨度", span));
            _root.Children.Add(Card("概览", summary));

            var months = new SortedDictionary<string, int>();
            foreach (var time in times)
            {
                var key = DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime.ToString("yyyy-MM");
                months[key] = months.TryGetValue(key, out var count) ? count + 1 : 1;
            }
            _root.Children.Add(Card("按月分布", ColumnChart(months.TakeLast(12).Select(p => (p.Key, p.Value)).ToList())));

            var hours = new int[24];
            foreach (var time in times)
            {
                hours[DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime.Hour]++;
            }
            var hourData = Enumerable.Range(0, 24).Select(h => (h.ToString(), hours[h])).ToList();
            _root.Children.Add(Card("活跃时段", ColumnChart(hourData)));

            var topForums = new StackPanel();
            foreach (var group in forums.Take(10))
            {
                topForums.Children.Add(BarRow(group.Key, group.Count(), forums[0].Count()));
            }
            _root.Children.Add(Card("常逛的吧 Top 10", topForums.Children.Count == 0
                ? Ui.Text("暂无数据", 12, null, "TextTertiary")
                : topForums));

            var words = new Dictionary<string, int>();
            foreach (var item in all)
            {
                var source = (item.Title ?? "") + " " + (item.PreviewText ?? "");
                foreach (Match match in Regex.Matches(source, @"[\u4e00-\u9fa5]{2,4}"))
                {
                    var word = match.Value;
                    words[word] = words.TryGetValue(word, out var count) ? count + 1 : 1;
                }
            }
            var cloud = new WrapPanel();
            foreach (var pair in words.OrderByDescending(p => p.Value).Take(30))
            {
                var size = Math.Min(24, 12 + pair.Value * 2);
                var text = Ui.Text(pair.Key, size, FontWeights.Medium, "Accent");
                text.Margin = new Thickness(0, 0, 10, 6);
                cloud.Children.Add(text);
            }
            _root.Children.Add(Card("高频词云", cloud.Children.Count == 0
                ? Ui.Text("暂无数据", 12, null, "TextTertiary")
                : cloud));
        }

        private static UIElement Row(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(Ui.Text(label, 13, null, "TextSecondary"));
            var valueText = Ui.Text(value, 13.5, FontWeights.SemiBold);
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);
            return grid;
        }

        private static UIElement ColumnChart(List<(string label, int value)> data)
        {
            var max = Math.Max(1, data.Count == 0 ? 1 : data.Max(d => d.value));
            var host = new StackPanel();
            var bars = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = 130,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            foreach (var item in data)
            {
                var height = Math.Max(3, 120.0 * item.value / max);
                var bar = new Border
                {
                    Width = 18,
                    Height = height,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = item.label + "：" + item.value,
                };
                bar.SetResourceReference(Border.BackgroundProperty, "Accent");
                bars.Children.Add(bar);
            }
            host.Children.Add(bars);
            var labels = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (data.Count > 0) labels.Children.Add(Ui.Text(data[0].label, 10.5, null, "TextTertiary"));
            if (data.Count > 1)
            {
                var last = Ui.Text(" → " + data[^1].label, 10.5, null, "TextTertiary");
                labels.Children.Add(last);
            }
            host.Children.Add(labels);
            return host;
        }

        private static UIElement BarRow(string label, int value, int max)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            grid.Children.Add(Ui.Text(label, 12, null, "TextSecondary"));
            var track = new Border
            {
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            track.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            var fill = new Border
            {
                Height = 12,
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(12, 150.0 * value / Math.Max(1, max)),
            };
            fill.SetResourceReference(Border.BackgroundProperty, "Accent");
            var trackGrid = new Grid();
            trackGrid.Children.Add(track);
            trackGrid.Children.Add(fill);
            Grid.SetColumn(trackGrid, 1);
            grid.Children.Add(trackGrid);
            var count = Ui.Text(value.ToString(), 11.5, null, "TextTertiary");
            Grid.SetColumn(count, 2);
            grid.Children.Add(count);
            stack.Children.Add(grid);
            return stack;
        }

        private static Border Card(string title, UIElement body)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 10, 0, 0),
            };
            card.SetResourceReference(Border.BackgroundProperty, "Surface");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            var stack = new StackPanel();
            stack.Children.Add(Ui.Text(title, 15, FontWeights.SemiBold));
            var content = new Border { Margin = new Thickness(0, 10, 0, 0), Child = body };
            stack.Children.Add(content);
            card.Child = stack;
            return card;
        }
    }
}
