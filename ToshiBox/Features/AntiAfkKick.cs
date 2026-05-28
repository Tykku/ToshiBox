using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using ToshiBox.Common;

namespace ToshiBox.Features
{
    public unsafe class AntiAfkKick : IDisposable
    {
        private readonly Config _config;
        private DateTime _lastCheck = DateTime.MinValue;
        private const int CheckIntervalSeconds = 10;

        public AntiAfkKick(Config config)
        {
            _config = config;
        }

        public void IsEnabled()
        {
            if (_config.AntiAfkKickConfig.Enabled)
                Enable();
            else
                Disable();
        }

        public void Enable() => Svc.Framework.Update += OnUpdate;
        public void Dispose() => Disable();
        public void Disable() => Svc.Framework.Update -= OnUpdate;

        private void OnUpdate(IFramework framework)
        {
            if (DateTime.Now - _lastCheck < TimeSpan.FromSeconds(CheckIntervalSeconds))
                return;
            _lastCheck = DateTime.Now;

            var inputModule = UIModule.Instance()->GetInputTimerModule();
            float maxTimer = Math.Max(inputModule->AfkTimer, Math.Max(inputModule->ContentInputTimer, inputModule->InputTimer));

            if (maxTimer > _config.AntiAfkKickConfig.TimerLimit)
            {
                if (NativeWindow.TryFindGameWindow(out var hwnd))
                {
                    NativeWindow.SendMessage(hwnd, NativeWindow.WM_KEYDOWN, (IntPtr)NativeWindow.LControlKey, IntPtr.Zero);
                    NativeWindow.SendMessage(hwnd, NativeWindow.WM_KEYUP, (IntPtr)NativeWindow.LControlKey, IntPtr.Zero);
                    Svc.Log.Debug($"Anti-AFK: sent keypress (timer was {maxTimer:F1}s)");
                }
            }
        }
    }

    internal static class NativeWindow
    {
        public const int LControlKey = 162;
        public const uint WM_KEYDOWN = 0x100;
        public const uint WM_KEYUP = 0x101;

        public static bool TryFindGameWindow(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            while (true)
            {
                hwnd = FindWindowEx(IntPtr.Zero, hwnd, "FFXIVGAME", null);
                if (hwnd == IntPtr.Zero) break;
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == Process.GetCurrentProcess().Id) break;
            }
            return hwnd != IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}