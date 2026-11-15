using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    public sealed class EmojiManagerWindow : Window
    {
        public EmojiManagerWindow()
        {
            Width = 620;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Title = "表情包管理";
            SetResourceReference(BackgroundProperty, "AppBackground");
            if (Svc.Window != null) Owner = Svc.Window;

            var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel();
            header.Children.Add(Ui.Text("表情包管理", 18, FontWeights.SemiBold));
            var hint = Ui.Text("取消勾选后，该表情不会出现在发帖表情面板中。", 12, null, "TextTertiary");
            hint.Margin = new Thickness(0, 6, 0, 12);
            header.Children.Add(hint);
            root.Children.Add(header);

            var panel = new WrapPanel();
            var hidden = new HashSet<string>(Svc.Settings.HiddenEmoticons ?? Array.Empty<string>());
            foreach (var pair in ContentParser.AllEmoticons)
            {
                var check = new CheckBox
                {
                    IsChecked = !hidden.Contains(pair.Key),
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new Image
                            {
                                Source = ImageLoader.FromFile(ContentParser.EmoticonPath(pair.Value), 48),
                                Width = 22,
                                Height = 22,
                                Margin = new Thickness(0, 0, 6, 0),
                            },
                            Ui.Text(pair.Key, 11.5, null, "TextSecondary"),
                        },
                    },
                    Margin = new Thickness(4, 4, 10, 4),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = pair.Key,
                };
                check.SetResourceReference(FrameworkElement.StyleProperty, "PillCheck");
                var name = pair.Key;
                check.Click += (_, _) =>
                {
                    if (check.IsChecked == true) hidden.Remove(name);
                    else hidden.Add(name);
                    Svc.Settings.HiddenEmoticons = hidden.ToArray();
                    Svc.Settings.Save();
                };
                panel.Children.Add(check);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
            };
            var all = Ui.Button("全部显示", false, 12);
            all.Margin = new Thickness(0, 0, 8, 0);
            all.Click += (_, _) =>
            {
                Svc.Settings.HiddenEmoticons = Array.Empty<string>();
                Svc.Settings.Save();
                Close();
            };
            footer.Children.Add(all);
            var ok = Ui.Button("完成", true, 12);
            ok.Click += (_, _) => Close();
            footer.Children.Add(ok);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }
}
