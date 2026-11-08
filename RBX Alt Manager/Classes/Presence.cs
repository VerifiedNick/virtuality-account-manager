#pragma warning disable CS8632
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    public static class Presence
    {
        public static Dictionary<UserPresenceType, Color> Colors = new Dictionary<UserPresenceType, Color> {
            { UserPresenceType.Online, Color.FromArgb(255, 0, 162, 255) },
            { UserPresenceType.InGame, Color.FromArgb(255, 2, 183, 87) },
            { UserPresenceType.InStudio, Color.FromArgb(255, 70, 41, 216) }
        };

        public static RestClient PresenceClient = new RestClient("https://presence.roblox.com/");

        /// <summary>Asks without a cookie first (as RAM always did); if Roblox wants a login for it, asks again as the last used account.</summary>
        private static async Task<RestResponse> Request(long[] UserIds)
        {
            var Response = await PresenceClient.ExecuteAsync(new RestRequest("v1/presence/users", Method.Post).AddJsonBody(new { userIds = UserIds }));

            Account Auth = AccountManager.LastValidAccount;

            if (Response.IsSuccessful || Auth == null) return Response;

            RestRequest Make(string Token)
            {
                var R = Auth.MakeRequest("v1/presence/users", Method.Post).AddJsonBody(new { userIds = UserIds });
                if (!string.IsNullOrEmpty(Token)) R.AddHeader("X-CSRF-TOKEN", Token);
                return R;
            }

            var Second = await PresenceClient.ExecuteAsync(Make(Auth.CSRFToken));

            if ((int)Second.StatusCode == 403 && Second.Headers?.FirstOrDefault(h => string.Equals(h.Name, "x-csrf-token", StringComparison.OrdinalIgnoreCase))?.Value is string NewToken)
            {
                Auth.CSRFToken = NewToken;
                Second = await PresenceClient.ExecuteAsync(Make(NewToken));
            }

            return Second.IsSuccessful ? Second : Response;
        }

        public static async Task UpdatePresence(params long[] UserIds)
        {
            if (!Utilities.IsConnectedToInternet()) return;

            List<Account> Updated = new List<Account>();
            var Response = await Request(UserIds);

            if (Response.IsSuccessful && JsonConvert.DeserializeObject(Response.Content) is JObject Data && Data.ContainsKey("userPresences"))
            {
                foreach (var Presence in Data["userPresences"].ToObject<List<UserPresence>>())
                    if (AccountManager.AccountsList.FirstOrDefault(acc => acc.UserID == Presence.userId) is Account account)
                    {
                        account.Presence = Presence;

                        Updated.Add(account);
                    }
            }

            AccountManager.Instance.InvokeIfRequired(() => AccountManager.Instance.AccountsView.RefreshObjects(Updated));
        }

        public static async Task<Dictionary<long, UserPresence>> GetPresence(params long[] UserIds)
        {
            if (!Utilities.IsConnectedToInternet()) return null;

            var Dict = new Dictionary<long, UserPresence>();
            var Response = await Request(UserIds);

            if (Response.IsSuccessful && JsonConvert.DeserializeObject(Response.Content) is JObject Data && Data.ContainsKey("userPresences"))
            {
                foreach (var Presence in Data["userPresences"].ToObject<List<UserPresence>>())
                    Dict.Add(Presence.userId, Presence);

                return Dict;
            }

            return null;
        }

        public static async Task<UserPresence> GetPresenceSingular(long UserId) => ((await GetPresence(UserId)) is Dictionary<long, UserPresence> Dict && Dict.ContainsKey(UserId)) ? Dict[UserId] : null;
    }

    public class UserPresence
    {
        public UserPresenceType userPresenceType { get; set; }
        public string lastLocation { get; set; }
        public long? placeId { get; set; }
        public long? rootPlaceId { get; set; }
        public string? gameId { get; set; }
        public long? universeId { get; set; }
        public long userId { get; set; }
        public DateTime lastOnline { get; set; }
    }

    public enum UserPresenceType
    {
        Offline,
        Online,
        InGame,
        InStudio
    }
}