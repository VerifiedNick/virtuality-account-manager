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

        private static readonly Color Accent = Color.FromArgb(0, 150, 215);

        private float S => Program.Scale;
        private int ItemHeight => (int)(52 * S);
        private int Gap => (int)(4 * S);
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

            TitleFont = new Font("Segoe UI Semibold", 9f * S);
            CountFont = new Font("Segoe UI", 8.25f * S);
            InitialsFont = new Font("Segoe UI Semibold", 10f * S);
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
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.InterpolationMode = InterpolationMode.HighQualityBicubic;
            G.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color Back = ThemeEditor.AccountBackground;
            Color Fore = ThemeEditor.AccountForeground;

            G.Clear(Back);

            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle R = ItemBounds(i);

                if (R.Bottom < 0 || R.Top > ClientSize.Height) continue;

                var Item = Items[i];
                bool Active = HasActive && Item.Key == ActiveKey;
                Color Card = Active ? Blend(Back, Accent, 0.28f) : (i == HoverIndex ? Blend(Back, Fore, 0.12f) : Blend(Back, Fore, 0.05f));

                using (var Path = Rounded(R, 7 * S))
                using (var Brush = new SolidBrush(Card))
                    G.FillPath(Brush, Path);

                if (Active)
                    using (var Bar = new SolidBrush(Accent))
                        G.FillRectangle(Bar, R.X, R.Y + (int)(8 * S), (int)(3 * S), R.Height - (int)(16 * S));

                int IconSize = R.Height - (int)(10 * S);
                Rectangle IconR = new Rectangle(R.X + (int)(8 * S), R.Y + (R.Height - IconSize) / 2, IconSize, IconSize);

                DrawIcon(G, Item, IconR);

                int TextX = IconR.Right + (int)(8 * S);
                var TitleR = new Rectangle(TextX, R.Y + (int)(7 * S), R.Right - TextX - (int)(6 * S), (int)(20 * S));
                var CountR = new Rectangle(TextX, TitleR.Bottom, TitleR.Width, (int)(18 * S));

                TextRenderer.DrawText(G, Item.Title, TitleFont, TitleR, Fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(G, Item.Count == 1 ? "1 account" : $"{Item.Count} accounts", CountFont, CountR, Blend(Fore, Back, 0.4f), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawIcon(Graphics G, GroupSidebarItem Item, Rectangle R)
        {
            Image Picture = null;
            GroupPetInfo Info = null;

            if (Item.Key != null)
                Picture = GroupImages.GetImage(Item.Key, out Info);

            using (var Path = Rounded(R, 6 * S))
            {
                if (Picture != null)
                {
                    using (var Bg = new SolidBrush(Blend(ThemeEditor.AccountBackground, ThemeEditor.AccountForeground, 0.1f)))
                        G.FillPath(Bg, Path);

                    var Old = G.Clip;
                    G.SetClip(Path);
                    G.DrawImage(Picture, R);
                    G.Clip = Old;

                    if (Info?.Variant == EntryVariant.Rainbow)
                        using (var Ring = new LinearGradientBrush(R, Color.Red, Color.Violet, 45f))
                        {
                            Ring.InterpolationColors = new ColorBlend
                            {
                                Colors = new[] { Color.FromArgb(255, 80, 80), Color.FromArgb(255, 200, 60), Color.FromArgb(80, 220, 120), Color.FromArgb(70, 160, 255), Color.FromArgb(190, 90, 255) },
                                Positions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }
                            };

                            using (var Pen = new Pen(Ring, 2.5f * S))
                                G.DrawPath(Pen, Path);
                        }
                    else if (Info?.Variant == EntryVariant.Golden)
                        using (var Pen = new Pen(Color.FromArgb(240, 190, 50), 2f * S))
                            G.DrawPath(Pen, Path);
                }
                else
                {
                    Color C = Item.Key == null ? Accent : ColorFor(Item.Title);

                    using (var Brush = new SolidBrush(C))
                        G.FillPath(Brush, Path);

                    string Text = Item.Key == null ? "ALL" : Initials(Item.Title);

                    TextRenderer.DrawText(G, Text, InitialsFont, R, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
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
