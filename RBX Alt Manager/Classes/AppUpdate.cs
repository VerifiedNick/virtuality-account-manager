using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Auto-update from Virtuality Manager's OWN GitHub repo (public). The app compares its built-in
    /// <see cref="BuildNumber"/> with "Download/version.txt" in the repo; if the repo's number is higher it
    /// offers to update, downloads the new exe and replaces itself.
    ///
    ///   start ──check version.txt──▶ newer? ──ask──▶ download new exe to Update\ ──run it with -applyupdate──▶
    ///   new exe waits for the old to close, copies itself over the old exe, starts it again, exits.
    ///
    /// Needs the repo to be PUBLIC (raw.githubusercontent.com). While it is private the check just fails quietly.
    /// To publish a new version: raise BuildNumber here AND put the same number in Download/version.txt, then push.
    /// </summary>
    public static class AppUpdate
    {
        /// <summary>Raise this by one for every released build; must match Download/version.txt in the repo.</summary>
        public const int BuildNumber = 9;

        private const string Base = "https://raw.githubusercontent.com/VerifiedNick/virtuality-manager/main/Download/";
        private const string VersionUrl = Base + "version.txt";
        private const string ExeUrl = Base + "Virtuality%20Manager.exe";

        private static WebClient Web()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var WC = new WebClient();
            WC.Headers["User-Agent"] = "VirtualityManager";
            WC.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            return WC;
        }

        /// <summary>Quietly check at start-up and offer the update if one is available.</summary>
        public static void CheckOnStartup() => _ = Task.Run(() => Check(false));

        /// <summary>"Check for updates" button: also tell the user when nothing is new or the server is unreachable.</summary>
        public static void CheckNow() => _ = Task.Run(() => Check(true));

        private static void Check(bool Interactive)
        {
            try
            {
                int Remote = FetchRemoteBuild();

                if (Remote < 0) { if (Interactive) Info("Updates", "Could not reach the update server. Try again later (the GitHub repo must be public)."); return; }

                if (Remote <= BuildNumber) { if (Interactive) Info("Updates", $"You already have the latest version (build {BuildNumber})."); return; }

                if (Ask($"A new version is available (build {Remote}, you have build {BuildNumber}).\n\nDownload and update now?"))
                    Apply();
            }
            catch (Exception x)
            {
                Program.Logger?.Warn($"[Update] {x.Message}");
                if (Interactive) Info("Updates", "Update failed:\n\n" + x.Message);
            }
        }

        private static int FetchRemoteBuild()
        {
            try
            {
                using (var WC = Web())
                {
                    string Text = WC.DownloadString(VersionUrl + "?t=" + DateTime.UtcNow.Ticks); // the number makes GitHub's cache hand out the newest file, not one from a few minutes ago
                    string Digits = new string((Text ?? string.Empty).Trim().TakeWhile(char.IsDigit).ToArray());
                    return int.TryParse(Digits, out int N) ? N : -1;
                }
            }
            catch { return -1; }
        }

        private static void Apply()
        {
            string Dir = AppDomain.CurrentDomain.BaseDirectory;
            string Staging = Path.Combine(Dir, "Update");
            Directory.CreateDirectory(Staging);
            string NewExe = Path.Combine(Staging, "Virtuality Manager.exe");

            using (var WC = Web()) WC.DownloadFile(ExeUrl + "?t=" + DateTime.UtcNow.Ticks, NewExe);

            // make sure we really downloaded a Windows program, not an error page
            var Bytes = File.ReadAllBytes(NewExe);
            if (Bytes.Length < 200000 || Bytes[0] != (byte)'M' || Bytes[1] != (byte)'Z')
                throw new Exception("the downloaded file is not a valid program (is the repo public?)");

            string Target = Process.GetCurrentProcess().MainModule.FileName;
            int Pid = Process.GetCurrentProcess().Id;

            Process.Start(new ProcessStartInfo(NewExe, $"-applyupdate \"{Target}\" {Pid}")
            {
                UseShellExecute = false,
                WorkingDirectory = Staging
            });

            Environment.Exit(0);
        }

        /// <summary>
        /// Runs inside the freshly downloaded exe (in Update\). Waits for the old window to close,
        /// copies itself over the old exe and starts it again. Called very early from Program.Main.
        /// </summary>
        public static void ApplyUpdateStep(string Target, int OldPid)
        {
            try
            {
                try { Process.GetProcessById(OldPid).WaitForExit(20000); } catch { }

                string Self = Process.GetCurrentProcess().MainModule.FileName;

                // an old background lock holder started from the app folder would keep the exe locked: end it (running games are not affected)
                try
                {
                    foreach (var P in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Target)))
                        try { if (P.Id != Process.GetCurrentProcess().Id && string.Equals(P.MainModule.FileName, Target, StringComparison.OrdinalIgnoreCase)) { P.Kill(); P.WaitForExit(3000); } } catch { }
                }
                catch { }

                for (int i = 0; i < 30; i++)
                {
                    try { File.Copy(Self, Target, true); break; }
                    catch { Thread.Sleep(500); } // old exe may still be locked for a moment
                }

                Process.Start(new ProcessStartInfo(Target)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(Target)
                });
            }
            catch { }

            Environment.Exit(0);
        }

        /// <summary>Remove the leftover Update\ folder from a previous update (called on a normal start).</summary>
        public static void CleanupStaging()
        {
            try
            {
                string Staging = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");
                if (Directory.Exists(Staging)) Directory.Delete(Staging, true);
            }
            catch { }
        }

        private static bool Ask(string Text)
        {
            bool Yes = false;
            try
            {
                AccountManager.Instance.InvokeIfRequired(() =>
                    Yes = ModernDialog.Show(Text, "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes);
            }
            catch { }
            return Yes;
        }

        private static void Info(string Title, string Text)
        {
            try { AccountManager.Instance.InvokeIfRequired(() => ModernDialog.Show(Text, Title, MessageBoxButtons.OK, MessageBoxIcon.Information)); }
            catch { }
        }
    }
}
