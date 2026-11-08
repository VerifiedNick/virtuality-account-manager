using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Multi Roblox: Roblox only opens a second window while somebody else holds its lock "ROBLOX_singletonMutex".
    /// The lock is held by a small background copy of the manager ("-holdroblox", no window) instead of the manager itself,
    /// so restarting or updating the manager never releases it – running games are never closed because of a restart.
    /// The holder ends by itself as soon as neither the manager nor any Roblox window is open any more.
    ///
    /// States:  no holder ──Ensure()──▶ starting ──lock taken──▶ holding ──(manager + Roblox gone | Release())──▶ no holder
    /// </summary>
    public static class RobloxLock
    {
        private const string RobloxMutex = "ROBLOX_singletonMutex";
        private const string HolderMarker = "VirtualityManager_RobloxLockHolder";
        private const string ReleaseEvent = "VirtualityManager_RobloxLockRelease";

        private static Process Starting;

        public static bool HolderRunning
        {
            get
            {
                try
                {
                    if (Mutex.TryOpenExisting(HolderMarker, out Mutex M)) { M.Dispose(); return true; }
                }
                catch { }
                return false;
            }
        }

        /// <summary>Manager side: makes sure the holder runs. True = multi Roblox works. Waits at most ~3 s.</summary>
        public static bool Ensure()
        {
            if (HolderRunning) return true;

            try
            {
                if (Starting == null || Starting.HasExited)
                    Starting = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "-holdroblox")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                    });
            }
            catch (Exception x) { Program.Logger.Warn($"[MultiRoblox] could not start the lock holder: {x.Message}"); return false; }

            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(100);
                if (HolderRunning) return true;
                if (Starting != null && Starting.HasExited) break;
            }

            return HolderRunning;
        }

        /// <summary>Multi Roblox switched off: the holder lets the lock go.</summary>
        public static void Release()
        {
            try { using (var E = new EventWaitHandle(false, EventResetMode.ManualReset, ReleaseEvent)) E.Set(); } catch { }
        }

        private static FileStream TakeCookieFile()
        {
            try
            {
                string FN = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Roblox\LocalStorage\RobloxCookies.dat");

                if (!File.Exists(FN) || File.Exists(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "no773fix.txt"))) return null;

                return new FileStream(FN, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch { return null; } // still in use – tried again in 3 s
        }

        /// <summary>Holder side (the "-holdroblox" process). No window, no log, no data files.</summary>
        public static void Run()
        {
            Mutex Lock = null, Marker = null;
            bool Owned = false;
            FileStream CookieLock = null;

            try
            {
                Lock = new Mutex(false, RobloxMutex);

                try { Owned = Lock.WaitOne(10000); }
                catch (AbandonedMutexException) { Owned = true; } // the last holder (older manager) is gone – the lock is ours now

                if (!Owned) return; // a Roblox window started without the manager holds it

                Marker = new Mutex(true, HolderMarker, out bool IsNew);
                if (!IsNew) return; // another holder already runs

                using (var Release = new EventWaitHandle(false, EventResetMode.ManualReset, ReleaseEvent))
                {
                    Release.Reset();

                    var Me = Process.GetCurrentProcess();

                    while (!Release.WaitOne(3000))
                    {
                        // error 773 fix: nobody else may write Roblox's cookie file while several accounts run. The manager takes it when it starts;
                        // if Roblox was already open then (or the manager restarted), the holder takes it as soon as it is free – and keeps it through restarts.
                        if (CookieLock == null) CookieLock = TakeCookieFile();

                        bool Manager = Process.GetProcessesByName(Me.ProcessName).Any(p => p.Id != Me.Id);
                        bool Roblox = Process.GetProcessesByName("RobloxPlayerBeta").Length > 0;

                        if (!Manager && !Roblox) break;
                    }
                }
            }
            catch { }
            finally
            {
                try { CookieLock?.Dispose(); } catch { }
                if (Owned) try { Lock.ReleaseMutex(); } catch { }
                Marker?.Dispose();
                Lock?.Dispose();
            }
        }
    }
}
