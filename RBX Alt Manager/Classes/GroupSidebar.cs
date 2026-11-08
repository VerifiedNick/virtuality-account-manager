using RBX_Alt_Manager.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    public class GroupSidebarItem
    {
        public string Key;   // group key as stored on the accounts, null for "All accounts"
        public string Title;
        public int Count;
        public int Banned;
        public int NeedLogin;
    }

    public class GroupSidebarEventArgs : EventArgs
    {
        public GroupSidebarItem Item { get; }
        public Point Location { get; }

        public GroupSidebarEventArgs(GroupSidebarItem Item, Point Location)
        {
            this.Item = Item;
            this.Location = Location;
        }
    }

    /// <summary>
    /// Left-hand overview of all account groups: one card per group with the Rank's picture, the name and the account count.
    /// Click = jump to the group, double click = select the group's accounts, right click = change the picture.
    /// </summary>
    public class GroupSidebar : Panel
    {
        public event EventHandler<GroupSidebarEventArgs> GroupClicked;
        public event EventHandler<GroupSidebarEventArgs> GroupDoubleClicked;
        public event EventHandler<GroupSidebarEventArgs> GroupRightClicked;

        private readonly List<GroupSidebarItem> Items = new List<GroupSidebarItem>();
        private int HoverIndex = -1;
        private string ActiveKey;
        private bool HasActive;
        private Font TitleFont, CountFont, InitialsFont;
        private float FontScale = -1;

        private static Color Accent => Modern.Accent;

        private float S => Program.Scale;
        private int ItemHeight => (int)(52 * S);
        private int Gap => (int)(2 * S);
        private int Pad => (int)(6 * S);

        public GroupSidebar()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            AutoScroll = true;
            Tag = "NoScaling"; // fonts are created at the right size by this control itself
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        }

        public void SetGroups(IEnumerable<GroupSidebarItem> Groups)
        {
            Items.Clear();
            Items.AddRange(Groups);

            if (HasActive && !Items.Any(i => i.Key == ActiveKey)) HasActive = false;

            HoverIndex = -1;
            AutoScrollMinSize = new Size(0, Items.Count * (ItemHeight + Gap) + Gap);
            Invalidate();
        }

        public void SetActive(string Key)
        {
            ActiveKey = Key;
            HasActive = true;
            Invalidate();
        }

        private void EnsureFonts()
        {
            if (FontScale == S && TitleFont != null) return;

            TitleFont?.Dispose(); CountFont?.Dispose(); InitialsFont?.Dispose();

            TitleFont = new Font("Segoe UI Semibold", 9.25f * S);
            CountFont = new Font("Segoe UI", 8.25f * S);
            InitialsFont = new Font("Segoe UI Semibold", 9f * S);
            FontScale = S;
        }

        private Rectangle ItemBounds(int Index)
        {
            int Y = Gap + Index * (ItemHeight + Gap) + AutoScrollPosition.Y;

            return new Rectangle(Pad, Y, ClientSize.Width - Pad * 2, ItemHeight);
        }

        private int HitTest(Point P)
        {
            for (int i = 0; i < Items.Count; i++)
                if (ItemBounds(i).Contains(P)) return i;

            return -1;
        }

        internal static Color Blend(Color A, Color B, float Amount) =>
            Color.FromArgb((int)(A.R + (B.R - A.R) * Amount), (int)(A.G + (B.G - A.G) * Amount), (int)(A.B + (B.B - A.B) * Amount));

        private static GraphicsPath Rounded(RectangleF R, float Radius)
        {
            var Path = new GraphicsPath();
            float D = Radius * 2;

            Path.AddArc(R.X, R.Y, D, D, 180, 90);
            Path.AddArc(R.Right - D, R.Y, D, D, 270, 90);
            Path.AddArc(R.Right - D, R.Bottom - D, D, D, 0, 90);
            Path.AddArc(R.X, R.Bottom - D, D, D, 90, 90);
            Path.CloseFigure();

            return Path;
        }

        private static string Initials(string Title)
        {
            if (string.IsNullOrWhiteSpace(Title)) return "?";

            var AllWords = Title.Split(new[] { ' ', ',', '-', '_', ':' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => char.IsLetterOrDigit(w[0]))
                                .ToList();
            var Words = AllWords.Where(w => !string.Equals(w, "Rank", StringComparison.OrdinalIgnoreCase) && !string.Equals(w, "and", StringComparison.OrdinalIgnoreCase)).ToList();

            if (Words.Count == 0) Words = AllWords;
            if (Words.Count == 0) return "?";
            if (Words.Count == 1) return Words[0].Length <= 3 ? Words[0].ToUpperInvariant() : Words[0].Substring(0, 2).ToUpperInvariant();

            return string.Concat(Words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
        }

        private static Color ColorFor(string Text)
        {
            int Hash = 17;

            foreach (char C in Text ?? string.Empty) Hash = unchecked(Hash * 31 + C);

            Color[] Palette = { Color.FromArgb(91, 110, 225), Color.FromArgb(0, 150, 136), Color.FromArgb(214, 93, 177), Color.FromArgb(230, 126, 34), Color.FromArgb(46, 134, 193), Color.FromArgb(142, 68, 173), Color.FromArgb(39, 174, 96), Color.FromArgb(192, 57, 43) };

            return Palette[Math.Abs(Hash % Palette.Length)];
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureFonts();

            Graphics G = e.Graphics;
            Modern.Prepare(G);

            Color Back = BackColor;

            G.Clear(Back);

            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle R = ItemBounds(i);

                if (R.Bottom < 0 || R.Top > ClientSize.Height) continue;

                var Item = Items[i];
                bool Active = HasActive && Item.Key == ActiveKey;
                Color Card = Active ? Modern.Selected : (i == HoverIndex ? Modern.Hover : Back);

                Modern.Box(G, R, Card, Active ? Modern.FieldBorder : (Color?)null);

                if (Active)
                    using (var Bar = new SolidBrush(Accent))
                        G.FillRectangle(Bar, R.X, R.Y, (int)(3 * S), R.Height);

                int IconSize = R.Height - (int)(14 * S);
                Rectangle IconR = new Rectangle(R.X + (int)(10 * S), R.Y + (R.Height - IconSize) / 2, IconSize, IconSize);

                DrawIcon(G, Item, IconR);

                int TextX = IconR.Right + (int)(10 * S);
                var TitleR = new Rectangle(TextX, R.Y + (int)(7 * S), R.Right - TextX - (int)(8 * S), (int)(20 * S));
                var CountR = new Rectangle(TextX, TitleR.Bottom, TitleR.Width, (int)(17 * S));

                TextRenderer.DrawText(G, Item.Title, TitleFont, TitleR, Active ? Modern.Text : Modern.Text2, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                string CountText = Item.Count == 1 ? "1 account" : $"{Item.Count} accounts";
                TextRenderer.DrawText(G, CountText, CountFont, CountR, Modern.Sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // banned / not logged in: small marks at the right end of the second line (the totals are in the header, so not on "All accounts")
                int BadgeLeft = CountR.Right;

                if (Item.Key != null)
                {
                    Font BF = Modern.Font(7.5f, FontStyle.Regular, true);

                    void Badge(int Count, Color Fore, Color Fill, Color Border)
                    {
                        if (Count <= 0) return;

                        string T = Count.ToString();
                        int BW = Math.Max((int)(16 * S), TextRenderer.MeasureText(T, BF, Size.Empty, TextFormatFlags.NoPadding).Width + (int)(8 * S));
                        var BR = new Rectangle(BadgeLeft - BW, CountR.Y + (CountR.Height - (int)(14 * S)) / 2, BW, (int)(14 * S));

                        Modern.Box(G, BR, Fill, Border);
                        TextRenderer.DrawText(G, T, BF, BR, Fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        BadgeLeft = BR.Left - (int)(4 * S);
                    }

                    Badge(Item.Banned, Modern.Danger, Modern.DangerBg, Modern.DangerLine);
                    Badge(Item.NeedLogin, Modern.Warn, Color.FromArgb(24, 18, 4), Color.FromArgb(80, 60, 15));
                }

                if (Item.Key != null)
                {
                    int CW = TextRenderer.MeasureText(CountText, CountFont, Size.Empty, TextFormatFlags.NoPadding).Width + (int)(12 * S);

                    if (BadgeLeft - (CountR.X + CW) > (int)(52 * S))
                        Modern.VariantTag(G, GroupImages.Resolve(Item.Key).Variant, CountR.X + CW, CountR.Y + (CountR.Height - (int)(13 * S)) / 2, (int)(13 * S));
                }
            }
        }

        private void DrawIcon(Graphics G, GroupSidebarItem Item, Rectangle R)
        {
            Image Picture = null;
            GroupPetInfo Info = null;

            if (Item.Key != null)
                Picture = GroupImages.GetImage(Item.Key, out Info);

            Modern.Box(G, R, Modern.Field, Modern.FieldBorder);

            if (Picture != null)
            {
                var Old = G.SmoothingMode;
                G.SmoothingMode = SmoothingMode.AntiAlias;
                G.DrawImage(Picture, Rectangle.Inflate(R, -(int)(2 * S), -(int)(2 * S)));
                G.SmoothingMode = Old;

                Modern.VariantFrame(G, R, Info?.Variant);
            }
            else
            {
                string Text = Item.Key == null ? "ALL" : Initials(Item.Title);

                TextRenderer.DrawText(G, Text, InitialsFont, R, Item.Key == null ? Modern.Accent : Modern.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int Index = HitTest(e.Location);

            if (Index != HoverIndex)
            {
                HoverIndex = Index;
                Cursor = Index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            HoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus(); // so the mouse wheel scrolls the sidebar
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            int Index = HitTest(e.Location);

            if (Index < 0) return;

            if (e.Button == MouseButtons.Left)
            {
                SetActive(Items[Index].Key);
                GroupClicked?.Invoke(this, new GroupSidebarEventArgs(Items[Index], e.Location));
            }
            else if (e.Button == MouseButtons.Right)
                GroupRightClicked?.Invoke(this, new GroupSidebarEventArgs(Items[Index], e.Location));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            int Index = HitTest(e.Location);

            if (Index >= 0 && e.Button == MouseButtons.Left)
                GroupDoubleClicked?.Invoke(this, new GroupSidebarEventArgs(Items[Index], e.Location));
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TitleFont?.Dispose();
                CountFont?.Dispose();
                InitialsFont?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
