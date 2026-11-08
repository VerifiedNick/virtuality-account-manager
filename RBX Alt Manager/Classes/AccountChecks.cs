using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    public enum BanCheckResult { Ok, Banned, TempBanned, NeedsLogin, Unknown }

    public enum FriendResult { Sent, AlreadyDone, Captcha, Failed }

    /// <summary>Roblox API checks used by the main window: ban check and friend requests to the main account.</summary>
    public static class AccountChecks
    {
        private static async Task<RestResponse> Send(RestClient Client, Func<RestRequest> Make, CancellationToken Token)
        {
            RestResponse Response = null;

            for (int Attempt = 0; Attempt < 5; Attempt++)
            {
                Token.ThrowIfCancellationRequested();

                Response = await Client.ExecuteAsync(Make(), Token);

                if ((int)Response.StatusCode != 429) break;

                await Task.Delay(TimeSpan.FromSeconds(4 + Attempt * 4), Token); // rate limited, wait and try again
            }

            return Response;
        }

        /// <summary>
        /// 1) public profile (users.roblox.com/v1/users/{id}) says isBanned for terminated accounts,
        /// 2) the account's own session tells if the cookie expired or the account is temporarily moderated.
        /// Nothing is changed on the account.
        /// </summary>
        public static async Task<BanCheckResult> Check(Account Account, CancellationToken Token)
        {
            RestResponse Public = await Send(AccountManager.UsersClient, () => new RestRequest($"v1/users/{Account.UserID}", Method.Get), Token);

            if (Public.StatusCode == HttpStatusCode.NotFound) return BanCheckResult.Banned; // account no longer exists

            if (Public.StatusCode == HttpStatusCode.OK && Public.Content.TryParseJson(out JObject Info) && Info["isBanned"]?.Type == JTokenType.Boolean && Info["isBanned"].Value<bool>())
                return BanCheckResult.Banned;

            bool PublicOk = Public.StatusCode == HttpStatusCode.OK;

            RestResponse Session = await Send(AccountManager.UsersClient, () => Account.MakeRequest("v1/users/authenticated", Method.Get), Token);
            string Body = Session.Content ?? string.Empty;

            if (Session.StatusCode != HttpStatusCode.OK && Body.IndexOf("moderated", StringComparison.OrdinalIgnoreCase) >= 0) return BanCheckResult.TempBanned;
            if (Session.StatusCode == HttpStatusCode.Unauthorized) return BanCheckResult.NeedsLogin;
            if (Session.StatusCode == HttpStatusCode.OK) return BanCheckResult.Ok;

            return PublicOk ? BanCheckResult.Ok : BanCheckResult.Unknown;
        }

        public static async Task<(FriendResult Result, string Message)> SendFriendRequest(Account Account, long TargetUserId, CancellationToken Token)
        {
            if (Account.UserID == TargetUserId) return (FriendResult.AlreadyDone, "this is the main account itself");

            if (!await Task.Run(() => Account.GetCSRFToken(out _), Token) || string.IsNullOrEmpty(Account.CSRFToken))
                return (FriendResult.Failed, "session expired (log in again)");

            RestRequest Make() => Account.MakeRequest($"/v1/users/{TargetUserId}/request-friendship", Method.Post)
                .AddHeader("X-CSRF-TOKEN", Account.CSRFToken)
                .AddJsonBody(new { friendshipOriginSourceType = "UserProfile" });

            RestRequest MakePlain() => Account.MakeRequest($"/v1/users/{TargetUserId}/request-friendship", Method.Post) // the way RAM always sent it
                .AddHeader("X-CSRF-TOKEN", Account.CSRFToken)
                .AddHeader("Content-Type", "application/json");

            RestResponse Response = await Send(AccountManager.FriendsClient, Make, Token);

            if ((Response.StatusCode == HttpStatusCode.BadRequest || Response.StatusCode == HttpStatusCode.UnsupportedMediaType) && (Response.Content ?? string.Empty).IndexOf("already", StringComparison.OrdinalIgnoreCase) < 0)
                Response = await Send(AccountManager.FriendsClient, MakePlain, Token);

            if (Response.StatusCode == HttpStatusCode.OK)
            {
                if (Response.Content.TryParseJson(out JObject Ok) && Ok["isCaptchaRequired"]?.Type == JTokenType.Boolean && Ok["isCaptchaRequired"].Value<bool>())
                    return (FriendResult.Captcha, "Roblox wants a captcha");

                return (FriendResult.Sent, "sent");
            }

            string Message = $"{(int)Response.StatusCode} {Response.StatusDescription}";

            if (Response.Content.TryParseJson(out JObject Error) && Error["errors"] is JArray Errors && Errors.Count > 0)
                Message = Errors[0]["message"]?.Value<string>() ?? Message;

            bool Challenge = Response.Headers?.Any(h => h.Name != null && h.Name.StartsWith("rblx-challenge", StringComparison.OrdinalIgnoreCase)) == true;

            if (Challenge || Message.IndexOf("captcha", StringComparison.OrdinalIgnoreCase) >= 0 || Message.IndexOf("challenge", StringComparison.OrdinalIgnoreCase) >= 0)
                return (FriendResult.Captcha, "Roblox wants a captcha");

            if (Message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0)
                return (FriendResult.AlreadyDone, Message);

            if (Response.StatusCode == HttpStatusCode.Unauthorized)
                return (FriendResult.Failed, "session expired (log in again)");

            return (FriendResult.Failed, Message);
        }
    }
}
