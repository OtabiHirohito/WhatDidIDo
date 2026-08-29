using System.Windows.Media;

namespace WhatDidIDo.Models
{
    public class LogEntry
    {
        public int Index { get; set; }
        public string Timestamp { get; set; } = "";
        public string Type { get; set; } = "";          // "KEYBOARD" / "MOUSE"
        public string Action { get; set; } = "";        // "KeyDown" / "Click" etc
        public string Detail { get; set; } = "";        // キー名 / ボタン名
        public string Position { get; set; } = "";      // マウス座標
        public string WindowTitle { get; set; } = "";   // アクティブウィンドウタイトル

        // UI表示用色
        public Brush TypeBackground => Type == "KEYBOARD"
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x28, 0x40))
            : new SolidColorBrush(Color.FromRgb(0x28, 0x1A, 0x35));

        public Brush TypeForeground => Type == "KEYBOARD"
            ? new SolidColorBrush(Color.FromRgb(0x40, 0x90, 0xFF))
            : new SolidColorBrush(Color.FromRgb(0xC0, 0x60, 0xFF));
    }
}
