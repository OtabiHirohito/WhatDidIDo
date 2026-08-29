using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WhatDidIDo.Models;

namespace WhatDidIDo
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                WindowWidth = settings.WindowWidth,
                WindowHeight = settings.WindowHeight,
                WindowLeft = settings.WindowLeft,
                WindowTop = settings.WindowTop,
                IsMaximized = settings.IsMaximized,
                ColumnWidths = new System.Collections.Generic.Dictionary<string, double>(settings.ColumnWidths),

                AutoStopEnabled = settings.AutoStopEnabled,
                AutoStopSeconds = settings.AutoStopSeconds,
                MaxEntriesEnabled = settings.MaxEntriesEnabled,
                MaxEntriesCount = settings.MaxEntriesCount,
                BlacklistEnabled = settings.BlacklistEnabled,
                BlacklistItems = settings.BlacklistItems,
                AutoCsvEnabled = settings.AutoCsvEnabled,
                AutoCsvPath = settings.AutoCsvPath,
                AutoClearOnStartEnabled = settings.AutoClearOnStartEnabled
            };

            LoadValues();
        }

        private void LoadValues()
        {
            ChkAutoClearOnStart.IsChecked = Settings.AutoClearOnStartEnabled;

            ChkAutoStop.IsChecked = Settings.AutoStopEnabled;
            TxtAutoStopSeconds.Text = Settings.AutoStopSeconds.ToString();
            TxtAutoStopSeconds.IsEnabled = Settings.AutoStopEnabled;

            ChkMaxEntries.IsChecked = Settings.MaxEntriesEnabled;
            TxtMaxEntriesCount.Text = Settings.MaxEntriesCount.ToString();
            TxtMaxEntriesCount.IsEnabled = Settings.MaxEntriesEnabled;

            ChkBlacklist.IsChecked = Settings.BlacklistEnabled;
            TxtBlacklistItems.Text = Settings.BlacklistItems;
            TxtBlacklistItems.IsEnabled = Settings.BlacklistEnabled;

            ChkAutoCsv.IsChecked = Settings.AutoCsvEnabled;
            TxtAutoCsvPath.Text = Settings.AutoCsvPath;
            TxtAutoCsvPath.IsEnabled = Settings.AutoCsvEnabled;
        }

        private void ChkAutoStop_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtAutoStopSeconds != null)
                TxtAutoStopSeconds.IsEnabled = ChkAutoStop.IsChecked == true;
        }

        private void ChkMaxEntries_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtMaxEntriesCount != null)
                TxtMaxEntriesCount.IsEnabled = ChkMaxEntries.IsChecked == true;
        }

        private void ChkBlacklist_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtBlacklistItems != null)
                TxtBlacklistItems.IsEnabled = ChkBlacklist.IsChecked == true;
        }

        private void ChkAutoCsv_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtAutoCsvPath != null)
                TxtAutoCsvPath.IsEnabled = ChkAutoCsv.IsChecked == true;
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "保存先フォルダーを選択してください"
            };

            if (!string.IsNullOrWhiteSpace(TxtAutoCsvPath.Text) && Directory.Exists(TxtAutoCsvPath.Text))
            {
                dlg.InitialDirectory = TxtAutoCsvPath.Text;
            }

            if (dlg.ShowDialog() == true)
            {
                TxtAutoCsvPath.Text = dlg.FolderName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (ChkAutoStop.IsChecked == true)
            {
                if (!int.TryParse(TxtAutoStopSeconds.Text, out int sec) || sec <= 0)
                {
                    MessageBox.Show("自動停止の秒数は正の整数を指定してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Settings.AutoStopSeconds = sec;
            }

            if (ChkMaxEntries.IsChecked == true)
            {
                if (!int.TryParse(TxtMaxEntriesCount.Text, out int cnt) || cnt <= 0)
                {
                    MessageBox.Show("上書き記録の保持件数は正の整数を指定してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Settings.MaxEntriesCount = cnt;
            }

            if (ChkAutoCsv.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(TxtAutoCsvPath.Text) || !Directory.Exists(TxtAutoCsvPath.Text))
                {
                    MessageBox.Show("有効な保存先フォルダーを指定してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            Settings.AutoClearOnStartEnabled = ChkAutoClearOnStart.IsChecked == true;
            Settings.AutoStopEnabled = ChkAutoStop.IsChecked == true;
            Settings.MaxEntriesEnabled = ChkMaxEntries.IsChecked == true;
            Settings.BlacklistEnabled = ChkBlacklist.IsChecked == true;
            Settings.BlacklistItems = TxtBlacklistItems.Text.Trim();
            Settings.AutoCsvEnabled = ChkAutoCsv.IsChecked == true;
            Settings.AutoCsvPath = TxtAutoCsvPath.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
