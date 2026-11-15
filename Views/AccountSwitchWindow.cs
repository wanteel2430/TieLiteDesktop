using System;
using System.Windows;
using System.Windows.Controls;
using TieLiteDesktop.Controls;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Views
{
    public sealed class AccountSwitchWindow : Window
    {
        public AccountSwitchWindow()
        {
            Width = 440;
            Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Title = "切换账号";
            SetResourceReference(BackgroundProperty, "AppBackground");
            if (Svc.Window != null) Owner = Svc.Window;

            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            root.Children.Add(Ui.Text("切换账号", 18, FontWeights.SemiBold));
            var hint = Ui.Text("选择一个已保存的账号，或关闭此窗口后使用「添加账号」。", 12, null, "TextTertiary");
            hint.Margin = new Thickness(0, 6, 0, 14);
            root.Children.Add(hint);
            var list = new StackPanel();
            var accounts = AccountStore.All();
            if (accounts.Count == 0)
            {
                list.Children.Add(Ui.Card(Ui.Text("还没有保存其他账号", 12.5, null, "TextTertiary"), 18, 12));
            }
            foreach (var account in accounts)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = Ui.Text(string.IsNullOrWhiteSpace(account.UserName) ? "ID " + account.Uid : account.UserName,
                    13.5, FontWeights.Medium);
                row.Children.Add(name);
                var use = Ui.Button("切换", true, 12);
                use.Click += (_, _) =>
                {
                    AccountStore.Upsert(Svc.Account);
                    Svc.UseAccount(account);
                    Svc.Window?.UpdateUserCard();
                    Svc.Window?.ShowToast("已切换账号");
                    DialogResult = true;
                    Close();
                };
                Grid.SetColumn(use, 1);
                row.Children.Add(use);
                var card = Ui.Card(row, 14, 12);
                card.Margin = new Thickness(0, 0, 0, 8);
                list.Children.Add(card);
            }
            var scroll = new ScrollViewer { Height = 320, Content = list };
            root.Children.Add(scroll);
            Content = root;
            Loaded += (_, _) => Anim.PopIn(this);
        }
    }
}
