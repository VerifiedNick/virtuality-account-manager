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
                Heading = () => ("Account tools", SelectedAccount != null ? SelectedAccount.Username : "No account selected"),
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
                Heading = () => ("Account control", ControlForm.PageTabs.SelectedTab?.Text ?? string.Empty)
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
                    ControlForm.PageTabs.HideStrip = true;
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
