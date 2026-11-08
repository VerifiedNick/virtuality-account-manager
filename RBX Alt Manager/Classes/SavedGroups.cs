using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Groups the user created on purpose (Daten\Groups.json). They stay in the sidebar even without accounts,
    /// so a group can be made first and filled later ("Add account" inside the empty group).
    /// Groups that only come from accounts work as before (they disappear when they are empty).
    /// </summary>
    public static class SavedGroups
    {
        private static string FilePath => Path.Combine(Environment.CurrentDirectory, "Groups.json");
        private static List<string> Names;
        private static readonly object Lock = new object();

        private static List<string> Load()
        {
            if (Names != null) return Names;

            try { Names = File.Exists(FilePath) ? JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>() : new List<string>(); }
            catch (Exception x) { Program.Logger?.Warn($"[Groups] {x.Message}"); Names = new List<string>(); }

            return Names;
        }

        private static void Save()
        {
            try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(Names, Formatting.Indented)); }
            catch (Exception x) { Program.Logger?.Warn($"[Groups] could not save: {x.Message}"); }
        }

        public static List<string> All { get { lock (Lock) return Load().ToList(); } }

        public static bool Contains(string Name) { lock (Lock) return Name != null && Load().Any(n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase)); }

        public static void Add(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            lock (Lock) { if (!Load().Any(n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase))) { Names.Add(Name.Trim()); Save(); } }
        }

        public static void Remove(string Name)
        {
            lock (Lock) { if (Load().RemoveAll(n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase)) > 0) Save(); }
        }

        public static void Rename(string Old, string New)
        {
            lock (Lock)
            {
                if (Load().RemoveAll(n => string.Equals(n, Old, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    if (!Names.Any(n => string.Equals(n, New, StringComparison.OrdinalIgnoreCase))) Names.Add(New);
                    Save();
                }
            }
        }
    }
}
