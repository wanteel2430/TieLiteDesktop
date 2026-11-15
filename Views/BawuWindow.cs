using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Views
{
    /// <summary>吧务团队列表。</summary>
    public sealed class BawuWindow : Window
    {
        private readonly StackPanel _list;
        private readonly long _forumId;

        public BawuWindow(long forumId, string forumName)
        {
            _forumId = forumId;
            Width = 480;
            Height = 560;
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
                Padding = new Thickness(24, 18, 24, 20),
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
            titleStack.Children.Add(Ui.Text(forumName + " · 吧务团队", 19, FontWeights.Bold));
            var subtitle = Ui.Text("吧主 / 小吧主 / 图片小编等", 11.5, null, "TextTertiary");
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

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 14, 0, 0) };
            _list = new StackPanel();
            scroll.Content = _list;
            root.Children.Add(scroll);
            shell.Child = root;
            Content = shell;
            Loaded += async (_, _) =>
            {
                Anim.PopIn(this);
                await LoadAsync();
            };
        }

        private async Task LoadAsync()
        {
            _list.Children.Clear();
            _list.Children.Add(HomeView.Skeleton(3));
            try
            {
                var members = await Svc.Api.GetBawuTeamAsync(_forumId);
                _list.Children.Clear();
                if (members.Count == 0)
                {
                    _list.Children.Add(HomeView.EmptyCard("暂时没有获取到吧务信息"));
                    return;
                }
                foreach (var role in members.GroupBy(m => string.IsNullOrWhiteSpace(m.RoleName) ? "吧务" : m.RoleName))
                {
                    var label = Ui.Text(role.Key, 12, FontWeights.SemiBold, "TextTertiary");
                    label.Margin = new Thickness(2, 8, 0, 6);
                    _list.Children.Add(label);
                    foreach (var member in role)
                    {
                        _list.Children.Add(MemberRow(member));
                    }
                }
            }
            catch (Exception ex)
            {
                _list.Children.Clear();
                _list.Children.Add(HomeView.ErrorCard(ex.Message, LoadAsync));
            }
        }

        private UIElement MemberRow(BawuMember member)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
            };
            card.SetResourceReference(BackgroundProperty, "SurfaceAlt");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Avatar(member.AvatarUrl, 36, member.Name));
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Ui.Text(member.Name, 13.5, FontWeights.Medium));
            text.Children.Add(Ui.Text("UID " + member.Uid, 11, null, "TextTertiary"));
            row.Children.Add(text);
            card.Child = row;
            card.MouseLeftButtonUp += (_, _) =>
            {
                Close();
                Svc.Window?.OpenProfile(member.Uid, member.Name);
            };
            return card;
        }
    }
}
