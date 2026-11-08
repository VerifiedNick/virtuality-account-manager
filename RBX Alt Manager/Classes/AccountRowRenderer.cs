using BrightIdeasSoftware;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    public enum AccountColumnKind { Account, Note, Status, LastUsed, Group, Empty }

    public enum AccountState { Offline, Online, InGame, InStudio, NeedsLogin, Banned }

    /// <summary>Draws the rows of the account list in the main window (flat, black, green bar on the selected row).</summary>
    public class AccountRowRenderer : BaseRenderer
    {
        private readonly AccountColumnKind Kind;

        public AccountRowRenderer(AccountColumnKind Kind) => this.Kind = Kind;

        public static AccountState StateOf(Account A)
        {
            string Ban = A.GetField("BanStatus");

            if (Ban == "Banned" || Ban == "TempBanned") return AccountState.Banned;
            if (Ban == "NeedsLogin") return AccountState.NeedsLogin;

            if (AccountManager.General != null && AccountManager.General.Get<bool>("ShowPresence") && A.Presence != null)
                switch (A.Presence.userPresenceType)
                {
                    case UserPresenceType.InGame: return AccountState.InGame;
                    case UserPresenceType.InStudio: return AccountState.InStudio;
                    case UserPresenceType.Online: return AccountState.Online;
                }

            return AccountState.Offline;
        }

        public static string StateText(Account A, AccountState State)
        {
            switch (State)
            {
                case AccountState.Banned: return A.GetField("BanStatus") == "TempBanned" ? "Banned (temp)" : "Banned";
                case AccountState.NeedsLogin: return "Not logged in";
                case AccountState.InGame: return "In game";
                case AccountState.InStudio: return "In Studio";
                case AccountState.Online: return "Online";
                default: return "Offline";
            }
        }

        public static Color StateColor(AccountState State)
        {
            switch (State)
            {
                case AccountState.Banned: return Modern.Danger;
                case AccountState.NeedsLogin: return Modern.Warn;
                case AccountState.InGame: return Modern.Online;
                case AccountState.Online: return Modern.Text;
                case AccountState.InStudio: return Modern.Text2;
                default: return Modern.Faint;
            }
        }

        /// <summary>Display name of a group (set by the main window: without number prefix / "A:").</summary>
        public static Func<string, string> GroupTitle;

        public static bool HideNames => AccountManager.General != null && AccountManager.General.Get<bool>("HideUsernames");

        public override void Render(Graphics G, Rectangle R)
        {
            if (!(RowObject is Account A)) { base.Render(G, R); return; }

            Modern.Prepare(G);

            AccountState State = StateOf(A);
            bool Sel = IsItemSelected;
            bool Banned = State == AccountState.Banned;

            Color Back = Sel ? Modern.Selected : (Banned ? Color.FromArgb(14, 4, 4) : Modern.Bg);

            using (var B = new SolidBrush(Back)) G.FillRectangle(B, R);
            using (var P = new Pen(Modern.Line)) G.DrawLine(P, R.Left, R.Bottom - 1, R.Right, R.Bottom - 1);

            int Pad = Modern.Px(12);

            switch (Kind)
            {
                case AccountColumnKind.Account:
                {
                    if (Sel)
                        using (var Bar = new SolidBrush(Modern.Accent))
                            G.FillRectangle(Bar, R.X, R.Y, Modern.Px(3), R.Height - 1);
                    else if (Banned)
                        using (var Bar = new SolidBrush(Modern.Danger))
                            G.FillRectangle(Bar, R.X, R.Y, Modern.Px(3), R.Height - 1);

                    int Sz = Math.Min(Modern.Px(30), R.Height - Modern.Px(14));
                    Rectangle Av = new Rectangle(R.X + Pad + Modern.Px(4), R.Y + (R.Height - Sz) / 2, Sz, Sz);

                    Modern.Box(G, Av, Banned ? Modern.DangerBg : Modern.Hover, Banned ? Modern.DangerLine : Modern.FieldBorder);
                    Modern.TextPlus(G, HideNames ? "··" : Modern.Initials(A.Username), Modern.Font(8f, FontStyle.Regular, true), Av, Banned ? Modern.Danger : Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

                    int TX = Av.Right + Modern.Px(10), TW = R.Right - TX - Modern.Px(6);
                    string Name = HideNames ? (string.IsNullOrEmpty(A.Alias) ? "Hidden" : A.Alias) : A.Username;
                    string Second = HideNames ? string.Empty : A.Alias;

                    if (string.IsNullOrEmpty(Second))
                        Modern.TextPlus(G, Name, Modern.Font(9.75f, FontStyle.Regular, true), new Rectangle(TX, R.Y, TW, R.Height), Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding);
                    else
                    {
                        int H = R.Height / 2;
                        Modern.TextPlus(G, Name, Modern.Font(9.75f, FontStyle.Regular, true), new Rectangle(TX, R.Y + Modern.Px(4), TW, H - Modern.Px(2)), Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding | TextFormatFlags.Bottom);
                        Modern.TextPlus(G, Second, Modern.Font(8.25f), new Rectangle(TX, R.Y + H, TW, H - Modern.Px(6)), Modern.Sub, TextFormatFlags.NoPadding | TextFormatFlags.Top);
                    }
                    break;
                }
                case AccountColumnKind.Note:
                {
                    string Note = (A.Description ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
                    Modern.TextPlus(G, Note, Modern.Font(9f), new Rectangle(R.X + Modern.Px(4), R.Y, R.Width - Pad, R.Height), Modern.Text2, TextFormatFlags.NoPadding);
                    break;
                }
                case AccountColumnKind.Status:
                {
                    Color C = StateColor(State);
                    int Dot = Modern.Px(7);

                    using (var B = new SolidBrush(C))
                        G.FillRectangle(B, R.X + Modern.Px(4), R.Y + (R.Height - Dot) / 2, Dot, Dot);

                    Modern.TextPlus(G, StateText(A, State), Modern.Font(9f, FontStyle.Regular, State == AccountState.Banned), new Rectangle(R.X + Modern.Px(4) + Dot + Modern.Px(8), R.Y, R.Width - Dot - Modern.Px(16), R.Height), State == AccountState.Offline ? Modern.Sub : C, TextFormatFlags.NoPadding);
                    break;
                }
                case AccountColumnKind.Group:
                {
                    string Title = GroupTitle != null ? GroupTitle(A.Group) : A.Group;
                    Modern.TextPlus(G, Title, Modern.Font(9f), new Rectangle(R.X + Modern.Px(4), R.Y, R.Width - Pad, R.Height), Modern.Sub, TextFormatFlags.NoPadding);
                    break;
                }
                case AccountColumnKind.LastUsed:
                {
                    double Days = (DateTime.Now - A.LastUse).TotalDays;
                    bool Aging = Days > 20 && !(AccountManager.General?.Get<bool>("DisableAgingAlert") ?? false);
                    Color C = Aging ? (Days > 30 ? Modern.Danger : Modern.Warn) : Modern.Sub;

                    Modern.TextPlus(G, Modern.Ago(A.LastUse), Modern.Font(9f), new Rectangle(R.X + Modern.Px(4), R.Y, R.Width - Pad, R.Height), C, TextFormatFlags.NoPadding);
                    break;
                }
            }
        }
    }
}
