using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommons.DalamudServices;

namespace ToshiBox.Common
{
    // Gracefully closes ACT (same as clicking X), then relaunches it minimized.
    public static class ActRestarter
    {
        private const string ActPath = @"C:\Program Files (x86)\Advanced Combat Tracker\Advanced Combat Tracker.exe";
        private const string ProcName = "Advanced Combat Tracker";
        private const string TitleStart = "Advanced Combat Tracker";

        private const int SW_RESTORE = 9;
        private const uint WM_SYSCOMMAND = 0x0112;
        private static readonly IntPtr SC_CLOSE = (IntPtr)0xF060;

        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int cmd);
        private const int SW_SHOWMINNOACTIVE = 7;
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

        private static int _running;

        public static void Run()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                Print("ACT restart already in progress.");
                return;
            }

            Task.Run(() =>
            {
                try { Restart(); }
                catch (Exception ex) { PrintError($"ACT restart failed: {ex.Message}"); }
                finally { Interlocked.Exchange(ref _running, 0); }
            });
        }

        private static void Restart()
        {
            var procs = Process.GetProcessesByName(ProcName);
            foreach (var proc in procs)
            {
                var windows = FindWindows((uint)proc.Id, visibleOnly: false);
                if (windows.Count == 0)
                {
                    PrintError("Couldn't find ACT's main window. Close ACT manually.");
                    return;
                }

                foreach (var w in windows)
                {
                    ShowWindow(w, SW_RESTORE);
                    PostMessage(w, WM_SYSCOMMAND, SC_CLOSE, IntPtr.Zero);
                }
                
                if (!proc.WaitForExit(30000))
                {
                    PrintError("ACT didn't close (it may be showing a prompt). Not restarting.");
                    return;
                }
            }

            var started = Process.Start(new ProcessStartInfo
            {
                FileName = ActPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
            });
            if (started == null)
            {
                PrintError("Failed to start ACT.");
                return;
            }

            var seen = new HashSet<IntPtr>();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            DateTime? lastMinimize = null;
            while (DateTime.UtcNow < deadline && !started.HasExited)
            {
                foreach (var w in FindWindows((uint)started.Id, visibleOnly: true, titleStart: ""))
                {
                    var title = GetTitle(w);
                    if (seen.Add(w))
                        Svc.Log.Info($"[ActRestarter] ACT window: \"{title}\" (0x{w.ToInt64():X})");

                    if (title.StartsWith(TitleStart) && !IsIconic(w))
                    {
                        var ok = ShowWindowAsync(w, SW_SHOWMINNOACTIVE);
                        Svc.Log.Info($"[ActRestarter] Minimize \"{title}\" (0x{w.ToInt64():X}) -> {ok}");
                        lastMinimize = DateTime.UtcNow;
                    }
                }

                if (lastMinimize.HasValue && DateTime.UtcNow - lastMinimize.Value > TimeSpan.FromSeconds(30))
                {
                    Print("ACT restarted.");
                    return;
                }
                Thread.Sleep(250);
            }

            Print(lastMinimize.HasValue ? "ACT restarted." : "ACT started, but its window wasn't found to minimize.");
        }

        private static string GetTitle(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            return sb.ToString();
        }

        private static List<IntPtr> FindWindows(uint pid, bool visibleOnly, string titleStart = TitleStart)
        {
            var found = new List<IntPtr>();
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out var p);
                if (p == pid && (!visibleOnly || IsWindowVisible(h)))
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(h, sb, 256);
                    if (sb.ToString().StartsWith(titleStart)) found.Add(h);
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static void Print(string msg) =>
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.Print($"[ToshiBox] {msg}"));

        private static void PrintError(string msg) =>
            Svc.Framework.RunOnFrameworkThread(() => Svc.Chat.PrintError($"[ToshiBox] {msg}"));
    }
}
