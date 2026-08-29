using System.Collections.Generic;

namespace WhatDidIDo.Models
{
    public class AppSettings
    {
        public double WindowWidth { get; set; } = 900;
        public double WindowHeight { get; set; } = 680;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public bool IsMaximized { get; set; } = false;
        public Dictionary<string, double> ColumnWidths { get; set; } = new();

        // 1. 自動停止
        public bool AutoStopEnabled { get; set; } = false;
        public int AutoStopSeconds { get; set; } = 60;

        // 2. 上書き記録
        public bool MaxEntriesEnabled { get; set; } = true;
        public int MaxEntriesCount { get; set; } = 500;

        // 3. ブラックリスト
        public bool BlacklistEnabled { get; set; } = false;
        public string BlacklistItems { get; set; } = "";

        // 4. 自動CSV保存
        public bool AutoCsvEnabled { get; set; } = false;
        public string AutoCsvPath { get; set; } = "";

        // 5. START時に自動でCLEAR
        public bool AutoClearOnStartEnabled { get; set; } = false;
    }
}
