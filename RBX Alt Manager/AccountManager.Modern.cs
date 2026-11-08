using BrightIdeasSoftware;
using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    /// <summary>
    /// The new main window: header (game / job / join), groups on the left, account list in the middle,
    /// account card with copy / login / friend request on the right. All old controls and handlers are reused;
    /// the ones without a place in the new layout live in a hidden holder and are reached through menus.
    /// </summary>
    public partial class AccountManager
    {
        private Panel Shell, LegacyHolder;
        private LinePanel HeaderBar, SideBar, CenterPane, ListFrame;
        private FieldBox GameField, JobField, SearchField;
        private TextBox SearchBox;
        private ModernButton JoinSelectedButton, ServerListIcon, RecentGamesIcon, AccountControlIcon, SettingsIcon, SaveJobIcon;
        private ModernButton NewGroupButton;
        private Panel EmptyGroupPanel;

        /// <summary>Shows the "no accounts yet" page for an empty group (not while searching).</summary>
        private void UpdateEmptyGroup()
        {
            if (EmptyGroupPanel == null) return;

            bool Show = ActiveGroupKey != null && SearchBox.Text.Trim().Length == 0 && !AccountsList.Any(a => a.Group == ActiveGroupKey);

            if (Show) { EmptyGroupPanel.PerformLayout(); EmptyGroupPanel.Invalidate(true); EmptyGroupPanel.BringToFront(); }
            EmptyGroupPanel.Visible = Show;
        }
        private ModernButton AddAccountButton, CheckBansButton, SelectGroupButton, ViewToggleButton, LoginGroupButton;
        private AccountGrid Grid;
        private bool CardView = true;
        private readonly System.Windows.Forms.Timer CardPresenceTimer = new System.Windows.Forms.Timer() { Interval = 60000 };
        private ModernLabel GroupsCaption, GroupTitleLabel, GroupSubLabel, TipLabel;
        private AccountCardPanel AccountCard;
        private ContextMenuStrip MoreToolsMenu;
        private ToolStripMenuItem HideUsernamesMenuItem;
        private OLVColumn StatusColumn;

        private string ActiveGroupKey; // null = all accounts
        private readonly System.Windows.Forms.Timer ClipboardTimer = new System.Windows.Forms.Timer() { Interval = 30000 };
        private string ClipboardSecret;
        private CancellationTokenSource FriendTokenSource, BanTokenSource;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, string lParam);

        private static int Px(float Value) => Modern.Px(Value);

        #region Layout

        /// <summary>Runs right after InitializeComponent (before Rescale), so sizes here are in designer units.</summary>
        private void BuildModernShell()
        {
            SuspendLayout();

            ClientSize = new Size(1260, 740);
            MinimumSize = new Size(1100, 700);
            MaximizeBox = true;
            BackColor = Modern.Bg;
            Text = "Virtuality Manager";

            // old controls that have no place in the new layout: kept (their code still runs), just never shown
            LegacyHolder = new Panel() { Name = "LegacyHolder", Visible = false, Size = new Size(10, 10) };

            foreach (Control C in new Control[] { Remove, Add, JoinServer, SetDescription, SetAlias, Follow, LabelUserID, UserID, ServerList, HideUsernamesCheckbox, BrowserButton, ArgumentsB, LabelPlaceID, LabelJobID, JoinDiscord, SaveToAccount, DonateButton, EditTheme, LaunchNexus, ConfigButton, HistoryIcon, Alias, DescriptionBox, OpenBrowser })
            {
                Controls.Remove(C);
                LegacyHolder.Controls.Add(C);
            }

            Shell = new Panel() { Name = "Shell", Location = Point.Empty, Size = ClientSize, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Modern.Bg, Tag = "NoScaling" };

            HeaderBar = new LinePanel() { Dock = DockStyle.Top, Height = 64, BackColor = Modern.Panel, Lines = AnchorStyles.Bottom };
            SideBar = new LinePanel() { Dock = DockStyle.Left, Width = 266, BackColor = Modern.Panel, Lines = AnchorStyles.Right };
            CenterPane = new LinePanel() { Dock = DockStyle.Fill, BackColor = Modern.Bg };
            AccountCard = new AccountCardPanel() { Dock = DockStyle.Right, Width = 356 };

            // docking happens in reverse order: header first, then the sides, the list takes the rest
            Shell.Controls.Add(CenterPane);
            Shell.Controls.Add(AccountCard);
            Shell.Controls.Add(SideBar);
            Shell.Controls.Add(HeaderBar);

            BuildHeader();
            BuildSideBar();
            BuildCenter();
            BuildCard();
            BuildPages();
            BuildLockScreen();

            Controls.Add(Shell);
            Controls.Add(LegacyHolder);

            PasswordPanel.BringToFront();

            ClipboardTimer.Tick += ClipboardTimer_Tick;

            ResumeLayout(false);
        }

        private void BuildHeader()
        {
            // left of the header: a quick overview instead of a second "Lizer Manager" (the name is in the window title)
            HeaderBar.Paint += (s, e) =>
            {
                var G = e.Graphics;
                Modern.Prepare(G);

                var List = AccountsList;
                if (List == null) return;

                int Total = List.Count, Online = 0, InGame = 0, Banned = 0, Login = 0;

                foreach (var A in List)
                    switch (AccountRowRenderer.StateOf(A))
                    {
                        case AccountState.InGame: InGame++; break;
                        case AccountState.Online: case AccountState.InStudio: Online++; break;
                        case AccountState.Banned: Banned++; break;
                        case AccountState.NeedsLogin: Login++; break;
                    }

                int X = Modern.Gutter, W = SideBar.Width - Modern.Gutter * 2, H = HeaderBar.Height;

                Modern.Text_(G, $"{Total} accounts", Modern.Font(10.5f, FontStyle.Regular, true), new Rectangle(X, H / 2 - Px(20), W, Px(20)), Modern.Text, TextFormatFlags.NoPadding);

                int CX = X, CY = H / 2 + Px(2), CH = Px(16);
                Font F = Modern.Font(8.25f);

                // something running in the background (login window, group login, ban check, joining …): show it instead of the counts
                var Jobs = Activities.Running;

                if (Jobs.Count > 0)
                {
                    var Job = Jobs[Jobs.Count - 1];
                    int Dot = Px(7);
                    bool On = (Environment.TickCount / 400) % 2 == 0;

                    using (var B = new SolidBrush(On ? Modern.AccentText : Modern.Accent)) G.FillRectangle(B, CX, CY + (CH - Dot) / 2, Dot, Dot);

                    string T = Job.Text + (Jobs.Count > 1 ? $"  +{Jobs.Count - 1}" : string.Empty);
                    Modern.Text_(G, T, F, new Rectangle(CX + Dot + Px(6), CY, W - Dot - Px(6), CH), Modern.AccentText, TextFormatFlags.NoPadding);
                    return;
                }

                var Stats = new List<(int Count, string Long, string Short, Color C)>
                {
                    (InGame, "in game", "in game", Modern.Online),
                    (Online, "online", "online", Modern.Text2),
                    (Login, "logged out", "out", Modern.Warn),
                    (Banned, "banned", "banned", Modern.Danger),
                };

                int Need(bool Short) => Stats.Where(t => t.Count > 0).Sum(t => Px(7) + Px(5) + TextRenderer.MeasureText($"{t.Count} {(Short ? t.Short : t.Long)}", F, Size.Empty, TextFormatFlags.NoPadding).Width + Px(10)) - Px(10);
                bool UseShort = Need(false) > W; // narrow: "161 out" instead of "161 logged out", nothing gets cut off

                foreach (var (Count, Long, Short, C) in Stats)
                {
                    if (Count <= 0 || CX > X + W) continue;

                    int Dot = Px(7);
                    using (var B = new SolidBrush(C)) G.FillRectangle(B, CX, CY + (CH - Dot) / 2, Dot, Dot);

                    string T = $"{Count} {(UseShort ? Short : Long)}";
                    int TW = TextRenderer.MeasureText(T, F, Size.Empty, TextFormatFlags.NoPadding).Width;
                    Modern.Text_(G, T, F, new Rectangle(CX + Dot + Px(5), CY, Math.Min(TW + Px(2), X + W - CX), CH), Modern.Sub, TextFormatFlags.NoPadding);
                    CX += Dot + Px(5) + TW + Px(10);
                }

                if (InGame + Online + Login + Banned == 0)
                    Modern.Text_(G, "all offline", F, new Rectangle(X, CY, W, CH), Modern.Sub, TextFormatFlags.NoPadding);
            };

            GameField = new FieldBox() { Caption = "Game" };
            CurrentPlace.AutoSize = false;
            CurrentPlace.Text = string.Empty;
            PlaceID.BorderStyle = BorderStyle.None;
            GameField.AddPart(PlaceID, 110);
            GameField.AddPart(CurrentPlace, -1);
            SaveTooltip.SetToolTip(PlaceID, "Place ID or game link");

            JobField = new FieldBox() { Caption = "Job ID" };
            JobID.BorderStyle = BorderStyle.None;
            SaveJobIcon = new ModernButton() { Icon = IconKind.Save, Style = ModernButtonStyle.Ghost };
            SaveJobIcon.Click += (s, e) => SaveToAccount_Click(SaveJobIcon, EventArgs.Empty);
            JobField.AddPart(JobID, -1);
            JobField.AddPart(ShuffleIcon, 18);
            JobField.AddPart(SaveJobIcon, 26);
            ShuffleIcon.Cursor = Cursors.Hand;
            SaveTooltip.SetToolTip(SaveJobIcon, "Saves the Place ID + Job ID to the selected account(s)\nClear both boxes and click Save to remove it\nShift + click removes every saved Place ID");

            JoinSelectedButton = new ModernButton() { Text = "Join selected", Style = ModernButtonStyle.Primary, TextPoints = 9.75f };
            JoinSelectedButton.Click += (s, e) => JoinServer_Click(JoinSelectedButton, EventArgs.Empty);
            SaveTooltip.SetToolTip(JoinSelectedButton, "Joins the game above with every selected account");

            ServerListIcon = new ModernButton() { Icon = IconKind.Servers };
            ServerListIcon.Click += (s, e) => ServerList_Click(ServerListIcon, EventArgs.Empty);
            SaveTooltip.SetToolTip(ServerListIcon, "Servers, games, favorites, outfits, watcher");

            RecentGamesIcon = new ModernButton() { Icon = IconKind.Clock };
            RecentGamesIcon.Click += (s, e) => OpenPage("recent");
            SaveTooltip.SetToolTip(RecentGamesIcon, "Recent games");

            AccountControlIcon = new ModernButton() { Icon = IconKind.Tools };
            AccountControlIcon.Click += (s, e) => OpenPage("tools");
            SaveTooltip.SetToolTip(AccountControlIcon, "Account tools: password, e-mail, display name, friends, privacy");

            SettingsIcon = new ModernButton() { Icon = IconKind.Gear };
            SettingsIcon.Click += (s, e) => ConfigButton_Click(SettingsIcon, EventArgs.Empty);
            SaveTooltip.SetToolTip(SettingsIcon, "Settings");

            HeaderBar.Controls.AddRange(new Control[] { GameField, JobField, JoinSelectedButton, ServerListIcon, RecentGamesIcon, AccountControlIcon, SettingsIcon });

            // background jobs: the header shows them (blinking square) on every page; click there to stop one
            var Pulse = new System.Windows.Forms.Timer() { Interval = 400 };
            Pulse.Tick += (s, e) => { if (Activities.Any) HeaderBar.Invalidate(new Rectangle(0, 0, SideBar.Width, HeaderBar.Height)); else Pulse.Stop(); };

            Activities.Changed += () =>
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke((MethodInvoker)(() => { HeaderBar.Invalidate(); if (Activities.Any) Pulse.Start(); }));
            };

            HeaderBar.MouseClick += (s, e) =>
            {
                if (e.X > SideBar.Width || !Activities.Any) return;

                var Menu = new ContextMenuStrip();

                foreach (var Job in Activities.Running)
                {
                    var J = Job;
                    var Item = Menu.Items.Add($"Stop: {J.Text}", null, (a, b) => { try { J.Cancel?.Cancel(); } catch { } });
                    Item.Enabled = J.Cancel != null;
                }

                ModernSkin.StyleMenu(Menu);
                Menu.Closed += (a, b) => BeginInvoke((MethodInvoker)Menu.Dispose);
                Menu.Show(HeaderBar, e.Location);
            };

            HeaderBar.Layout += (s, e) =>
            {
                if (HeaderBar.Width < Px(400)) return;

                // the header follows the three columns below it: overview | game + job (= list width) | join + icons (= card width)
                int G = Modern.Gutter, H = Modern.RowH, Y = (HeaderBar.Height - H) / 2, Gap = Px(8);

                int CenterLeft = SideBar.Width + G, CenterRight = HeaderBar.Width - AccountCard.Width - G;
                int RightLeft = HeaderBar.Width - AccountCard.Width + G, Right = HeaderBar.Width - G;

                // icons start on the line of the account card, "Join selected" ends at the right edge
                int Left = RightLeft;

                foreach (var Icon in new[] { ServerListIcon, RecentGamesIcon, AccountControlIcon, SettingsIcon })
                {
                    Icon.SetBounds(Left, Y, H, H);
                    Left += H + Gap;
                }

                JoinSelectedButton.SetBounds(Left, Y, Math.Max(Px(90), Right - Left), H);

                int JobW = Math.Min(Px(280), (CenterRight - CenterLeft) / 3);
                JobField.SetBounds(CenterRight - JobW, Y, JobW, H);
                GameField.SetBounds(CenterLeft, Y, Math.Max(Px(160), JobField.Left - Gap - CenterLeft), H);
            };
        }

        private void BuildSideBar()
        {
            SearchBox = new TextBox() { BorderStyle = BorderStyle.None, Name = "SearchBox", Tag = "NoScaling" };
            SearchBox.TextChanged += (s, e) => ApplyAccountFilter();
            SearchBox.HandleCreated += (s, e) => SendMessage(SearchBox.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, "Search accounts…");

            SearchField = new FieldBox() { CaptionIcon = IconKind.Search };
            SearchField.AddPart(SearchBox, -1);

            GroupsCaption = new ModernLabel() { Text = "GROUPS", Points = 7.75f, Bold = true, Color = Modern.Sub };

            NewGroupButton = new ModernButton() { Text = "New group", Icon = IconKind.Plus, Style = ModernButtonStyle.Ghost, TextPoints = 8.75f };
            NewGroupButton.Click += (s, e) => NewGroup();
            SaveTooltip.SetToolTip(NewGroupButton, "Makes a new group (the selected accounts can move there, or it starts empty) and lets you choose its picture.\nRight click a group to rename it or change its picture.");

            GroupsSidebar = new GroupSidebar() { Name = "GroupsSidebar", BackColor = Modern.Panel };
            GroupsSidebar.GroupClicked += (s, e) => SetActiveGroup(e.Item.Key);
            GroupsSidebar.GroupDoubleClicked += (s, e) => { SetActiveGroup(e.Item.Key); SelectAllVisible(); };
            GroupsSidebar.GroupRightClicked += GroupsSidebar_GroupRightClicked;
            SaveTooltip.SetToolTip(GroupsSidebar, "Click: show group · Double click: select all accounts of the group · Right click: change picture");

            AddAccountButton = new ModernButton() { Text = "Add account", Icon = IconKind.Plus, TextPoints = 9.75f };
            AddAccountButton.Click += (s, e) =>
            {
                if (IsDownloadingChromium) DownloadProgressBar_Click(AddAccountButton, EventArgs.Empty);
                else ShowAddAccountDialog();
            };

            Controls.Remove(DownloadProgressBar);
            Controls.Remove(DLChromiumLabel);

            SideBar.Controls.AddRange(new Control[] { SearchField, GroupsCaption, NewGroupButton, GroupsSidebar, DLChromiumLabel, DownloadProgressBar, AddAccountButton });

            DownloadProgressBar.VisibleChanged += (s, e) => SideBar.PerformLayout();
            DLChromiumLabel.VisibleChanged += (s, e) => SideBar.PerformLayout();

            SideBar.Layout += (s, e) =>
            {
                if (SideBar.Width < Px(60) || SideBar.Height < Px(200)) return;

                int P = Modern.Gutter, W = SideBar.Width - P * 2 - 1;

                SearchField.SetBounds(P, P, W, Modern.RowH);
                GroupsCaption.SetBounds(P, SearchField.Bottom + Px(10), W - Px(120), Px(26));
                NewGroupButton.SetBounds(P + W - Px(116), SearchField.Bottom + Px(10), Px(116), Px(26));

                int Bottom = SideBar.Height - P;

                AddAccountButton.SetBounds(P, Bottom - Px(42), W, Px(42));
                Bottom -= Px(42) + Px(10);

                if (DownloadProgressBar.Visible || DLChromiumLabel.Visible)
                {
                    DownloadProgressBar.SetBounds(P, Bottom - Px(8), W, Px(8));
                    Bottom -= Px(8) + Px(4);
                    DLChromiumLabel.AutoSize = false;
                    DLChromiumLabel.SetBounds(P, Bottom - Px(18), W, Px(18));
                    Bottom -= Px(18) + Px(8);
                }

                // the group cards have 6 px inner padding, so the control starts 6 px further left to line up with the search field
                int Top = GroupsCaption.Bottom + Px(6);
                GroupsSidebar.SetBounds(P - Px(6), Top, SideBar.Width - (P - Px(6)) * 2 - 1, Math.Max(Px(40), Bottom - Top));
            };
        }

        private void BuildCenter()
        {
            GroupTitleLabel = new ModernLabel() { Text = "All accounts", Points = 15f, Bold = true };
            GroupSubLabel = new ModernLabel() { Points = 9f, Color = Modern.Sub };
            TipLabel = new ModernLabel() { Text = "Select several: Ctrl / Shift + click or drag a frame  ·  Right click a card for more", Points = 8.25f, Color = Modern.Sub };

            LoginGroupButton = new ModernButton() { Text = "Log in group", Icon = IconKind.Login, Style = ModernButtonStyle.Outline, Visible = false };
            LoginGroupButton.Click += (s, e) => LoginGroupQueue();
            SaveTooltip.SetToolTip(LoginGroupButton, "Logs in every logged-out account of this group, one after another with a pause.\nYou only solve the captcha. Click again to stop.");

            CheckBansButton = new ModernButton() { Text = "Check for bans", FullText = "Check for bans", ShortText = "Bans", Icon = IconKind.Ban, Style = ModernButtonStyle.Danger };
            CheckBansButton.Click += (s, e) => CheckForBans();
            SaveTooltip.SetToolTip(CheckBansButton, "Checks every account with the Roblox API and marks banned ones red.\nNothing is deleted or moved.");

            SelectGroupButton = new ModernButton() { Text = "Select group", FullText = "Select group", ShortText = "Select" };
            SelectGroupButton.Click += (s, e) => SelectAllVisible();

            CardView = General == null || !General.Exists("MainView") || General.Get("MainView") != "List";

            ViewToggleButton = new ModernButton() { Text = CardView ? "List view" : "Card view", FullText = CardView ? "List view" : "Card view", ShortText = CardView ? "List" : "Cards" };
            ViewToggleButton.Click += (s, e) => SetCardView(!CardView);
            SaveTooltip.SetToolTip(ViewToggleButton, "Switch between account cards and the classic list (the list shows more accounts at once and can be sorted by clicking a column)");

            ListFrame = new LinePanel() { BackColor = Modern.Bg, FillBorderColor = Modern.Line, Padding = new Padding(1) };

            Controls.Remove(AccountsView);
            AccountsView.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            AccountsView.Dock = DockStyle.Fill;
            AccountsView.BorderStyle = BorderStyle.None;
            ListFrame.Controls.Add(AccountsView);

            Grid = new AccountGrid() { Dock = DockStyle.Fill, Visible = true, RowMode = !CardView };
            Grid.GetSelection = () => new HashSet<Account>(AccountsView.SelectedObjects.Cast<object>().OfType<Account>());
            Grid.SetSelection = List => { AccountsView.SelectedObjects = List; Grid.Invalidate(); };
            Grid.JoinClicked = A => { SelectedAccount = A; JoinServer_Click(Grid, EventArgs.Empty); };
            Grid.JoinMainClicked = JoinMainWith;
            Grid.ReloginClicked = A => LoginAccount(A); // the card knows its account (the right card updates a moment later)
            Grid.ContextRequested = (A, P) => AccountsStrip.Show(Grid, P);
            Grid.SectionTitle = GroupTitleOf;
            ListFrame.Controls.Add(Grid);

            // empty group (created without accounts): explain it and offer "Add account" right here
            EmptyGroupPanel = new Panel() { Dock = DockStyle.Fill, BackColor = Modern.Bg, Visible = false, Tag = "NoScaling" };
            var EmptyTitle = new ModernLabel() { Points = 13f, Bold = true, Center = true };
            var EmptySub = new ModernLabel() { Text = "Add a new account here – it goes straight into this group.\nOr move existing accounts: select them, right click → Move to group.", Points = 9.5f, Color = Modern.Sub, Center = true, Wrap = true };
            var EmptyAdd = new ModernButton() { Text = "Add account", Icon = IconKind.Plus, Style = ModernButtonStyle.Primary, TextPoints = 10f };
            var EmptyDelete = new ModernButton() { Text = "Delete this empty group", Style = ModernButtonStyle.Ghost, Bold = false };

            EmptyAdd.Click += (s, e) => ShowAddAccountDialog();
            EmptyDelete.Click += (s, e) => { if (ActiveGroupKey != null) DeleteEmptyGroup(ActiveGroupKey); };

            EmptyGroupPanel.Controls.AddRange(new Control[] { EmptyTitle, EmptySub, EmptyAdd, EmptyDelete });
            EmptyGroupPanel.Layout += (s, e) =>
            {
                EmptyTitle.Text = ActiveGroupKey != null ? $"{GroupTitleOf(ActiveGroupKey)} has no accounts yet" : string.Empty;

                int W = Math.Min(EmptyGroupPanel.Width - Modern.Gutter * 2, Px(460)), X = (EmptyGroupPanel.Width - W) / 2;
                int Y = Math.Max(Modern.Gutter, EmptyGroupPanel.Height / 2 - Px(110));

                EmptyTitle.SetBounds(X, Y, W, Px(30)); Y += Px(30) + Px(8);
                EmptySub.SetBounds(X, Y, W, Px(44)); Y += Px(44) + Px(20);
                EmptyAdd.SetBounds(X + (W - Px(220)) / 2, Y, Px(220), Px(44)); Y += Px(44) + Px(10);
                EmptyDelete.SetBounds(X + (W - Px(220)) / 2, Y, Px(220), Px(34));
            };
            ListFrame.Controls.Add(EmptyGroupPanel);

            Modern.DarkScrollbars(Grid);
            Modern.DarkScrollbars(AccountsView);
            Modern.DarkScrollbars(GroupsSidebar);
            Grid.BringToFront(); // the list stays behind it (still used for selection, menus and the list view)

            AvatarCache.Changed += (s, e) => { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)(() => { Grid?.Invalidate(); AccountCard?.Invalidate(true); })); };

            CardPresenceTimer.Tick += async (s, e) => await UpdateCardPresence(true);
            CardPresenceTimer.Start();

            CenterPane.Controls.AddRange(new Control[] { GroupTitleLabel, GroupSubLabel, LoginGroupButton, CheckBansButton, SelectGroupButton, ViewToggleButton, ListFrame, TipLabel });

            CenterPane.Layout += (s, e) =>
            {
                if (CenterPane.Width < Px(300) || CenterPane.Height < Px(160)) return;

                int P = Modern.Gutter, W = CenterPane.Width - P * 2, RH = Modern.RowH, Gap = Px(8);

                // buttons from the right; when the group name would get too little room they get shorter labels, then icons only
                var Row = new List<ModernButton>();
                if (LoginGroupButton.Visible) Row.Add(LoginGroupButton);
                Row.AddRange(new[] { CheckBansButton, SelectGroupButton, ViewToggleButton });

                int TitleRight = 0;

                for (int Mode = 0; Mode < 3; Mode++)
                {
                    foreach (var B in Row)
                    {
                        B.Compact = Mode >= 1;
                        B.IconOnly = Mode >= 2 && B.Icon != IconKind.None;
                    }

                    int X = P + W;
                    foreach (var B in Row.AsEnumerable().Reverse())
                    {
                        int BW = Math.Max(Mode == 0 ? Px(100) : Px(RH), B.NeededWidth());
                        X -= BW;
                        B.SetBounds(X, P, BW, RH);
                        X -= Gap;
                    }

                    TitleRight = X;
                    if (TitleRight - P >= Px(Mode == 0 ? 260 : 200)) break;
                }

                foreach (var B in Row) B.Invalidate();

                // title + subtitle share the height of the button row; never under the buttons
                GroupTitleLabel.SetBounds(P, P - Px(4), Math.Max(Px(40), TitleRight - P - Px(6)), Px(28));
                GroupSubLabel.SetBounds(P, P + RH - Px(16), GroupTitleLabel.Width, Px(16));

                int ListTop = P + RH + Px(14);
                ListFrame.SetBounds(P, ListTop, W, Math.Max(Px(60), CenterPane.Height - ListTop - Px(40)));
                TipLabel.SetBounds(P, ListFrame.Bottom + Px(10), W, Px(18));
            };

            // list look: flat rows drawn by AccountRowRenderer
            AccountsView.Font = new Font("Segoe UI", 9.25f);
            AccountsView.OwnerDraw = true;
            AccountsView.UseFiltering = true;
            AccountsView.EmptyListMsg = "No accounts here";
            AccountsView.HeaderUsesThemes = false;
            AccountsView.BackColor = Modern.Bg;
            AccountsView.ForeColor = Modern.Text;
            AccountsView.ShowGroups = false; // groups are in the sidebar; the GROUP column shows it per row
            AccountsView.GridLines = false;
            AccountsView.UseAlternatingBackColors = false;

            StatusColumn = new OLVColumn("STATUS", string.Empty) { IsEditable = false, Sortable = false, Width = 120, AspectGetter = o => o is Account A ? AccountRowRenderer.StateText(A, AccountRowRenderer.StateOf(A)) : string.Empty };

            Username.Text = "ACCOUNT";
            Username.Width = 250;
            Description.Text = "NOTE";
            Description.Width = 180;
            Description.FillsFreeSpace = true;
            LastUsedColumn.Text = "LAST USED";
            LastUsedColumn.Width = 110;
            LastUsedColumn.IsVisible = true;
            LastUsedColumn.TextAlign = HorizontalAlignment.Left;
            LastUsedColumn.IsEditable = false;
            AccountAlias.IsVisible = false;

            AccountsView.AllColumns.Add(StatusColumn);
            AccountsView.Columns.Clear();
            AccountsView.Columns.AddRange(new ColumnHeader[] { Username, Description, StatusColumn, LastUsedColumn, Group });

            Username.Renderer = new AccountRowRenderer(AccountColumnKind.Account);
            Description.Renderer = new AccountRowRenderer(AccountColumnKind.Note);
            StatusColumn.Renderer = new AccountRowRenderer(AccountColumnKind.Status);
            LastUsedColumn.Renderer = new AccountRowRenderer(AccountColumnKind.LastUsed);
            Group.Text = "GROUP";
            Group.Width = 170;
            Group.IsEditable = false;
            Group.Renderer = new AccountRowRenderer(AccountColumnKind.Group);
            AccountRowRenderer.GroupTitle = GroupTitleOf;

            AccountsView.SelectionChanged += (s, e) => { UpdateAccountCard(); Grid?.Invalidate(); };
        }

        private void BuildCard()
        {
            AccountCard.GroupTitle = GroupTitleOf;

            AccountCard.CopyUser.Click += (s, e) => CopyToClipboard(string.Join(Environment.NewLine, AccountCard.Selection.Select(a => a.Username)), AccountCard.CopyUser);
            AccountCard.CopyPassword.Click += (s, e) => CopyToClipboard(string.Join(Environment.NewLine, AccountCard.Selection.Where(a => !string.IsNullOrEmpty(a.Password)).Select(a => a.Password)), AccountCard.CopyPassword);
            AccountCard.Login.Click += (s, e) => LoginSelectedAccount();
            AccountCard.LoginRequested = A => LoginAccount(A);
            AccountCard.AddFriend.Click += (s, e) => AddFriendToMain();
            AccountCard.OpenBrowser.Click += (s, e) => OpenBrowser_Click(AccountCard.OpenBrowser, EventArgs.Empty);
            AccountCard.OpenBrowser.Menu = OpenBrowserStrip;
            AccountCard.Follow.Click += (s, e) => FollowUserPrompt();
            AccountCard.ChangeMain.Click += (s, e) => ChangeMainAccount();
            AccountCard.ChangeJoin.Click += (s, e) => ShowJoinTargetMenu(AccountCard.ChangeJoin, new Point(0, AccountCard.ChangeJoin.Height));
            Grid.JoinTargetMenuRequested = P => ShowJoinTargetMenu(Grid, P);
            SaveTooltip.SetToolTip(AccountCard.ChangeJoin, "Choose which account the \"Join …\" button on every account joins – from your groups or any Roblox user.\nTip: right click the \"Join …\" button does the same.");
            AccountCard.EditNote.Click += (s, e) => EditNoteDialog();
            AccountCard.Remove.Click += (s, e) => Remove_Click(AccountCard.Remove, EventArgs.Empty);

            SaveTooltip.SetToolTip(AccountCard.AddFriend, "The selected accounts send your main account a friend request, one after another with a pause.\nClick again to stop.");
            SaveTooltip.SetToolTip(AccountCard.OpenBrowser, "Opens a browser logged in as the account (arrow: URL / URL + JS / Join group)");

            MoreToolsMenu = new ContextMenuStrip();
            MoreToolsMenu.Items.Add("Account tools", null, (s, e) => OpenPage("tools"));
            MoreToolsMenu.Items.Add("Account control (Nexus)", null, (s, e) => LaunchNexus_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Servers & games", null, (s, e) => ServerList_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Recent games", null, (s, e) => OpenPage("recent"));
            MoreToolsMenu.Items.Add(new ToolStripSeparator());
            MoreToolsMenu.Items.Add("Save Place + Job ID to account", null, (s, e) => SaveToAccount_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Quick Log In…", null, (s, e) => quickLogInToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Copy username:password", null, (s, e) => copyUserPassComboToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Copy profile link", null, (s, e) => copyProfileToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Move to group…", null, (s, e) => moveToToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Dump details", null, (s, e) => ShowDetailsToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add(new ToolStripSeparator());

            HideUsernamesMenuItem = new ToolStripMenuItem("Hide usernames") { CheckOnClick = true };
            HideUsernamesMenuItem.CheckedChanged += (s, e) => HideUsernamesCheckbox.Checked = HideUsernamesMenuItem.Checked;
            MoreToolsMenu.Items.Add(HideUsernamesMenuItem);
            MoreToolsMenu.Items.Add("Settings", null, (s, e) => ConfigButton_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Launch arguments (dev)", null, (s, e) => ArgumentsB_Click(null, EventArgs.Empty)).Name = "LaunchArgumentsItem";
            MoreToolsMenu.Items.Add(new ToolStripSeparator());
            MoreToolsMenu.Items.Add("Help: groups", null, (s, e) => groupsToolStripMenuItem_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Info", null, (s, e) => infoToolStripMenuItem1_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Join RAM Discord", null, (s, e) => JoinDiscord_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Donate to the RAM developer", null, (s, e) => DonateButton_Click(null, EventArgs.Empty));

            MoreToolsMenu.Opening += (s, e) =>
            {
                HideUsernamesMenuItem.Checked = HideUsernamesCheckbox.Checked;
                MoreToolsMenu.Items["LaunchArgumentsItem"].Visible = Developer != null && Developer.Get<bool>("DevMode");
            };

            AccountCard.MoreTools.Menu = MoreToolsMenu;

            AccountCard.FriendTarget = FriendTargetName;
            Grid.MainName = JoinTargetName;
            AccountCard.JoinTarget = JoinTargetName;
        }

        /// <summary>Restyles the reused controls after the theme engine ran over them.</summary>
        private void ApplyModernStyle()
        {
            if (Shell == null) return;

            BackColor = Modern.Bg;

            foreach (TextBox T in new TextBox[] { PlaceID, JobID, SearchBox })
            {
                T.BorderStyle = BorderStyle.None;
                T.BackColor = Modern.Field;
                T.ForeColor = Modern.Text;
                T.Font = Modern.Font(9.75f);
            }

            CurrentPlace.AutoSize = false;
            CurrentPlace.BackColor = Modern.Field;
            CurrentPlace.ForeColor = Modern.Sub;
            CurrentPlace.Font = Modern.Font(9f);

            ShuffleIcon.BackColor = Modern.Field;

            DLChromiumLabel.BackColor = Modern.Panel;
            DLChromiumLabel.ForeColor = Modern.Sub;
            DLChromiumLabel.Font = Modern.Font(8.25f);

            AccountsView.BackColor = Modern.Bg;
            AccountsView.ForeColor = Modern.Text;

            var Header = new HeaderFormatStyle();
            Header.SetBackColor(Modern.Bg);
            Header.SetForeColor(Modern.Sub);
            Header.SetFont(Modern.Font(7.75f, FontStyle.Regular, true));
            AccountsView.HeaderFormatStyle = Header;

            GroupsSidebar.BackColor = Modern.Panel;

            foreach (var Strip in new ToolStrip[] { MoreToolsMenu, AccountsStrip, OpenBrowserStrip, AddAccountsStrip })
                StyleMenu(Strip);

            GameField.PerformLayout();
            JobField.PerformLayout();
            SearchField.PerformLayout();
            Shell.Invalidate(true);
        }

        private class DarkMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Modern.Panel;
            public override Color MenuBorder => Modern.FieldBorder;
            public override Color MenuItemBorder => Modern.FieldBorder;
            public override Color MenuItemSelected => Modern.Hover;
            public override Color MenuItemSelectedGradientBegin => Modern.Hover;
            public override Color MenuItemSelectedGradientEnd => Modern.Hover;
            public override Color MenuItemPressedGradientBegin => Modern.Pressed;
            public override Color MenuItemPressedGradientEnd => Modern.Pressed;
            public override Color ImageMarginGradientBegin => Modern.Panel;
            public override Color ImageMarginGradientMiddle => Modern.Panel;
            public override Color ImageMarginGradientEnd => Modern.Panel;
            public override Color SeparatorDark => Modern.Line;
            public override Color SeparatorLight => Modern.Line;
            public override Color CheckBackground => Modern.Hover;
            public override Color CheckSelectedBackground => Modern.Pressed;
            public override Color CheckPressedBackground => Modern.Pressed;
        }

        private static readonly ToolStripProfessionalRenderer DarkMenuRenderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false };

        private static void StyleMenu(ToolStrip Strip)
        {
            if (Strip == null) return;

            Strip.Renderer = DarkMenuRenderer;
            Strip.BackColor = Modern.Panel;
            Strip.ForeColor = Modern.Text;

            foreach (ToolStripItem Item in Strip.Items)
            {
                Item.ForeColor = Modern.Text;

                if (Item is ToolStripMenuItem Menu && Menu.HasDropDownItems)
                    StyleMenu(Menu.DropDown);
            }
        }

        #endregion

        #region Groups + filter

        /// <summary>Display name of a group: without RAM's number prefix and without the "A: " letter (the group itself is not renamed, so the order stays).</summary>
        private string GroupTitleOf(string Key)
        {
            if (Key == null) return "All accounts";

            string Title = (Group.GroupKeyToTitleConverter?.Invoke(Key) ?? Key).Trim().TrimStart(':', '-', '.', ' ');
            string Short = System.Text.RegularExpressions.Regex.Replace(Title, @"^[A-Za-z0-9]{1,3}\s*:\s*", string.Empty).Trim();

            if (Short.Length == 0) Short = Title;

            // abbreviations like "HSA" show the Rank chosen for the group's picture
            if (!Short.Contains(" ") && Short.Length <= 5 && Short.ToUpperInvariant() == Short && GroupImages.GetOverride(Key) is string Entry && !GroupImages.IsCustom(Entry))
                return Entry;

            return Short;
        }

        private void SetActiveGroup(string Key)
        {
            ActiveGroupKey = Key;
            NewAccountGroup = Key; // "Add account" puts new accounts into the open group
            GroupsSidebar?.SetActive(Key);

            ApplyAccountFilter();

            if (AccountsView.GetItemCount() > 0)
                AccountsView.EnsureVisible(0);
        }

        private void ApplyAccountFilter()
        {
            if (AccountsView == null || SearchBox == null) return;

            string Query = SearchBox.Text.Trim();
            string Key = ActiveGroupKey;

            bool Has(string Value) => !string.IsNullOrEmpty(Value) && Value.IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0;

            AccountsView.ModelFilter = new ModelFilter(o => o is Account A && (Key == null || A.Group == Key) && (Query.Length == 0 || Has(A.Username) || Has(A.Alias) || Has(A.Description) || Has(A.Group)));

            UpdateGroupHeader();
            RefreshGrid(true);
        }

        /// <summary>Cards show the same accounts as the list (same filter), sorted by group like the list's groups.</summary>
        private void RefreshGrid(bool ScrollTop = false)
        {
            if (Grid == null || AccountsView == null) return;

            var Visible = AccountsView.FilteredObjects.Cast<object>().OfType<Account>()
                .Select((A, Index) => (A, Index))
                .OrderBy(x => x.A.Group ?? string.Empty, StringComparer.CurrentCulture).ThenBy(x => x.Index)
                .Select(x => x.A);

            Grid.SetItems(Visible, ActiveGroupKey == null);
            UpdateEmptyGroup();

            if (ScrollTop) Grid.AutoScrollPosition = Point.Empty;

            _ = UpdateCardPresence();
        }

        private void SetCardView(bool Cards)
        {
            CardView = Cards;
            Grid.Visible = true;          // both views are drawn by the grid now (cards or rows) – crisp text, real pictures, no grid lines
            Grid.RowMode = !Cards;
            Grid.BringToFront();
            ViewToggleButton.Text = ViewToggleButton.FullText = Cards ? "List view" : "Card view";
            ViewToggleButton.ShortText = Cards ? "List" : "Cards";
            CenterPane.PerformLayout();

            RefreshGrid();
            CardPresenceTimer.Start();

            General?.Set("MainView", Cards ? "Cards" : "List");
            IniSettings?.Save("RAMSettings.ini");
        }

        private bool UpdatingCardPresence;
        private DateTime LastCardPresence;

        /// <summary>Online status for every card (not only the rows the list happens to show).</summary>
        private async Task UpdateCardPresence(bool Force = false)
        {
            if (UpdatingCardPresence || Grid == null || !General.Get<bool>("ShowPresence")) return;
            if (!Force && (DateTime.Now - LastCardPresence).TotalSeconds < 15) return;

            UpdatingCardPresence = true;
            LastCardPresence = DateTime.Now;

            try
            {
                var Ids = Grid.Shown.Select(a => a.UserID).Where(id => id > 0).Distinct().ToList();

                for (int i = 0; i < Ids.Count; i += 50)
                    try { await Presence.UpdatePresence(Ids.Skip(i).Take(50).ToArray()); } catch { }

                Grid.Invalidate();
                AccountCard?.Invalidate();
                HeaderBar?.Invalidate();
            }
            finally { UpdatingCardPresence = false; }
        }

        private async void JoinMainWith(Account A)
        {
            string Target = JoinTargetName;
            if (Target.Length == 0) { ShowJoinTargetMenu(Grid, Grid.PointToClient(Cursor.Position)); return; } // nothing chosen yet: choose first

            // one of your own accounts: its user id is known, no lookup needed
            var Own = AccountsList.FirstOrDefault(a => string.Equals(a.Username, Target, StringComparison.OrdinalIgnoreCase));
            var (Found, TargetId) = Own != null && Own.UserID > 0 ? (true, Own.UserID) : await Task.Run(() => (GetUserID(Target, out long Id, out _), Id));

            if (!Found || TargetId <= 0) { ModernDialog.Show($"The Roblox user \"{Target}\" was not found. Choose another one on the right (\"Join button joins … Change\").", "Join " + Target, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (Own == A) { ModernDialog.Show($"{A.Username} can't join itself. Choose another account for the Join button (on the right, or right click the button).", "Join " + Target, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (!(await Presence.GetPresenceSingular(TargetId) is UserPresence Status && Status.userPresenceType == UserPresenceType.InGame && Status.placeId is long PlaceId && PlaceId > 0) &&
                !Utilities.YesNoPrompt("Join " + Target, $"{Target} is not in a game right now or has joins turned off", "Do you want to try to join anyway?"))
                return;

            if (GuardDoubleJoin(new List<Account> { A }, "Join " + Target).Count == 0) return;

            CancelLaunching();

            string Result = await A.JoinServer(TargetId, string.Empty, true);
            MarkJoined(A);

            if (!Result.Contains("Success"))
                ModernDialog.Show(Result, "Join " + Target, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #region Groups: new, rename, own picture

        /// <summary>
        /// A group exists through its accounts, so a new group is made from the selected accounts:
        /// name → accounts move there → choose a picture (Rank or own image).
        /// </summary>
        /// <summary>
        /// New group: name → (selected accounts move there, if you want) → picture.
        /// It may also stay empty – then it shows "Add account" and new accounts land in it.
        /// </summary>
        private void NewGroup()
        {
            var Selected = AccountsView.SelectedObjects.Cast<object>().OfType<Account>().ToList();

            string Name = ShowDialog("Name of the new group:", "New group");
            if (Name == "/UC" || string.IsNullOrWhiteSpace(Name)) return;
            Name = Name.Trim();

            string Existing = AccountsList.Select(a => a.Group).Concat(SavedGroups.All).FirstOrDefault(g => string.Equals(g, Name, StringComparison.OrdinalIgnoreCase));
            bool Exists = Existing != null;
            if (Exists) Name = Existing;

            bool Move = Selected.Count > 0 && ModernDialog.Show($"Move the {Selected.Count} selected account{(Selected.Count == 1 ? "" : "s")} into \"{Name}\"?\n\nNo = the group starts empty, you can add accounts later.", "New group", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            SavedGroups.Add(Name);

            if (Move)
            {
                foreach (var A in Selected) A.Group = Name;
                RefreshView();
                SaveAccounts(true);
            }
            else
                UpdateGroupSidebar();

            SetActiveGroup(Name);

            if (!Exists) AskGroupPicture(Name);
        }

        /// <summary>Removes a group that has no accounts (only the name – there is nothing else in it).</summary>
        private void DeleteEmptyGroup(string Key)
        {
            if (AccountsList.Any(a => a.Group == Key)) return;

            SavedGroups.Remove(Key);
            GroupImages.SetOverride(Key, null);
            SetActiveGroup(null);
            UpdateGroupSidebar();
        }

        private void AskGroupPicture(string Key)
        {
            var Menu = new ContextMenuStrip();
            Menu.Items.Add("Choose a Rank as picture…", null, (s, e) => ChooseGroupPicture(Key, GroupTitleOf(Key)));
            Menu.Items.Add("Choose an image file…", null, (s, e) => ChooseGroupPictureFile(Key));
            Menu.Items.Add("Automatic (from the group name)", null, (s, e) => { });
            ModernSkin.StyleMenu(Menu);
            Menu.Closed += (s, e) => BeginInvoke((MethodInvoker)Menu.Dispose);
            Menu.Show(NewGroupButton, new Point(0, NewGroupButton.Height));
        }

        private void ChooseGroupPictureFile(string Key)
        {
            using (var Dialog = new OpenFileDialog() { Title = "Picture for the group", Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|All files (*.*)|*.*" })
            {
                if (Dialog.ShowDialog(this) != DialogResult.OK) return;

                try { GroupImages.SetCustomImage(Key, Dialog.FileName); }
                catch (Exception x) { ModernDialog.Show($"That file could not be used as a picture.\n\n{x.Message}", "Group picture", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void RenameGroup(string Key, string Title)
        {
            string Name = ShowDialog($"New name for \"{Title}\":", "Rename group", Key);
            if (Name == "/UC" || string.IsNullOrWhiteSpace(Name) || Name.Trim() == Key) return;
            Name = Name.Trim();

            string Picture = GroupImages.GetOverride(Key);

            foreach (var A in AccountsList.Where(a => a.Group == Key)) A.Group = Name;
            SavedGroups.Rename(Key, Name);

            if (Picture != null) { GroupImages.SetOverride(Name, Picture); GroupImages.SetOverride(Key, null); }

            RefreshView();
            SaveAccounts(true);
            SetActiveGroup(Name);
        }

        #endregion

        #region Same server

        /// <summary>Settings → Joining: when no Job ID is given, every account joins the server of the first one.</summary>
        private bool SameServerOn => !General.Exists("SameServer") || General.Get<bool>("SameServer");

        /// <summary>
        /// Waits until the account is in the game and returns its server (Job ID). The Job ID field is filled with it,
        /// so every following Join – also single ones – goes to the same server. Clear the field for a new server.
        /// </summary>
        private async Task<string> WaitForServer(Account A, long PlaceId, CancellationToken Token, int MaxSeconds = 120)
        {
            for (int i = 0; i < MaxSeconds / 3 && !Token.IsCancellationRequested; i++) // Roblox needs a moment to load
            {
                try { await Task.Delay(3000, Token); } catch (TaskCanceledException) { break; }

                var P = await Presence.OwnPresence(A);

                if (P != null && P.userPresenceType == UserPresenceType.InGame && !string.IsNullOrEmpty(P.gameId) && (P.placeId == PlaceId || P.rootPlaceId == PlaceId || PlaceId <= 0))
                {
                    string Server = P.gameId;
                    Program.Logger.Info($"[SameServer] {A.Username} is in server {Server}");

                    this.InvokeIfRequired(() =>
                    {
                        // only fill it if the user didn't type another server meanwhile
                        if (string.IsNullOrWhiteSpace(JobID.Text)) JobID.Text = Server;
                        SaveTooltip.SetToolTip(JobID, "Same server: filled automatically with the server of the first account.\nEvery Join goes here now – clear the field to join a new server.");
                    });

                    return Server;
                }
            }

            Program.Logger.Warn($"[SameServer] could not find the server of {A.Username}");
            return null;
        }

        /// <summary>Puts the shared server into the Job ID field (so every later Join uses it) and explains it in the tooltip.</summary>
        private void ShowSameServer(string Server)
        {
            this.InvokeIfRequired(() =>
            {
                JobID.Text = Server;
                SaveTooltip.SetToolTip(JobID, "Same server: all accounts join this server.\nIf it gets full, a new one with room is picked automatically. Clear the field for a new server.");
            });
        }

        #endregion

        #region Double join protection

        private readonly Dictionary<long, DateTime> LastJoined = new Dictionary<long, DateTime>();

        private void MarkJoined(Account A)
        {
            if (A == null) return;
            lock (LastJoined) LastJoined[A.UserID] = DateTime.Now;
        }

        /// <summary>
        /// Roblox allows one game per account: joining again kicks the open window ("same account launched from another device").
        /// Accounts that are in a game or were started in the last 2 minutes need a confirmation; "No" skips only those.
        /// </summary>
        private List<Account> GuardDoubleJoin(List<Account> Accounts, string Title)
        {
            var Busy = new List<(Account A, string Why)>();

            if (General.Exists("ConfirmDoubleJoin") && !General.Get<bool>("ConfirmDoubleJoin"))
            {
                foreach (var A in Accounts) MarkJoined(A);
                return Accounts; // switched off in Settings → Joining
            }

            foreach (var A in Accounts)
            {
                DateTime When;
                bool Recent;
                lock (LastJoined) Recent = LastJoined.TryGetValue(A.UserID, out When) && (DateTime.Now - When).TotalSeconds < 120;

                if (Recent) Busy.Add((A, $"started {(int)(DateTime.Now - When).TotalSeconds} s ago"));
                else if (AccountRowRenderer.StateOf(A) == AccountState.InGame) Busy.Add((A, "already in a game"));
            }

            if (Busy.Count == 0)
            {
                foreach (var A in Accounts) MarkJoined(A);
                return Accounts;
            }

            string Names = string.Join("\n", Busy.Take(8).Select(b => $"•  {b.A.Username} – {b.Why}")) + (Busy.Count > 8 ? $"\n…and {Busy.Count - 8} more" : string.Empty);
            string Question = Accounts.Count == Busy.Count
                ? $"{Names}\n\nRoblox allows only one game per account – joining again closes the Roblox window it is in right now.\n\nJoin anyway?"
                : $"{Names}\n\nRoblox allows only one game per account – joining again closes their open Roblox window.\n\nYes = join all {Accounts.Count}   ·   No = skip these {Busy.Count}";

            var Answer = ModernDialog.Show(Question, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            var Result = Answer == DialogResult.Yes ? Accounts : Accounts.Where(a => !Busy.Exists(b => b.A == a)).ToList();
            foreach (var A in Result) MarkJoined(A);
            return Result;
        }

        #endregion

        private void SelectAllVisible()
        {
            AccountsView.SelectAll();
            AccountsView.Focus();
        }

        private void UpdateGroupHeader()
        {
            if (GroupTitleLabel == null || AccountsList == null) return;

            // the selected group may have been renamed or emptied
            if (ActiveGroupKey != null && !AccountsList.Any(a => a.Group == ActiveGroupKey) && !SavedGroups.Contains(ActiveGroupKey))
            {
                ActiveGroupKey = null;
                GroupsSidebar?.SetActive(null);
                ApplyAccountFilter();
                return;
            }

            var InGroup = AccountsList.Where(a => ActiveGroupKey == null || a.Group == ActiveGroupKey).ToList();
            int Banned = InGroup.Count(a => AccountRowRenderer.StateOf(a) == AccountState.Banned);
            int NeedLogin = InGroup.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin);

            GroupTitleLabel.Text = GroupTitleOf(ActiveGroupKey);

            var Parts = new List<string> { InGroup.Count == 1 ? "1 account" : $"{InGroup.Count} accounts" };

            if (Banned > 0) Parts.Add($"{Banned} banned");
            if (NeedLogin > 0) Parts.Add($"{NeedLogin} logged out");
            if (SearchBox.Text.Trim().Length > 0) Parts.Add($"{AccountsView.GetItemCount()} match \"{SearchBox.Text.Trim()}\"");

            GroupSubLabel.Text = string.Join("  ·  ", Parts);
            GroupSubLabel.Color = Banned > 0 ? Modern.Danger : Modern.Sub;
            GroupSubLabel.Invalidate();

            UpdateLoginGroupButton();
        }

        private void UpdateGroupSidebar()
        {
            if (GroupsSidebar == null || AccountsList == null) return;

            var Items = new List<GroupSidebarItem>
            {
                new GroupSidebarItem() { Key = null, Title = "All accounts", Count = AccountsList.Count, Banned = AccountsList.Count(a => AccountRowRenderer.StateOf(a) == AccountState.Banned), NeedLogin = AccountsList.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin) }
            };

            // groups of the accounts + groups created on purpose (may be empty)
            var Keys = AccountsList.Select(a => a.Group).Distinct().ToList();
            foreach (string Saved in SavedGroups.All) if (!Keys.Any(k => string.Equals(k, Saved, StringComparison.OrdinalIgnoreCase))) Keys.Add(Saved);

            // nobody made groups yet (new install): everything is simply under "All accounts", no lonely "Default" group
            if (Keys.Count == 1 && (string.IsNullOrEmpty(Keys[0]) || Keys[0] == "Default")) Keys.Clear();

            foreach (string Key in Keys.OrderBy(k => k, StringComparer.CurrentCulture))
            {
                var InGroup = AccountsList.Where(a => a.Group == Key).ToList();

                Items.Add(new GroupSidebarItem()
                {
                    Key = Key,
                    Title = GroupTitleOf(Key),
                    Count = InGroup.Count,
                    Banned = InGroup.Count(a => AccountRowRenderer.StateOf(a) == AccountState.Banned),
                    NeedLogin = InGroup.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin)
                });
            }

            GroupsSidebar.SetGroups(Items);
            GroupsSidebar.SetActive(ActiveGroupKey);
            HeaderBar?.Invalidate();

            if (!QuietCheckStarted && AccountsList.Count > 0 && !PasswordPanel.Visible) QuietSessionCheck();

            UpdateGroupHeader();
        }

        private void UpdateAccountCard()
        {
            if (AccountCard == null) return;

            AccountCard.SetAccounts(AccountsView.SelectedObjects.Cast<object>().OfType<Account>());
        }

        #endregion

        #region Account card actions

        private void CopyToClipboard(string Text, ModernButton Source = null)
        {
            if (string.IsNullOrEmpty(Text)) return;

            for (int Attempt = 0; Attempt < 3; Attempt++)
            {
                try { Clipboard.SetText(Text); break; }
                catch (ExternalException) { Thread.Sleep(80); } // clipboard busy
            }

            ClipboardSecret = Text;
            ClipboardTimer.Stop();
            ClipboardTimer.Start();

            if (Source != null)
            {
                string Old = Source.Text;
                Source.Text = "Copied";
                Source.Style = ModernButtonStyle.Outline;

                Task.Delay(1300).ContinueWith(_ => Source.InvokeIfRequired(() => { Source.Text = Old; Source.Style = ModernButtonStyle.Secondary; Source.Invalidate(); }));
            }
        }

        private void ClipboardTimer_Tick(object sender, EventArgs e)
        {
            ClipboardTimer.Stop();

            try
            {
                if (ClipboardSecret != null && Clipboard.ContainsText() && Clipboard.GetText() == ClipboardSecret)
                    Clipboard.Clear(); // only if nothing else was copied in the meantime
            }
            catch { }

            ClipboardSecret = null;
        }

        private void LoginSelectedAccount() => LoginAccount(AccountCard.Single ?? AccountCard.NextToLogin);

        /// <summary>A login window is open (only one at a time).</summary>
        private bool LoginRunning => Activities.IsRunning(ActivityKind.Login);

        private async void LoginAccount(Account A) => await LoginAccountAsync(A);

        #region Group login queue

        private CancellationTokenSource LoginQueueCts;

        private int LoginQueueDelay => General != null && General.Exists("LoginQueueDelay") && int.TryParse(General.Get("LoginQueueDelay"), out int D) ? Math.Max(5, Math.Min(D, 600)) : 20;

        private List<Account> GroupNeedingLogin() =>
            ActiveGroupKey == null ? new List<Account>() : AccountsList.Where(a => a.Group == ActiveGroupKey && AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin).ToList();

        /// <summary>Shows "Log in group (n)" when the open group has logged-out accounts (and nothing is running).</summary>
        private void UpdateLoginGroupButton()
        {
            if (LoginGroupButton == null) return;
            if (LoginQueueCts != null) return; // the running queue writes its own text

            int N = GroupNeedingLogin().Count;
            bool Show = N > 0;

            LoginGroupButton.Text = LoginGroupButton.FullText = $"Log in group ({N})";
            LoginGroupButton.ShortText = $"Log in ({N})";
            LoginGroupButton.Style = ModernButtonStyle.Outline;

            if (LoginGroupButton.Visible != Show) { LoginGroupButton.Visible = Show; CenterPane.PerformLayout(); }
            else LoginGroupButton.Invalidate();
        }

        /// <summary>
        /// Logs in the logged-out accounts of the open group one after another: one login window at a time,
        /// then a pause (Settings → General) so Roblox doesn't block the logins. Click the button again to stop.
        /// </summary>
        private async void LoginGroupQueue()
        {
            if (LoginQueueCts != null) { LoginQueueCts.Cancel(); LoginGroupButton.Text = "Stopping…"; return; }

            string Group = ActiveGroupKey;
            var Queue = GroupNeedingLogin();

            if (Queue.Count == 0) { UpdateLoginGroupButton(); return; }
            if (LoginRunning) { ModernDialog.Show("A login window is open right now. Finish it first.", "Log in group", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (ModernDialog.Show($"Log in {Queue.Count} account{(Queue.Count == 1 ? "" : "s")} of {GroupTitleOf(Group)} one after another?\n\nFor each account a login window opens with username and password filled in – you only solve the captcha. Between two accounts there is a {LoginQueueDelay} s pause so Roblox doesn't block the logins.\n\nClick the button again to stop.", "Log in group", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            LoginQueueCts = new CancellationTokenSource();
            var Token = LoginQueueCts.Token;
            int Done = 0, Failed = 0;

            LoginGroupButton.Style = ModernButtonStyle.Danger;
            var Job = Activities.Begin(ActivityKind.LoginQueue, $"Log in group {GroupTitleOf(Group)}", LoginQueueCts);

            try
            {
                for (int i = 0; i < Queue.Count && !Token.IsCancellationRequested; i++)
                {
                    var A = Queue[i];
                    if (AccountRowRenderer.StateOf(A) != AccountState.NeedsLogin) continue; // logged in meanwhile

                    LoginGroupButton.Text = $"Stop · {i + 1}/{Queue.Count}";
                    Activities.Update(Job, $"Log in group {GroupTitleOf(Group)} · {i + 1}/{Queue.Count}");
                    SaveTooltip.SetToolTip(LoginGroupButton, $"Logging in {A.Username} – click to stop the queue");
                    CenterPane.PerformLayout();

                    await LoginAccountAsync(A);

                    if (AccountRowRenderer.StateOf(A) == AccountState.NeedsLogin) Failed++; else Done++;

                    if (i < Queue.Count - 1 && !Token.IsCancellationRequested)
                        for (int s = LoginQueueDelay; s > 0 && !Token.IsCancellationRequested; s--)
                        {
                            LoginGroupButton.Text = $"Stop · next in {s} s";
                            Activities.Update(Job, $"Log in group {GroupTitleOf(Group)} · next in {s} s");
                            try { await Task.Delay(1000, Token); } catch (TaskCanceledException) { }
                        }
                }
            }
            finally
            {
                Activities.End(Job);
                bool Stopped = Token.IsCancellationRequested;
                LoginQueueCts = null;
                UpdateLoginGroupButton();
                CenterPane.PerformLayout();

                ModernDialog.Show($"{(Stopped ? "Stopped. " : string.Empty)}{Done} logged in{(Failed > 0 ? $", {Failed} not (window closed or login failed)" : string.Empty)}.", "Log in group", MessageBoxButtons.OK, Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        #endregion

        private async Task LoginAccountAsync(Account A)
        {
            if (A == null) { ModernDialog.Show("Select an account first.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (LoginRunning) { ModernDialog.Show("A login window is already open. Finish that one first (or close it), then the next account.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (IsDownloadingChromium) { DownloadProgressBar_Click(null, EventArgs.Empty); return; }

            string Password = A.Password;

            if (string.IsNullOrEmpty(Password))
                ModernDialog.Show($"No password is saved for {A.Username}.\nThe username gets filled in, type the password yourself.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (CleanLogin.FindBrowser() != null)
            {
                await LoginInRealBrowser(A.Username, Password, A.UserID);
                return;
            }

            if (PuppeteerSupported)
            {
                AccountCard.Login.Enabled = false;

                try { await new AccountBrowser().Login(A.Username, Password ?? string.Empty); }
                catch (Exception x)
                {
                    Program.Logger.Error($"[Login] {x}");
                    ModernDialog.Show($"The login window could not be opened.\n\n{x.Message}", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { AccountCard.RefreshState(); }
            }
            else
                CefBrowser.Instance.Login(A.Username, Password);
        }

        /// <summary>Login in the installed Chrome / Edge (normal window, own temporary profile). Used for "Login", "Re-login" and "Add account → Browser".</summary>
        private async Task LoginInRealBrowser(string Username = null, string Password = null, long UserId = 0)
        {
            string Browser = CleanLogin.FindBrowser();
            string Profile = CleanLogin.ProfileFor(UserId);

            var Job = Activities.Begin(ActivityKind.Login, string.IsNullOrEmpty(Username) ? "Login window open" : $"Login window: {Username}");
            AccountCard.LoginBusy = true;
            AddAccountButton.Enabled = false;

            try
            {
                var Result = await CleanLogin.Run(Browser, Username, Password, Profile);

                if (Result is (string Cookie, string Pw))
                {
                    Account Added = await Task.Run(() => AddAccount(Cookie, Pw ?? string.Empty));

                    if (Added == null)
                        ModernDialog.Show("Roblox did not accept the login cookie. Please try again.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    {
                        CleanLogin.AdoptProfile(Profile, Added.UserID);

                        AccountSession.LoggedIn(Added, "login window");

                        // read the e-mail right away now that the account is logged in again
                        try { AccountChecks.Apply(Added, await AccountChecks.CheckSession(Added, true, CancellationToken.None)); } catch { }

                        SaveAccounts(true);
                        RefreshAfterChecks();
                    }
                }
            }
            catch (Exception x)
            {
                Program.Logger.Error($"[Login] {x}");
                ModernDialog.Show($"The login window could not be opened.\n\n{x.Message}", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Activities.End(Job);
                AccountCard.LoginBusy = false;
                AddAccountButton.Enabled = true;
            }
        }

        /// <summary>The account the "Join …" button on every account joins. Its own setting; until chosen it is the main account.</summary>
        private string JoinTargetName => General != null && General.Exists("JoinTarget") && !string.IsNullOrWhiteSpace(General.Get("JoinTarget")) ? General.Get("JoinTarget").Trim() : FriendTargetName;

        private void SetJoinTarget(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;

            General.Set("JoinTarget", Name.Trim());
            IniSettings.Save("RAMSettings.ini");

            Grid.MainName = JoinTargetName;
            Grid.Invalidate();
            AccountCard.JoinTarget = JoinTargetName;
            AccountCard.Invalidate();
        }

        /// <summary>Menu to choose the join target: the main account, any account by group, or any Roblox user by name.</summary>
        private void ShowJoinTargetMenu(Control Owner, Point Where)
        {
            var Menu = new ContextMenuStrip();
            string Current = JoinTargetName;

            ToolStripMenuItem Pick(string Name, string Text)
            {
                var I = new ToolStripMenuItem(Text) { Checked = string.Equals(Name, Current, StringComparison.OrdinalIgnoreCase) };
                I.Click += (s, e) => SetJoinTarget(Name);
                return I;
            }

            Menu.Items.Add(new ToolStripMenuItem("The Join button joins:") { Enabled = false });
            if (FriendTargetName.Length > 0)
            {
                Menu.Items.Add(Pick(FriendTargetName, $"{FriendTargetName}   (main account)"));
                Menu.Items.Add(new ToolStripSeparator());
            }

            foreach (var G in AccountsList.Where(a => !string.IsNullOrEmpty(a.Username)).GroupBy(a => a.Group ?? string.Empty).OrderBy(g => GroupTitleOf(g.Key), StringComparer.OrdinalIgnoreCase))
            {
                var Sub = new ToolStripMenuItem($"{GroupTitleOf(G.Key)}   ({G.Count()})");
                foreach (var A in G.OrderBy(a => a.Username, StringComparer.OrdinalIgnoreCase))
                    Sub.DropDownItems.Add(Pick(A.Username, A.Username));
                if (G.Any(a => string.Equals(a.Username, Current, StringComparison.OrdinalIgnoreCase))) Sub.Checked = true;
                ModernSkin.StyleMenu(Sub.DropDown);
                Menu.Items.Add(Sub);
            }

            Menu.Items.Add(new ToolStripSeparator());
            var Other = new ToolStripMenuItem("Other Roblox user…");
            Other.Click += (s, e) =>
            {
                using (var Dialog = MakeDialog("Join button", 130))
                {
                    var NameBox = DialogTextBox(Dialog, "Roblox username the Join button joins", Current, 14);
                    DialogButtons(Dialog);
                    if (Dialog.ShowDialog(this) == DialogResult.OK) SetJoinTarget(NameBox.Text);
                }
            };
            Menu.Items.Add(Other);

            ModernSkin.StyleMenu(Menu);
            Menu.Closed += (s, e) => BeginInvoke(new Action(Menu.Dispose));
            Menu.Show(Owner, Where);
        }

        private string FriendTargetName => General != null && General.Exists("FriendTarget") && !string.IsNullOrWhiteSpace(General.Get("FriendTarget")) ? General.Get("FriendTarget").Trim() : string.Empty;

        private int FriendDelaySeconds => General != null && General.Exists("FriendRequestDelay") && int.TryParse(General.Get("FriendRequestDelay"), out int D) ? Math.Max(1, Math.Min(D, 600)) : 6;

        private async void AddFriendToMain()
        {
            if (FriendTokenSource != null) { FriendTokenSource.Cancel(); return; } // button works as "stop" while running

            string Target = FriendTargetName;
            if (Target.Length == 0) { ChangeMainAccount(); return; } // no main account yet: choose it first

            var Accounts = AccountCard.Selection.Where(a => !string.Equals(a.Username, Target, StringComparison.OrdinalIgnoreCase)).ToList();

            if (Accounts.Count == 0) { ModernDialog.Show($"Select the alt accounts that should add {Target}.", "Add main account", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (Accounts.Count > 1 && ModernDialog.Show($"Send {Target} a friend request from {Accounts.Count} accounts?\n\nThere is a {FriendDelaySeconds} s pause between the accounts. Click the button again to stop.", "Add main account", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var (Found, TargetId) = await Task.Run(() => (GetUserID(Target, out long Id, out _), Id));

            if (!Found || TargetId <= 0) { ModernDialog.Show($"The Roblox user \"{Target}\" was not found. Check the main account with \"Change\".", "Add main account", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            FriendTokenSource = new CancellationTokenSource();
            var Token = FriendTokenSource.Token;
            AccountCard.AddFriendBusy = true;
            var Job = Activities.Begin(ActivityKind.FriendRequests, $"Friend requests to {Target}", FriendTokenSource);

            int Sent = 0, Already = 0, Done = 0;
            var Problems = new List<string>();
            bool Stopped = false;

            try
            {
                for (int i = 0; i < Accounts.Count; i++)
                {
                    AccountCard.AddFriend.Text = Accounts.Count > 1 ? $"{i + 1}/{Accounts.Count} · Stop" : "Sending…";
                    Activities.Update(Job, $"Friend requests to {Target} · {i + 1}/{Accounts.Count}");

                    var (Result, Message) = await AccountChecks.SendFriendRequest(Accounts[i], TargetId, Token);
                    Done++;

                    switch (Result)
                    {
                        case FriendResult.Sent: Sent++; break;
                        case FriendResult.AlreadyDone: Already++; break;
                        case FriendResult.Captcha: Problems.Add($"{Accounts[i].Username}: captcha needed – use \"Open browser\" and add {Target} there"); break;
                        default: Problems.Add($"{Accounts[i].Username}: {Message}"); break;
                    }

                    if (i < Accounts.Count - 1)
                        await Task.Delay(FriendDelaySeconds * 1000, Token);
                }
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (Exception x) { Problems.Add(x.Message); Program.Logger.Error($"[AddFriend] {x}"); }
            finally
            {
                Activities.End(Job);
                FriendTokenSource.Dispose();
                FriendTokenSource = null;
                AccountCard.AddFriendBusy = false;
                AccountCard.RefreshState();
            }

            string Summary = $"{(Stopped ? "Stopped. " : string.Empty)}{Sent} request{(Sent == 1 ? "" : "s")} sent to {Target}";

            if (Already > 0) Summary += $", {Already} already friends / already sent";
            if (Done < Accounts.Count) Summary += $", {Accounts.Count - Done} not done";
            if (Problems.Count > 0) Summary += $"\n\nProblems ({Problems.Count}):\n" + string.Join("\n", Problems.Take(12)) + (Problems.Count > 12 ? $"\n… and {Problems.Count - 12} more" : string.Empty);

            ModernDialog.Show(Summary, "Add main account", MessageBoxButtons.OK, Problems.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async void CheckForBans()
        {
            if (BanTokenSource != null) { BanTokenSource.Cancel(); return; }

            var Accounts = AccountsList.ToList();

            if (Accounts.Count == 0) return;

            BanTokenSource = new CancellationTokenSource();
            var Token = BanTokenSource.Token;
            bool Stopped = false, PublicFailed = false;
            HashSet<long> BannedSet = null;
            var Sessions = new ConcurrentDictionary<Account, AccountChecks.SessionInfo>();

            CheckBansButton.Text = "Checking… · Stop";
            var Job = Activities.Begin(ActivityKind.BanCheck, "Checking bans + logins", BanTokenSource);

            try
            {
                // 1) terminated accounts: one request per 100 accounts
                BannedSet = await AccountChecks.BannedIds(Accounts.Select(a => a.UserID).ToList(), Token);
                PublicFailed = BannedSet == null;

                // 2) every other account: is it still logged in (+ e-mail)
                var ToCheck = Accounts.Where(a => BannedSet == null || !BannedSet.Contains(a.UserID)).ToList();

                await RunSessionChecks(ToCheck, Sessions, Token, (Done, Total) => { CheckBansButton.Text = $"{Done}/{Total} · Stop"; Activities.Update(Job, $"Checking bans + logins · {Done}/{Total}"); });
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (Exception x) { Program.Logger.Error($"[BanCheck] {x}"); }

            PublicFailed = BannedSet == null; // also when the lookup threw

            try
            {
            if (BannedSet != null)
                foreach (var A in Accounts)
                {
                    if (BannedSet.Contains(A.UserID)) AccountSession.Set(A, SessionStatus.Banned, StatusSource.BanCheck, "terminated");
                    else AccountSession.Set(A, SessionStatus.Active, StatusSource.BanCheck, "not banned any more"); // only does something when it was Banned

                    AccountSession.Stamp(A, "BanChecked");
                }

            foreach (var Pair in Sessions) AccountChecks.Apply(Pair.Key, Pair.Value);

            SaveAccounts(true);
            }
            finally
            {
                Activities.End(Job);
                BanTokenSource.Dispose();
                BanTokenSource = null;
                CheckBansButton.Text = "Check for bans";
            }

            RefreshAfterChecks();

            int Banned = BannedSet?.Count ?? 0;
            int Temp = Sessions.Count(r => r.Value.Result == BanCheckResult.TempBanned);
            int NeedLogin = Sessions.Count(r => r.Value.Result == BanCheckResult.NeedsLogin);
            int Unknown = Sessions.Count(r => r.Value.Result == BanCheckResult.Unknown);

            string Summary = (Stopped ? "Stopped before the end.\n\n" : $"Checked {Accounts.Count} accounts.\n\n") +
                             (PublicFailed ? "Roblox did not answer the ban lookup – try again in a minute.\n" : $"Banned: {Banned}\n") +
                             (Temp > 0 ? $"Temporarily banned: {Temp}\n" : string.Empty) +
                             $"Not logged in any more: {NeedLogin}" +
                             (Unknown > 0 ? $"\nCould not be checked: {Unknown}" : string.Empty) +
                             (Banned + Temp > 0 ? "\n\nBanned accounts are marked red. Nothing was deleted or moved." : string.Empty) +
                             (NeedLogin > 0 ? "\nNot logged in accounts have a \"Re-login\" button on their card." : string.Empty);

            ModernDialog.Show(Summary, "Check for bans", MessageBoxButtons.OK, Banned + Temp > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async Task RunSessionChecks(List<Account> Accounts, ConcurrentDictionary<Account, AccountChecks.SessionInfo> Results, CancellationToken Token, Action<int, int> Progress = null)
        {
            int Done = 0;

            using (var Gate = new SemaphoreSlim(4))
            {
                async Task One(Account A)
                {
                    await Gate.WaitAsync(Token);

                    try { Results[A] = await AccountChecks.CheckSession(A, !AccountChecks.CheckedWithin(A, "EmailChecked", TimeSpan.FromDays(3)) || !A.Fields.ContainsKey("Email"), Token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception x) { Program.Logger.Warn($"[SessionCheck] {A.Username}: {x.Message}"); }
                    finally
                    {
                        Gate.Release();
                        int D = Interlocked.Increment(ref Done);
                        if (Progress != null) this.InvokeIfRequired(() => Progress(D, Accounts.Count));
                    }
                }

                await Task.WhenAll(Accounts.Select(One).ToList());
            }
        }

        private void RefreshAfterChecks()
        {
            HeaderBar?.Invalidate();
            AccountsView.Invalidate();
            UpdateGroupSidebar();
            AccountCard.RefreshState();
            Grid.Invalidate();
        }

        private bool QuietCheckStarted;

        /// <summary>Once after the accounts are loaded: which accounts are no longer logged in (+ e-mails). Quiet, read only, skips accounts checked in the last 2 hours.</summary>
        private async void QuietSessionCheck()
        {
            if (QuietCheckStarted || AccountsList == null || AccountsList.Count == 0) return;

            QuietCheckStarted = true;

            await Task.Delay(4000);

            var Due = AccountsList.Where(a => AccountSession.Of(a) != SessionStatus.Banned && !AccountChecks.CheckedWithin(a, "SessionChecked", TimeSpan.FromHours(2))).ToList();

            if (Due.Count == 0 || BanTokenSource != null) return;

            var Results = new ConcurrentDictionary<Account, AccountChecks.SessionInfo>();

            try { await RunSessionChecks(Due, Results, CancellationToken.None); }
            catch (Exception x) { Program.Logger.Warn($"[SessionCheck] {x.Message}"); }

            foreach (var Pair in Results) AccountChecks.Apply(Pair.Key, Pair.Value);

            if (Results.Count > 0) SaveAccounts(true);

            RefreshAfterChecks();
        }

        private Form MakeDialog(string Title, int Height)
        {
            var Dialog = new Form()
            {
                Text = Title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Modern.Panel,
                ForeColor = Modern.Text,
                ClientSize = new Size(Px(380), Px(Height)),
                Font = Modern.Font(9f)
            };

            SetDarkBar(Dialog.Handle);

            return Dialog;
        }

        private static TextBox DialogTextBox(Form Dialog, string Caption, string Value, int Y, bool Multiline = false, int Height = 0)
        {
            Dialog.Controls.Add(new ModernLabel() { Text = Caption, Points = 8.25f, Color = Modern.Sub, Bounds = new Rectangle(Px(16), Px(Y), Px(348), Px(18)) });

            var Box = new TextBox()
            {
                Text = Value ?? string.Empty,
                BackColor = Modern.Field,
                ForeColor = Modern.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Modern.Font(9.75f),
                Multiline = Multiline,
                ScrollBars = Multiline ? ScrollBars.Vertical : ScrollBars.None,
                Bounds = new Rectangle(Px(16), Px(Y + 20), Px(348), Multiline ? Px(Height) : Px(28))
            };

            Dialog.Controls.Add(Box);

            return Box;
        }

        private static void DialogButtons(Form Dialog)
        {
            var Ok = new ModernButton() { Text = "Save", Style = ModernButtonStyle.Primary };
            var Cancel = new ModernButton() { Text = "Cancel" };

            Ok.SetBounds(Dialog.ClientSize.Width - Px(16) - Px(100), Dialog.ClientSize.Height - Px(16) - Px(34), Px(100), Px(34));
            Cancel.SetBounds(Ok.Left - Px(8) - Px(100), Ok.Top, Px(100), Px(34));

            Ok.Click += (s, e) => { Dialog.DialogResult = DialogResult.OK; Dialog.Close(); };
            Cancel.Click += (s, e) => { Dialog.DialogResult = DialogResult.Cancel; Dialog.Close(); };

            Dialog.Controls.Add(Ok);
            Dialog.Controls.Add(Cancel);
        }

        private void EditNoteDialog()
        {
            var Selected = AccountCard.Selection.ToList();

            if (Selected.Count == 0) return;

            string OldAlias = Selected.Select(a => a.Alias).Distinct().Count() == 1 ? Selected[0].Alias : string.Empty;
            string OldNote = Selected.Select(a => a.Description).Distinct().Count() == 1 ? Selected[0].Description : string.Empty;

            using (var Dialog = MakeDialog(Selected.Count == 1 ? $"Edit note – {Selected[0].Username}" : $"Edit note – {Selected.Count} accounts", 250))
            {
                var AliasBox = DialogTextBox(Dialog, "Alias (short name shown under the username)", OldAlias, 14);
                AliasBox.MaxLength = 50;
                var NoteBox = DialogTextBox(Dialog, "Note / description", OldNote, 72, true, 90);

                DialogButtons(Dialog);

                if (Dialog.ShowDialog(this) != DialogResult.OK) return;

                // only what was actually changed is written, so a multi-selection keeps its different notes
                if (AliasBox.Text != OldAlias) { Alias.Text = AliasBox.Text; SetAlias_Click(null, EventArgs.Empty); }
                if (NoteBox.Text != OldNote) { DescriptionBox.Text = NoteBox.Text; SetDescription_Click(null, EventArgs.Empty); }
            }

            AccountCard.RefreshState();
        }

        private void FollowUserPrompt()
        {
            using (var Dialog = MakeDialog("Follow user", 130))
            {
                var NameBox = DialogTextBox(Dialog, "Join the game of this Roblox user with the selected account(s)", UserID.Text, 14);
                DialogButtons(Dialog);
                Dialog.Controls.OfType<ModernButton>().First(b => b.Style == ModernButtonStyle.Primary).Text = "Follow";

                if (Dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(NameBox.Text)) return;

                UserID.Text = NameBox.Text.Trim();
            }

            General.Set("SavedFollowUser", UserID.Text);
            Follow_Click(null, EventArgs.Empty);
        }

        private void ChangeMainAccount()
        {
            using (var Dialog = MakeDialog("Main account", 190))
            {
                var NameBox = DialogTextBox(Dialog, "Main account (gets the friend requests)", FriendTargetName, 14);
                var DelayBox = DialogTextBox(Dialog, "Pause between the accounts (seconds)", FriendDelaySeconds.ToString(), 72);
                DialogButtons(Dialog);

                if (Dialog.ShowDialog(this) != DialogResult.OK) return;

                if (!string.IsNullOrWhiteSpace(NameBox.Text)) General.Set("FriendTarget", NameBox.Text.Trim());
                if (int.TryParse(DelayBox.Text.Trim(), out int Delay) && Delay > 0) General.Set("FriendRequestDelay", Math.Min(Delay, 600).ToString());

                IniSettings.Save("RAMSettings.ini");
            }

            AccountCard.FriendTarget = FriendTargetName;
            AccountCard.RefreshState();
            Grid.MainName = JoinTargetName;
            AccountCard.JoinTarget = JoinTargetName;
            Grid.Invalidate();
        }

        #endregion
    }
}
