using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    public enum EntryVariant { Normal, Golden, Rainbow, Shiny }

    public class GroupPetInfo
    {
        public string EntryName;     // the catalog name that was matched, null when nothing matched
        public string AssetId;     // Roblox image asset of the entry (golden image for golden variants)
        public EntryVariant Variant;
        public bool FromOverride;  // true when the user picked the entry for this group by hand
    }

    /// <summary>
    /// Finds a picture for every account group by matching the group name (e.g. "G: Rank Sample Name")
    /// against the the catalog entry list from the Big Games API and downloading the entry's thumbnail.
    /// Everything is cached in the "GroupImages" folder next to the exe; failures just mean "no picture".
    /// </summary>
    public static class GroupImages
    {
        private static readonly string CacheFolder = Path.Combine(Environment.CurrentDirectory, "GroupImages");
        private static readonly string CatalogCacheFile = Path.Combine(CacheFolder, "entries.json");
        private static readonly string OverridesFile = Path.Combine(Environment.CurrentDirectory, "GroupImages.json");
        private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(20) };

        private static volatile Dictionary<string, (string Thumb, string Golden, string Name)> Catalog; // lower-case entry name -> asset ids
        private static readonly ConcurrentDictionary<string, Image> Images = new ConcurrentDictionary<string, Image>();
        private static readonly ConcurrentDictionary<string, DateTime> Requested = new ConcurrentDictionary<string, DateTime>();
        private static readonly object OverridesLock = new object();
        private static Dictionary<string, string> Overrides;
        private static Task CatalogTask;

        /// <summary>Raised (on a background thread) whenever new entry data or a new picture became available.</summary>
        public static event EventHandler ImagesChanged;

        public static void Initialize()
        {
            LoadOverrides();

            if (CatalogTask == null)
                CatalogTask = Task.Run(LoadEntries);
        }

        public static string StripGroupPrefix(string Group)
        {
            if (string.IsNullOrWhiteSpace(Group)) return string.Empty;

            string Name = Regex.Replace(Group, @"^\s*\d{1,3}\s+", string.Empty); // RAM's own "1 Name" sort prefix
            Name = Regex.Replace(Name, @"^\s*[A-Za-z0-9]{1,3}\s*[:.)]\s*", string.Empty); // "A: Name" style prefixes

            return Regex.Replace(Name, @"\s+", " ").Trim();
        }

        public static GroupPetInfo Resolve(string Group)
        {
            var Info = new GroupPetInfo();
            var EntryList = Catalog;

            string Override = GetOverride(Group);
            if (IsCustom(Override)) Override = null; // own picture file: no entry behind it
            string Name = Override ?? StripGroupPrefix(Group);

            Info.FromOverride = Override != null;

            if (Regex.IsMatch(Name, @"\bgolden\b", RegexOptions.IgnoreCase)) Info.Variant = EntryVariant.Golden;
            else if (Regex.IsMatch(Name, @"\brainbow\b", RegexOptions.IgnoreCase)) Info.Variant = EntryVariant.Rainbow;
            else if (Regex.IsMatch(Name, @"\bshiny\b", RegexOptions.IgnoreCase)) Info.Variant = EntryVariant.Shiny;

            if (EntryList == null) return Info;

            string Base = Regex.Replace(Name, @"\b(golden|rainbow|shiny)\b", string.Empty, RegexOptions.IgnoreCase);
            Base = Regex.Replace(Base, @"\s+", " ").Trim();

            (string Thumb, string Golden, string Name)? Found = null;

            foreach (string Candidate in Candidates(Base))
                if (EntryList.TryGetValue(Candidate.ToLowerInvariant(), out var Entry)) { Found = Entry; break; }

            if (Found == null && !Info.FromOverride)
                Found = LooseMatch(EntryList, Base);

            if (Found != null)
            {
                Info.EntryName = Found.Value.Name;
                Info.AssetId = Info.Variant == EntryVariant.Golden && !string.IsNullOrEmpty(Found.Value.Golden) ? Found.Value.Golden : Found.Value.Thumb;
            }

            return Info;
        }

        /// <summary>"Rank Sample" -> "Rank Sample Dragon": the shortest Rank whose name contains every word of the group name.</summary>
        private static (string Thumb, string Golden, string Name)? LooseMatch(Dictionary<string, (string Thumb, string Golden, string Name)> EntryList, string Base)
        {
            string Singular = Regex.Replace(Base, @"(?<=\w{3})s$", string.Empty, RegexOptions.IgnoreCase);
            var Words = Singular.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w != "rank").ToList();

            if (Words.Count == 0 || Words.Count > 3 || Words.Any(w => w.Length < 3 || !Regex.IsMatch(w, @"^[a-z]+$"))) return null;

            var Match = EntryList.Values
                .Where(p => p.Name.StartsWith("Rank ", StringComparison.OrdinalIgnoreCase))
                .Where(p => Words.All(w => Regex.IsMatch(p.Name, $@"\b{w}\b", RegexOptions.IgnoreCase)))
                .OrderBy(p => p.Name.Length)
                .FirstOrDefault();

            return Match.Name != null ? Match : ((string, string, string)?)null;
        }

        private static IEnumerable<string> Candidates(string Name)
        {
            if (string.IsNullOrEmpty(Name)) yield break;

            string Singular = Regex.Replace(Name, @"(?<=\w{3})s$", string.Empty, RegexOptions.IgnoreCase); // "Rank Lucky Cats" -> "Rank Lucky Cat"

            yield return Name;
            yield return Singular;

            if (!Name.StartsWith("Rank ", StringComparison.OrdinalIgnoreCase) && !Name.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Rank " + Name;
                yield return "Rank " + Singular;
            }
        }

        private static string CustomFolder => Path.Combine(CacheFolder, "custom");

        /// <summary>True when the group uses a picture file the user chose (not a Rank).</summary>
        public static bool IsCustom(string Override) => Override != null && Override.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

        /// <summary>Own picture for a group: square-cropped, stored as 256 px PNG in GroupImages\custom.</summary>
        public static void SetCustomImage(string Group, string SourcePath)
        {
            Directory.CreateDirectory(CustomFolder);

            string Name = $"{Guid.NewGuid():N}.png";

            using (var Src = Image.FromStream(new MemoryStream(File.ReadAllBytes(SourcePath))))
            using (var Out = new Bitmap(256, 256))
            using (var G = Graphics.FromImage(Out))
            {
                int Side = Math.Min(Src.Width, Src.Height);
                var Crop = new Rectangle((Src.Width - Side) / 2, (Src.Height - Side) / 2, Side, Side);

                G.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                G.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                G.DrawImage(Src, new Rectangle(0, 0, 256, 256), Crop, GraphicsUnit.Pixel);

                Out.Save(Path.Combine(CustomFolder, Name), System.Drawing.Imaging.ImageFormat.Png);
            }

            SetOverride(Group, "file:" + Name);
        }

        /// <summary>Returns the picture for the group, or null if there is none (yet). Missing pictures are fetched in the background.</summary>
        public static Image GetImage(string Group, out GroupPetInfo Info)
        {
            string Own = GetOverride(Group);

            if (IsCustom(Own))
            {
                Info = new GroupPetInfo() { FromOverride = true };

                if (Images.TryGetValue(Own, out Image Mine)) return Mine;

                try
                {
                    var Img = Image.FromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(CustomFolder, Own.Substring(5)))));
                    Images[Own] = Img;
                    return Img;
                }
                catch { return null; }
            }

            Info = Resolve(Group);

            if (string.IsNullOrEmpty(Info.AssetId)) return null;

            if (Images.TryGetValue(Info.AssetId, out Image Cached)) return Cached;

            string AssetId = Info.AssetId;

            if (!Requested.TryGetValue(AssetId, out DateTime Last) || (DateTime.Now - Last).TotalSeconds > 60)
            {
                Requested[AssetId] = DateTime.Now;
                Task.Run(() => LoadImage(AssetId));
            }

            return null;
        }

        public static List<string> FindEntries(string Query, int Max = 6)
        {
            var EntryList = Catalog;

            if (EntryList == null || string.IsNullOrWhiteSpace(Query)) return new List<string>();

            string[] Words = Query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return EntryList.Values
                .Select(p => p.Name)
                .Where(n => Words.All(w => n.ToLowerInvariant().Contains(w)))
                .OrderBy(n => n.StartsWith("Rank", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(n => n.Length)
                .Take(Max)
                .ToList();
        }

        public static bool EntryListReady => Catalog != null;

        public static bool IsKnownEntry(string Name)
        {
            var EntryList = Catalog;

            if (EntryList == null || string.IsNullOrWhiteSpace(Name)) return false;

            string Base = Regex.Replace(Name, @"\b(golden|rainbow|shiny)\b", string.Empty, RegexOptions.IgnoreCase);
            Base = Regex.Replace(Base, @"\s+", " ").Trim();

            return Candidates(Base).Any(c => EntryList.ContainsKey(c.ToLowerInvariant()));
        }

        public static string GetOverride(string Group)
        {
            lock (OverridesLock)
            {
                if (Overrides == null || Group == null) return null;
                if (Overrides.TryGetValue(Group, out string Entry)) return Entry;

                // "A: HSA" in GroupImages.json also matches "a:hsa" or "1 A: HSA" (RAM's number sort prefix)
                string Wanted = NormalizeKey(Group);

                foreach (var Pair in Overrides)
                    if (NormalizeKey(Pair.Key) == Wanted) return Pair.Value;

                return null;
            }
        }

        private static string NormalizeKey(string Group)
        {
            string Key = Regex.Replace(Group ?? string.Empty, @"^\s*\d{1,3}\s+", string.Empty);

            return Regex.Replace(Key, @"\s+", string.Empty).ToLowerInvariant();
        }

        public static void SetOverride(string Group, string EntryName)
        {
            if (Group == null) return;

            lock (OverridesLock)
            {
                Overrides ??= new Dictionary<string, string>();

                if (string.IsNullOrWhiteSpace(EntryName))
                {
                    string Wanted = NormalizeKey(Group);

                    foreach (string Key in Overrides.Keys.Where(k => NormalizeKey(k) == Wanted).ToList())
                        Overrides.Remove(Key);
                }
                else
                    Overrides[Group] = EntryName.Trim();

                try { File.WriteAllText(OverridesFile, JsonConvert.SerializeObject(Overrides, Formatting.Indented)); }
                catch (Exception x) { Program.Logger.Error($"[GroupImages] Could not save group pictures: {x.Message}"); }
            }

            ImagesChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void LoadOverrides()
        {
            lock (OverridesLock)
            {
                if (Overrides != null) return;

                try
                {
                    Overrides = File.Exists(OverridesFile)
                        ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(OverridesFile)) ?? new Dictionary<string, string>()
                        : new Dictionary<string, string>();
                }
                catch (Exception x)
                {
                    Program.Logger.Error($"[GroupImages] Could not read {OverridesFile}: {x.Message}");
                    Overrides = new Dictionary<string, string>();
                }
            }
        }

        private static async Task LoadEntries()
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);

                string Json = null;
                bool CacheFresh = File.Exists(CatalogCacheFile) && (DateTime.Now - File.GetLastWriteTime(CatalogCacheFile)).TotalDays < 3;

                if (CacheFresh)
                    Json = File.ReadAllText(CatalogCacheFile);
                else
                {
                    try
                    {
                        Json = await Http.GetStringAsync("https://catalog.invalid/api/collection/Catalog");

                        if (Parse(Json) != null)
                            File.WriteAllText(CatalogCacheFile, Json);
                    }
                    catch (Exception x)
                    {
                        Program.Logger.Warn($"[GroupImages] Entry list download failed: {x.Message}");

                        if (File.Exists(CatalogCacheFile)) Json = File.ReadAllText(CatalogCacheFile); // an old list is better than none
                    }
                }

                var Parsed = Json != null ? Parse(Json) : null;

                if (Parsed != null)
                {
                    Catalog = Parsed;
                    ImagesChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (Exception x) { Program.Logger.Error($"[GroupImages] {x}"); }
        }

        private static Dictionary<string, (string, string, string)> Parse(string Json)
        {
            try
            {
                if (!(JObject.Parse(Json)["data"] is JArray Data)) return null;

                var Result = new Dictionary<string, (string, string, string)>();

                foreach (JToken Entry in Data)
                {
                    string Name = Entry.Value<string>("configName");
                    JToken Config = Entry["configData"];

                    if (string.IsNullOrEmpty(Name) || Config == null) continue;

                    string Thumb = AssetIdOf(Config.Value<string>("thumbnail"));
                    string Golden = AssetIdOf(Config.Value<string>("goldenThumbnail"));

                    if (Thumb == null && Golden == null) continue;

                    Result[Name.ToLowerInvariant()] = (Thumb ?? Golden, Golden, Name);
                }

                return Result.Count > 0 ? Result : null;
            }
            catch { return null; }
        }

        private static string AssetIdOf(string Value)
        {
            if (string.IsNullOrEmpty(Value)) return null;

            Match match = Regex.Match(Value, @"\d{3,}");

            return match.Success ? match.Value : null;
        }

        private static async Task LoadImage(string AssetId)
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);

                string FileName = Path.Combine(CacheFolder, AssetId + ".png");
                byte[] Bytes = null;

                if (File.Exists(FileName))
                    Bytes = File.ReadAllBytes(FileName);
                else
                {
                    string Info = await Http.GetStringAsync($"https://thumbnails.roblox.com/v1/assets?assetIds={AssetId}&returnPolicy=PlaceHolder&size=150x150&format=Png&isCircular=false");
                    JToken Entry = (JObject.Parse(Info)["data"] as JArray)?.FirstOrDefault();

                    if (Entry == null || Entry.Value<string>("state") != "Completed" || string.IsNullOrEmpty(Entry.Value<string>("imageUrl")))
                        return; // Roblox is still rendering the thumbnail, it will be requested again later

                    Bytes = await Http.GetByteArrayAsync(Entry.Value<string>("imageUrl"));

                    File.WriteAllBytes(FileName, Bytes);
                }

                using (var Stream = new MemoryStream(Bytes))
                using (var Loaded = Image.FromStream(Stream))
                    Images[AssetId] = new Bitmap(Loaded);

                ImagesChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception x) { Program.Logger.Warn($"[GroupImages] Picture {AssetId} failed: {x.Message}"); }
        }
    }
}
