using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TieLiteDesktop.Core;
using TieLiteDesktop.Controls;

namespace TieLiteDesktop.Views
{
    /// <summary>Manage keywords hidden from thread lists and post bodies.</summary>
    public sealed class BlockWordsWindow : Window
    {
        private readonly List<string> _words;
        private readonly StackPanel _list;
        private readonly TextBox _input;
        private readonly TextBlock _count;

        public BlockWordsWindow()
        {
            Title = "屏蔽词管理";
            Width = 520;
            Height = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "AppBackground");
            if (Svc.Window != null) Owner = Svc.Window;

            _words = BlockWords.All.ToList();
            var root = new Grid { Margin = new Thickness(28, 24, 28, 22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel();
            header.Children.Add(Ui.Text("屏蔽词管理", 20, FontWeights.SemiBold));
            var desc = Ui.Text("标题或正文包含这些词的帖子会被自动隐藏，每行一个。", 12.5, null, "TextTertiary");
            desc.Margin = new Thickness(0, 6, 0, 0);
            desc.TextWrapping = TextWrapping.Wrap;
            header.Children.Add(desc);
            root.Children.Add(header);

            var editor = new Grid { Margin = new Thickness(0, 18, 0, 12) };
            editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _input = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(10, 7, 10, 7),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _input.SetResourceReference(TextBox.FontFamilyProperty, "UiFont");
            _input.SetResourceReference(TextBox.ForegroundProperty, "TextPrimary");
            _input.SetResourceReference(TextBox.CaretBrushProperty, "TextPrimary");
            _input.SetResourceReference(TextBox.BackgroundProperty, "SurfaceAlt");
            _input.SetResourceReference(TextBox.BorderBrushProperty, "Border");
            _input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) AddWord();
            };
            editor.Children.Add(_input);
            var add = Ui.Button("添加", true, 12.5);
            add.Margin = new Thickness(10, 0, 0, 0);
            add.Click += (_, _) => AddWord();
            Grid.SetColumn(add, 1);
            editor.Children.Add(add);
            Grid.SetRow(editor, 1);
            root.Children.Add(editor);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            _list = new StackPanel();
            scroll.Content = _list;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _count = Ui.Text("", 11.5, null, "TextTertiary");
            _count.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(_count);
            var save = Ui.Button("保存", true, 13);
            save.Padding = new Thickness(24, 8, 24, 8);
            save.Click += (_, _) =>
            {
                Svc.Settings.BlockedWords = _words.ToArray();
                Svc.Settings.Save();
                DialogResult = true;
                Close();
            };
            Grid.SetColumn(save, 1);
            footer.Children.Add(save);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
            Render();
        }

        private void AddWord()
        {
            var word = _input.Text?.Trim();
            if (string.IsNullOrWhiteSpace(word)) return;
            if (!_words.Any(item => string.Equals(item, word, StringComparison.OrdinalIgnoreCase)))
            {
                _words.Add(word);
            }
            _input.Clear();
            Render();
            _input.Focus();
        }

        private void Render()
        {
            _list.Children.Clear();
            _count.Text = "共 " + _words.Count + " 个屏蔽词";
            if (_words.Count == 0)
            {
                var empty = Ui.Card(new TextBlock
                {
                    Text = "还没有屏蔽词",
                    FontSize = 12.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20),
                }, 20, 14);
                _list.Children.Add(empty);
                return;
            }
            foreach (var word in _words.ToList())
            {
                var row = new Grid { Margin = new Thickness(2, 3, 2, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = Ui.Text(word, 13);
                label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label);
                var remove = Ui.Button("删除", false, 11.5);
                var captured = word;
                remove.Click += (_, _) =>
                {
                    _words.RemoveAll(item => string.Equals(item, captured, StringComparison.OrdinalIgnoreCase));
                    Render();
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                var card = Ui.Card(row, 14, 10);
                card.Margin = new Thickness(0, 0, 0, 6);
                _list.Children.Add(card);
            }
        }
    }
}
