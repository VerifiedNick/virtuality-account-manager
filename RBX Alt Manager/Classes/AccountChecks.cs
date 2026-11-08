using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
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
        /// Banned/terminated accounts in one request per 100 accounts: users.roblox.com/v1/users with excludeBannedUsers
        /// leaves them out of the answer. Returns null for a batch that could not be checked.
        /// </summary>
        public static async Task<HashSet<long>> BannedIds(IList<long> UserIds, CancellationToken Token)
        {
            var Banned = new HashSet<long>();

            for (int i = 0; i < UserIds.Count; i += 100)
            {
                var Batch = UserIds.Skip(i).Take(100).ToList();
                RestResponse Response = await Send(AccountManager.UsersClient, () => new RestRequest("v1/users", Method.Post).AddJsonBody(new { userIds = Batch, excludeBannedUsers = true }), Token);

                if (Response.StatusCode != HttpStatusCode.OK || !Response.Content.TryParseJson(out JObject Data) || !(Data["data"] is JArray Users))
                    return null;

                var Active = new HashSet<long>(Users.Select(u => u.Value<long?>("id") ?? 0));

                foreach (long Id in Batch)
                    if (!Active.Contains(Id)) Banned.Add(Id);
            }

            return Banned;
        }

        public class SessionInfo
        {
            public BanCheckResult Result = BanCheckResult.Unknown;
            public string Email;      // masked by Roblox, e.g. ni***@gmail.com ("" = no email on the account)
            public bool? Verified;
        }

        /// <summary>Checks the account's own login (cookie) and, if it works, reads the (masked) e-mail. Read only.</summary>
        public static async Task<SessionInfo> CheckSession(Account Account, bool WithEmail, CancellationToken Token)
        {
            var Info = new SessionInfo();

            RestResponse Session = await Send(AccountManager.UsersClient, () => Account.MakeRequest("v1/users/authenticated", Method.Get), Token);
            string Body = Session.Content ?? string.Empty;

            if (Session.StatusCode == HttpStatusCode.Unauthorized)
            {
                // second opinion the way RAM itself checks a session: auth ticket endpoint answers 403 + token for a valid login, 401 for an expired one
                RestResponse Ticket = await Send(AccountManager.AuthClient, () => Account.MakeRequest("v1/authentication-ticket/", Method.Post).AddHeader("Referer", "https://www.roblox.com/"), Token);

                Program.Logger.Info($"[SessionCheck] {Account.Username}: users/authenticated {(int)Session.StatusCode}, authentication-ticket {(int)Ticket.StatusCode}");

                if (Ticket.StatusCode != HttpStatusCode.Forbidden) { Info.Result = BanCheckResult.NeedsLogin; return Info; }
            }
            if (Session.StatusCode != HttpStatusCode.OK && Body.IndexOf("moderated", StringComparison.OrdinalIgnoreCase) >= 0) { Info.Result = BanCheckResult.TempBanned; return Info; }
            if (Session.StatusCode != HttpStatusCode.OK && Session.StatusCode != HttpStatusCode.Unauthorized) return Info;

            Info.Result = BanCheckResult.Ok;

            if (WithEmail)
            {
                RestResponse Mail = await Send(AccountManager.AccountClient, () => Account.MakeRequest("v1/email", Method.Get), Token);

                if (Mail.StatusCode == HttpStatusCode.OK && Mail.Content.TryParseJson(out JObject M))
                {
                    Info.Email = M["emailAddress"]?.Type == JTokenType.String ? M["emailAddress"].Value<string>() : string.Empty;
                    Info.Verified = M["verified"]?.Type == JTokenType.Boolean ? M["verified"].Value<bool>() : (bool?)null;
                }
            }

            return Info;
        }

        /// <summary>Writes a session result onto the account (Fields only, nothing else is touched).</summary>
        public static void Apply(Account Account, SessionInfo Info)
        {
            switch (Info.Result)
            {
                case BanCheckResult.NeedsLogin: AccountSession.Set(Account, SessionStatus.NeedsLogin, StatusSource.SessionCheck, "session ended"); break;
                case BanCheckResult.TempBanned: AccountSession.Set(Account, SessionStatus.TempBanned, StatusSource.SessionCheck, "temporary ban"); break;
                case BanCheckResult.Ok: AccountSession.Set(Account, SessionStatus.Active, StatusSource.SessionCheck, "session valid"); break;
                default: return; // unknown (network): nothing changes
            }

            AccountSession.Stamp(Account, "SessionChecked");

            if (Info.Email != null)
            {
                Account.Fields["Email"] = Info.Email;
                Account.Fields["EmailVerified"] = Info.Verified == true ? "true" : "false";
                AccountSession.Stamp(Account, "EmailChecked");
            }
        }

        public static bool CheckedWithin(Account Account, string Field, TimeSpan Age) =>
            DateTime.TryParse(Account.GetField(Field), out DateTime When) && DateTime.Now - When < Age;

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
