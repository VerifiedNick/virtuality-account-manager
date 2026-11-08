using log4net;
using Microsoft.Win32;
using RBX_Alt_Manager.Properties;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;

namespace RBX_Alt_Manager
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        public static readonly ILog Logger = LogManager.GetLogger("Account Manager");
        public static bool Closed = false; // RobloxProcess.cs would cause the program to chill in the background as long roblox was also running
        public static bool Elevated;
        public static float Scale
        {
            get
            {
                float _Scale = (AccountManager.General != null && AccountManager.General.Exists("WindowScale")) ? AccountManager.General.Get<float>("WindowScale") : 0f;

                if (_Scale > 3f) AccountManager.General.RemoveProperty("WindowScale");

                if (_Scale > 0 && _Scale <= 3)
                    return AccountManager.General.Get<float>("WindowScale");

                return 1f;
            }
        }

        public static bool ScaleFonts
        {
            get
            {
                if (AccountManager.General != null && AccountManager.General.Exists("ScaleFonts"))
                    return AccountManager.General.Get<bool>("ScaleFonts");

                return true;
            }
        }

        private static string ExeFolder => Path.GetDirectoryName(Application.ExecutablePath);

        /// <summary>
        /// Virtuality Manager keeps the program at the top of its folder and every account / settings file in "Daten".
        /// An old flat folder (data next to the exe) keeps working as before.
        /// First start in a new folder: "migrate-from.txt" names the old folder, its data is COPIED (the old folder stays as it is).
        /// </summary>
        private static void SetupDataFolder()
        {
            try
            {
                string Exe = ExeFolder;

                if (File.Exists(Path.Combine(Exe, "AccountData.json")) || File.Exists(Path.Combine(Exe, "RAMSettings.ini")))
                {
                    Environment.CurrentDirectory = Exe;
                    return;
                }

                string Data = Path.Combine(Exe, "Daten");
                Directory.CreateDirectory(Data);

                string Marker = Path.Combine(Exe, "migrate-from.txt");

                if (File.Exists(Marker) && !File.Exists(Path.Combine(Data, "AccountData.json")))
                {
                    string From = File.ReadAllText(Marker).Trim().Trim('"');

                    if (Directory.Exists(From) && File.Exists(Path.Combine(From, "AccountData.json")))
                    {
                        int Copied = 0, Failed = 0;
                        CopyFolder(new DirectoryInfo(From), Data, true, ref Copied, ref Failed);

                        if (File.Exists(Path.Combine(Data, "AccountData.json")))
                        {
                            File.WriteAllText(Path.Combine(Data, "Umzug.txt"), $"Daten kopiert am {DateTime.Now:dd.MM.yyyy HH:mm} aus:\r\n{From}\r\n\r\n{Copied} Dateien kopiert, {Failed} übersprungen (z.B. gesperrte Browser-Cache-Dateien).\r\nDer alte Ordner wurde nicht verändert.");
                            File.Delete(Marker);
                        }
                    }
                }

                Environment.CurrentDirectory = Data;
            }
            catch (Exception x)
            {
                try { File.AppendAllText(Path.Combine(ExeFolder, "start-error.txt"), $"{DateTime.Now}: {x}\r\n"); } catch { }
            }
        }

        private static readonly string[] NotData = { "Roblox Account Manager.exe", "Roblox Account Manager.exe.config", "Roblox Account Manager Modern.exe", "Roblox Account Manager Modern.exe.config", "Virtuality Manager.exe", "Virtuality Manager.exe.config", "Auto Update.exe", "AU.exe", "libsodium.dll", "log4.config", "log.txt", "log.txt.1", "RAM-Modern-Quellcode.zip", "loader-proxy-rs.log" };

        private static void CopyFolder(DirectoryInfo From, string To, bool Top, ref int Copied, ref int Failed)
        {
            Directory.CreateDirectory(To);

            foreach (var F in From.GetFiles())
            {
                if (Top && Array.IndexOf(NotData, F.Name) >= 0) continue;

                string Target = Path.Combine(To, F.Name);
                if (File.Exists(Target)) continue; // never overwrite

                try { F.CopyTo(Target); Copied++; }
                catch { Failed++; }
            }

            foreach (var D in From.GetDirectories())
            {
                try { CopyFolder(D, Path.Combine(To, D.Name), false, ref Copied, ref Failed); }
                catch { Failed++; }
            }
        }

#if !DEBUG
        private static readonly Mutex mutex = new Mutex(true, "{93b3858f-3dac-4dc0-99cb-0476efc5adce}");
