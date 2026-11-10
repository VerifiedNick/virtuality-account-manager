using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Card view of the accounts: avatar, username, alias, online status and the two quick buttons
    /// "Join" (game in the header) and "Join &lt;main&gt;" (follow the main account). Selection is the list's selection,
    /// so every other button and menu keeps working on whatever is selected here.
    /// </summary>
    public class AccountGrid : ScrollableControl
    {
        private class Entry
        {
            public Account Account;
            public string Section;     // section header shown before this card (null = none)
            public Rectangle Card;     // unscrolled
            public Rectangle Header;   // unscrolled, empty when no header
            public int Count;          // accounts in the section (computed once per layout, not per paint)
        }

        private readonly List<Entry> Entries = new List<Entry>();
        private List<Account> Items = new List<Account>();
        private bool Sections;
        private Account SelectionAnchor;

        // drag selection (rubber band): starts on empty space, or anywhere while Ctrl is held
        private bool DragArmed, Dragging;
        private bool CardDragArmed;
        private Point CardDragStart;
        private List<Account> CardDragAccounts;
        private Point DragStart;                  // content coordinates (unscrolled)
        private Point DragNow;
        private HashSet<Account> DragBase;
        private Account PendingToggle;            // Ctrl + click without dragging toggles the card
        private bool PendingClear;                // click on empty space without dragging clears the selection
        private List<Account> LastDragSelection = new List<Account>();
        private readonly Timer DragScroll = new Timer() { Interval = 30 };

        private Account HoverAccount;
        private int HoverButton; // 0 none, 1 join, 2 join main

        public Func<HashSet<Account>> GetSelection = () => new HashSet<Account>();
        public Action<List<Account>> SetSelection = _ => { };
        public Action<Account> JoinClicked = _ => { };
        public Action<Account> JoinMainClicked = _ => { };
        public Action<Account> ReloginClicked = _ => { };
        public Action<Account, Point> ContextRequested = (_, __) => { };
        /// <summary>Right click on the "Join …" button: choose which account that button joins.</summary>
        public Action<Point> JoinTargetMenuRequested = _ => { };
        public Func<string, string> SectionTitle = g => g;
        public string MainName = string.Empty;
        private string MainLabel => string.IsNullOrEmpty(MainName) ? "Join …" : $"Join {MainName}";

        private int Pad => Modern.Px(4); // top/bottom only: the cards run flush with the title and buttons above
        private int GapX => Modern.Px(10);
        private int CardMinW => Modern.Px(232);
        private int CardH => Modern.Px(138);
        private int HeaderH => Modern.Px(34);

        public AccountGrid()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            AutoScroll = true;
            BackColor = Modern.Bg;
            Tag = "NoScaling";
            Pinned = new PinnedHeader(this);

            DragScroll.Tick += (s, e) =>
            {
                if (!Dragging) { DragScroll.Stop(); return; }

                Point P = PointToClient(Cursor.Position);
                int Step = P.Y < 0 ? P.Y : (P.Y > ClientSize.Height ? P.Y - ClientSize.Height : 0);

                if (Step == 0) return;

                Step = Math.Sign(Step) * Math.Min(Modern.Px(40), Math.Abs(Step) / 2 + Modern.Px(4));
                AutoScrollPosition = new Point(0, Math.Max(0, -AutoScrollPosition.Y + Step));
                UpdatePinned();
                UpdateDrag(P);
            };
        }

        private Rectangle DragRect => Rectangle.FromLTRB(Math.Min(DragStart.X, DragNow.X), Math.Min(DragStart.Y, DragNow.Y), Math.Max(DragStart.X, DragNow.X), Math.Max(DragStart.Y, DragNow.Y));

        private void UpdateDrag(Point Client)
        {
            DragNow = new Point(Client.X - AutoScrollPosition.X, Client.Y - AutoScrollPosition.Y);

            var Rect = DragRect;
            var Hit = Entries.Where(E => E.Card.IntersectsWith(Rect)).Select(E => E.Account);
            var Result = Items.Where(a => DragBase.Contains(a) || Hit.Contains(a)).ToList();

            if (LastDragSelection == null || !Result.SequenceEqual(LastDragSelection))
            {
                LastDragSelection = Result;
                SetSelection(Result);
            }

            Invalidate();
        }

        public void SetItems(IEnumerable<Account> Accounts, bool ShowSections)
        {
            Items = Accounts?.ToList() ?? new List<Account>();
            Sections = ShowSections;

            Relayout();
            Invalidate();
        }

        public IReadOnlyList<Account> Shown => Items;

        /// <summary>List view: one wide row per account instead of cards (same features: selection, drag, buttons, menus).</summary>
        private bool RowMode_;
        public bool RowMode { get => RowMode_; set { if (RowMode_ == value) return; RowMode_ = value; Relayout(); Invalidate(); } }

        private int RowH => Modern.Px(64);
        private int RowGap => Modern.Px(6);

        private void Relayout()
        {
            Entries.Clear();

            Dictionary<string, int> Counts = Sections ? Items.GroupBy(a => a.Group ?? string.Empty).ToDictionary(g => g.Key, g => g.Count()) : null;

            // the scrollbar lane is always reserved, so the cards end at the same place with or without a scrollbar
            int W = Math.Max(Modern.Px(160), Width - SystemInformation.VerticalScrollBarWidth);
            LayoutW = W;
            int Cols = Math.Max(1, (W + GapX) / (CardMinW + GapX));
            int CardW = Math.Max(Modern.Px(160), (W - GapX * (Cols - 1)) / Cols);
            int Y = Pad, Col = 0;
            string LastGroup = null;
            bool First = true;

            if (RowMode_) { Cols = 1; CardW = W; }

            int ItemH = RowMode_ ? RowH : CardH, ItemGap = RowMode_ ? RowGap : GapX;

            foreach (Account A in Items)
            {
                var E = new Entry() { Account = A };

                if (Sections && (First || A.Group != LastGroup))
                {
                    if (!First && Col != 0) Y += ItemH + ItemGap;
                    if (!First) Y += Modern.Px(8);

                    Col = 0;
                    E.Section = A.Group;
                    E.Count = Counts.TryGetValue(A.Group ?? string.Empty, out int N) ? N : 0;
                    E.Header = new Rectangle(0, Y, W, HeaderH);
                    Y += HeaderH + Modern.Px(4);
                }

                First = false;
                LastGroup = A.Group;

                E.Card = new Rectangle(Col * (CardW + GapX), Y, CardW, ItemH);
                Entries.Add(E);

                Col++;

                if (Col >= Cols) { Col = 0; Y += ItemH + ItemGap; }
            }

            if (Col != 0) Y += ItemH + ItemGap;

            AutoScrollMinSize = new Size(0, Y + Pad);
            UpdatePinned();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
            Invalidate();
        }

        private Rectangle Shift(Rectangle R) => new Rectangle(R.X + AutoScrollPosition.X, R.Y + AutoScrollPosition.Y, R.Width, R.Height);

        private Rectangle FullButton(Rectangle Card)
        {
            if (RowMode_)
            {
                int W = Modern.Px(140) + Modern.Px(8) + Modern.Px(170), H = Modern.Px(32);
                return new Rectangle(Card.Right - Modern.Px(14) - W, Card.Y + (Card.Height - H) / 2, W, H);
            }

            return new Rectangle(Card.X + Modern.Px(12), Card.Bottom - Modern.Px(12) - Modern.Px(30), Card.Width - Modern.Px(24), Modern.Px(30));
        }

        private bool NoTarget => string.IsNullOrEmpty(MainName);

        private (Rectangle Join, Rectangle Main) Buttons(Rectangle Card)
        {
            if (RowMode_)
            {
                int RH_ = Modern.Px(32), RY = Card.Y + (Card.Height - RH_) / 2, RMainW = Modern.Px(170), RJoinW = Modern.Px(140);
                if (NoTarget) { int Full_ = RJoinW + Modern.Px(8) + RMainW; return (new Rectangle(Card.Right - Modern.Px(14) - Full_, RY, Full_, RH_), Rectangle.Empty); }

                var RMain = new Rectangle(Card.Right - Modern.Px(14) - RMainW, RY, RMainW, RH_);
                return (new Rectangle(RMain.X - Modern.Px(8) - RJoinW, RY, RJoinW, RH_), RMain);
            }

            int P = Modern.Px(12), H = Modern.Px(30), G = Modern.Px(8);
            int Y = Card.Bottom - P - H, W = Card.Width - P * 2;
            // no second account to join: a single full-width "Join" bar instead of two "Join" buttons
            if (NoTarget) return (new Rectangle(Card.X + P, Y, W, H), Rectangle.Empty);

            int JoinW = Math.Max(Modern.Px(56), W * 36 / 100);

            return (new Rectangle(Card.X + P, Y, JoinW, H), new Rectangle(Card.X + P + JoinW + G, Y, W - JoinW - G, H));
        }

        private bool Disabled(Account A) => AccountRowRenderer.StateOf(A) == AccountState.Banned;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            Modern.Prepare(G);
            G.Clear(BackColor);

            UpdatePinned(); // safety net: any scroll change that did not come through the handlers below

            if (Entries.Count == 0)
            {
                Modern.Text_(G, "No accounts here", Modern.Font(10f), ClientRectangle, Modern.Sub, TextFormatFlags.HorizontalCenter);
                return;
            }

            var Selected = GetSelection();

            foreach (var E in Entries)
            {
                if (E.Section != null)
                {
                    var H = Shift(E.Header);

                    if (H.Bottom >= 0 && H.Top <= Height) PaintHeader(G, E.Section, E.Count, H);
                }

                var R = Shift(E.Card);

                if (R.Bottom < 0 || R.Top > Height) continue;

                if (RowMode_) DrawRow(G, E.Account, R, Selected.Contains(E.Account), E.Account == HoverAccount);
                else DrawCard(G, E.Account, R, Selected.Contains(E.Account), E.Account == HoverAccount);
            }

            if (Dragging)
            {
                var DR = Shift(DragRect);
                using (var DB = new SolidBrush(Color.FromArgb(40, Modern.Accent))) G.FillRectangle(DB, DR);
                using (var DP = new Pen(Modern.AccentText)) G.DrawRectangle(DP, DR.X, DR.Y, Math.Max(0, DR.Width - 1), Math.Max(0, DR.Height - 1));
            }
        }

        /// <summary>Section header: group picture, title, number of accounts and a line. Also drawn by the pinned copy at the top of the list.</summary>
        private void PaintHeader(Graphics G, string Section, int Count, Rectangle H)
        {
            int Sz = Modern.Px(22);
            var Icon = new Rectangle(H.X, H.Y + (H.Height - Sz) / 2, Sz, Sz);
            Image Pic = GroupImages.GetImage(Section, out _);

            var Variant = GroupImages.Resolve(Section).Variant;

            Modern.Box(G, Icon, Modern.Field, Modern.FieldBorder);
            if (Pic != null) G.DrawImage(Pic, Rectangle.Inflate(Icon, -1, -1));
            Modern.VariantFrame(G, Icon, Variant, 1.5f);

            string Title = SectionTitle(Section);
            Font TF = Modern.Font(10f, FontStyle.Regular, true);
            int TW = TextRenderer.MeasureText(Title, TF, Size.Empty, TextFormatFlags.NoPadding).Width;
            int X = Icon.Right + Modern.Px(10);

            Modern.Text_(G, Title, TF, new Rectangle(X, H.Y, H.Width, H.Height), Modern.Text, TextFormatFlags.NoPadding);
            X += TW + Modern.Px(8);

            Modern.Text_(G, $"{Count}", Modern.Font(8.5f), new Rectangle(X, H.Y, Modern.Px(60), H.Height), Modern.Sub, TextFormatFlags.NoPadding);

            using (var P = new Pen(Modern.Line))
                G.DrawLine(P, H.X, H.Bottom - 1, H.Right, H.Bottom - 1);
        }

        /// <summary>
        /// Copy of the current section header that stays at the top of the list while its cards scroll underneath.
        /// It is a separate control above the grid (same parent): scrolling moves the grid's pixels, a header painted
        /// inside the grid would be dragged along for a moment. Mouse input passes through to the grid.
        /// </summary>
        public sealed class PinnedHeader : Control
        {
            private readonly AccountGrid Owner;
            internal string Section;
            internal int Count;

            internal PinnedHeader(AccountGrid Owner)
            {
                this.Owner = Owner;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                BackColor = Modern.Bg;
                Tag = "NoScaling";
                Visible = false;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; } // WM_NCHITTEST → HTTRANSPARENT: clicks go to the grid underneath

                base.WndProc(ref m);
            }

            protected override void OnMouseWheel(MouseEventArgs e) => Owner.OnMouseWheel(e);

            protected override void OnPaint(PaintEventArgs e)
            {
                Modern.Prepare(e.Graphics);
                e.Graphics.Clear(Owner.BackColor);

                // bottom aligned: when the next section pushes this one out, the control gets shorter and the header slides up
                if (Section != null) Owner.PaintHeader(e.Graphics, Section, Count, new Rectangle(0, Height - Owner.HeaderH, Width, Owner.HeaderH));
            }
        }

        /// <summary>The pinned header control. The owner adds it to the grid's parent, above the grid.</summary>
        public readonly PinnedHeader Pinned;

        private Rectangle PinnedR;   // area of the grid covered by the pinned header (client coordinates)
        private int LayoutW;

        /// <summary>Finds the section that is scrolled past the top and places the pinned header over it.</summary>
        private void UpdatePinned()
        {
            if (Pinned == null || Pinned.Parent == null || Pinned.Parent != Parent) return;

            Entry Current = null;
            int NextTop = int.MaxValue;

            if (Sections && Visible)
                foreach (var E in Entries)
                {
                    if (E.Section == null) continue;

                    int Top = E.Header.Y + AutoScrollPosition.Y;

                    if (Top < 0) Current = E;
                    else { NextTop = Top; break; }
                }

            int H = Current == null ? 0 : Math.Min(HeaderH, NextTop);

            if (H <= 0)
            {
                PinnedR = Rectangle.Empty;
                if (Pinned.Visible) Pinned.Visible = false;
                return;
            }

            PinnedR = new Rectangle(0, 0, LayoutW, H);

            var Target = new Rectangle(Left, Top, LayoutW, H);
            bool Changed = Pinned.Section != Current.Section || Pinned.Count != Current.Count;

            Pinned.Section = Current.Section;
            Pinned.Count = Current.Count;

            if (Pinned.Bounds != Target) { Pinned.Bounds = Target; Changed = true; }
            if (!Pinned.Visible) { Pinned.Visible = true; Pinned.BringToFront(); Changed = true; }
            if (Changed) Pinned.Invalidate();
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);

            // group pictures arrive later, names change: the pinned copy follows every repaint of the list
            if (Pinned != null && Pinned.Visible) Pinned.Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdatePinned(); }
        protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); UpdatePinned(); }

        private void DrawCard(Graphics G, Account A, Rectangle R, bool Sel, bool Hover)
        {
            AccountState State = AccountRowRenderer.StateOf(A);
            bool Banned = State == AccountState.Banned;
            bool Login = State == AccountState.NeedsLogin;
            bool InGame = State == AccountState.InGame;

            Color Fill = Sel ? Modern.Selected : (Hover ? Color.FromArgb(14, 14, 14) : (InGame ? Modern.OnlineBg : Modern.Panel));
            Color Border = Sel ? Modern.Accent : (Banned ? Modern.DangerLine : (Login ? Color.FromArgb(110, 84, 20) : (InGame ? Modern.OnlineLine : Modern.Line)));

            Modern.Box(G, R, Fill, Border);

            // in game: green frame all around (also when selected, inside the purple one)
            if (InGame)
                using (var P_ = new Pen(Sel ? Modern.OnlineLine : Modern.Online))
                    G.DrawRectangle(P_, R.X + (Sel ? 1 : 0), R.Y + (Sel ? 1 : 0), R.Width - (Sel ? 3 : 1), R.Height - (Sel ? 3 : 1));

            if (Banned || Login || InGame)
                using (var B = new SolidBrush(Banned ? Modern.Danger : (Login ? Modern.Warn : Modern.Online)))
                    G.FillRectangle(B, R.X, R.Y, Modern.Px(3), R.Height);

            int P = Modern.Px(12), Av = Modern.Px(56);
            var AvR = new Rectangle(R.X + P, R.Y + P, Av, Av);

            Modern.Box(G, AvR, Modern.Field, Banned ? Modern.DangerLine : (Login ? Modern.Warn : (InGame ? Modern.Online : Modern.FieldBorder)));

            Image Pic = AvatarCache.Get(A.UserID);

            if (Pic != null && !AccountRowRenderer.HideNames)
                G.DrawImage(Pic, Rectangle.Inflate(AvR, -1, -1));
            else
                Modern.Text_(G, AccountRowRenderer.HideNames ? "··" : Modern.Initials(A.Username), Modern.Font(12f, FontStyle.Regular, true), AvR, Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

            Color Dot = AccountRowRenderer.StateColor(State);

            if (Login)
            {
                // "!" badge: this account has to log in again
                int BS = Modern.Px(18);
                var BR = new Rectangle(AvR.Right - BS + Modern.Px(4), AvR.Y - Modern.Px(4), BS, BS);
                Modern.Box(G, BR, Modern.Warn);
                Modern.Text_(G, "!", Modern.Font(9f, FontStyle.Bold, true), BR, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                int DS = Modern.Px(10);
                var DotR = new Rectangle(AvR.Right - DS + Modern.Px(3), AvR.Bottom - DS + Modern.Px(3), DS, DS);
                using (var B = new SolidBrush(Dot)) G.FillRectangle(B, DotR);
                using (var Pn = new Pen(Fill, Modern.Px(2))) G.DrawRectangle(Pn, DotR);
            }

            int TX = AvR.Right + Modern.Px(12), TW = R.Right - TX - P;
            string Name = AccountRowRenderer.HideNames ? (string.IsNullOrEmpty(A.Alias) ? "Hidden" : A.Alias) : A.Username;
            Font NF = Modern.Font(10.25f, FontStyle.Regular, true);

            Modern.Text_(G, Name, NF, new Rectangle(TX, R.Y + P - Modern.Px(2), TW, Modern.Px(22)), Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding);

            if (!AccountRowRenderer.HideNames && !string.IsNullOrEmpty(A.Alias))
            {
                int NW = TextRenderer.MeasureText(Name, NF, Size.Empty, TextFormatFlags.NoPadding).Width + Modern.Px(8);
                if (NW < TW - Modern.Px(30))
                    Modern.Text_(G, A.Alias, Modern.Font(8.5f), new Rectangle(TX + NW, R.Y + P - Modern.Px(2), TW - NW, Modern.Px(22)), Modern.Sub, TextFormatFlags.NoPadding);
            }

            // e-mail (masked by Roblox)
            string Mail = A.GetField("Email");
            string MailText = !A.Fields.ContainsKey("Email") ? "e-mail: not checked yet" : (string.IsNullOrEmpty(Mail) ? "no e-mail" : Mail);
            bool Verified = A.GetField("EmailVerified") == "true";

            if (AccountRowRenderer.HideNames && !string.IsNullOrEmpty(Mail)) MailText = "e-mail hidden";

            Modern.Text_(G, MailText + (Verified && !string.IsNullOrEmpty(Mail) ? "  ✓" : string.Empty), Modern.Font(8.5f), new Rectangle(TX, R.Y + P + Modern.Px(20), TW, Modern.Px(18)), string.IsNullOrEmpty(Mail) ? Modern.Faint : Modern.Sub, TextFormatFlags.NoPadding);

            string StatusText = AccountRowRenderer.StateText(A, State);

            if (State == AccountState.InGame && !string.IsNullOrEmpty(A.Presence?.lastLocation))
                StatusText = A.Presence.lastLocation;

            Modern.Text_(G, StatusText, Modern.Font(8.75f, FontStyle.Regular, State != AccountState.Offline && State != AccountState.Online), new Rectangle(TX, R.Y + P + Modern.Px(38), TW, Modern.Px(18)), State == AccountState.Offline ? Modern.Sub : Dot, TextFormatFlags.NoPadding);

            // quick buttons
            if (Login)
            {
                var Full = FullButton(R);
                bool H = A == HoverAccount && HoverButton == 3;

                Modern.Box(G, Full, H ? Color.FromArgb(40, 30, 6) : Color.FromArgb(24, 18, 4), Modern.Warn);
                Modern.Text_(G, "Re-login", Modern.Font(8.75f, FontStyle.Regular, true), Full, Modern.Warn, TextFormatFlags.HorizontalCenter);
                return;
            }

            var (JoinR, MainR) = Buttons(R);

            DrawButton(G, JoinR, "Join", false, !Banned && A == HoverAccount && HoverButton == 1, Banned);
            if (!MainR.IsEmpty) DrawButton(G, MainR, MainLabel, false, !Banned && A == HoverAccount && HoverButton == 2, Banned);
        }

        /// <summary>List view row: picture, name + e-mail, status, last used, note, and the same buttons as the card.</summary>
        private void DrawRow(Graphics G, Account A, Rectangle R, bool Sel, bool Hover)
        {
            AccountState State = AccountRowRenderer.StateOf(A);
            bool Banned = State == AccountState.Banned, Login = State == AccountState.NeedsLogin, InGame = State == AccountState.InGame;

            Color Fill = Sel ? Modern.Selected : (Hover ? Color.FromArgb(14, 14, 14) : (InGame ? Modern.OnlineBg : Modern.Panel));
            Color Border = Sel ? Modern.Accent : (Banned ? Modern.DangerLine : (Login ? Color.FromArgb(110, 84, 20) : (InGame ? Modern.OnlineLine : Modern.Line)));

            Modern.Box(G, R, Fill, Border);

            if (InGame)
                using (var P_ = new Pen(Sel ? Modern.OnlineLine : Modern.Online))
                    G.DrawRectangle(P_, R.X + (Sel ? 1 : 0), R.Y + (Sel ? 1 : 0), R.Width - (Sel ? 3 : 1), R.Height - (Sel ? 3 : 1));

            if (Banned || Login || InGame)
                using (var B = new SolidBrush(Banned ? Modern.Danger : (Login ? Modern.Warn : Modern.Online)))
                    G.FillRectangle(B, R.X, R.Y, Modern.Px(3), R.Height);

            // picture
            int Av = Modern.Px(44);
            var AvR = new Rectangle(R.X + Modern.Px(14), R.Y + (R.Height - Av) / 2, Av, Av);
            Modern.Box(G, AvR, Modern.Field, Banned ? Modern.DangerLine : (Login ? Modern.Warn : (InGame ? Modern.Online : Modern.FieldBorder)));

            Image Pic = AccountRowRenderer.HideNames ? null : AvatarCache.Get(A.UserID);
            if (Pic != null) G.DrawImage(Pic, Rectangle.Inflate(AvR, -1, -1));
            else Modern.Text_(G, AccountRowRenderer.HideNames ? "··" : Modern.Initials(A.Username), Modern.Font(11f, FontStyle.Regular, true), AvR, Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

            Color Dot = AccountRowRenderer.StateColor(State);

            if (Login)
            {
                int BS = Modern.Px(16);
                var BR = new Rectangle(AvR.Right - BS + Modern.Px(4), AvR.Y - Modern.Px(4), BS, BS);
                Modern.Box(G, BR, Modern.Warn);
                Modern.Text_(G, "!", Modern.Font(9f, FontStyle.Bold, true), BR, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }

            // columns (right edge = where the buttons start)
            Rectangle ButtonsLeft = Login ? FullButton(R) : Buttons(R).Join;
            int X = AvR.Right + Modern.Px(14), Right = ButtonsLeft.X - Modern.Px(16);
            int Avail = Right - X;

            int NameW = Math.Min(Modern.Px(300), Math.Max(Modern.Px(150), Avail * 40 / 100));
            int StatusW = Math.Min(Modern.Px(220), Math.Max(Modern.Px(110), Avail * 28 / 100));
            int UsedW = Modern.Px(110);

            string Name = AccountRowRenderer.HideNames ? (string.IsNullOrEmpty(A.Alias) ? "Hidden" : A.Alias) : A.Username;
            Modern.Text_(G, Name, Modern.Font(11f, FontStyle.Regular, true), new Rectangle(X, R.Y + Modern.Px(11), NameW, Modern.Px(22)), Banned ? Modern.Danger : Modern.Text, TextFormatFlags.NoPadding);

            string Mail = A.GetField("Email");
            string MailText = !A.Fields.ContainsKey("Email") ? "e-mail not checked yet" : (string.IsNullOrEmpty(Mail) ? "no e-mail" : (AccountRowRenderer.HideNames ? "e-mail hidden" : Mail + (A.GetField("EmailVerified") == "true" ? "  ✓" : string.Empty)));
            Modern.Text_(G, MailText, Modern.Font(9f), new Rectangle(X, R.Y + Modern.Px(34), NameW, Modern.Px(18)), string.IsNullOrEmpty(Mail) ? Modern.Faint : Modern.Sub, TextFormatFlags.NoPadding);

            X += NameW + Modern.Px(16);

            if (Right - X > Modern.Px(80))
            {
                string StatusText = AccountRowRenderer.StateText(A, State);
                if (InGame && !string.IsNullOrEmpty(A.Presence?.lastLocation)) StatusText = A.Presence.lastLocation;

                int DS = Modern.Px(9);
                using (var B = new SolidBrush(Dot)) G.FillRectangle(B, X, R.Y + (R.Height - DS) / 2, DS, DS);
                Modern.Text_(G, StatusText, Modern.Font(9.75f, FontStyle.Regular, State != AccountState.Offline), new Rectangle(X + DS + Modern.Px(8), R.Y, Math.Min(StatusW, Right - X) - DS - Modern.Px(8), R.Height), State == AccountState.Offline ? Modern.Sub : Dot, TextFormatFlags.NoPadding);
                X += StatusW + Modern.Px(16);
            }

            if (Right - X > Modern.Px(80))
            {
                double Days = (DateTime.Now - A.LastUse).TotalDays;
                Color UsedC = Days > 30 ? Modern.Danger : (Days > 20 ? Modern.Warn : Modern.Sub);
                Modern.Text_(G, "used " + Modern.Ago(A.LastUse), Modern.Font(9f), new Rectangle(X, R.Y, Math.Min(UsedW, Right - X), R.Height), UsedC, TextFormatFlags.NoPadding);
                X += UsedW + Modern.Px(16);
            }

            string Note = FirstLine(A.Description);
            if (!string.IsNullOrEmpty(Note) && Right - X > Modern.Px(60))
                Modern.Text_(G, Note, Modern.Font(9f), new Rectangle(X, R.Y, Right - X, R.Height), Modern.Text2, TextFormatFlags.NoPadding);

            // buttons
            if (Login)
            {
                var Full = FullButton(R);
                bool H = A == HoverAccount && HoverButton == 3;
                Modern.Box(G, Full, H ? Color.FromArgb(40, 30, 6) : Color.FromArgb(24, 18, 4), Modern.Warn);
                Modern.Text_(G, "Re-login", Modern.Font(9f, FontStyle.Regular, true), Full, Modern.Warn, TextFormatFlags.HorizontalCenter);
                return;
            }

            var (JoinR, MainR) = Buttons(R);
            DrawButton(G, JoinR, "Join", false, !Banned && A == HoverAccount && HoverButton == 1, Banned);
            if (!MainR.IsEmpty) DrawButton(G, MainR, MainLabel, false, !Banned && A == HoverAccount && HoverButton == 2, Banned);
        }

        private static string FirstLine(string Text) => (Text ?? string.Empty).Split('\n')[0].Trim();

        private void DrawButton(Graphics G, Rectangle R, string Text, bool Accent, bool Hover, bool Off)
        {
            Color Fill = Off ? Modern.Panel : (Hover ? Modern.Hover : Modern.Field);
            Color Border = Off ? Modern.Line : (Accent ? Modern.Accent : Modern.FieldBorder);
            Color Fore = Off ? Modern.Faint : (Accent ? Modern.AccentText : Modern.Text);

            Modern.Box(G, R, Fill, Border);
            Modern.Text_(G, Text, Modern.Font(8.75f, FontStyle.Regular, true), R, Fore, TextFormatFlags.HorizontalCenter);
        }

        private (Account Account, int Button) HitTest(Point P)
        {
            // under the pinned header nothing is clickable (the cards there are hidden)
            if (!PinnedR.IsEmpty && PinnedR.Contains(P)) return (null, 0);

            foreach (var E in Entries)
            {
                var R = Shift(E.Card);

                if (!R.Contains(P)) continue;

                if (AccountRowRenderer.StateOf(E.Account) == AccountState.NeedsLogin)
                    return (E.Account, FullButton(R).Contains(P) ? 3 : 0);

                var (JoinR, MainR) = Buttons(R);

                if (JoinR.Contains(P)) return (E.Account, 1);
                if (!MainR.IsEmpty && MainR.Contains(P)) return (E.Account, 2);

                return (E.Account, 0);
            }

            return (null, 0);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (CardDragArmed && e.Button == MouseButtons.Left)
            {
                if (Math.Abs(e.X - CardDragStart.X) > Modern.Px(5) || Math.Abs(e.Y - CardDragStart.Y) > Modern.Px(5))
                {
                    CardDragArmed = false;
                    var Accounts = CardDragAccounts;
                    CardDragAccounts = null;

                    if (Accounts != null && Accounts.Count > 0)
                        DoDragDrop(new AccountDragData(Accounts), DragDropEffects.Move);
                }

                return;
            }

            if (DragArmed && e.Button == MouseButtons.Left)
            {
                Point Content = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);

                if (!Dragging && (Math.Abs(Content.X - DragStart.X) > Modern.Px(5) || Math.Abs(Content.Y - DragStart.Y) > Modern.Px(5)))
                {
                    Dragging = true;
                    PendingToggle = null;
                    PendingClear = false;
                    LastDragSelection = null;
                    Cursor = Cursors.Cross;
                    DragScroll.Start();
                }

                if (Dragging) { UpdateDrag(e.Location); return; }
            }

            var (A, B) = HitTest(e.Location);

            if (A != HoverAccount || B != HoverButton)
            {
                HoverAccount = A;
                HoverButton = B;
                Cursor = A != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoverAccount = null;
            HoverButton = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var (A, Button) = HitTest(e.Location);
            var Selected = GetSelection();

            DragArmed = Dragging = false;
            CardDragArmed = false;
            CardDragAccounts = null;
            PendingToggle = null;
            PendingClear = false;

            if (A == null)
            {
                if (e.Button == MouseButtons.Left)
                {
                    // empty space: drag to select; a plain click clears the selection on release
                    DragArmed = true;
                    DragStart = DragNow = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
                    DragBase = ModifierKeys.HasFlag(Keys.Control) || ModifierKeys.HasFlag(Keys.Shift) ? Selected : new HashSet<Account>();
                    PendingClear = ModifierKeys == Keys.None;
                }

                return;
            }

            if (e.Button == MouseButtons.Right && Button == 2)
            {
                JoinTargetMenuRequested(e.Location);
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (!Selected.Contains(A)) { SetSelection(new List<Account> { A }); SelectionAnchor = A; }

                ContextRequested(A, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (Button != 0 && !Disabled(A))
            {
                SetSelection(new List<Account> { A });
                SelectionAnchor = A;

                if (Button == 1) JoinClicked(A); else if (Button == 2) JoinMainClicked(A); else ReloginClicked(A);
                return;
            }

            if (ModifierKeys.HasFlag(Keys.Shift) && SelectionAnchor != null && Items.Contains(SelectionAnchor))
            {
                int From = Items.IndexOf(SelectionAnchor), To = Items.IndexOf(A);
                var Range = Items.Skip(Math.Min(From, To)).Take(Math.Abs(To - From) + 1);

                SetSelection((ModifierKeys.HasFlag(Keys.Control) ? Selected.Concat(Range) : Range).Distinct().ToList());
            }
            else if (ModifierKeys.HasFlag(Keys.Control))
            {
                // Ctrl: drag over cards to add them; a click without dragging toggles this card (on release)
                DragArmed = true;
                DragStart = DragNow = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
                DragBase = Selected;
                PendingToggle = A;
                SelectionAnchor = A;
            }
            else
            {
                // dragging an already-selected card moves the whole selection; otherwise just this one
                bool KeepMultiSelection = Selected.Count > 1 && Selected.Contains(A);

                if (!KeepMultiSelection) SetSelection(new List<Account> { A });
                SelectionAnchor = A;

                CardDragAccounts = KeepMultiSelection ? Selected.ToList() : new List<Account> { A };
                CardDragStart = e.Location;
                CardDragArmed = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left) return;

            DragScroll.Stop();

            if (!Dragging)
            {
                if (PendingToggle != null)
                {
                    var Selected = GetSelection();
                    if (Selected.Contains(PendingToggle)) Selected.Remove(PendingToggle); else Selected.Add(PendingToggle);
                    SetSelection(Items.Where(Selected.Contains).ToList());
                }
                else if (PendingClear)
                    SetSelection(new List<Account>());
            }

            bool WasDragging = Dragging;

            DragArmed = Dragging = false;
            CardDragArmed = false;
            CardDragAccounts = null;
            PendingToggle = null;
            PendingClear = false;

            if (WasDragging)
            {
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Control && e.KeyCode == Keys.A)
            {
                SetSelection(Items.ToList());
                e.Handled = true;
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            UpdatePinned();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            UpdatePinned();
            Invalidate();
        }

        /// <summary>Scrolls so the account's card is visible.</summary>
        public void Reveal(Account A)
        {
            var E = Entries.FirstOrDefault(x => x.Account == A);

            if (E == null) return;

            var R = Shift(E.Card);

            // with sections the pinned header covers the top of the list, a card under it does not count as visible
            int Covered = Sections ? HeaderH : 0;

            if (R.Top < Covered || R.Bottom > ClientSize.Height)
                AutoScrollPosition = new Point(0, Math.Max(0, E.Card.Y - Pad - Covered));

            UpdatePinned();
            Invalidate();
        }
    }
}
