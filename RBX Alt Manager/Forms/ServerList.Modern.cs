using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    // Server list / games / favorites / universe / outfits / watcher as a page of the main window.
    // Every tab gets one tool row on the same grid (gutter 16, rows 34 px, gaps 8) and the content below it in a 1 px frame.
    public partial class ServerList
    {
        private bool ModernBuilt;
        private readonly Dictionary<TabPage, Action> TabLayouts = new Dictionary<TabPage, Action>();

        public NBTabControl PageTabs => Tabs;

        public void UseModernLayout()
        {
            if (ModernBuilt) return;
            ModernBuilt = true;

            SuspendLayout();

            Tabs.HideStrip = true;

            // "L" in the corner becomes a normal button on the watcher page
            Controls.Remove(OpenLogsButton);
            OpenLogsButton.Text = "Open Roblox logs";
            OpenLogsButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            OpenLogsButton.Font = Modern.Font(9f, FontStyle.Regular, true);
            RobloxScan.Controls.Add(OpenLogsButton);

            // labels move into the fields as captions
            foreach (var L in new Label[] { UsernameLabel, label1, uPlaceIDLabel, uUniverseIdLabel, OutfitUsernameLabel })
                L.Visible = false;

            foreach (var CB in new[] { ListViewCB, FavoriteListViewCB })
            {
                CB.Appearance = Appearance.Button;
                CB.TextAlign = ContentAlignment.MiddleCenter;
                CB.AutoSize = false;
                CB.Text = "List view";
            }

            RefreshServers.Text = "Refresh";
            RefreshServers.AccessibleDescription = "Primary";
            Search.AccessibleDescription = "Primary";
            Search.Text = "Search";
            SearchPlayer.Text = "Find";
            Favorite.Text = "Favorite current game";
            GetUniverseID.Text = "Get universe ID";
            ViewUniverse.Text = "View games";
            ViewOutfits.Text = "View outfits";
            WearCustomButton.Text = "Wear custom…";

            // ---- Servers
            var OtherPlace = ModernSkin.Field(OtherPlaceId, "Place ID", "other place (optional)");
            var FindPlayer = ModernSkin.Field(Username, "Player", "username");
            var ServersFrame = ModernSkin.Frame(ServerListView);

            TabLayouts[ServersTab] = () =>
            {
                ModernSkin.ToolRow(ServersTab, Modern.Gutter, new (Control, int)[] { (RefreshServers, 110), (OtherPlace, 280) }, new (Control, int)[] { (FindPlayer, 280), (SearchPlayer, 90) });
                Below(ServersTab, ServersFrame);
            };

            // ---- Games
            var SearchTerm = ModernSkin.Field(Term, null, "Search games…");
            SearchTerm.CaptionIcon = IconKind.Search;
            var Page = ModernSkin.Field(PageNum, "Page");
            var GamesFrame = ModernSkin.Frame(GameListPanel, GamesListView);

            TabLayouts[GamesPage] = () =>
            {
                ModernSkin.ToolRow(GamesPage, Modern.Gutter, new (Control, int)[] { (SearchTerm, -1), (ListViewCB, 100), (Page, 110), (Search, 100) });
                Below(GamesPage, GamesFrame);
            };

            // ---- Favorites
            var FavFrame = ModernSkin.Frame(FavoriteGamesPanel, FavoritesListView);

            TabLayouts[FavoritesPage] = () =>
            {
                ModernSkin.ToolRow(FavoritesPage, Modern.Gutter, new (Control, int)[] { (Favorite, 200) }, new (Control, int)[] { (FavoriteListViewCB, 100) });
                Below(FavoritesPage, FavFrame);
            };

            // ---- Universe
            var UniPlace = ModernSkin.Field(PlaceIDUniTB, "Place ID");
            var UniId = ModernSkin.Field(UniverseIDTB, "Universe ID");
            var UniFrame = ModernSkin.Frame(UniverseGamesPanel);

            TabLayouts[UniversePage] = () =>
            {
                ModernSkin.ToolRow(UniversePage, Modern.Gutter, new (Control, int)[] { (UniPlace, 240), (GetUniverseID, 150) }, new (Control, int)[] { (UniId, 240), (ViewUniverse, 130) });
                Below(UniversePage, UniFrame);
            };

            // ---- Outfits
            var OutfitUser = ModernSkin.Field(OutfitUsernameTB, "Player", "username");
            var OutfitFrame = ModernSkin.Frame(OutfitsPanel);

            TabLayouts[OutfitsPage] = () =>
            {
                ModernSkin.ToolRow(OutfitsPage, Modern.Gutter, new (Control, int)[] { (OutfitUser, 280), (ViewOutfits, 130) }, new (Control, int)[] { (WearCustomButton, 140) });
                Below(OutfitsPage, OutfitFrame);
            };

            // ---- Watcher: one setting per row, the values in one column
            foreach (Control C in WatcherPanel.Controls.Cast<Control>().ToList())
            {
                WatcherPanel.Controls.Remove(C);
                C.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                RobloxScan.Controls.Add(C);
            }

            WatcherPanel.Visible = false;
            RobloxScan.AutoScroll = true;

            RobloxScannerCB.Text = "Enable Roblox watcher";
            ScanESLabel.Text = "Scan interval (seconds)";
            label2.Text = "Read interval (ms)";
            ExitIfBetaDetectedCB.Text = "Close Roblox if the beta home menu is detected";
            ExitIfNoConnectionCB.Text = "Close Roblox if it has no server connection for";
            ConnectionSecondsLabel.Text = "seconds";
            SaveWindowPositionsCB.Text = "Save window positions";
            VerifyDataModelCB.Text = "Data model verification";
            IgnoreExistingProcesses.Text = "Ignore Roblox windows that were already open at start";
            RbxMemoryCB.Text = "Close Roblox if its memory is below";
            MBLabel.Text = "MB";
            CloseRbxWindowTitleCB.Text = "Close Roblox if the window title isn't";

            var ScanBox = ModernSkin.Field(ScanIntervalN);
            var ReadBox = ModernSkin.Field(ReadIntervalN);
            var TimeoutBox = ModernSkin.Field(TimeoutNum);
            var MemoryBox = ModernSkin.Field(RbxMemoryLTNum);
            var TitleBox = ModernSkin.Field(RbxWindowNameTB);

            var WatcherRows = new (Control Left, Control Value, Control Unit)[]
            {
                (RobloxScannerCB, null, null),
                (ScanESLabel, ScanBox, null),
                (label2, ReadBox, null),
                (ExitIfBetaDetectedCB, null, null),
                (ExitIfNoConnectionCB, TimeoutBox, ConnectionSecondsLabel),
                (SaveWindowPositionsCB, null, null),
                (VerifyDataModelCB, null, null),
                (IgnoreExistingProcesses, null, null),
                (RbxMemoryCB, MemoryBox, MBLabel),
                (CloseRbxWindowTitleCB, TitleBox, null),
            };

            TabLayouts[RobloxScan] = () =>
            {
                int G = Modern.Gutter, Y = G + RobloxScan.AutoScrollPosition.Y, H = ModernSkin.Row, ValueX = G + Modern.Px(400);
                int TextX = G + Modern.Px(24); // labels start where the check box texts start

                foreach (var (Left, Value, Unit) in WatcherRows)
                {
                    int LH = Left is CheckBox ? Modern.Px(22) : Left.PreferredSize.Height;
                    if (Left is CheckBox) Left.SetBounds(G, Y + (H - LH) / 2, Modern.Px(380), LH);
                    else Left.SetBounds(TextX, Y + (H - LH) / 2, Modern.Px(356), LH);

                    if (Value != null) Value.SetBounds(ValueX, Y, Modern.Px(120), H);

                    if (Unit != null)
                    {
                        int UH = Unit.PreferredSize.Height;
                        Unit.SetBounds(ValueX + Modern.Px(128), Y + (H - UH) / 2, Modern.Px(80), UH);
                    }

                    Y += H + Modern.Px(6);
                }

                OpenLogsButton.SetBounds(G, Y + Modern.Px(12), Modern.Px(170), H);
            };

            foreach (var Page_ in TabLayouts.Keys)
            {
                var P = Page_;
                P.Resize += (s, e) => TabLayouts[P]();
                P.Padding = Padding.Empty;
            }

            ResumeLayout(false);

            foreach (var L in TabLayouts.Values) L();

            foreach (var List in new BrightIdeasSoftware.ObjectListView[] { ServerListView, GamesListView, FavoritesListView })
                ModernSkin.SkinList(List);
        }

        /// <summary>Content frame from under the tool row to the bottom, gutter on every side.</summary>
        private static void Below(Control Page, Control Content)
        {
            int G = Modern.Gutter, Top = G + ModernSkin.Row + G;
            Content.SetBounds(G, Top, Math.Max(Modern.Px(50), Page.ClientSize.Width - G * 2), Math.Max(Modern.Px(50), Page.ClientSize.Height - Top - G));
        }
    }
}
