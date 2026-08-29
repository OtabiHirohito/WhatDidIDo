using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using WhatDidIDo.Models;
using WhatDidIDo.Services;
using Microsoft.Win32;

namespace WhatDidIDo
{
    public partial class MainWindow : Window
    {
        // ====== フィールド ======
        private readonly GlobalInputHook _hook = new();
        private readonly ObservableCollection<LogEntry> _allEntries = new();
        private readonly ObservableCollection<LogEntry> _filteredEntries = new();
        private bool _isLogging = false;
        private int _entryIndex = 0;
        private DateTime? _sessionStart;
        private AppSettings _settings = new();
        private System.Windows.Threading.DispatcherTimer? _autoStopTimer;

        // ====== 初期化 ======
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            LogListView.ItemsSource = _filteredEntries;
            _hook.InputDetected += OnInputDetected;
        }

        // ====== 設定ボタン ======
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings)
            {
                Owner = this
            };

            if (win.ShowDialog() == true)
            {
                _settings = win.Settings;
                SettingsService.Save(_settings);
                StatusLabel.Text = "✓ Settings saved.";
            }
        }

        private void LoadSettings()
        {
            _settings = SettingsService.Load();

            // ウィンドウサイズと位置
            if (_settings.WindowWidth > 0) Width = _settings.WindowWidth;
            if (_settings.WindowHeight > 0) Height = _settings.WindowHeight;
            if (_settings.WindowLeft >= 0) Left = _settings.WindowLeft;
            if (_settings.WindowTop >= 0) Top = _settings.WindowTop;
            if (_settings.IsMaximized) WindowState = WindowState.Maximized;

            // 列の幅
            if (_settings.ColumnWidths.TryGetValue("ColIndex", out double wIndex)) ColIndex.Width = wIndex;
            if (_settings.ColumnWidths.TryGetValue("ColTimestamp", out double wTs)) ColTimestamp.Width = wTs;
            if (_settings.ColumnWidths.TryGetValue("ColType", out double wType)) ColType.Width = wType;
            if (_settings.ColumnWidths.TryGetValue("ColAction", out double wAction)) ColAction.Width = wAction;
            if (_settings.ColumnWidths.TryGetValue("ColDetail", out double wDetail)) ColDetail.Width = wDetail;
            if (_settings.ColumnWidths.TryGetValue("ColPosition", out double wPos)) ColPosition.Width = wPos;
            if (_settings.ColumnWidths.TryGetValue("ColWindowTitle", out double wWinTitle)) ColWindowTitle.Width = wWinTitle;
        }

        private void SaveSettings()
        {
            // 最大化時は通常時のサイズを保存したいため、WindowStateで判定
            if (WindowState == WindowState.Maximized)
            {
                _settings.IsMaximized = true;
            }
            else
            {
                _settings.IsMaximized = false;
                _settings.WindowWidth = Width;
                _settings.WindowHeight = Height;
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }

            // 列の幅
            _settings.ColumnWidths["ColIndex"] = ColIndex.Width;
            _settings.ColumnWidths["ColTimestamp"] = ColTimestamp.Width;
            _settings.ColumnWidths["ColType"] = ColType.Width;
            _settings.ColumnWidths["ColAction"] = ColAction.Width;
            _settings.ColumnWidths["ColDetail"] = ColDetail.Width;
            _settings.ColumnWidths["ColPosition"] = ColPosition.Width;
            _settings.ColumnWidths["ColWindowTitle"] = ColWindowTitle.Width;

            SettingsService.Save(_settings);
        }

        // ====== ログ受信 ======
        private void OnInputDetected(object? sender, InputHookEventArgs e)
        {
            if (!_isLogging) return;

            // フィルター確認
            bool showKeyboard = FilterKeyboard.IsChecked == true;
            bool showMouse    = FilterMouse.IsChecked    == true;

            if (e.Type == "KEYBOARD" && !showKeyboard) return;
            if (e.Type == "MOUSE"    && !showMouse)    return;

            // 3. ブラックリスト判定
            if (_settings.BlacklistEnabled && !string.IsNullOrWhiteSpace(_settings.BlacklistItems))
            {
                var blacklist = _settings.BlacklistItems
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var item in blacklist)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        if (e.Action.Equals(item, StringComparison.OrdinalIgnoreCase) ||
                            e.Detail.Equals(item, StringComparison.OrdinalIgnoreCase))
                        {
                            return; // ブラックリストに一致するので記録しない
                        }
                    }
                }
            }

            var entry = new LogEntry
            {
                Index       = ++_entryIndex,
                Timestamp   = DateTime.Now.ToString("HH:mm:ss.fff"),
                Type        = e.Type,
                Action      = e.Action,
                Detail      = e.Detail,
                Position    = e.Type == "MOUSE" ? $"({e.X}, {e.Y})" : "",
                WindowTitle = e.WindowTitle
            };

            // UIスレッドで更新
            Dispatcher.Invoke(() =>
            {
                _allEntries.Add(entry);

                // 2. 上書き記録（件数制限）
                if (_settings.MaxEntriesEnabled && _settings.MaxEntriesCount > 0)
                {
                    while (_allEntries.Count > _settings.MaxEntriesCount)
                    {
                        _allEntries.RemoveAt(0);
                    }
                }

                ApplySearchFilter();
                UpdateCountLabel();

                // 自動スクロール
                if (_filteredEntries.Count > 0)
                    LogListView.ScrollIntoView(_filteredEntries[^1]);
            });
        }

        // ====== START ======
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.AutoClearOnStartEnabled)
            {
                _allEntries.Clear();
                _filteredEntries.Clear();
                _entryIndex = 0;
                UpdateCountLabel();
            }

            _isLogging    = true;
            _sessionStart = DateTime.Now;

            _hook.Start();

            StartButton.IsEnabled      = false;
            StopButton.IsEnabled       = true;
            ExportCsvButton.IsEnabled  = false;
            ClearButton.IsEnabled      = false;

            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x55));
            RecordingLabel.Visibility = Visibility.Visible;
            StatusLabel.Text = $"Recording started at {_sessionStart:HH:mm:ss}";

            // 1. 自動停止タイマーの設定
            if (_settings.AutoStopEnabled && _settings.AutoStopSeconds > 0)
            {
                _autoStopTimer?.Stop();
                _autoStopTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_settings.AutoStopSeconds)
                };
                _autoStopTimer.Tick += (s, args) =>
                {
                    _autoStopTimer.Stop();
                    if (_isLogging)
                    {
                        StopButton_Click(this, new RoutedEventArgs());
                        StatusLabel.Text = $"Automated stop triggered after {_settings.AutoStopSeconds} seconds.";
                    }
                };
                _autoStopTimer.Start();
            }
        }

        // ====== STOP ======
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _isLogging = false;
            _hook.Stop();
            _autoStopTimer?.Stop();

            StartButton.IsEnabled      = true;
            StopButton.IsEnabled       = false;
            ExportCsvButton.IsEnabled  = _allEntries.Count > 0;
            ClearButton.IsEnabled      = _allEntries.Count > 0;

            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x96));
            RecordingLabel.Visibility = Visibility.Collapsed;

            var duration = _sessionStart.HasValue
                ? (DateTime.Now - _sessionStart.Value).ToString(@"mm\:ss")
                : "N/A";
            StatusLabel.Text = $"Recording stopped — {_entryIndex} events in {duration}";
            UpdateSessionLabel();

            // 4. 自動CSV保存
            if (_settings.AutoCsvEnabled && !string.IsNullOrWhiteSpace(_settings.AutoCsvPath) && System.IO.Directory.Exists(_settings.AutoCsvPath) && _allEntries.Count > 0)
            {
                try
                {
                    string filePath = System.IO.Path.Combine(_settings.AutoCsvPath, $"AutoLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    CsvExportService.Export(_allEntries, filePath);
                    StatusLabel.Text += $" | ✓ Auto CSV saved → {filePath}";
                }
                catch (Exception ex)
                {
                    StatusLabel.Text += $" | ✕ Auto CSV failed: {ex.Message}";
                }
            }
        }

        // ====== CSV EXPORT ======
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter           = "CSV files (*.csv)|*.csv",
                FileName         = $"InputLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt       = ".csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // 現在の表示（フィルター済み）をエクスポート
                var target = string.IsNullOrWhiteSpace(SearchBox.Text)
                    ? _allEntries
                    : (IEnumerable<LogEntry>)_filteredEntries;

                CsvExportService.Export(target, dlg.FileName);
                StatusLabel.Text = $"✓ CSV exported → {dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====== CLEAR ======
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("すべてのログを削除しますか？",
                "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _allEntries.Clear();
            _filteredEntries.Clear();
            _entryIndex = 0;

            ExportCsvButton.IsEnabled = false;
            ClearButton.IsEnabled     = false;
            StatusLabel.Text          = "Log cleared.";
            UpdateCountLabel();
        }

        // ====== フィルター変更 ======
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplySearchFilter();
        }

        // ====== 検索 ======
        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        // ====== フィルター＆検索適用 ======
        private void ApplySearchFilter()
        {
            if (FilterKeyboard == null || FilterMouse == null || SearchBox == null) return;

            bool showKeyboard = FilterKeyboard.IsChecked == true;
            bool showMouse    = FilterMouse.IsChecked    == true;
            string query      = SearchBox.Text.Trim().ToLower();

            var filtered = _allEntries
                .Where(entry =>
                {
                    if (entry.Type == "KEYBOARD" && !showKeyboard) return false;
                    if (entry.Type == "MOUSE"    && !showMouse)    return false;
                    if (!string.IsNullOrEmpty(query))
                    {
                        return entry.Detail.ToLower().Contains(query)
                            || entry.Action.ToLower().Contains(query)
                            || entry.Timestamp.Contains(query)
                            || entry.WindowTitle.ToLower().Contains(query);
                    }
                    return true;
                })
                .ToList();

            _filteredEntries.Clear();
            foreach (var entry in filtered)
                _filteredEntries.Add(entry);

            UpdateCountLabel();
        }

        // ====== ラベル更新 ======
        private void UpdateCountLabel()
        {
            CountLabel.Text = _allEntries.Count == _filteredEntries.Count
                ? $"{_allEntries.Count} events"
                : $"{_filteredEntries.Count} / {_allEntries.Count} events";
        }

        private void UpdateSessionLabel()
        {
            if (_sessionStart.HasValue)
                SessionLabel.Text = $"Session: {_sessionStart:HH:mm:ss}";
        }

        // ====== コピー機能 (CTRL+C) ======
        private void LogListView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.C && 
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                CopySelectedEntries();
            }
        }

        private void CopySelectedEntries()
        {
            var selectedItems = LogListView.SelectedItems.Cast<LogEntry>().ToList();
            if (selectedItems.Count == 0) return;

            var sb = new StringBuilder();
            // ヘッダーを追加
            sb.AppendLine("Index\tTimestamp\tType\tAction\tDetail\tPosition\tWindowTitle");

            foreach (var entry in selectedItems)
            {
                sb.AppendLine($"{entry.Index}\t{entry.Timestamp}\t{entry.Type}\t{entry.Action}\t{entry.Detail}\t{entry.Position}\t{entry.WindowTitle}");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusLabel.Text = $"✓ Copied {selectedItems.Count} entries to clipboard.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"✕ Copy failed: {ex.Message}";
            }
        }

        // ====== ウィンドウ終了時 ======
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            _hook.Stop();
            _hook.Dispose();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
