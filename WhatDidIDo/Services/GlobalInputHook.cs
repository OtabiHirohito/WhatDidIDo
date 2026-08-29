using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace WhatDidIDo.Services
{
    public class InputHookEventArgs : EventArgs
    {
        public string Type { get; set; } = "";
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public string WindowTitle { get; set; } = "";
    }

    public class GlobalInputHook : IDisposable
    {
        // ====== Win32 定義 ======
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL    = 14;

        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_KEYUP      = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP   = 0x0105;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP   = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP   = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP   = 0x0208;
        private const int WM_MOUSEWHEEL  = 0x020A;
        private const int WM_MOUSEMOVE   = 0x0200;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP   = 0x020C;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint   vkCode;
            public uint   scanCode;
            public uint   flags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT  pt;
            public uint   mouseData;
            public uint   flags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        // ====== フィールド ======
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook    = IntPtr.Zero;
        private readonly LowLevelProc _keyboardProc;
        private readonly LowLevelProc _mouseProc;

        private bool _logMouseMove = false;   // マウス移動は量が多いのでデフォルトOFF

        public event EventHandler<InputHookEventArgs>? InputDetected;

        public bool LogMouseMove
        {
            get => _logMouseMove;
            set => _logMouseMove = value;
        }

        // ====== コンストラクター ======
        public GlobalInputHook()
        {
            _keyboardProc = KeyboardHookCallback;
            _mouseProc    = MouseHookCallback;
        }

        // ====== フック開始／停止 ======
        public void Start()
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule!;
            IntPtr hModule = GetModuleHandle(curModule.ModuleName!);

            if (_keyboardHook == IntPtr.Zero)
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
            if (_mouseHook == IntPtr.Zero)
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hModule, 0);
        }

        public void Stop()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        private static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "";

                var sb = new System.Text.StringBuilder(256);
                if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
                {
                    return sb.ToString();
                }
            }
            catch
            {
                // 無視して空文字を返す
            }
            return "";
        }

        // ====== キーボードコールバック ======
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info   = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vk     = (Key)KeyInterop.KeyFromVirtualKey((int)info.vkCode);
                string key = vk.ToString();

                string action = (int)wParam switch
                {
                    WM_KEYDOWN    => "KeyDown",
                    WM_KEYUP      => "KeyUp",
                    WM_SYSKEYDOWN => "SysKeyDown",
                    WM_SYSKEYUP   => "SysKeyUp",
                    _             => "Unknown"
                };

                InputDetected?.Invoke(this, new InputHookEventArgs
                {
                    Type        = "KEYBOARD",
                    Action      = action,
                    Detail      = key,
                    WindowTitle = GetActiveWindowTitle()
                });
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        // ====== マウスコールバック ======
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int msg  = (int)wParam;

                if (msg == WM_MOUSEMOVE && !_logMouseMove)
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                string action = msg switch
                {
                    WM_LBUTTONDOWN => "Down",
                    WM_LBUTTONUP   => "Up",
                    WM_RBUTTONDOWN => "Down",
                    WM_RBUTTONUP   => "Up",
                    WM_MBUTTONDOWN => "Down",
                    WM_MBUTTONUP   => "Up",
                    WM_XBUTTONDOWN => "Down",
                    WM_XBUTTONUP   => "Up",
                    WM_MOUSEWHEEL  => "Wheel",
                    WM_MOUSEMOVE   => "Move",
                    _              => "Unknown"
                };

                string detail = msg switch
                {
                    WM_LBUTTONDOWN or WM_LBUTTONUP => "Left Click",
                    WM_RBUTTONDOWN or WM_RBUTTONUP => "Right Click",
                    WM_MBUTTONDOWN or WM_MBUTTONUP => "Middle Click",
                    WM_XBUTTONDOWN or WM_XBUTTONUP => "Extra Button",
                    WM_MOUSEWHEEL => GetWheelDetail(info.mouseData),
                    WM_MOUSEMOVE  => "Move",
                    _             => "Unknown"
                };

                InputDetected?.Invoke(this, new InputHookEventArgs
                {
                    Type        = "MOUSE",
                    Action      = action,
                    Detail      = detail,
                    X           = info.pt.x,
                    Y           = info.pt.y,
                    WindowTitle = GetActiveWindowTitle()
                });
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private static string GetWheelDetail(uint mouseData)
        {
            short delta = (short)((mouseData >> 16) & 0xFFFF);
            return delta > 0 ? "Wheel Up" : "Wheel Down";
        }

        public void Dispose() => Stop();
    }
}
