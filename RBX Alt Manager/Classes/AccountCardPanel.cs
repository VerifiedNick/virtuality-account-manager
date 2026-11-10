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
        public readonly ModernButton AddFriend = new ModernButton() { Text = "Add main account" };
        public readonly ModernButton OpenBrowser = new ModernButton() { Text = "Open browser" };
        public readonly ModernButton Follow = new ModernButton() { Text = "Follow user" };
        public readonly ModernButton MoreTools = new ModernButton() { Text = "More tools", SplitMenu = false };
        public readonly ModernButton ChangeMain = new ModernButton() { Text = "Change", Style = ModernButtonStyle.Ghost, Bold = false, TextPoints = 8.5f };
        public readonly ModernButton ChangeJoin = new ModernButton() { Text = "Change", Style = ModernButtonStyle.Ghost, Bold = false, TextPoints = 8.5f };
        public readonly ModernButton EditNote = new ModernButton() { Text = "Edit note" };
        public readonly ModernButton Remove = new ModernButton() { Text = "Remove…", Style = ModernButtonStyle.Danger };

        public Func<string, string> GroupTitle = g => g;
        public string FriendTarget = string.Empty;
        public string JoinTarget = string.Empty;

        /// <summary>Login for one account (the per-row buttons of a multi selection).</summary>
        public Action<Account> LoginRequested = _ => { };

        /// <summary>A login window is open: no second one at the same time.</summary>
        private bool LoginBusy_;
        public bool LoginBusy { get => LoginBusy_; set { LoginBusy_ = value; RefreshState(); } }

        private readonly SelectionList List;
        private Rectangle ListBoxR, ListCaptionR;

        private bool Multi => Accounts.Count > 1;

        /// <summary>First selected account that has to log in again.</summary>
        public Account NextToLogin => Accounts.FirstOrDefault(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin);

        private List<Account> Accounts = new List<Account>();
        private bool PasswordVisible;

        // painted areas, filled by OnLayout
        private Rectangle AvatarR, NameR, ChipsR, WarningR, LoginBoxR, LoginCaptionR, UserFieldR, PassFieldR, EmailR, CopyHintR, LoginHintR, MainRowR, JoinRowR;

        public AccountCardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Modern.Panel;
            Tag = "NoScaling";

            foreach (var B in new[] { CopyUser, CopyPassword, ShowPassword, Login, AddFriend, OpenBrowser, Follow, MoreTools, EditNote, Remove })
                Controls.Add(B);

            List = new SelectionList(this) { Visible = false };
            Controls.Add(List);
            Modern.DarkScrollbars(List);

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

            CopyUser.Text = Multi ? "Copy names" : "Copy";
            CopyPassword.Text = Multi ? "Copy passwords" : "Copy";
            CopyUser.Style = CopyPassword.Style = Multi ? ModernButtonStyle.Ghost : ModernButtonStyle.Secondary;
            CopyUser.Bold = CopyPassword.Bold = !Multi;

            if (Multi)
            {
                int Left = Accounts.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin);
                Login.Enabled = Left > 0 && !LoginBusy_;
                Login.Text = Left > 0 ? $"Log in next · {Left} left" : "All selected are logged in";
            }
            else
            {
                Login.Enabled = Single != null && !LoginBusy_;
                Login.Text = Single != null && AccountSession.NeedsLogin(Single) ? "Log in again" : "Login";
            }

            if (LoginBusy_) Login.Text = "Login window open…";

            // purple only when a login is really needed; for a logged-in account it is a normal button
            bool NeedsLogin = Multi ? Accounts.Any(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin) : (Single != null && AccountRowRenderer.StateOf(Single) == AccountState.NeedsLogin);
            Login.Style = NeedsLogin ? ModernButtonStyle.Primary : ModernButtonStyle.Secondary;

            List.Visible = Multi;
            List.SetAccounts(Multi ? Accounts : new List<Account>());
            OpenBrowser.Enabled = Follow.Enabled = EditNote.Enabled = Remove.Enabled = Any;

            if (!AddFriendBusy)
            {
                AddFriend.Text = string.IsNullOrEmpty(FriendTarget) ? "Add main account" : $"Add {FriendTarget}";
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

            if (Multi)
            {
                LayoutMulti(P, W, Y, Gap);
                return;
            }

            ListBoxR = ListCaptionR = Rectangle.Empty;

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

            // third row of the same block (same size, same gap): no empty space between the tools and "Edit note" / "Remove…".
            // "Friend requests go to" / "Join button joins" are global: they live in Settings → Joining, not on a single account
            Y -= Modern.Px(2);
            EditNote.SetBounds(P, Y, Half, BH);
            Remove.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
        }

        /// <summary>Several accounts: a scrollable list of them, each with its own Login button.</summary>
        private void LayoutMulti(int P, int W, int Y, int Gap)
        {
            int Inner = Modern.Px(12), Half = (W - Gap) / 2, BH = Modern.Px(40);

            // everything under the list has a fixed height, the list takes the rest
            int Below = Modern.Px(14) + Modern.Px(46) + Modern.Px(4) + Modern.Px(34) + Modern.Px(10) + BH + Modern.Px(8) + BH + Modern.Px(8) + BH + P;
            int BoxH = Math.Max(Modern.Px(150), Height - Y - Below);

            ListBoxR = new Rectangle(P, Y, W, BoxH);
            ListCaptionR = new Rectangle(P + Inner, Y + Inner, W - Inner * 2, Modern.Px(16));

            int CopyW = Modern.Px(112);
            CopyPassword.SetBounds(ListBoxR.Right - Inner - CopyW, Y + Modern.Px(6), CopyW, Modern.Px(26));
            CopyUser.SetBounds(CopyPassword.Left - Modern.Px(4) - Modern.Px(92), Y + Modern.Px(6), Modern.Px(92), Modern.Px(26));
            ListCaptionR.Width = CopyUser.Left - ListCaptionR.X - Modern.Px(8);

            List.SetBounds(P + 1, ListCaptionR.Bottom + Modern.Px(10), W - 2, ListBoxR.Bottom - 1 - (ListCaptionR.Bottom + Modern.Px(10)));
            Y = ListBoxR.Bottom + Modern.Px(14);

            Login.SetBounds(P, Y, W, Modern.Px(46));
            LoginHintR = new Rectangle(P, Login.Bottom + Modern.Px(4), W, Modern.Px(34));
            Y = LoginHintR.Bottom + Modern.Px(10);

            AddFriend.SetBounds(P, Y, Half, BH);
            OpenBrowser.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
            Y += BH + Modern.Px(8);
            Follow.SetBounds(P, Y, Half, BH);
            MoreTools.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
            Y += BH + Modern.Px(10);

            // third row of the same block, exactly like the single account view
            Y -= Modern.Px(2);
            EditNote.SetBounds(P, Y, Half, BH);
            Remove.SetBounds(P + Half + Gap, Y, W - Half - Gap, BH);
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

            // avatar: the group's picture if there is one, otherwise the initials
            Image Picture = null;
            string GroupKey = Accounts.Count > 0 && Accounts.All(a => a.Group == Accounts[0].Group) ? Accounts[0].Group : null;

            GroupPictureInfo PictureInfo = null;

            if (One != null && !AccountRowRenderer.HideNames) Picture = AvatarCache.Get(One.UserID);
            if (Picture == null && GroupKey != null && One == null) Picture = GroupImages.GetImage(GroupKey, out PictureInfo);

            Modern.Box(G, AvatarR, Banned ? Modern.DangerBg : Modern.Field, Banned ? Modern.DangerLine : Modern.FieldBorder);

            if (Picture != null && Accounts.Count > 0)
            {
                var Inset = Rectangle.Inflate(AvatarR, -1, -1);
                G.DrawImage(Picture, Inset);
                Modern.VariantFrame(G, AvatarR, PictureInfo?.Variant);
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

            if (Multi)
            {
                Modern.Box(G, ListBoxR, Modern.Bg, Modern.Line);
                int Need = Accounts.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin);
                Modern.Text_(G, Need > 0 ? $"{Need} TO LOG IN" : $"{Accounts.Count} SELECTED", Modern.Font(7.75f, FontStyle.Regular, true), ListCaptionR, Need > 0 ? Modern.Warn : Modern.Sub, TextFormatFlags.NoPadding);

                TextRenderer.DrawText(G, "One login window at a time: do the captcha, then the next account. Each row has its own Login button too.", Modern.Font(8f), LoginHintR, Modern.Sub, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Top | TextFormatFlags.NoPadding);
                return;
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

            TextRenderer.DrawText(G, "Opens the Roblox login with username and password filled in. You only do the captcha / 2-step code.", Modern.Font(8f), LoginHintR, Modern.Sub, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        }

        /// <summary>Scrollable rows of the selected accounts: picture, name, status and a Login button per account.</summary>
        private class SelectionList : Panel
        {
            private readonly AccountCardPanel Owner;
            private List<Account> Items = new List<Account>();
            private int HoverRow = -1;
            private bool HoverButton;

            private int RowH => Modern.Px(52);
            private int Inner => Modern.Px(11);

            public SelectionList(AccountCardPanel Owner)
            {
                this.Owner = Owner;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                AutoScroll = true;
                BackColor = Modern.Bg;
                Tag = "NoScaling";
            }

            public void SetAccounts(List<Account> Accounts)
            {
                Items = Accounts.ToList();
                AutoScrollMinSize = new Size(0, Items.Count * RowH);
                Invalidate();
            }

            private Rectangle RowRect(int i) => new Rectangle(0, i * RowH + AutoScrollPosition.Y, ClientSize.Width, RowH);

            private Rectangle ButtonRect(Rectangle Row)
            {
                int W = Modern.Px(78), H = Modern.Px(28);
                return new Rectangle(Row.Right - Inner - W, Row.Y + (Row.Height - H) / 2, W, H);
            }

            private bool CanLogin(Account A) => !Owner.LoginBusy && AccountRowRenderer.StateOf(A) != AccountState.Banned;

            protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
            protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                int Row = (e.Y - AutoScrollPosition.Y) / RowH;
                if (Row < 0 || Row >= Items.Count) Row = -1;

                bool OnButton = Row >= 0 && ButtonRect(RowRect(Row)).Contains(e.Location);

                if (Row != HoverRow || OnButton != HoverButton)
                {
                    HoverRow = Row;
                    HoverButton = OnButton;
                    Cursor = OnButton && CanLogin(Items[Row]) ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                HoverRow = -1;
                HoverButton = false;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);

                if (e.Button != MouseButtons.Left || HoverRow < 0 || HoverRow >= Items.Count) return;

                if (ButtonRect(RowRect(HoverRow)).Contains(e.Location) && CanLogin(Items[HoverRow]))
                    Owner.LoginRequested(Items[HoverRow]);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var G = e.Graphics;
                G.Clear(Modern.Bg);
                Modern.Prepare(G);

                int First = Math.Max(0, -AutoScrollPosition.Y / RowH), Last = Math.Min(Items.Count - 1, First + ClientSize.Height / RowH + 1);

                for (int i = First; i <= Last; i++)
                {
                    var A = Items[i];
                    var R = RowRect(i);
                    var State = AccountRowRenderer.StateOf(A);
                    bool Need = State == AccountState.NeedsLogin, Banned = State == AccountState.Banned;

                    if (i == HoverRow)
                        using (var B = new SolidBrush(Modern.Panel)) G.FillRectangle(B, R);

                    if (Need)
                        using (var B = new SolidBrush(Modern.Warn)) G.FillRectangle(B, R.X, R.Y + Modern.Px(8), Modern.Px(3), R.Height - Modern.Px(16));

                    using (var P = new Pen(Modern.Line)) G.DrawLine(P, R.X + Inner, R.Bottom - 1, R.Right - Inner, R.Bottom - 1);

                    // picture
                    int Av = Modern.Px(32);
                    var AvR = new Rectangle(R.X + Inner, R.Y + (R.Height - Av) / 2, Av, Av);
                    Modern.Box(G, AvR, Banned ? Modern.DangerBg : Modern.Field, Banned ? Modern.DangerLine : Modern.FieldBorder);

                    Image Pic = AccountRowRenderer.HideNames ? null : AvatarCache.Get(A.UserID);
                    if (Pic != null) G.DrawImage(Pic, Rectangle.Inflate(AvR, -1, -1));
                    else Modern.Text_(G, AccountRowRenderer.HideNames ? "··" : Modern.Initials(A.Username), Modern.Font(8f, FontStyle.Regular, true), AvR, Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

                    // button
                    var BR = ButtonRect(R);
                    bool Can = CanLogin(A), Hot = i == HoverRow && HoverButton && Can;
                    Color Fill = !Can ? Modern.Bg : (Need ? (Hot ? Modern.AccentHover : Modern.Accent) : (Hot ? Modern.Hover : Modern.Field));
                    Color Border = !Can ? Modern.Line : (Need ? Fill : Modern.FieldBorder);
                    Color Fore = !Can ? Modern.Faint : (Need ? Color.White : Modern.Text);

                    Modern.Box(G, BR, Fill, Border);
                    Modern.Text_(G, Need ? "Log in" : "Login", Modern.Font(8.75f, FontStyle.Regular, true), BR, Fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

                    // name + status
                    int TX = AvR.Right + Modern.Px(10), TW = BR.Left - TX - Modern.Px(8);
                    string Name = AccountRowRenderer.HideNames ? (string.IsNullOrEmpty(A.Alias) ? "Hidden" : A.Alias) : A.Username;

                    Modern.Text_(G, Name, Modern.Font(9.25f, FontStyle.Regular, true), new Rectangle(TX, R.Y + Modern.Px(7), TW, Modern.Px(20)), Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding);
                    Modern.Text_(G, AccountRowRenderer.StateText(A, State), Modern.Font(8.25f), new Rectangle(TX, R.Y + Modern.Px(26), TW, Modern.Px(18)), State == AccountState.Offline ? Modern.Sub : AccountRowRenderer.StateColor(State), TextFormatFlags.NoPadding);
                }
            }
        }
    }
}
