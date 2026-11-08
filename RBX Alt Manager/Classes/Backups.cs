using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Automatic copies of AccountData.json, kept in two places:
    ///   1. "Backups" next to the data (easy to find, "Open folder" in Settings)
    ///   2. %LocalAppData%\Virtuality Manager\Backups – OUTSIDE the app folder, so the accounts survive
    ///      even if the whole Virtuality Manager folder gets deleted.
    /// A copy is made at every start, when the manager closes and at most every 30 minutes while saving –
    /// only when something changed. The files stay encrypted exactly like the original (same PIN).
    /// If AccountData.json is missing at start but a safe copy exists, the manager offers to put it back.
    /// </summary>
    public static class Backups
    {
        public const int Keep = 10, KeepSafe = 20;

        public static string Folder => Path.Combine(Environment.CurrentDirectory, "Backups");
        public static string SafeFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Virtuality Manager", "Backups");
        private static string Data => Path.Combine(Environment.CurrentDirectory, "AccountData.json");

        private static readonly object Gate = new object();
        private static DateTime LastSaveBackup = DateTime.MinValue;

        private static List<FileInfo> ListIn(string Dir)
        {
            try
            {
                var D = new DirectoryInfo(Dir);
                if (!D.Exists) return new List<FileInfo>();
                return D.GetFiles("AccountData *.json").OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        /// <summary>All copies of both places, newest first (a copy that exists in both places is listed once).</summary>
        public static List<FileInfo> List()
        {
            var Local = ListIn(Folder);
            var Names = new HashSet<string>(Local.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            return Local.Concat(ListIn(SafeFolder).Where(f => !Names.Contains(f.Name)))
                .OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
        }

        public static void MakeStartBackup() => MakeBackup("start");

        /// <summary>Called after every save: makes a copy at most every 30 minutes.</summary>
        public static void AfterSave()
        {
            if ((DateTime.Now - LastSaveBackup).TotalMinutes < 30) return;
            MakeBackup("save");
        }

        public static void MakeBackup(string Why)
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(Data)) return;

                    byte[] Current = File.ReadAllBytes(Data);
                    if (Current.Length == 0) return;

                    LastSaveBackup = DateTime.Now;
                    string Name = $"AccountData {DateTime.Now:yyyy-MM-dd HH-mm-ss}.json";

                    WriteCopy(Folder, Name, Current, Keep);
                    WriteCopy(SafeFolder, Name, Current, KeepSafe);
                }
                catch (Exception x) { Program.Logger?.Warn($"[Backup] {Why}: {x.Message}"); }
            }
        }

        private static void WriteCopy(string Dir, string Name, byte[] Current, int KeepCount)
        {
            try
            {
                Directory.CreateDirectory(Dir);

                var Newest = ListIn(Dir).FirstOrDefault();
                if (Newest != null && Newest.Length == Current.Length && File.ReadAllBytes(Newest.FullName).SequenceEqual(Current)) return; // nothing changed since the last copy

                File.WriteAllBytes(Path.Combine(Dir, Name), Current);

                // only the automatic copies are rotated; "Before restore" copies are kept
                foreach (var Old in ListIn(Dir).Skip(KeepCount))
                    try { Old.Delete(); } catch { }
            }
            catch (Exception x) { Program.Logger?.Warn($"[Backup] {Dir}: {x.Message}"); }
        }

        /// <summary>Puts a backup back: the current file is first saved as "Before restore …".</summary>
        public static void Restore(FileInfo Backup)
        {
            Directory.CreateDirectory(Folder);

            if (File.Exists(Data))
                File.Copy(Data, Path.Combine(Folder, $"Before restore {DateTime.Now:yyyy-MM-dd HH-mm-ss}.json"), true);

            File.Copy(Backup.FullName, Data, true);
        }

        /// <summary>
        /// Start without AccountData.json (app folder deleted / new folder) but a safe copy exists:
        /// ask once per start whether to put the newest copy back. "No" starts empty, the copies stay.
        /// </summary>
        public static void OfferRecovery()
        {
            try
            {
                if (File.Exists(Data)) return;

                var Newest = ListIn(SafeFolder).FirstOrDefault();
                if (Newest == null) return;

                var Answer = ModernDialog.Show(
                    $"There are no accounts in this folder, but a backup of your accounts was found from {Newest.LastWriteTime:dd.MM.yyyy HH:mm}.\n\n" +
                    "Put it back? You unlock it with the PIN you had back then.\n\nNo = start empty (the backup stays where it is).",
                    "Backup found", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question);

                if (Answer != System.Windows.Forms.DialogResult.Yes) return;

                File.Copy(Newest.FullName, Data, false);
                Program.Logger?.Info($"[Backup] restored {Newest.Name} after AccountData.json was missing");
            }
            catch (Exception x) { Program.Logger?.Warn($"[Backup] recovery: {x.Message}"); }
        }
    }
}