#endif

        [STAThread]
        static void Main(params string[] Arguments)
        {
            int Stupid = 1337;

            SetupDataFolder();
            RBX_Alt_Manager.Classes.AppIcon.ApplyAsDefault();

            try
            {
                if (Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName.Contains(Path.GetTempPath().Remove(Path.GetTempPath().Length - 1)))
                {
                    ModernDialog.Show("Roblox Account Manager must be extracted in order to function correctly!", "Roblox Account Manager", MessageBoxButtons.OK);
                    Environment.Exit(Stupid);
                }
            }
            catch { }

            try
            {
                if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                    Elevated = true;

                // ModernDialog.Show("Some features may not work properly if you ran the account manager as admin!", "Roblox Account Manager", MessageBoxButtons.OK, MessageBoxIcon.Error); // I don't think this is an issue anymore
            }
            catch { }

#if DEBUG
            bool IsAutoUpdater = true;
#else
            bool IsAutoUpdater = Application.ExecutablePath.EndsWith("Auto Update.exe");
#endif

            if (!IsAutoUpdater && !File.Exists($"{Application.ExecutablePath}.config"))
            {
                var Parent = Directory.GetParent(Application.ExecutablePath);
                var Files = Parent.GetFiles();

                if (!File.Exists(Path.Combine(Parent.FullName, "AccountData.json")) && !File.Exists(Path.Combine(Environment.CurrentDirectory, "AccountData.json")) && Files.Length > 1)
                {
                    if (!Utilities.YesNoPrompt("Roblox Account Manager", "It is recommended you install Roblox Account Manager to it's own folder", "Skip this check and install here anyways?", false))
                        Environment.Exit(4);
                }
            }

            if (Arguments.Length == 1 && Arguments[0] == "-update")
            {
                if (!IsAutoUpdater)
                {
                    string AFN = Path.Combine(Directory.GetCurrentDirectory(), "Auto Update.exe");

                    File.WriteAllBytes(AFN, File.ReadAllBytes(Application.ExecutablePath));
                    Process.Start(AFN, "-update");
                    Environment.Exit(2);
                }

                static void StartUpdater() {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Auto_Update.AutoUpdater());
                }

                if (!Elevated)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(Application.ExecutablePath) { Arguments = "-update", Verb = "runas" });
                        Environment.Exit(0);
                    }
                    catch{ StartUpdater(); }
                }
                else
                    StartUpdater();

                return;
            }

            Application.ApplicationExit += (s, e) => Closed = true;

            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "RAMTheme.ini")))
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "RAMTheme.ini"), Resources.DefaultTheme);

            if (!(Arguments.Length == 1 && Arguments[0] == "-restart"))
            {
                string AppConfigPath = $"{Application.ExecutablePath}.config";
                string LogConfigPath = Path.Combine(ExeFolder, "log4.config"); // log4net reads it next to the exe
                string AppConfigHash = Utilities.FileSHA256(AppConfigPath);
                string LogConfigHash = Utilities.FileSHA256(LogConfigPath);

                bool AppConfigValid = File.Exists(AppConfigPath) && AppConfigHash == Resources.AppConfigHash;
                bool Log4ConfigValid = File.Exists(LogConfigPath) && LogConfigHash == Resources.Log4ConfigHash;

                if (!AppConfigValid || !Log4ConfigValid)
                {
                    Logger.Warn($"Restarting app due to config hash mismatch\n[App.config]\n\tCurrent:  {AppConfigHash}\n\tExpected: {Resources.AppConfigHash}\n[log4.config]\n\tCurrent:  {LogConfigHash}\n\tExpected: {Resources.Log4ConfigHash}");

                    File.WriteAllBytes(AppConfigPath, Resources.App);
                    File.WriteAllBytes(LogConfigPath, Resources.log4);

                    Process.Start(Application.ExecutablePath, "-restart");
                    Environment.Exit(0);
                }
            }

            if (!File.Exists(Path.Combine(ExeFolder, "libsodium.dll")))
                File.WriteAllBytes(Path.Combine(ExeFolder, "libsodium.dll"), Resources.libsodium);

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

            using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)?.OpenSubKey(subkey))
            {
                if (ndpKey == null || ndpKey.GetValue("Release") == null || (int)ndpKey.GetValue("Release") < 461808)
                {
                    if (ModernDialog.Show("Failed to detect .NET Framework 4.7.2, would you like to install it now?\n(Required in order to use the account manager)\nIn case this error persists, fully re-install the account manager.", "Roblox Account Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        Process.Start("https://dotnet.microsoft.com/en-us/download/dotnet-framework");
                }
            }

#if !DEBUG
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
#endif
                try
                {
                    string CookiesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Roblox\LocalStorage\RobloxCookies.dat");
                    bool Apply773Fix = !(string.IsNullOrEmpty(CookiesFile) || !File.Exists(CookiesFile) || File.Exists(Path.Combine(Environment.CurrentDirectory, "no773fix.txt")));

                    if (!Apply773Fix) Logger.Error($"Not applying 773 error fix | Cookies File Exists: {File.Exists(CookiesFile)} | User No Fix File Exists: {File.Exists(Path.Combine(Environment.CurrentDirectory, "no773fix.txt"))}");

                    if (Apply773Fix) try { using (new FileStream(CookiesFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } } catch { Apply773Fix = false; } // Check if the file is already locked by another program

                    using (Apply773Fix ? new FileStream(CookiesFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null)
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new AccountManager());
                    }
                }
#if DEBUG
            finally { }
#else
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            else
                ModernDialog.Show("Roblox Account Manager is already running!", "Roblox Account Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
        }
    }
}