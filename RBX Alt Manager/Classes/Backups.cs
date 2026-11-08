using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Automatic copies of AccountData.json in "Backups" (next to it): one at every start, only when something changed,
    /// the newest 10 are kept. The files stay encrypted exactly like the original.
    /// </summary>
    public static class Backups
    {
        public const int Keep = 10;

        public static string Folder => Path.Combine(Environment.CurrentDirectory, "Backups");
        private static string Data => Path.Combine(Environment.CurrentDirectory, "AccountData.json");

        public static List<FileInfo> List()
        {
            try
            {
                var D = new DirectoryInfo(Folder);
                if (!D.Exists) return new List<FileInfo>();
                return D.GetFiles("AccountData *.json").OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        public static void MakeStartBackup()
        {
            try
            {
                if (!File.Exists(Data)) return;

                byte[] Current = File.ReadAllBytes(Data);
                if (Current.Length == 0) return;

                Directory.CreateDirectory(Folder);

                var Newest = List().FirstOrDefault();
                if (Newest != null && Newest.Length == Current.Length && File.ReadAllBytes(Newest.FullName).SequenceEqual(Current)) return; // nothing changed since the last copy

                File.WriteAllBytes(Path.Combine(Folder, $"AccountData {DateTime.Now:yyyy-MM-dd HH-mm-ss}.json"), Current);

                // only the copies made here are rotated; "before restore" copies are kept
                foreach (var Old in List().Skip(Keep))
                    try { Old.Delete(); } catch { }
            }
            catch (Exception x) { Program.Logger?.Warn($"[Backup] {x.Message}"); }
        }

        /// <summary>Puts a backup back: the current file is first saved as "before restore …".</summary>
        public static void Restore(FileInfo Backup)
        {
            Directory.CreateDirectory(Folder);

            if (File.Exists(Data))
                File.Copy(Data, Path.Combine(Folder, $"Before restore {DateTime.Now:yyyy-MM-dd HH-mm-ss}.json"), true);

            File.Copy(Backup.FullName, Data, true);
        }
    }
}
