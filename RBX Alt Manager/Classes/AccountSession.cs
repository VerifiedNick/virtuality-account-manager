using System;
using System.Collections.Generic;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>Login / ban state of an account, as saved in AccountData.json (Fields["BanStatus"]).</summary>
    public enum SessionStatus
    {
        /// <summary>Nothing known against the account (no BanStatus field). Normal state.</summary>
        Active,
        /// <summary>Roblox ended the session – needs "Re-login".</summary>
        NeedsLogin,
        /// <summary>Temporary ban (moderation message).</summary>
        TempBanned,
        /// <summary>Account terminated (users API).</summary>
        Banned
    }

    /// <summary>Who reports a state change – decides which transitions are allowed.</summary>
    public enum StatusSource
    {
        /// <summary>A successful login / new cookie.</summary>
        Login,
        /// <summary>users/authenticated session check (Check for bans, quiet check, keep-alive).</summary>
        SessionCheck,
        /// <summary>users API "is banned" list (Check for bans).</summary>
        BanCheck
    }

    /// <summary>
    /// The account state machine in one place. Before, four places wrote the "BanStatus" field by hand with slightly different rules.
    ///
    ///   Active ──SessionCheck: logged out──▶ NeedsLogin ──Login / SessionCheck ok──▶ Active
    ///   Active / NeedsLogin ──SessionCheck: temp ban──▶ TempBanned ──SessionCheck ok──▶ Active
    ///   any ──BanCheck: banned──▶ Banned ──BanCheck: not banned──▶ Active
    ///
    /// Banned is sticky: only the ban check itself can lift it (a session check or a login never does).
    /// The saved value is the same string as before, so old AccountData.json files and the original RAM stay compatible.
    /// Every change is logged as "[Status] name: From → To (why)".
    /// </summary>
    public static class AccountSession
    {
        public const string FieldName = "BanStatus";

        /// <summary>Raised after a real change (from, to). May come from a background thread.</summary>
        public static event Action<Account, SessionStatus, SessionStatus> Changed;

        public static SessionStatus Of(Account A)
        {
            switch (A?.GetField(FieldName))
            {
                case "Banned": return SessionStatus.Banned;
                case "TempBanned": return SessionStatus.TempBanned;
                case "NeedsLogin": return SessionStatus.NeedsLogin;
                default: return SessionStatus.Active;
            }
        }

        /// <summary>The transition table. Everything not listed is ignored (and logged once as refused).</summary>
        public static bool Allowed(SessionStatus From, SessionStatus To, StatusSource Source)
        {
            if (From == To) return false;

            switch (Source)
            {
                case StatusSource.BanCheck:
                    return To == SessionStatus.Banned || (From == SessionStatus.Banned && To == SessionStatus.Active);

                case StatusSource.SessionCheck:
                    if (From == SessionStatus.Banned) return false;                       // only the ban check lifts a ban
                    return To == SessionStatus.NeedsLogin || To == SessionStatus.TempBanned || To == SessionStatus.Active;

                case StatusSource.Login:
                    return To == SessionStatus.Active && (From == SessionStatus.NeedsLogin || From == SessionStatus.TempBanned);

                default:
                    return false;
            }
        }

        /// <summary>Changes the state if the table allows it. Returns true when something changed.</summary>
        public static bool Set(Account A, SessionStatus To, StatusSource Source, string Why)
        {
            if (A == null) return false;

            SessionStatus From = Of(A);

            if (!Allowed(From, To, Source)) return false;

            lock (A.Fields)
            {
                if (To == SessionStatus.Active) A.Fields.Remove(FieldName);
                else A.Fields[FieldName] = To.ToString();
            }

            Program.Logger?.Info($"[Status] {A.Username}: {From} → {To} ({Source}: {Why})");

            try { Changed?.Invoke(A, From, To); } catch { }

            return true;
        }

        /// <summary>A login worked: NeedsLogin / TempBanned end, and the session counts as checked now.</summary>
        public static void LoggedIn(Account A, string Why)
        {
            Set(A, SessionStatus.Active, StatusSource.Login, Why);
            Stamp(A, "SessionChecked");
        }

        public static void Stamp(Account A, string Field)
        {
            if (A == null) return;
            lock (A.Fields) A.Fields[Field] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        public static bool IsBanned(Account A) => Of(A) == SessionStatus.Banned || Of(A) == SessionStatus.TempBanned;
        public static bool NeedsLogin(Account A) => Of(A) == SessionStatus.NeedsLogin;
    }
}
