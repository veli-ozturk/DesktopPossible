using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Desktop_Frames
{
    /// <summary>
    /// Stacking order of the frame band. Frames are desktop furniture: the whole band sits
    /// just above the wallpaper and below every application window, in three layers —
    ///   bottom:  Text frames (wallpaper text; no ordering among themselves),
    ///   above:   content frames (Data / Portal / Image, and future widgets) in their
    ///            persisted per-frame <c>ZOrder</c>, highest on top,
    ///   top:     everything else on the desktop (apps), which we never touch.
    /// Pinned (AlwaysOnTop) frames are left alone. Clicking a content frame while Frame Edit
    /// Mode is on moves it to the top of the band; outside Edit Mode the order never changes.
    /// </summary>
    public static class FrameZOrder
    {
        public const string Key = "ZOrder";

        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_BOTTOM = new(1);

        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        /// <summary>Called right after any frame window is shown: text frames get their mode, then the band is restacked.</summary>
        public static void AfterShown(NonActivatingWindow win, dynamic frame)
        {
            if (TextFramemanager.IsTextFrame(frame))
            {
                TextFramemanager.ApplyEditMode(win, frame, SettingsManager.FrameEditMode);
                TextFramemanager.Refresh(frame); // first render ran before the window existed: size it now
            }
            ApplyAll();
        }

        /// <summary>A click on a frame in Edit Mode: raise it to the top of the content band and persist.</summary>
        public static void BringToFront(NonActivatingWindow win)
        {
            try
            {
                if (win == null || !SettingsManager.FrameEditMode) return;
                dynamic? frame = Live(win.Tag?.ToString());
                if (frame == null || TextFramemanager.IsTextFrame(frame)) return;

                int top = FrameDataManager.FrameData.Select(f => ReadZ(f)).DefaultIfEmpty(0).Max();
                if (ReadZ(frame) == top && CountAt(top) == 1) return; // already on top

                SetZ(frame, top + 1);
                FrameDataManager.SaveFrameData();
                ApplyAll();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warn, LogManager.LogCategory.UI, $"FrameZOrder.BringToFront: {ex.Message}");
            }
        }

        /// <summary>
        /// Restacks every open, non-pinned frame window from the persisted order. Pushing to
        /// HWND_BOTTOM in DESCENDING order leaves the lowest window at the very bottom, so:
        /// content frames highest-first, then every text frame last.
        /// </summary>
        public static void ApplyAll()
        {
            try
            {
                if (Application.Current == null) return;
                var windows = Application.Current.Windows.OfType<NonActivatingWindow>().Where(w => !w.Topmost).ToList();
                if (windows.Count == 0) return;

                var content = new List<(int Z, NonActivatingWindow Win)>();
                var text = new List<NonActivatingWindow>();
                bool assigned = false;
                int next = FrameDataManager.FrameData.Select(f => ReadZ(f)).DefaultIfEmpty(0).Max();

                foreach (var win in windows)
                {
                    dynamic? frame = Live(win.Tag?.ToString());
                    if (frame == null) continue;
                    if (TextFramemanager.IsTextFrame(frame)) { text.Add(win); continue; }

                    int z = ReadZ(frame);
                    if (z <= 0)
                    {
                        // New or migrated frame: append on top of the band, once.
                        z = ++next;
                        SetZ(frame, z);
                        assigned = true;
                    }
                    content.Add((z, win));
                }
                if (assigned) FrameDataManager.SaveFrameData();

                foreach (var (_, win) in content.OrderByDescending(c => c.Z)) PushToBottom(win);
                foreach (var win in text) PushToBottom(win);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warn, LogManager.LogCategory.UI, $"FrameZOrder.ApplyAll: {ex.Message}");
            }
        }

        // "Show desktop" (Win+D, the taskbar corner) raises Explorer's desktop window over every
        // normal window, frames included, without minimizing or hiding them. While the desktop
        // is the foreground window the band is lifted into the topmost layer; as soon as any
        // other app comes forward it is dropped back to the bottom.
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, WINEVENT_OUTOFCONTEXT = 0;
        private static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2);
        private delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        private static WinEventProc? _foregroundProc; // held so the GC can't collect the callback
        private static IntPtr _foregroundHook;
        private static bool _raisedOverDesktop;

        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);

        /// <summary>Starts following the foreground window (UI thread: out-of-context events arrive on its message loop).</summary>
        public static void StartShowDesktopWatch()
        {
            if (_foregroundHook != IntPtr.Zero) return;
            _foregroundProc = OnForegroundChanged;
            _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _foregroundProc, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private static void OnForegroundChanged(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == (uint)Environment.ProcessId) return; // our own windows: keep the current layer

                var cls = new System.Text.StringBuilder(32);
                GetClassName(hwnd, cls, cls.Capacity);
                bool desktop = IsDesktopClass(cls.ToString());
                if (desktop == _raisedOverDesktop) return;
                _raisedOverDesktop = desktop;

                if (desktop) RaiseOverDesktop();
                else { SetBand(HWND_NOTOPMOST, Band()); ApplyAll(); }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Warn, LogManager.LogCategory.UI, $"FrameZOrder.OnForegroundChanged: {ex.Message}");
            }
        }

        public static bool IsDesktopClass(string cls) => cls == "Progman" || cls == "WorkerW";

        /// <summary>Inserting at HWND_TOPMOST puts each window on top, so go bottom-up: text frames, then content by ascending Z.</summary>
        private static void RaiseOverDesktop()
        {
            var band = Band();
            // Casts: a dynamic argument makes the calls dynamic, and OrderBy can't compare dynamic keys.
            var text = band.Where(w => (bool)TextFramemanager.IsTextFrame(Live(w.Tag?.ToString()))).ToList();
            var content = band.Except(text).OrderBy(w => (int)ReadZ(Live(w.Tag?.ToString())));

            // The UI thread's default IME window is owned by whichever frame last had focus, and
            // Windows silently refuses to make a window topmost while it owns that IME window.
            // Unowning it is harmless: Windows re-owns it the next time a window takes focus.
            if (band.Count > 0)
            {
                var ime = ImmGetDefaultIMEWnd(new WindowInteropHelper(band[0]).Handle);
                if (ime != IntPtr.Zero) SetWindowLongPtr(ime, GWLP_HWNDPARENT, IntPtr.Zero);
            }

            SetBand(HWND_TOPMOST, text.Concat(content));
        }

        private const int GWLP_HWNDPARENT = -8;
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        private static List<NonActivatingWindow> Band() =>
            Application.Current?.Windows.OfType<NonActivatingWindow>().Where(w => !w.Topmost && Live(w.Tag?.ToString()) != null).ToList() ?? new();

        private static void SetBand(IntPtr insertAfter, IEnumerable<NonActivatingWindow> windows)
        {
            foreach (var win in windows)
            {
                var hwnd = new WindowInteropHelper(win).Handle;
                if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        private static void PushToBottom(Window win)
        {
            try
            {
                var hwnd = new WindowInteropHelper(win).Handle;
                if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }

        public static int ReadZ(dynamic frame)
        {
            try
            {
                object? v = frame is IDictionary<string, object> d ? (d.TryGetValue(Key, out var o) ? o : null)
                          : frame is Newtonsoft.Json.Linq.JObject jo ? jo[Key] : null;
                return v != null && int.TryParse(v.ToString(), out int z) ? z : 0;
            }
            catch { return 0; }
        }

        private static void SetZ(dynamic frame, int z)
        {
            if (frame is IDictionary<string, object> d) d[Key] = z;
            else if (frame is Newtonsoft.Json.Linq.JObject jo) jo[Key] = z;
        }

        private static int CountAt(int z) => FrameDataManager.FrameData.Count(f => ReadZ(f) == z);

        private static dynamic? Live(string? frameId) =>
            string.IsNullOrEmpty(frameId) ? null : FrameDataManager.FrameData.FirstOrDefault(f => f.Id?.ToString() == frameId);
    }
}
