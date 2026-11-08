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
        private ModernButton AddAccountButton, CheckBansButton, SelectGroupButton, ViewToggleButton;
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

                int Total = List.Count, Online = 0, Banned = 0, Login = 0;

                foreach (var A in List)
                    switch (AccountRowRenderer.StateOf(A))
                    {
                        case AccountState.Online: case AccountState.InGame: case AccountState.InStudio: Online++; break;
                        case AccountState.Banned: Banned++; break;
                        case AccountState.NeedsLogin: Login++; break;
                    }

                int X = Modern.Gutter, W = SideBar.Width - Modern.Gutter * 2, H = HeaderBar.Height;

                Modern.Text_(G, $"{Total} accounts", Modern.Font(10.5f, FontStyle.Regular, true), new Rectangle(X, H / 2 - Px(20), W, Px(20)), Modern.Text, TextFormatFlags.NoPadding);

                int CX = X, CY = H / 2 + Px(2), CH = Px(16);
                Font F = Modern.Font(8.25f);

                void Stat(int Count, string Label, Color C)
                {
                    if (Count <= 0 || CX > X + W) return;

                    int Dot = Px(7);
                    using (var B = new SolidBrush(C)) G.FillRectangle(B, CX, CY + (CH - Dot) / 2, Dot, Dot);

                    string T = $"{Count} {Label}";
                    int TW = TextRenderer.MeasureText(T, F, Size.Empty, TextFormatFlags.NoPadding).Width;
                    Modern.Text_(G, T, F, new Rectangle(CX + Dot + Px(5), CY, Math.Min(TW + Px(2), X + W - CX), CH), Modern.Sub, TextFormatFlags.NoPadding);
                    CX += Dot + Px(5) + TW + Px(10);
                }

                Stat(Online, "online", Modern.AccentText);
                Stat(Login, "login", Modern.Warn);
                Stat(Banned, "banned", Modern.Danger);

                if (Online + Login + Banned == 0)
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
            SaveTooltip.SetToolTip(ServerListIcon, "Utilities: server list, games list, favorites");

            RecentGamesIcon = new ModernButton() { Icon = IconKind.Clock };
            RecentGamesIcon.Click += (s, e) => RGForm.ShowForm();
            SaveTooltip.SetToolTip(RecentGamesIcon, "Recent games");

            AccountControlIcon = new ModernButton() { Icon = IconKind.Grid };
            AccountControlIcon.Click += (s, e) => LaunchNexus_Click(AccountControlIcon, EventArgs.Empty);
            SaveTooltip.SetToolTip(AccountControlIcon, "Account Control (Nexus)");

            SettingsIcon = new ModernButton() { Icon = IconKind.Gear };
            SettingsIcon.Click += (s, e) => ConfigButton_Click(SettingsIcon, EventArgs.Empty);
            SaveTooltip.SetToolTip(SettingsIcon, "Settings");

            HeaderBar.Controls.AddRange(new Control[] { GameField, JobField, JoinSelectedButton, ServerListIcon, RecentGamesIcon, AccountControlIcon, SettingsIcon });

            HeaderBar.Layout += (s, e) =>
            {
                if (HeaderBar.Width < Px(400)) return;

                // the header follows the three columns below it: overview | game + job (= list width) | join + icons (= card width)
                int G = Modern.Gutter, H = Modern.RowH, Y = (HeaderBar.Height - H) / 2, Gap = Px(8);

                int CenterLeft = SideBar.Width + G, CenterRight = HeaderBar.Width - AccountCard.Width - G;
                int RightLeft = HeaderBar.Width - AccountCard.Width + G, Right = HeaderBar.Width - G;

                foreach (var Icon in new[] { SettingsIcon, AccountControlIcon, RecentGamesIcon, ServerListIcon })
                {
                    Right -= H;
                    Icon.SetBounds(Right, Y, H, H);
                    Right -= Gap;
                }

                JoinSelectedButton.SetBounds(RightLeft, Y, Math.Max(Px(90), Right - RightLeft), H);

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

            SideBar.Controls.AddRange(new Control[] { SearchField, GroupsCaption, GroupsSidebar, DLChromiumLabel, DownloadProgressBar, AddAccountButton });

            DownloadProgressBar.VisibleChanged += (s, e) => SideBar.PerformLayout();
            DLChromiumLabel.VisibleChanged += (s, e) => SideBar.PerformLayout();

            SideBar.Layout += (s, e) =>
            {
                if (SideBar.Width < Px(60) || SideBar.Height < Px(200)) return;

                int P = Modern.Gutter, W = SideBar.Width - P * 2 - 1;

                SearchField.SetBounds(P, P, W, Modern.RowH);
                GroupsCaption.SetBounds(P, SearchField.Bottom + Px(14), W, Px(16));

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
            TipLabel = new ModernLabel() { Text = "Ctrl / Shift + click selects several accounts – the buttons on the right then work for all of them. Right click a card for every other option.", Points = 8.25f, Color = Modern.Sub };

            CheckBansButton = new ModernButton() { Text = "Check for bans", Icon = IconKind.Ban, Style = ModernButtonStyle.Danger };
            CheckBansButton.Click += (s, e) => CheckForBans();
            SaveTooltip.SetToolTip(CheckBansButton, "Checks every account with the Roblox API and marks banned ones red.\nNothing is deleted or moved.");

            SelectGroupButton = new ModernButton() { Text = "Select group" };
            SelectGroupButton.Click += (s, e) => SelectAllVisible();

            CardView = General == null || !General.Exists("MainView") || General.Get("MainView") != "List";

            ViewToggleButton = new ModernButton() { Text = CardView ? "List view" : "Card view" };
            ViewToggleButton.Click += (s, e) => SetCardView(!CardView);
            SaveTooltip.SetToolTip(ViewToggleButton, "Switch between account cards and the classic list (the list supports drag & drop into groups)");

            ListFrame = new LinePanel() { BackColor = Modern.Bg, FillBorderColor = Modern.Line, Padding = new Padding(1) };

            Controls.Remove(AccountsView);
            AccountsView.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            AccountsView.Dock = DockStyle.Fill;
            AccountsView.BorderStyle = BorderStyle.None;
            ListFrame.Controls.Add(AccountsView);

            Grid = new AccountGrid() { Dock = DockStyle.Fill, Visible = CardView };
            Grid.GetSelection = () => new HashSet<Account>(AccountsView.SelectedObjects.Cast<object>().OfType<Account>());
            Grid.SetSelection = List => { AccountsView.SelectedObjects = List; Grid.Invalidate(); };
            Grid.JoinClicked = A => { SelectedAccount = A; JoinServer_Click(Grid, EventArgs.Empty); };
            Grid.JoinMainClicked = JoinMainWith;
            Grid.ReloginClicked = A => LoginSelectedAccount();
            Grid.ContextRequested = (A, P) => AccountsStrip.Show(Grid, P);
            Grid.SectionTitle = GroupTitleOf;
            ListFrame.Controls.Add(Grid);

            Modern.DarkScrollbars(Grid);
            Modern.DarkScrollbars(AccountsView);
            Modern.DarkScrollbars(GroupsSidebar);
            Grid.BringToFront(); // the list stays behind it (still used for selection, menus and the list view)

            AvatarCache.Changed += (s, e) => { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)(() => Grid?.Invalidate())); };

            CardPresenceTimer.Tick += async (s, e) => await UpdateCardPresence(true);
            if (CardView) CardPresenceTimer.Start();

            CenterPane.Controls.AddRange(new Control[] { GroupTitleLabel, GroupSubLabel, CheckBansButton, SelectGroupButton, ViewToggleButton, ListFrame, TipLabel });

            CenterPane.Layout += (s, e) =>
            {
                if (CenterPane.Width < Px(300) || CenterPane.Height < Px(160)) return;

                int P = Modern.Gutter, W = CenterPane.Width - P * 2, RH = Modern.RowH, Gap = Px(8);

                ViewToggleButton.SetBounds(P + W - Px(100), P, Px(100), RH);
                SelectGroupButton.SetBounds(ViewToggleButton.Left - Gap - Px(116), P, Px(116), RH);
                CheckBansButton.SetBounds(SelectGroupButton.Left - Gap - Px(170), P, Px(170), RH);

                // title + subtitle share the height of the button row
                GroupTitleLabel.SetBounds(P, P - Px(4), Math.Max(Px(80), CheckBansButton.Left - P - Px(10)), Px(28));
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
            Group.Renderer = new AccountRowRenderer(AccountColumnKind.Empty);

            AccountsView.SelectionChanged += (s, e) => { UpdateAccountCard(); Grid?.Invalidate(); };
        }

        private void BuildCard()
        {
            AccountCard.GroupTitle = GroupTitleOf;

            AccountCard.CopyUser.Click += (s, e) => CopyToClipboard(string.Join(Environment.NewLine, AccountCard.Selection.Select(a => a.Username)), AccountCard.CopyUser);
            AccountCard.CopyPassword.Click += (s, e) => CopyToClipboard(string.Join(Environment.NewLine, AccountCard.Selection.Where(a => !string.IsNullOrEmpty(a.Password)).Select(a => a.Password)), AccountCard.CopyPassword);
            AccountCard.Login.Click += (s, e) => LoginSelectedAccount();
            AccountCard.AddFriend.Click += (s, e) => AddFriendToMain();
            AccountCard.OpenBrowser.Click += (s, e) => OpenBrowser_Click(AccountCard.OpenBrowser, EventArgs.Empty);
            AccountCard.OpenBrowser.Menu = OpenBrowserStrip;
            AccountCard.Follow.Click += (s, e) => FollowUserPrompt();
            AccountCard.ChangeMain.Click += (s, e) => ChangeMainAccount();
            AccountCard.EditNote.Click += (s, e) => EditNoteDialog();
            AccountCard.Remove.Click += (s, e) => Remove_Click(AccountCard.Remove, EventArgs.Empty);

            SaveTooltip.SetToolTip(AccountCard.AddFriend, "The selected accounts send your main account a friend request, one after another with a pause.\nClick again to stop.");
            SaveTooltip.SetToolTip(AccountCard.OpenBrowser, "Opens a browser logged in as the account (arrow: URL / URL + JS / Join group)");

            MoreToolsMenu = new ContextMenuStrip();
            MoreToolsMenu.Items.Add("Account Utilities…", null, (s, e) => BrowserButton_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Account Control (Nexus)", null, (s, e) => LaunchNexus_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Server list / Games / Favorites", null, (s, e) => ServerList_Click(null, EventArgs.Empty));
            MoreToolsMenu.Items.Add("Recent games", null, (s, e) => RGForm.ShowForm());
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
            MoreToolsMenu.Items.Add("Edit theme (other windows)", null, (s, e) => EditTheme_Click(null, EventArgs.Empty));
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
            Grid.MainName = FriendTargetName;
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
            if (!Short.Contains(" ") && Short.Length <= 5 && Short.ToUpperInvariant() == Short && GroupImages.GetOverride(Key) is string Entry)
                return Entry;

            return Short;
        }

        private void SetActiveGroup(string Key)
        {
            ActiveGroupKey = Key;
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

            if (ScrollTop) Grid.AutoScrollPosition = Point.Empty;

            if (CardView) _ = UpdateCardPresence();
        }

        private void SetCardView(bool Cards)
        {
            CardView = Cards;
            Grid.Visible = Cards;
            ViewToggleButton.Text = Cards ? "List view" : "Card view";

            if (Cards) { Grid.BringToFront(); RefreshGrid(); CardPresenceTimer.Start(); }
            else CardPresenceTimer.Stop();

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
            string Target = FriendTargetName;

            var (Found, TargetId) = await Task.Run(() => (GetUserID(Target, out long Id, out _), Id));

            if (!Found || TargetId <= 0) { MessageBox.Show($"The Roblox user \"{Target}\" was not found. Change the main account on the right (\"Change\").", "Join " + Target, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!(await Presence.GetPresenceSingular(TargetId) is UserPresence Status && Status.userPresenceType == UserPresenceType.InGame && Status.placeId is long PlaceId && PlaceId > 0) &&
                !Utilities.YesNoPrompt("Join " + Target, $"{Target} is not in a game right now or has joins turned off", "Do you want to try to join anyway?"))
                return;

            CancelLaunching();

            string Result = await A.JoinServer(TargetId, string.Empty, true);

            if (!Result.Contains("Success"))
                MessageBox.Show(Result, "Join " + Target, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SelectAllVisible()
        {
            AccountsView.SelectAll();
            AccountsView.Focus();
        }

        private void UpdateGroupHeader()
        {
            if (GroupTitleLabel == null || AccountsList == null) return;

            // the selected group may have been renamed or emptied
            if (ActiveGroupKey != null && !AccountsList.Any(a => a.Group == ActiveGroupKey))
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

            if (ActiveGroupKey == null) Parts.Add($"{AccountsList.Select(a => a.Group).Distinct().Count()} groups");
            if (Banned > 0) Parts.Add($"{Banned} banned");
            if (NeedLogin > 0) Parts.Add($"{NeedLogin} not logged in");
            if (SearchBox.Text.Trim().Length > 0) Parts.Add($"{AccountsView.GetItemCount()} match \"{SearchBox.Text.Trim()}\"");

            GroupSubLabel.Text = string.Join("  ·  ", Parts);
            GroupSubLabel.Color = Banned > 0 ? Modern.Danger : Modern.Sub;
            GroupSubLabel.Invalidate();
        }

        private void UpdateGroupSidebar()
        {
            if (GroupsSidebar == null || AccountsList == null) return;

            var Items = new List<GroupSidebarItem>
            {
                new GroupSidebarItem() { Key = null, Title = "All accounts", Count = AccountsList.Count, Banned = AccountsList.Count(a => AccountRowRenderer.StateOf(a) == AccountState.Banned), NeedLogin = AccountsList.Count(a => AccountRowRenderer.StateOf(a) == AccountState.NeedsLogin) }
            };

            foreach (string Key in AccountsList.Select(a => a.Group).Distinct().OrderBy(k => k, StringComparer.CurrentCulture))
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

        private async void LoginSelectedAccount()
        {
            Account A = AccountCard.Single;

            if (A == null) { MessageBox.Show("Select one account first.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (IsDownloadingChromium) { DownloadProgressBar_Click(null, EventArgs.Empty); return; }

            string Password = A.Password;

            if (string.IsNullOrEmpty(Password))
                MessageBox.Show($"No password is saved for {A.Username}.\nThe username gets filled in, type the password yourself.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                    MessageBox.Show($"The login window could not be opened.\n\n{x.Message}", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            AccountCard.Login.Enabled = false;
            AddAccountButton.Enabled = false;

            try
            {
                var Result = await CleanLogin.Run(Browser, Username, Password, Profile);

                if (Result is (string Cookie, string Pw))
                {
                    Account Added = await Task.Run(() => AddAccount(Cookie, Pw ?? string.Empty));

                    if (Added == null)
                        MessageBox.Show("Roblox did not accept the login cookie. Please try again.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    {
                        CleanLogin.AdoptProfile(Profile, Added.UserID);

                        if (Added.GetField("BanStatus") == "NeedsLogin") Added.Fields.Remove("BanStatus");
                        Added.Fields["SessionChecked"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

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
                MessageBox.Show($"The login window could not be opened.\n\n{x.Message}", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                AccountCard.RefreshState();
                AddAccountButton.Enabled = true;
            }
        }

        private string FriendTargetName => General != null && General.Exists("FriendTarget") && !string.IsNullOrWhiteSpace(General.Get("FriendTarget")) ? General.Get("FriendTarget").Trim() : "";

        private int FriendDelaySeconds => General != null && General.Exists("FriendRequestDelay") && int.TryParse(General.Get("FriendRequestDelay"), out int D) ? Math.Max(1, Math.Min(D, 600)) : 6;

        private async void AddFriendToMain()
        {
            if (FriendTokenSource != null) { FriendTokenSource.Cancel(); return; } // button works as "stop" while running

            string Target = FriendTargetName;
            var Accounts = AccountCard.Selection.Where(a => !string.Equals(a.Username, Target, StringComparison.OrdinalIgnoreCase)).ToList();

            if (Accounts.Count == 0) { MessageBox.Show($"Select the alt accounts that should add {Target}.", "Add main account", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            if (Accounts.Count > 1 && MessageBox.Show($"Send {Target} a friend request from {Accounts.Count} accounts?\n\nThere is a {FriendDelaySeconds} s pause between the accounts. Click the button again to stop.", "Add main account", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var (Found, TargetId) = await Task.Run(() => (GetUserID(Target, out long Id, out _), Id));

            if (!Found || TargetId <= 0) { MessageBox.Show($"The Roblox user \"{Target}\" was not found. Check the main account with \"Change\".", "Add main account", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            FriendTokenSource = new CancellationTokenSource();
            var Token = FriendTokenSource.Token;
            AccountCard.AddFriendBusy = true;

            int Sent = 0, Already = 0, Done = 0;
            var Problems = new List<string>();
            bool Stopped = false;

            try
            {
                for (int i = 0; i < Accounts.Count; i++)
                {
                    AccountCard.AddFriend.Text = Accounts.Count > 1 ? $"{i + 1}/{Accounts.Count} · Stop" : "Sending…";

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
                FriendTokenSource.Dispose();
                FriendTokenSource = null;
                AccountCard.AddFriendBusy = false;
                AccountCard.RefreshState();
            }

            string Summary = $"{(Stopped ? "Stopped. " : string.Empty)}{Sent} request{(Sent == 1 ? "" : "s")} sent to {Target}";

            if (Already > 0) Summary += $", {Already} already friends / already sent";
            if (Done < Accounts.Count) Summary += $", {Accounts.Count - Done} not done";
            if (Problems.Count > 0) Summary += $"\n\nProblems ({Problems.Count}):\n" + string.Join("\n", Problems.Take(12)) + (Problems.Count > 12 ? $"\n… and {Problems.Count - 12} more" : string.Empty);

            MessageBox.Show(Summary, "Add main account", MessageBoxButtons.OK, Problems.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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

            try
            {
                // 1) terminated accounts: one request per 100 accounts
                BannedSet = await AccountChecks.BannedIds(Accounts.Select(a => a.UserID).ToList(), Token);
                PublicFailed = BannedSet == null;

                // 2) every other account: is it still logged in (+ e-mail)
                var ToCheck = Accounts.Where(a => BannedSet == null || !BannedSet.Contains(a.UserID)).ToList();

                await RunSessionChecks(ToCheck, Sessions, Token, (Done, Total) => CheckBansButton.Text = $"{Done}/{Total} · Stop");
            }
            catch (OperationCanceledException) { Stopped = true; }

            string Now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            if (BannedSet != null)
                foreach (var A in Accounts)
                {
                    if (BannedSet.Contains(A.UserID)) A.Fields["BanStatus"] = "Banned";
                    else if (A.GetField("BanStatus") == "Banned") A.Fields.Remove("BanStatus"); // unbanned again

                    A.Fields["BanChecked"] = Now;
                }

            foreach (var Pair in Sessions) AccountChecks.Apply(Pair.Key, Pair.Value);

            SaveAccounts(true);

            BanTokenSource.Dispose();
            BanTokenSource = null;
            CheckBansButton.Text = "Check for bans";

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

            MessageBox.Show(Summary, "Check for bans", MessageBoxButtons.OK, Banned + Temp > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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

            var Due = AccountsList.Where(a => a.GetField("BanStatus") != "Banned" && !AccountChecks.CheckedWithin(a, "SessionChecked", TimeSpan.FromHours(2))).ToList();

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
            Grid.MainName = FriendTargetName;
            Grid.Invalidate();
        }

        #endregion
    }
}
