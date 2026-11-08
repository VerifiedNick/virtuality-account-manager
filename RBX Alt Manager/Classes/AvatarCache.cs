using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Roblox avatar headshots for the account cards. Downloaded in batches from thumbnails.roblox.com and cached
    /// in the "AvatarCache" folder next to the exe (refreshed after a week). Failures just mean "no picture".
    /// </summary>
    public static class AvatarCache
    {
        private static readonly string Folder = Path.Combine(Environment.CurrentDirectory, "AvatarCache");
        private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly ConcurrentDictionary<long, Image> Images = new ConcurrentDictionary<long, Image>();
        private static readonly ConcurrentDictionary<long, DateTime> Requested = new ConcurrentDictionary<long, DateTime>();
        private static readonly ConcurrentQueue<long> Queue = new ConcurrentQueue<long>();
        private static int Running;

        public static event EventHandler Changed;

        public static Image Get(long UserId)
        {
            if (UserId <= 0) return null;
            if (Images.TryGetValue(UserId, out Image Cached)) return Cached;

            if (!Requested.TryGetValue(UserId, out DateTime Last) || (DateTime.Now - Last).TotalMinutes > 5)
            {
                Requested[UserId] = DateTime.Now;
                Queue.Enqueue(UserId);

                if (System.Threading.Interlocked.Exchange(ref Running, 1) == 0)
                    Task.Run(Work);
            }

            return null;
        }

        private static async Task Work()
        {
            try
            {
                Directory.CreateDirectory(Folder);

                while (!Queue.IsEmpty)
                {
                    await Task.Delay(150); // let a whole screen of cards queue up first

                    var Batch = new List<long>();
                    while (Batch.Count < 50 && Queue.TryDequeue(out long Id))
                        if (!Batch.Contains(Id)) Batch.Add(Id);

                    var Missing = new List<long>();

                    foreach (long Id in Batch)
                    {
                        string File_ = Path.Combine(Folder, Id + ".png");

                        if (File.Exists(File_) && Load(Id, File_) && (DateTime.Now - File.GetLastWriteTime(File_)).TotalDays < 7) continue;

                        Missing.Add(Id);
                    }

                    if (Missing.Count > 0)
                        await Download(Missing);

                    Changed?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (Exception x) { Program.Logger.Warn($"[AvatarCache] {x.Message}"); }
            finally
            {
                System.Threading.Interlocked.Exchange(ref Running, 0);

                if (!Queue.IsEmpty && System.Threading.Interlocked.Exchange(ref Running, 1) == 0)
                    _ = Task.Run(Work);
            }
        }

        private static bool Load(long Id, string FileName)
        {
            try
            {
                byte[] Bytes = File.ReadAllBytes(FileName);

                using (var Stream = new MemoryStream(Bytes))
                using (var Loaded = Image.FromStream(Stream))
                    Images[Id] = new Bitmap(Loaded);

                return true;
            }
            catch { return false; }
        }

        private static async Task Download(List<long> Ids)
        {
            try
            {
                string Json = await Http.GetStringAsync($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={string.Join(",", Ids)}&size=150x150&format=Png&isCircular=false");

                if (!(JObject.Parse(Json)["data"] is JArray Data)) return;

                foreach (JToken Entry in Data)
                {
                    long Id = Entry.Value<long?>("targetId") ?? 0;
                    string Url = Entry.Value<string>("imageUrl");

                    if (Id <= 0 || Entry.Value<string>("state") != "Completed" || string.IsNullOrEmpty(Url))
                    {
                        continue; // still rendering (or blocked): Get() asks again after 5 minutes
                    }

                    try
                    {
                        byte[] Bytes = await Http.GetByteArrayAsync(Url);
                        string FileName = Path.Combine(Folder, Id + ".png");

                        File.WriteAllBytes(FileName, Bytes);
                        Load(Id, FileName);
                    }
                    catch (Exception x) { Program.Logger.Warn($"[AvatarCache] {Id}: {x.Message}"); }
                }
            }
            catch (Exception x) { Program.Logger.Warn($"[AvatarCache] batch failed: {x.Message}"); }
        }
    }
}
