using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    // Settings, server list, recent games, account tools and account control open as pages inside the main window
    // (no extra windows). Layout follows the accounts page: the navigation sits in the column of the groups,
    // the content where the account list is.
    public partial class AccountManager
    {
        private Panel PageHost, PageBody, PageContent;
        private LinePanel PageNav;
        private ModernButton PageBack;
        private ModernLabel PageCaption, PageTitle, PageSub;
        private readonly List<NavItem> PageNavItems = new List<NavItem>();
        private string CurrentPage;

        private SettingsPage SettingsUI;
        private Panel RecentPage;
        private FlowLayoutPanel RecentTiles;

        private class PageDef
        {
            public string Key, Caption;
            public ModernButton Icon;
            public Control Content;
            public bool OwnTitle;                        // content draws its own title (settings)
            public Func<List<string>> Nav;               // navigation entries
            public Func<int> ActiveNav;
            public Action<int> SelectNav;
            public Func<(string Title, string Sub)> Heading;
            public Action Opened;
        }

        private readonly Dictionary<string, PageDef> Pages = new Dictionary<string, PageDef>();

        /// <summary>Navigation entry in the left column (same size + look as a group card).</summary>
        private class NavItem : Control
        {
            public bool Active;
            private bool Hover;

            public NavItem()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                Tag = "NoScaling";
            }

            protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var G = e.Graphics;
                G.Clear(Parent?.BackColor ?? Modern.Panel);
                Modern.Prepare(G);

                if (Active || Hover)
                    using (var B = new SolidBrush(Active ? Modern.Selected : Modern.Hover)) G.FillRectangle(B, ClientRectangle);

                if (Active)
                    using (var B = new SolidBrush(Modern.Accent)) G.FillRectangle(B, 0, 0, Modern.Px(3), Height);

                Modern.Text_(G, Text, Modern.Font(9.75f, FontStyle.Regular, Active), new Rectangle(Modern.Px(14), 0, Width - Modern.Px(20), Height), Active ? Modern.Text : Modern.Text2, TextFormatFlags.NoPadding);
            }
        }

        private void BuildPages()
        {
            PageHost = new Panel() { Name = "PageHost", BackColor = Modern.Bg, Visible = false, Tag = "NoScaling" };
            PageNav = new LinePanel() { Dock = DockStyle.Left, BackColor = Modern.Panel, Lines = AnchorStyles.Right };
            PageBody = new Panel() { Dock = DockStyle.Fill, BackColor = Modern.Bg, Tag = "NoScaling" };
            PageContent = new Panel() { BackColor = Modern.Bg, Tag = "NoScaling" };

            PageBack = new ModernButton() { Text = "Accounts", Icon = IconKind.Back, TextPoints = 9.75f };
            PageBack.Click += (s, e) => ClosePage();
            SaveTooltip.SetToolTip(PageBack, "Back to your accounts (Esc)");

            PageCaption = new ModernLabel() { Points = 7.75f, Bold = true, Color = Modern.Sub, Upper = true };
            PageTitle = new ModernLabel() { Points = 15f, Bold = true };
            PageSub = new ModernLabel() { Points = 9f, Color = Modern.Sub };

            PageNav.Controls.AddRange(new Control[] { PageBack, PageCaption });
            PageBody.Controls.AddRange(new Control[] { PageTitle, PageSub, PageContent });
            PageHost.Controls.Add(PageBody);
            PageHost.Controls.Add(PageNav);

            Shell.Controls.Add(PageHost);

            void Place()
            {
                if (HeaderBar == null) return;
                PageHost.SetBounds(0, HeaderBar.Height, Shell.ClientSize.Width, Math.Max(0, Shell.ClientSize.Height - HeaderBar.Height));
                PageNav.Width = SideBar.Width;
            }

            Shell.Resize += (s, e) => Place();
            SideBar.Resize += (s, e) => Place();
            Place();

            PageNav.Layout += (s, e) => LayoutPageNav();
            Modern.PageReserveRight = AccountCard.Width;
            AccountCard.Resize += (s, e) => Modern.PageReserveRight = AccountCard.Width;
            PageBody.Layout += (s, e) => LayoutPageBody();

            DefinePages();
        }

        private void LayoutPageNav()
        {
            int P = Modern.Gutter, W = PageNav.Width - P * 2 - 1;

            // same lines as the accounts page: back button = search field, caption = "GROUPS", entries = group cards
            PageBack.SetBounds(P, P, W, Modern.RowH);
            PageCaption.SetBounds(P, PageBack.Bottom + Px(14), W, Px(16));

            int Y = PageCaption.Bottom + Px(6), H = Px(40);

            foreach (var Item in PageNavItems)
            {
                Item.SetBounds(P, Y, W, H);
                Y += H + Px(4);
            }
        }

        private void LayoutPageBody()
        {
            int P = Modern.Gutter, RH = Modern.RowH, W = PageBody.Width - P * 2;

            bool Own = CurrentPage != null && Pages.TryGetValue(CurrentPage, out var Def) && Def.OwnTitle;

            PageTitle.Visible = PageSub.Visible = !Own;

            if (Own)
            {
                PageContent.SetBounds(0, 0, PageBody.Width, PageBody.Height);
                return;
            }

            // title + subtitle exactly where the group title sits; the content's own gutter puts its first row on the list's top line
            PageTitle.SetBounds(P, P - Px(4), W, Px(28));
            PageSub.SetBounds(P, P + RH - Px(16), W, Px(16));

            int Top = RH + Px(14);
            PageContent.SetBounds(0, Top, PageBody.Width, Math.Max(0, PageBody.Height - Top));
        }

        private void DefinePages()
        {
            // ---- Settings
            Pages["settings"] = new PageDef()
            {
                Key = "settings",
                Caption = "Settings",
                Icon = SettingsIcon,
                OwnTitle = true,
                Content = null, // made on first open
                Nav = () => SettingsUI.Sections.Select(x => x.Name).ToList(),
                ActiveNav = () => SettingsUI.Current,
                SelectNav = i => SettingsUI.ShowSection(i),
                Opened = () => SettingsUI.Reload()
            };

            // ---- Servers / games / favorites …
            var ServerTexts = new Dictionary<string, (string, string)>()
            {
                ["Servers"] = ("Servers", "Public servers of the game in the Game field. Double click a server to join it with the selected accounts."),
                ["Games"] = ("Games", "Search Roblox games. Double click (or right click) to use one."),
                ["Favorites"] = ("Favorites", "Your saved games. Right click to rename or remove."),
                ["Universe"] = ("Universe", "All places of a game (sub places)."),
                ["Outfits"] = ("Outfits", "Look at the outfits of a player and wear them on the selected account."),
                ["Watcher"] = ("Roblox watcher", "Closes broken Roblox windows automatically."),
            };

            Pages["servers"] = new PageDef()
            {
                Key = "servers",
                Caption = "Servers & games",
                Icon = ServerListIcon,
                Nav = () => ServerListForm.PageTabs.TabPages.Cast<TabPage>().Select(t => t.Text).ToList(),
                ActiveNav = () => ServerListForm.PageTabs.SelectedIndex,
                SelectNav = i => { ServerListForm.PageTabs.SelectedIndex = i; UpdatePageHeading(); },
                Heading = () =>
                {
                    string T = ServerListForm.PageTabs.SelectedTab?.Text ?? "Servers";
                    return ServerTexts.TryGetValue(T, out var V) ? V : (T, string.Empty);
                },
                Opened = () => ServerListForm.Busy = false
            };

            // ---- Recent games
            Pages["recent"] = new PageDef()
            {
                Key = "recent",
                Caption = "Recent games",
                Icon = RecentGamesIcon,
                Nav = () => new List<string>() { "Recent games" },
                ActiveNav = () => 0,
                SelectNav = i => { },
                Heading = () => ("Recent games", "Click a game to put it into the Game field above."),
                Opened = FillRecentGames
            };

            // ---- Account tools (password, e-mail, display name, friend / block, follow privacy)
            Pages["tools"] = new PageDef()
            {
                Key = "tools",
                Caption = "Account tools",
                Icon = AccountControlIcon,
                Nav = () => new List<string>() { "Selected account" },
                ActiveNav = () => 0,
                SelectNav = i => { },
                Heading = () => ("Account tools", "Password, e-mail, display name, friends and privacy of one account."),
                Opened = () => UtilsForm.RefreshFor()
            };

            // ---- Account control (Nexus)
            Pages["control"] = new PageDef()
            {
                Key = "control",
                Caption = "Account control",
                Nav = () => ControlForm.PageTabs.TabPages.Cast<TabPage>().Select(t => t.Text).ToList(),
                ActiveNav = () => ControlForm.PageTabs.SelectedIndex,
                SelectNav = i => { ControlForm.PageTabs.SelectedIndex = i; UpdatePageHeading(); },
                Heading = () =>
                {
                    switch (ControlForm.PageTabs.SelectedIndex)
                    {
                        case 1: return ("Account control · Settings", "How the connected accounts are kept in the game.");
                        case 2: return ("Account control · Help", "How to connect your accounts with Nexus.");
                        default: return ("Account control", "Accounts connected through Nexus. Check them on the left, then send a command or a script.");
                    }
                }
            };
        }

        /// <summary>Embeds an old window as page content (no frame, fills the page).</summary>
        private static Control Embed(Form F)
        {
            if (F.TopLevel)
            {
                F.TopLevel = false;
                F.FormBorderStyle = FormBorderStyle.None;
                F.ShowInTaskbar = false;
                F.Dock = DockStyle.Fill;
            }

            ModernSkin.Apply(F);
            return F;
        }

        private Control ContentOf(PageDef Def)
        {
            if (Def.Content != null) return Def.Content;

            switch (Def.Key)
            {
                case "settings":
                    SettingsUI = new SettingsPage() { Dock = DockStyle.Fill };
                    Def.Content = SettingsUI;
                    break;
                case "servers":
                    ServerListForm.UseModernLayout();
                    Def.Content = Embed(ServerListForm);
                    break;
                case "recent":
                    RecentTiles = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Modern.Bg, Padding = new Padding(Modern.Gutter - Px(4), Modern.Gutter - Px(4), Modern.Gutter, Modern.Gutter) };
                    Modern.DarkScrollbars(RecentTiles);
                    RecentPage = new Panel() { Dock = DockStyle.Fill, BackColor = Modern.Bg, Tag = "NoScaling" };
                    RecentPage.Controls.Add(RecentTiles);
                    Def.Content = RecentPage;
                    break;
                case "tools":
                    UtilsForm.UseModernLayout();
                    Def.Content = Embed(UtilsForm);
                    break;
                case "control":
                    ControlForm.UseModernLayout();
                    Def.Content = Embed(ControlForm);
                    break;
            }

            return Def.Content;
        }

        private void OpenPage(string Key)
        {
            if (!Pages.TryGetValue(Key, out var Def)) return;

            if (CurrentPage == Key && PageHost.Visible) { ClosePage(); return; }

            Control Content = ContentOf(Def);
            CurrentPage = Key;

            PageContent.SuspendLayout();

            foreach (Control C in PageContent.Controls.Cast<Control>().ToList())
                if (C != Content) { C.Visible = false; }

            if (Content.Parent != PageContent)
            {
                Content.Dock = DockStyle.Fill;
                PageContent.Controls.Add(Content);
            }

            Content.Visible = true;
            Content.BringToFront();
            PageContent.ResumeLayout(true);

            PageCaption.Text = Def.Caption;
            BuildPageNav(Def);

            foreach (var D in Pages.Values)
                if (D.Icon != null) D.Icon.Active = D == Def;

            PageHost.Visible = true;
            PageHost.BringToFront();
            PasswordPanel.BringToFront();

            try { Def.Opened?.Invoke(); } catch (Exception x) { Program.Logger.Warn($"[Page] {x.Message}"); }

            UpdatePageHeading();
            PageBody.PerformLayout();
            PageNav.PerformLayout();
        }

        private void ClosePage()
        {
            if (PageHost == null) return;

            PageHost.Visible = false;
            CurrentPage = null;

            foreach (var D in Pages.Values)
                if (D.Icon != null) D.Icon.Active = false;
        }

        private void BuildPageNav(PageDef Def)
        {
            foreach (var Item in PageNavItems) { PageNav.Controls.Remove(Item); Item.Dispose(); }
            PageNavItems.Clear();

            var Names = Def.Nav?.Invoke() ?? new List<string>();

            for (int i = 0; i < Names.Count; i++)
            {
                int Index = i;
                var Item = new NavItem() { Text = Names[i] };
                Item.Click += (s, e) =>
                {
                    Def.SelectNav?.Invoke(Index);
                    MarkNav(Def);
                };

                PageNavItems.Add(Item);
                PageNav.Controls.Add(Item);
            }

            MarkNav(Def);
            LayoutPageNav();
        }

        private void MarkNav(PageDef Def)
        {
            int Active = Def.ActiveNav?.Invoke() ?? -1;

            for (int i = 0; i < PageNavItems.Count; i++)
            {
                PageNavItems[i].Active = i == Active;
                PageNavItems[i].Invalidate();
            }
        }

        private void UpdatePageHeading()
        {
            if (CurrentPage == null || !Pages.TryGetValue(CurrentPage, out var Def) || Def.Heading == null) return;

            var (T, S) = Def.Heading();
            PageTitle.Text = T;
            PageSub.Text = S;
        }

        private void FillRecentGames()
        {
            if (RecentTiles == null) return;

            RecentTiles.SuspendLayout();

            foreach (Control C in RecentTiles.Controls.Cast<Control>().ToList()) C.Dispose();
            RecentTiles.Controls.Clear();

            var Games = RecentGames ?? new List<Game>();

            for (int i = Games.Count - 1; i >= 0; i--)
            {
                var Tile = new GameControl(Games[i]) { Margin = new Padding(Px(4), Px(4), Px(4), Px(4)) };
                Tile.Selected += (s, e) =>
                {
                    PlaceID.Text = e.Game.Details?.placeId.ToString();
                    ClosePage();
                };
                RecentTiles.Controls.Add(Tile);
            }

            if (Games.Count == 0)
                RecentTiles.Controls.Add(new ModernLabel() { Text = "No games yet – every game you join shows up here.", Color = Modern.Sub, Size = new Size(Px(420), Px(24)), Margin = new Padding(Px(4)) });

            ModernSkin.Apply(RecentTiles);
            RecentTiles.ResumeLayout(true);
        }

        #region Lock screen (PIN)

        private Panel LockCard;
        private PinField Pin;
        private ModernLabel PinMessage;
        private ModernButton UnlockModern;

        /// <summary>
        /// PIN entry: one square cell per character, the newest one "pops" in, the focused field gets the purple frame.
        /// Wrong PIN: the field shakes and turns red with the reason underneath (no pop-up). Right PIN: short "Unlocked" screen.
        /// </summary>
        private class PinField : Control
        {
            public readonly TextBox Input;
            private readonly Timer Anim = new Timer() { Interval = 15 };
            private DateTime PopStart = DateTime.MinValue, ShakeStart = DateTime.MinValue, SuccessStart = DateTime.MinValue;
            private int LastLength;
            /// <summary>Idle → (typing) → Checking → Wrong (shake, red) or the lock screen closes. Typing again leaves Wrong.</summary>
            public enum PinState { Idle, Checking, Wrong }

            private PinState State_ = PinState.Idle;
            public PinState State { get => State_; set { State_ = value; Start(); } }

            private bool Error => State_ == PinState.Wrong;
            private bool Busy => State_ == PinState.Checking;
            private const bool Success = false;

            public PinField(TextBox Box)
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.IBeam;
                Tag = "NoScaling";

                Input = Box;
                Input.Parent?.Controls.Remove(Input);
                Input.BorderStyle = BorderStyle.None;
                Input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                Input.UseSystemPasswordChar = true;
                Input.ShortcutsEnabled = true;
                Controls.Add(Input);
                Input.SetBounds(-2000, 0, 50, 20); // receives the keys, the field draws what was typed

                Input.TextChanged += (s, e) =>
                {
                    if (Input.Text.Length > LastLength) PopStart = DateTime.Now;
                    LastLength = Input.Text.Length;
                    if (State_ == PinState.Wrong && Input.Text.Length > 0) State_ = PinState.Idle;
                    Start();
                };
                Input.GotFocus += (s, e) => Invalidate();
                Input.LostFocus += (s, e) => Invalidate();

                Anim.Tick += (s, e) =>
                {
                    Invalidate();
                    bool Running = (DateTime.Now - PopStart).TotalMilliseconds < 200 || (DateTime.Now - ShakeStart).TotalMilliseconds < 420 || Busy || Input.Focused;
                    if (!Running) Anim.Stop();
                };
            }

            private void Start() { if (!Anim.Enabled) Anim.Start(); Invalidate(); }

            public void Shake() { State_ = PinState.Wrong; ShakeStart = DateTime.Now; Start(); }

            protected override void OnClick(EventArgs e) { base.OnClick(e); Input.Focus(); Start(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var G = e.Graphics;
                G.Clear(Parent?.BackColor ?? Modern.Bg);
                Modern.Prepare(G);

                // shake: damped sine for 420 ms
                double ShakeT = (DateTime.Now - ShakeStart).TotalMilliseconds;
                int Dx = ShakeT < 420 ? (int)(Math.Sin(ShakeT / 420 * Math.PI * 7) * Modern.Px(9) * (1 - ShakeT / 420)) : 0;

                Rectangle R = new Rectangle(Dx, 0, Width - 1, Height - 1);
                bool Focus = Input.Focused;
                Color Border = Error ? Modern.Danger : (Success ? Modern.Accent : (Focus ? Modern.Accent : Modern.FieldBorder));

                using (var B = new SolidBrush(Error ? Modern.DangerBg : Modern.Field)) G.FillRectangle(B, R);
                using (var P = new Pen(Border, Focus || Error ? Math.Max(1, Modern.Px(2)) : 1)) G.DrawRectangle(P, R.X + (Focus || Error ? 1 : 0), R.Y + (Focus || Error ? 1 : 0), R.Width - (Focus || Error ? 2 : 0), R.Height - (Focus || Error ? 2 : 0));

                // corner ticks: the "precise" frame look
                int T = Modern.Px(8);
                using (var P = new Pen(Error ? Modern.Danger : Modern.AccentText, Math.Max(1, Modern.Px(2))))
                    foreach (var (cx, cy, sx, sy) in new[] { (R.Left, R.Top, 1, 1), (R.Right, R.Top, -1, 1), (R.Left, R.Bottom, 1, -1), (R.Right, R.Bottom, -1, -1) })
                    {
                        G.DrawLine(P, cx, cy, cx + T * sx, cy);
                        G.DrawLine(P, cx, cy, cx, cy + T * sy);
                    }

                int N = Input.Text.Length;

                if (N == 0)
                {
                    Modern.Text_(G, Focus ? string.Empty : "Click here and type your PIN", Modern.Font(10.5f), R, Modern.Faint, TextFormatFlags.HorizontalCenter);

                    if (Focus && (DateTime.Now.Millisecond / 530) % 2 == 0)
                        using (var B = new SolidBrush(Modern.AccentText)) G.FillRectangle(B, R.X + R.Width / 2 - 1, R.Y + Modern.Px(14), Modern.Px(2), R.Height - Modern.Px(28));
                    return;
                }

                // one square per character, centered; the newest pops from small to full size
                int Cell = Modern.Px(16), Gap = Modern.Px(12);
                int Shown = Math.Min(N, Math.Max(1, (R.Width - Modern.Px(40)) / (Cell + Gap)));
                int Total = Shown * Cell + (Shown - 1) * Gap, X = R.X + (R.Width - Total) / 2, Y = R.Y + (R.Height - Cell) / 2;
                double Pop = Math.Min(1, (DateTime.Now - PopStart).TotalMilliseconds / 160.0);

                for (int i = 0; i < Shown; i++)
                {
                    float Sz = Cell;
                    if (i == Shown - 1 && Pop < 1) Sz = (float)(Cell * (0.4 + 0.6 * (1 - Math.Pow(1 - Pop, 3)) + Math.Sin(Pop * Math.PI) * 0.18));

                    var Rc = new RectangleF(X + i * (Cell + Gap) + (Cell - Sz) / 2f, Y + (Cell - Sz) / 2f, Sz, Sz);
                    using (var B = new SolidBrush(Error ? Modern.Danger : (Success ? Modern.AccentText : Modern.Text))) G.FillRectangle(B, Rc);
                }

                if (Focus && !Busy && !Success && (DateTime.Now.Millisecond / 530) % 2 == 0)
                    using (var B = new SolidBrush(Modern.AccentText)) G.FillRectangle(B, X + Total + Modern.Px(8), Y - Modern.Px(4), Modern.Px(2), Cell + Modern.Px(8));
            }
        }

        private void BuildLockScreen()
        {
            PasswordPanel.BackColor = Modern.Bg;

            Bitmap Logo = null;
            try { if (AppIcon.Value != null) using (var Big = new Icon(AppIcon.Value, 256, 256)) Logo = Big.ToBitmap(); } catch { }

            LockCard = new Panel() { BackColor = Modern.Bg, Tag = "NoScaling", Visible = false };

            var Title = new ModernLabel() { Text = "Virtuality Manager", Points = 20f, Bold = true, Center = true };
            var Sub = new ModernLabel() { Text = "Enter your PIN to unlock your accounts", Points = 11f, Color = Modern.Text2, Center = true };
            PinMessage = new ModernLabel() { Points = 10f, Color = Modern.Danger, Center = true };
            var Foot = new ModernLabel() { Text = "Press Enter to unlock  ·  your accounts stay encrypted on this PC", Points = 9f, Color = Modern.Sub, Center = true };

            PasswordLayoutPanel.Controls.Remove(PasswordTextBox);
            Pin = new PinField(PasswordTextBox);

            UnlockModern = new ModernButton() { Text = "Unlock", Style = ModernButtonStyle.Primary, TextPoints = 12f };
            UnlockModern.Click += (s, e) => TryUnlock();

            var LogoBox = new Panel() { BackColor = Modern.Bg, Tag = "NoScaling" };
            LogoBox.Paint += (s, e) =>
            {
                if (Logo == null) return;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(Logo, new Rectangle(0, 0, LogoBox.Width, LogoBox.Height));
            };

            LockCard.Controls.AddRange(new Control[] { LogoBox, Title, Sub, Pin, PinMessage, UnlockModern, Foot });

            void LayoutCard()
            {
                int W = Px(420), Y = 0;

                int LS = Px(96);
                LogoBox.SetBounds((W - LS) / 2, Y, LS, LS); Y += LS + Px(20);
                Title.SetBounds(0, Y, W, Px(38)); Y += Px(38) + Px(4);
                Sub.SetBounds(0, Y, W, Px(24)); Y += Px(24) + Px(26);
                Pin.SetBounds(0, Y, W, Px(64)); Y += Px(64) + Px(8);
                PinMessage.SetBounds(0, Y, W, Px(22)); Y += Px(22) + Px(10);
                UnlockModern.SetBounds(0, Y, W, Px(52)); Y += Px(52) + Px(20);
                Foot.SetBounds(0, Y, W, Px(20)); Y += Px(20);

                LockCard.Size = new Size(W, Y);
                LockCard.Location = new Point(Math.Max(0, (PasswordPanel.ClientSize.Width - W) / 2), Math.Max(0, (PasswordPanel.ClientSize.Height - Y) / 2 - Px(20)));
            }

            PasswordPanel.Controls.Add(LockCard);
            LockCard.BringToFront();

            // the old block stays (its Visible flag is what the unlock code switches) but is moved out of sight
            PasswordLayoutPanel.Anchor = AnchorStyles.None;
            PasswordLayoutPanel.Location = new Point(-5000, -5000);

            bool WasShown = false;

            void Sync()
            {
                bool Show = PasswordPanel.Visible && PasswordLayoutPanel.Visible;

                if (WasShown && !Show && !PasswordPanel.Visible && !IsResettingPassword) PlayUnlocked();

                WasShown = Show;
                LockCard.Visible = Show;

                if (Show)
                {
                    Pin.State = PinField.PinState.Idle;
                    PinMessage.Text = string.Empty;
                    LayoutCard();
                    LockCard.BringToFront();
                    BeginInvoke((MethodInvoker)(() => Pin.Input.Focus()));
                }
            }

            PasswordPanel.Resize += (s, e) => LayoutCard();
            PasswordPanel.VisibleChanged += (s, e) => Sync();
            PasswordLayoutPanel.VisibleChanged += (s, e) => Sync();

            // first setup / changing the encryption keep their old controls, in the new colors
            ModernSkin.Apply(PasswordSelectionPanel);
            ModernSkin.Apply(EncryptionSelectionPanel);
            PasswordSelectionPanel.BackColor = EncryptionSelectionPanel.BackColor = Modern.Bg;

            LayoutCard();
            Sync();
        }

        /// <summary>Unlock through the old code; a wrong PIN comes back through PinFailed.</summary>
        private void TryUnlock()
        {
            if (Pin == null) { UnlockButton.PerformClick(); return; }

            if (Pin.Input.Text.Length == 0) { PinFailed("Type your PIN first."); return; }

            Pin.State = PinField.PinState.Checking;
            UnlockModern.Text = "Unlocking…";
            UnlockModern.Enabled = false;
            LockCard.Refresh();

            try { UnlockButton.PerformClick(); }
            finally
            {
                UnlockModern.Text = "Unlock";
                UnlockModern.Enabled = true;
                if (Pin.State == PinField.PinState.Checking) Pin.State = PinField.PinState.Idle;
            }
        }

        /// <summary>Called by the unlock code instead of a message box while the PIN screen is shown.</summary>
        private bool PinFailed(string Reason)
        {
            if (Pin == null || LockCard == null || !LockCard.Visible) return false;

            PinMessage.Text = Reason;
            PinMessage.Invalidate();
            Pin.Shake();
            System.Media.SystemSounds.Hand.Play();
            return true;
        }

        /// <summary>Short confirmation after a correct PIN: logo + check, then the accounts.</summary>
        private void PlayUnlocked()
        {
            ProfileVault.SealAllInBackground(); // the PIN is known now: protect any profile left unlocked by a crash

            var Overlay = new Panel() { BackColor = Modern.Bg, Tag = "NoScaling", Bounds = ClientRectangle, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            var Started = DateTime.Now;

            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(Overlay, true);

            Overlay.Paint += (s, e) =>
            {
                var G = e.Graphics;
                Modern.Prepare(G);
                double T = (DateTime.Now - Started).TotalMilliseconds;

                int Box = Px(76), CX = Overlay.Width / 2, CY = Overlay.Height / 2 - Px(24);
                var R = new Rectangle(CX - Box / 2, CY - Box / 2, Box, Box);

                // square draws itself edge by edge, then the check
                double P = Math.Min(1, T / 260);
                using (var Pen = new Pen(Modern.Accent, Px(3)))
                {
                    float L = (float)(P * 4);
                    if (L > 0) G.DrawLine(Pen, R.Left, R.Top, R.Left + (int)(R.Width * Math.Min(1, L)), R.Top);
                    if (L > 1) G.DrawLine(Pen, R.Right, R.Top, R.Right, R.Top + (int)(R.Height * Math.Min(1, L - 1)));
                    if (L > 2) G.DrawLine(Pen, R.Right, R.Bottom, R.Right - (int)(R.Width * Math.Min(1, L - 2)), R.Bottom);
                    if (L > 3) G.DrawLine(Pen, R.Left, R.Bottom, R.Left, R.Bottom - (int)(R.Height * Math.Min(1, L - 3)));
                }

                if (T > 220) ModernSkin.DrawCheck(G, Rectangle.Inflate(R, -Px(14), -Px(14)), Color.White);
                if (T > 300) Modern.Text_(G, "Unlocked", Modern.Font(14f, FontStyle.Regular, true), new Rectangle(0, R.Bottom + Px(18), Overlay.Width, Px(30)), Modern.Text, TextFormatFlags.HorizontalCenter);
            };

            Controls.Add(Overlay);
            Overlay.BringToFront();

            var Clock = new Timer() { Interval = 15 };
            Clock.Tick += (s, e) =>
            {
                Overlay.Invalidate();

                if ((DateTime.Now - Started).TotalMilliseconds > 750)
                {
                    Clock.Stop();
                    Clock.Dispose();
                    Controls.Remove(Overlay);
                    Overlay.Dispose();
                }
            };
            Clock.Start();
        }

        #endregion

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && PageHost != null && PageHost.Visible && !PasswordPanel.Visible)
            {
                ClosePage();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
