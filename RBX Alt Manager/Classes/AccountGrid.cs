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
        }

        private readonly List<Entry> Entries = new List<Entry>();
        private List<Account> Items = new List<Account>();
        private bool Sections;
        private Account SelectionAnchor;

        // drag selection (rubber band): starts on empty space, or anywhere while Ctrl is held
        private bool DragArmed, Dragging;
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
        public Func<string, string> SectionTitle = g => g;
        public string MainName = "";

        private int Pad => Modern.Px(14);
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

            DragScroll.Tick += (s, e) =>
            {
                if (!Dragging) { DragScroll.Stop(); return; }

                Point P = PointToClient(Cursor.Position);
                int Step = P.Y < 0 ? P.Y : (P.Y > ClientSize.Height ? P.Y - ClientSize.Height : 0);

                if (Step == 0) return;

                Step = Math.Sign(Step) * Math.Min(Modern.Px(40), Math.Abs(Step) / 2 + Modern.Px(4));
                AutoScrollPosition = new Point(0, Math.Max(0, -AutoScrollPosition.Y + Step));
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

            if (!Result.SequenceEqual(LastDragSelection))
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

        private void Relayout()
        {
            Entries.Clear();

            int W = ClientSize.Width;
            int Cols = Math.Max(1, (W - Pad * 2 + GapX) / (CardMinW + GapX));
            int CardW = Math.Max(Modern.Px(160), (W - Pad * 2 - GapX * (Cols - 1)) / Cols);
            int Y = Pad, Col = 0;
            string LastGroup = null;
            bool First = true;

            foreach (Account A in Items)
            {
                var E = new Entry() { Account = A };

                if (Sections && (First || A.Group != LastGroup))
                {
                    if (!First && Col != 0) Y += CardH + GapX;
                    if (!First) Y += Modern.Px(8);

                    Col = 0;
                    E.Section = A.Group;
                    E.Header = new Rectangle(Pad, Y, W - Pad * 2, HeaderH);
                    Y += HeaderH + Modern.Px(4);
                }

                First = false;
                LastGroup = A.Group;

                E.Card = new Rectangle(Pad + Col * (CardW + GapX), Y, CardW, CardH);
                Entries.Add(E);

                Col++;

                if (Col >= Cols) { Col = 0; Y += CardH + GapX; }
            }

            if (Col != 0) Y += CardH + GapX;

            AutoScrollMinSize = new Size(0, Y + Pad);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
            Invalidate();
        }

        private Rectangle Shift(Rectangle R) => new Rectangle(R.X + AutoScrollPosition.X, R.Y + AutoScrollPosition.Y, R.Width, R.Height);

        private static Rectangle FullButton(Rectangle Card) =>
            new Rectangle(Card.X + Modern.Px(12), Card.Bottom - Modern.Px(12) - Modern.Px(30), Card.Width - Modern.Px(24), Modern.Px(30));

        private (Rectangle Join, Rectangle Main) Buttons(Rectangle Card)
        {
            int P = Modern.Px(12), H = Modern.Px(30), G = Modern.Px(8);
            int Y = Card.Bottom - P - H, W = Card.Width - P * 2;
            int JoinW = Math.Max(Modern.Px(56), W * 36 / 100);

            return (new Rectangle(Card.X + P, Y, JoinW, H), new Rectangle(Card.X + P + JoinW + G, Y, W - JoinW - G, H));
        }

        private bool Disabled(Account A) => AccountRowRenderer.StateOf(A) == AccountState.Banned;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            Modern.Prepare(G);
            G.Clear(BackColor);

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

                    if (H.Bottom >= 0 && H.Top <= Height)
                    {
                        int Count = Items.Count(a => a.Group == E.Section);
                        int Sz = Modern.Px(22);
                        var Icon = new Rectangle(H.X, H.Y + (H.Height - Sz) / 2, Sz, Sz);
                        Image Pic = GroupImages.GetImage(E.Section, out _);

                        var Variant = GroupImages.Resolve(E.Section).Variant;

                        Modern.Box(G, Icon, Modern.Field, Modern.FieldBorder);
                        if (Pic != null) G.DrawImage(Pic, Rectangle.Inflate(Icon, -1, -1));
                        Modern.VariantFrame(G, Icon, Variant, 1.5f);

                        string Title = SectionTitle(E.Section);
                        Font TF = Modern.Font(10f, FontStyle.Regular, true);
                        int TW = TextRenderer.MeasureText(Title, TF, Size.Empty, TextFormatFlags.NoPadding).Width;
                        int X = Icon.Right + Modern.Px(10);

                        Modern.Text_(G, Title, TF, new Rectangle(X, H.Y, H.Width, H.Height), Modern.Text, TextFormatFlags.NoPadding);
                        X += TW + Modern.Px(8);


                        Modern.Text_(G, $"{Count}", Modern.Font(8.5f), new Rectangle(X, H.Y, Modern.Px(60), H.Height), Modern.Sub, TextFormatFlags.NoPadding);

                        using (var P = new Pen(Modern.Line))
                            G.DrawLine(P, H.X, H.Bottom - 1, H.Right, H.Bottom - 1);
                    }
                }

                var R = Shift(E.Card);

                if (R.Bottom < 0 || R.Top > Height) continue;

                DrawCard(G, E.Account, R, Selected.Contains(E.Account), E.Account == HoverAccount);
            }

            if (Dragging)
            {
                var DR = Shift(DragRect);
                using (var DB = new SolidBrush(Color.FromArgb(40, Modern.Accent))) G.FillRectangle(DB, DR);
                using (var DP = new Pen(Modern.AccentText)) G.DrawRectangle(DP, DR.X, DR.Y, Math.Max(0, DR.Width - 1), Math.Max(0, DR.Height - 1));
            }
        }

        private void DrawCard(Graphics G, Account A, Rectangle R, bool Sel, bool Hover)
        {
            AccountState State = AccountRowRenderer.StateOf(A);
            bool Banned = State == AccountState.Banned;
            bool Login = State == AccountState.NeedsLogin;

            Color Fill = Sel ? Modern.Selected : (Hover ? Color.FromArgb(14, 14, 14) : Modern.Panel);
            Color Border = Sel ? Modern.Accent : (Banned ? Modern.DangerLine : (Login ? Color.FromArgb(110, 84, 20) : Modern.Line));

            Modern.Box(G, R, Fill, Border);

            if (Banned || Login)
                using (var B = new SolidBrush(Banned ? Modern.Danger : Modern.Warn))
                    G.FillRectangle(B, R.X, R.Y, Modern.Px(3), R.Height);

            int P = Modern.Px(12), Av = Modern.Px(56);
            var AvR = new Rectangle(R.X + P, R.Y + P, Av, Av);

            Modern.Box(G, AvR, Modern.Field, Banned ? Modern.DangerLine : (Login ? Modern.Warn : Modern.FieldBorder));

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
            DrawButton(G, MainR, $"Join {MainName}", true, !Banned && A == HoverAccount && HoverButton == 2, Banned);
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
            foreach (var E in Entries)
            {
                var R = Shift(E.Card);

                if (!R.Contains(P)) continue;

                if (AccountRowRenderer.StateOf(E.Account) == AccountState.NeedsLogin)
                    return (E.Account, FullButton(R).Contains(P) ? 3 : 0);

                var (JoinR, MainR) = Buttons(R);

                if (JoinR.Contains(P)) return (E.Account, 1);
                if (MainR.Contains(P)) return (E.Account, 2);

                return (E.Account, 0);
            }

            return (null, 0);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

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
                SetSelection(new List<Account> { A });
                SelectionAnchor = A;
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
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate();
        }

        /// <summary>Scrolls so the account's card is visible.</summary>
        public void Reveal(Account A)
        {
            var E = Entries.FirstOrDefault(x => x.Account == A);

            if (E == null) return;

            var R = Shift(E.Card);

            if (R.Top < 0 || R.Bottom > ClientSize.Height)
                AutoScrollPosition = new Point(0, Math.Max(0, E.Card.Y - Pad - (E.Section != null ? HeaderH : 0)));

            Invalidate();
        }
    }
}
