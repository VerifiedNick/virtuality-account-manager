using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// "Same server" helper: reads the public server list of a place and picks one with enough free slots
    /// for all accounts that are about to join, and tells whether a known server still has room.
    /// </summary>
    public static class ServerPicker
    {
        public class Server
        {
            public string Id;
            public int Playing, MaxPlayers;
            public int Free => Math.Max(0, MaxPlayers - Playing);
        }

        private static readonly RestClient Games = new RestClient("https://games.roblox.com/");
        private static readonly Dictionary<(long, bool), (DateTime When, List<Server> List)> Cache = new Dictionary<(long, bool), (DateTime, List<Server>)>();

        /// <summary>Busiest servers first (Roblox sorts by players) – or emptiest first with Emptiest – up to Pages × 100, cached for 10 s.</summary>
        public static async Task<List<Server>> Fetch(long PlaceId, int Pages, CancellationToken Token, bool Emptiest = false)
        {
            lock (Cache)
                if (Cache.TryGetValue((PlaceId, Emptiest), out var C) && (DateTime.Now - C.When).TotalSeconds < 10 && C.List.Count > 0)
                    return C.List;

            var List = new List<Server>();
            string Cursor = null;

            for (int Page = 0; Page < Pages && !Token.IsCancellationRequested; Page++)
            {
                var Request = new RestRequest($"v1/games/{PlaceId}/servers/Public").AddQueryParameter("sortOrder", Emptiest ? "Asc" : "Desc").AddQueryParameter("limit", "100");
                if (!string.IsNullOrEmpty(Cursor)) Request.AddQueryParameter("cursor", Cursor);

                var Response = await Games.ExecuteAsync(Request, Token);

                if ((int)Response.StatusCode == 429) { await Task.Delay(1500, Token); Page--; continue; }
                if (!Response.IsSuccessful || string.IsNullOrEmpty(Response.Content)) break;

                var Data = JObject.Parse(Response.Content);

                foreach (var S in Data["data"] ?? new JArray())
                    List.Add(new Server() { Id = S.Value<string>("id"), Playing = S.Value<int?>("playing") ?? 0, MaxPlayers = S.Value<int?>("maxPlayers") ?? 0 });

                Cursor = Data.Value<string>("nextPageCursor");
                if (string.IsNullOrEmpty(Cursor)) break;
            }

            lock (Cache) Cache[(PlaceId, Emptiest)] = (DateTime.Now, List);
            return List;
        }

        /// <summary>
        /// A server where all accounts fit (plus 2 spare slots, so strangers joining at the same time don't push one out).
        /// The busiest of those, so the accounts end up together in a lively server. Null if none fits.
        /// </summary>
        public static async Task<string> PickFor(long PlaceId, int Accounts, CancellationToken Token)
        {
            try
            {
                var List = await Fetch(PlaceId, 2, Token);
                var Best = Fit(List, Accounts);

                // the busiest 200 are all too full (big games) → look at the emptiest ones instead, they have room
                if (Best == null && !Token.IsCancellationRequested)
                {
                    var Empty = await Fetch(PlaceId, 1, Token, true);
                    Best = Fit(Empty, Accounts) ?? Empty.Where(s => s.MaxPlayers > 0 && s.Free >= 2).OrderByDescending(s => s.Free).FirstOrDefault();
                    List = List.Concat(Empty).ToList();
                }

                Program.Logger.Info($"[SameServer] {List.Count} servers read, picked {Best?.Id ?? "none"} ({Best?.Playing}/{Best?.MaxPlayers}) for {Accounts} account(s)");
                return Best?.Id;
            }
            catch (Exception x) { Program.Logger.Warn($"[SameServer] server list: {x.Message}"); return null; }
        }

        private static Server Fit(List<Server> List, int Accounts) =>
            List.Where(s => s.MaxPlayers > 0 && s.Free >= Accounts + 2).OrderByDescending(s => s.Playing).FirstOrDefault();

        /// <summary>True / false when the server is in the list; null when it can't be told (not in the first pages).</summary>
        public static async Task<bool?> HasRoom(long PlaceId, string JobId, CancellationToken Token)
        {
            try
            {
                var S = (await Fetch(PlaceId, 3, Token)).FirstOrDefault(s => string.Equals(s.Id, JobId, StringComparison.OrdinalIgnoreCase));
                return S == null ? (bool?)null : S.Free > 0;
            }
            catch { return null; }
        }

        /// <summary>Forget the cached list (after a join the player counts change).</summary>
        public static void Forget(long PlaceId) { lock (Cache) { Cache.Remove((PlaceId, false)); Cache.Remove((PlaceId, true)); } }
    }
}
