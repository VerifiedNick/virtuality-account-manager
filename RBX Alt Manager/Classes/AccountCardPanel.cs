using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Right side of the main window: the selected account with big copy buttons for username and password,
    /// login, friend request to the main account and the other account actions.
    /// </summary>
    public class AccountCardPanel : Panel
    {
        public readonly ModernButton CopyUser = new ModernButton() { Text = "Copy" };
        public readonly ModernButton CopyPassword = new ModernButton() { Text = "Copy" };
        public readonly ModernButton ShowPassword = new ModernButton() { Icon = IconKind.Eye, Style = ModernButtonStyle.Ghost };
        public readonly ModernButton Login = new ModernButton() { Text = "Login", Icon = IconKind.Login, Style = ModernButtonStyle.Primary, TextPoints = 10.5f };
        public readonly ModernButton AddFriend = new ModernButton() { Text = "Add ", Style = ModernButtonStyle.Outline };
        public readonly ModernButton OpenBrowser = new ModernButton() { Text = "Open browser" };
        public readonly ModernButton Follow = new ModernButton() { Text = "Follow user" };
        public readonly ModernButton MoreTools = new ModernButton() { Text = "More tools", SplitMenu = false };
        public readonly ModernButton ChangeMain = new ModernButton() { Text = "Change", Style = ModernButtonStyle.Ghost, Bold = false, TextPoints = 8.5f };
        public readonly ModernButton EditNote = new ModernButton() { Text = "Edit note", Bold = false };
        public readonly ModernButton Remove = new ModernButton() { Text = "Remove…", Style = ModernButtonStyle.Danger, Bold = false };

        public Func<string, string> GroupTitle = g => g;
        public string FriendTarget = "";

        private List<Account> Accounts = new List<Account>();
        private bool PasswordVisible;

        // painted areas, filled by OnLayout
        private Rectangle AvatarR, NameR, ChipsR, WarningR, LoginBoxR, LoginCaptionR, UserFieldR, PassFieldR, EmailR, CopyHintR, LoginHintR, MainRowR;

        public AccountCardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Modern.Panel;
            Tag = "NoScaling";

            foreach (var B in new[] { CopyUser, CopyPassword, ShowPassword, Login, AddFriend, OpenBrowser, Follow, MoreTools, ChangeMain, EditNote, Remove })
                Controls.Add(B);

            ShowPassword.Click += (s, e) => { PasswordVisible = !PasswordVisible; Invalidate(); };

            SetAccounts(null);
        }

        public Account Single => Accounts.Count == 1 ? Accounts[0] : null;
        public IReadOnlyList<Account> Selection => Accounts;

        public void SetAccounts(IEnumerable<Account> Selected)
        {
            var New = Selected?.Where(a => a != null).ToList() ?? new List<Account>();

            if (!(New.Count == 1 && Accounts.Count == 1 && New[0] == Accounts[0])) PasswordVisible = false;

            Accounts = New;
            RefreshState();
        }

        public void RefreshState()
        {
            bool Any = Accounts.Count > 0;
            bool HasPassword = Accounts.Any(a => !string.IsNullOrEmpty(a.Password));

            CopyUser.Enabled = Any;
            CopyPassword.Enabled = HasPassword;
            ShowPassword.Visible = Single != null && HasPassword;
            Login.Enabled = Single != null;
            Login.Text = Single != null && Single.GetField("BanStatus") == "NeedsLogin" ? "Log in again" : "Login";
            OpenBrowser.Enabled = Follow.Enabled = EditNote.Enabled = Remove.Enabled = Any;

            if (!AddFriendBusy)
            {
                AddFriend.Text = $"Add {FriendTarget}";
                AddFriend.Enabled = Any;
            }

            PerformLayout();
            Invalidate();
        }

        /// <summary>While friend requests are being sent the button shows the progress and works as a stop button.</summary>
        public bool AddFriendBusy;

        private bool ShowWarning => Accounts.Count > 0 && Accounts.Any(a => AccountRowRenderer.StateOf(a) == AccountState.Banned);

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (Width < Modern.Px(200) || Height < Modern.Px(200)) return;

            int P = Modern.Gutter, W = Width - P * 2, Y = Modern.Gutter, Gap = Modern.Px(8);

            int Av = Modern.Px(56);
            AvatarR = new Rectangle(P, Y, Av, Av);
            NameR = new Rectangle(AvatarR.Right + Modern.Px(14), Y + Modern.Px(4), W - Av - Modern.Px(14), Modern.Px(26));
            ChipsR = new Rectangle(NameR.X, NameR.Bottom + Modern.Px(4), NameR.Width, Modern.Px(22));
            Y = AvatarR.Bottom + Modern.Px(16);

            if (ShowWarning)
            {
                WarningR = new Rectangle(P, Y, W, Modern.Px(46));
                Y = WarningR.Bottom + Modern.Px(12);
            }
            else WarningR = Rectangle.Empty;

            // login data box
            int FieldH = Modern.Px(40), CopyW = Modern.Px(86), Inner = Modern.Px(12);
            LoginBoxR = new Rectangle(P, Y, W, 0);
            LoginCaptionR = new Rectangle(P + Inner, Y + Inner, W - Inner * 2, Modern.Px(16));

            int FY = LoginCaptionR.Bottom + Modern.Px(8);
            UserFieldR = new Rectangle(P + Inner, FY, W - Inner * 2 - CopyW - Modern.Px(8), FieldH);
            CopyUser.SetBounds(UserFieldR.Right + Modern.Px(8), FY, CopyW, FieldH);

            FY = UserFieldR.Bottom + Modern.Px(8);
            PassFieldR = new Rectangle(UserFieldR.X, FY, UserFieldR.Width, FieldH);
            CopyPassword.SetBounds(PassFieldR.Right + Modern.Px(8), FY, CopyW, FieldH);
            int Eye = FieldH - Modern.Px(10);
            ShowPassword.SetBounds(PassFieldR.Right - Eye - Modern.Px(5), FY + Modern.Px(5), Eye, Eye);

            EmailR = new Rectangle(UserFieldR.X, PassFieldR.Bottom + Modern.Px(8), W - Inner * 2, Modern.Px(18));
            CopyHintR = new Rectangle(UserFieldR.X, EmailR.Bottom + Modern.Px(2), W - Inner * 2, Modern.Px(18));
            LoginBoxR.Height = CopyHintR.Bottom + Inner - LoginBoxR.Y;
            Y = LoginBoxR.Bottom + Modern.Px(14);

            Login.SetBounds(P, Y, W, Modern.Px(46));
            LoginHintR = new Rectangle(P, Login.Bottom + Modern.Px(4), W, Modern.Px(34));
            Y = LoginHintR.Bottom + Modern.Px(10);

            int Half = (W - Gap) / 2, BH = Modern.Px(40);
            AddFriend.SetBounds(P, Y, Half, BH);
            OpenBrowser.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
            Y += BH + Modern.Px(8);
            Follow.SetBounds(P, Y, Half, BH);
            MoreTools.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
            Y += BH + Modern.Px(10);

            int ChangeW = Modern.Px(64);
            MainRowR = new Rectangle(P, Y, W - ChangeW - Modern.Px(6), Modern.Px(28));
            ChangeMain.SetBounds(P + W - ChangeW, Y, ChangeW, Modern.Px(28));
            Y += Modern.Px(28);

            int BottomH = Modern.Px(36);
            int BottomY = Math.Max(Y + Modern.Px(14), Height - P - BottomH);
            EditNote.SetBounds(P, BottomY, Half, BottomH);
            Remove.SetBounds(P + Half + Gap, BottomY, W - Half - Gap, BottomH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            Modern.Prepare(G);
            G.Clear(Modern.Panel);

            using (var Line = new Pen(Modern.Line))
                G.DrawLine(Line, 0, 0, 0, Height);

            var One = Single;
            bool Banned = One != null && AccountRowRenderer.StateOf(One) == AccountState.Banned;

            // avatar: the group's Rank if there is a picture, otherwise the initials
            Image Picture = null;
            string GroupKey = Accounts.Count > 0 && Accounts.All(a => a.Group == Accounts[0].Group) ? Accounts[0].Group : null;

            GroupPetInfo EntryInfo = null;

            if (One != null && !AccountRowRenderer.HideNames) Picture = AvatarCache.Get(One.UserID);
            if (Picture == null && GroupKey != null && One == null) Picture = GroupImages.GetImage(GroupKey, out EntryInfo);

            Modern.Box(G, AvatarR, Banned ? Modern.DangerBg : Modern.Field, Banned ? Modern.DangerLine : Modern.FieldBorder);

            if (Picture != null && Accounts.Count > 0)
            {
                var Inset = Rectangle.Inflate(AvatarR, -1, -1);
                G.DrawImage(Picture, Inset);
                Modern.VariantFrame(G, AvatarR, EntryInfo?.Variant);
            }
            else
            {
                string AvatarText = Accounts.Count == 0 ? "–" : (One != null ? (AccountRowRenderer.HideNames ? "··" : Modern.Initials(One.Username)) : Accounts.Count.ToString());
                Modern.Text_(G, AvatarText, Modern.Font(14f, FontStyle.Regular, true), AvatarR, Banned ? Modern.Danger : Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }

            string Name = Accounts.Count == 0 ? "No account selected" : (One != null ? (AccountRowRenderer.HideNames ? (string.IsNullOrEmpty(One.Alias) ? "Hidden" : One.Alias) : One.Username) : $"{Accounts.Count} accounts selected");
            Modern.Text_(G, Name, Modern.Font(13f, FontStyle.Regular, true), NameR, Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding);

            // chips: group + status, square tags
            int CX = ChipsR.X;

            void Chip(string Text, Color Fore, Color Border)
            {
                if (string.IsNullOrEmpty(Text)) return;

                Font F = Modern.Font(8.25f);
                int TW = TextRenderer.MeasureText(Text, F, Size.Empty, TextFormatFlags.NoPadding).Width + Modern.Px(14);
                TW = Math.Min(TW, ChipsR.Right - CX);
                if (TW <= Modern.Px(16)) return;

                var R = new Rectangle(CX, ChipsR.Y, TW, ChipsR.Height);
                Modern.Box(G, R, Modern.Panel, Border);
                Modern.Text_(G, Text, F, R, Fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                CX += TW + Modern.Px(6);
            }

            if (Accounts.Count == 0)
                Modern.Text_(G, "Click an account in the list", Modern.Font(8.75f), ChipsR, Modern.Sub, TextFormatFlags.NoPadding);
            else
            {
                Chip(GroupKey != null ? GroupTitle(GroupKey) : "several groups", Modern.Text2, Modern.FieldBorder);

                if (One != null)
                {
                    var State = AccountRowRenderer.StateOf(One);
                    Chip(AccountRowRenderer.StateText(One, State), State == AccountState.Offline ? Modern.Sub : AccountRowRenderer.StateColor(State), State == AccountState.Banned ? Modern.DangerLine : Modern.FieldBorder);
                }
            }

            if (!WarningR.IsEmpty)
            {
                Modern.Box(G, WarningR, Modern.DangerBg, Modern.DangerLine);

                int BannedCount = Accounts.Count(a => AccountRowRenderer.StateOf(a) == AccountState.Banned);
                string Msg = One != null ? "This account is banned on Roblox. It stays in your list, marked red, until you remove it yourself." : $"{BannedCount} of the selected accounts are banned. They stay in your list, marked red.";

                TextRenderer.DrawText(G, Msg, Modern.Font(8.5f), Rectangle.Inflate(WarningR, -Modern.Px(10), -Modern.Px(4)), Color.FromArgb(240, 170, 170), TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // login data
            Modern.Box(G, LoginBoxR, Modern.Bg, Modern.Line);
            Modern.Text_(G, "LOGIN DATA", Modern.Font(7.75f, FontStyle.Regular, true), LoginCaptionR, Modern.Sub, TextFormatFlags.NoPadding);

            Modern.Box(G, UserFieldR, Modern.Field, Modern.FieldBorder);
            Modern.Box(G, PassFieldR, Modern.Field, Modern.FieldBorder);

            var UserText = Rectangle.Inflate(UserFieldR, -Modern.Px(10), 0);
            var PassText = new Rectangle(PassFieldR.X + Modern.Px(10), PassFieldR.Y, PassFieldR.Width - Modern.Px(20) - (ShowPassword.Visible ? ShowPassword.Width : 0), PassFieldR.Height);

            if (Accounts.Count == 0)
            {
                Modern.Text_(G, "Username", Modern.Font(9.5f), UserText, Modern.Faint, TextFormatFlags.NoPadding);
                Modern.Text_(G, "Password", Modern.Font(9.5f), PassText, Modern.Faint, TextFormatFlags.NoPadding);
            }
            else if (One != null)
            {
                Modern.Text_(G, AccountRowRenderer.HideNames ? "••••••••" : One.Username, Modern.Font(9.75f), UserText, Modern.Text, TextFormatFlags.NoPadding);

                if (string.IsNullOrEmpty(One.Password))
                    Modern.Text_(G, "no password saved", Modern.Font(9.5f), PassText, Modern.Faint, TextFormatFlags.NoPadding);
                else
                    Modern.Text_(G, PasswordVisible ? One.Password : new string('•', 12), Modern.Font(9.75f), PassText, Modern.Text, TextFormatFlags.NoPadding);
            }
            else
            {
                int WithPw = Accounts.Count(a => !string.IsNullOrEmpty(a.Password));

                Modern.Text_(G, $"{Accounts.Count} usernames", Modern.Font(9.5f), UserText, Modern.Text2, TextFormatFlags.NoPadding);
                Modern.Text_(G, WithPw == 0 ? "no passwords saved" : $"{WithPw} passwords", Modern.Font(9.5f), PassText, WithPw == 0 ? Modern.Faint : Modern.Text2, TextFormatFlags.NoPadding);
            }

            // e-mail of the account (Roblox only gives it masked)
            string MailLine = "E-mail: –";

            if (One != null)
            {
                string Mail = One.GetField("Email");

                if (!One.Fields.ContainsKey("Email")) MailLine = "E-mail: not checked yet";
                else if (string.IsNullOrEmpty(Mail)) MailLine = "E-mail: none on this account";
                else MailLine = $"E-mail: {(AccountRowRenderer.HideNames ? "hidden" : Mail)}{(One.GetField("EmailVerified") == "true" ? "  (verified)" : "  (not verified)")}";
            }
            else if (Accounts.Count > 1)
                MailLine = $"E-mail: {Accounts.Count(a => !string.IsNullOrEmpty(a.GetField("Email")))} of {Accounts.Count} have one";

            Modern.Text_(G, MailLine, Modern.Font(8.5f), EmailR, Modern.Text2, TextFormatFlags.NoPadding);
            Modern.Text_(G, "Copies are cleared from the clipboard after 30 s.", Modern.Font(8f), CopyHintR, Modern.Sub, TextFormatFlags.NoPadding);

            TextRenderer.DrawText(G, "Opens the Roblox login with username and password filled in. You only do the captcha / 2-step code.", Modern.Font(8f), LoginHintR, Modern.Sub, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Top);

            // main account row
            Font MF = Modern.Font(8.5f);
            string Lead = "Main account for friend requests: ";
            int LW = TextRenderer.MeasureText(G, Lead, MF, Size.Empty, TextFormatFlags.NoPadding).Width;
            Modern.Text_(G, Lead, MF, MainRowR, Modern.Sub, TextFormatFlags.NoPadding);
            Modern.Text_(G, FriendTarget, Modern.Font(8.5f, FontStyle.Regular, true), new Rectangle(MainRowR.X + LW, MainRowR.Y, Math.Max(0, MainRowR.Width - LW), MainRowR.Height), Modern.Text, TextFormatFlags.NoPadding);
        }
    }
}
