using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Look of the main window: almost black, square corners, grey text, the green accent only for the main action and the selection.
    /// Fixed palette on purpose (the theme editor still styles the other windows).
    /// </summary>
    public static class Modern
    {
        public static readonly Color Bg = Color.FromArgb(0, 0, 0);
        public static readonly Color Panel = Color.FromArgb(9, 9, 9);
        public static readonly Color Line = Color.FromArgb(30, 30, 30);
        public static readonly Color Field = Color.FromArgb(17, 17, 17);
        public static readonly Color FieldBorder = Color.FromArgb(42, 42, 42);
        public static readonly Color Hover = Color.FromArgb(26, 26, 26);
        public static readonly Color Pressed = Color.FromArgb(36, 36, 36);
        public static readonly Color Selected = Color.FromArgb(20, 20, 20);
        public static readonly Color Text = Color.FromArgb(236, 236, 236);
        public static readonly Color Text2 = Color.FromArgb(190, 190, 190);
        public static readonly Color Sub = Color.FromArgb(128, 128, 128);
        public static readonly Color Faint = Color.FromArgb(80, 80, 80);
        public static readonly Color Accent = ColorTranslator.FromHtml("#A3E635");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#B5F04F");
        public static readonly Color Danger = Color.FromArgb(240, 100, 100);
        public static readonly Color DangerBg = Color.FromArgb(24, 8, 8);
        public static readonly Color DangerLine = Color.FromArgb(70, 24, 24);
        public static readonly Color Warn = Color.FromArgb(230, 180, 80);

        public static float S => Program.Scale;
        public static int Px(float Value) => (int)Math.Round(Value * S);

        private static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        /// <summary>Segoe UI in points, already multiplied with the window scale.</summary>
        public static Font Font(float Points, FontStyle Style = FontStyle.Regular, bool Semibold = false)
        {
            string Key = $"{Points}|{Style}|{Semibold}|{S}";

            if (!Fonts.TryGetValue(Key, out Font F))
                Fonts[Key] = F = new Font(Semibold ? "Segoe UI Semibold" : "Segoe UI", Points * S, Style);

            return F;
        }

        public static void Prepare(Graphics G)
        {
            G.SmoothingMode = SmoothingMode.None;
            G.InterpolationMode = InterpolationMode.HighQualityBicubic;
            G.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            G.PixelOffsetMode = PixelOffsetMode.Half;
        }

        public static void Box(Graphics G, Rectangle R, Color Fill, Color? Border = null)
        {
            using (var B = new SolidBrush(Fill))
                G.FillRectangle(B, R);

            if (Border is Color C)
                using (var P = new Pen(C))
                    G.DrawRectangle(P, R.X, R.Y, R.Width - 1, R.Height - 1);
        }

        public static void Text_(Graphics G, string Value, Font F, Rectangle R, Color C, TextFormatFlags Extra = TextFormatFlags.Left) =>
            TextRenderer.DrawText(G, Value ?? string.Empty, F, R, C, Extra | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        public static string Initials(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name)) return "?";

            Name = Name.Trim();

            return (Name.Length >= 2 ? Name.Substring(0, 2) : Name).ToUpperInvariant();
        }

        public static string Ago(DateTime When)
        {
            if (When == default) return "never";

            TimeSpan D = DateTime.Now - When;

            if (D.TotalMinutes < 1) return "just now";
            if (D.TotalMinutes < 60) return $"{(int)D.TotalMinutes} min ago";
            if (D.TotalHours < 24) return (int)D.TotalHours == 1 ? "1 hour ago" : $"{(int)D.TotalHours} hours ago";
            if (D.TotalDays < 60) return (int)D.TotalDays == 1 ? "1 day ago" : $"{(int)D.TotalDays} days ago";

            return When.ToString("dd.MM.yyyy");
        }

        /// <summary>Simple line icons, drawn so they stay sharp at every window scale.</summary>
        public static void Icon(Graphics G, IconKind Kind, Rectangle R, Color C)
        {
            if (Kind == IconKind.None) return;

            var Old = G.SmoothingMode;
            G.SmoothingMode = SmoothingMode.AntiAlias;

            float Size = Math.Min(R.Width, R.Height);
            float X = R.X + (R.Width - Size) / 2f, Y = R.Y + (R.Height - Size) / 2f, U = Size / 24f;

            PointF P(float px, float py) => new PointF(X + px * U, Y + py * U);
            RectangleF Rc(float px, float py, float w, float h) => new RectangleF(X + px * U, Y + py * U, w * U, h * U);

            using (var Pen = new Pen(C, Math.Max(1.4f, 1.9f * U)) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter })
            using (var Brush = new SolidBrush(C))
            {
                switch (Kind)
                {
                    case IconKind.Servers:
                        G.DrawRectangle(Pen, Rc(3, 4, 18, 6).X, Rc(3, 4, 18, 6).Y, 18 * U, 6 * U);
                        G.DrawRectangle(Pen, Rc(3, 14, 18, 6).X, Rc(3, 14, 18, 6).Y, 18 * U, 6 * U);
                        G.FillRectangle(Brush, Rc(6, 6.2f, 2, 2));
                        G.FillRectangle(Brush, Rc(6, 16.2f, 2, 2));
                        break;
                    case IconKind.Clock:
                        G.DrawEllipse(Pen, Rc(3, 3, 18, 18));
                        G.DrawLines(Pen, new[] { P(12, 7), P(12, 12), P(15.5f, 14) });
                        break;
                    case IconKind.Gear:
                        G.DrawEllipse(Pen, Rc(8.5f, 8.5f, 7, 7));
                        for (int i = 0; i < 8; i++)
                        {
                            double A = i * Math.PI / 4;
                            G.DrawLine(Pen, P(12 + (float)Math.Cos(A) * 6.5f, 12 + (float)Math.Sin(A) * 6.5f), P(12 + (float)Math.Cos(A) * 9.5f, 12 + (float)Math.Sin(A) * 9.5f));
                        }
                        G.DrawEllipse(Pen, Rc(5.5f, 5.5f, 13, 13));
                        break;
                    case IconKind.Grid:
                        foreach (var (gx, gy) in new[] { (3f, 3f), (13f, 3f), (3f, 13f), (13f, 13f) })
                            G.DrawRectangle(Pen, X + gx * U, Y + gy * U, 8 * U, 8 * U);
                        break;
                    case IconKind.Plus:
                        G.DrawLine(Pen, P(12, 5), P(12, 19));
                        G.DrawLine(Pen, P(5, 12), P(19, 12));
                        break;
                    case IconKind.Login:
                        G.DrawLines(Pen, new[] { P(14, 4), P(20, 4), P(20, 20), P(14, 20) });
                        G.DrawLine(Pen, P(3, 12), P(15, 12));
                        G.DrawLines(Pen, new[] { P(10, 7), P(15, 12), P(10, 17) });
                        break;
                    case IconKind.Ban:
                        G.DrawEllipse(Pen, Rc(3, 3, 18, 18));
                        G.DrawLine(Pen, P(6, 6), P(18, 18));
                        break;
                    case IconKind.Eye:
                        G.DrawBezier(Pen, P(2, 12), P(6, 5), P(18, 5), P(22, 12));
                        G.DrawBezier(Pen, P(2, 12), P(6, 19), P(18, 19), P(22, 12));
                        G.DrawEllipse(Pen, Rc(9, 9, 6, 6));
                        break;
                    case IconKind.Save:
                        G.DrawLines(Pen, new[] { P(4, 4), P(17, 4), P(20, 7), P(20, 20), P(4, 20), P(4, 4) });
                        G.DrawRectangle(Pen, X + 8 * U, Y + 4 * U, 8 * U, 5 * U);
                        G.DrawRectangle(Pen, X + 8 * U, Y + 13 * U, 8 * U, 7 * U);
                        break;
                    case IconKind.Search:
                        G.DrawEllipse(Pen, Rc(4, 4, 13, 13));
                        G.DrawLine(Pen, P(15, 15), P(20, 20));
                        break;
                    case IconKind.Shuffle:
                        G.DrawLines(Pen, new[] { P(3, 7), P(8, 7), P(16, 17), P(21, 17) });
                        G.DrawLines(Pen, new[] { P(3, 17), P(8, 17), P(16, 7), P(21, 7) });
                        G.DrawLines(Pen, new[] { P(18, 4), P(21, 7), P(18, 10) });
                        G.DrawLines(Pen, new[] { P(18, 14), P(21, 17), P(18, 20) });
                        break;
                    case IconKind.Chevron:
                        G.DrawLines(Pen, new[] { P(7, 10), P(12, 15), P(17, 10) });
                        break;
                }
            }

            G.SmoothingMode = Old;
        }
    }

    public enum IconKind { None, Servers, Clock, Gear, Grid, Plus, Login, Ban, Eye, Save, Search, Shuffle, Chevron }

    public enum ModernButtonStyle { Secondary, Primary, Outline, Danger, Ghost }

    /// <summary>Square flat button for the main window. Not a Button subclass on purpose, so the theme code leaves it alone.</summary>
    public class ModernButton : Control
    {
        private bool Hovered, Down;

        [DefaultValue(ModernButtonStyle.Secondary)] public ModernButtonStyle Style { get; set; } = ModernButtonStyle.Secondary;
        [DefaultValue(IconKind.None)] public IconKind Icon { get; set; } = IconKind.None;
        [DefaultValue(null)] public ContextMenuStrip Menu { get; set; }
        /// <summary>Only the arrow part on the right opens the menu; the rest of the button clicks normally. When false the whole button opens the menu.</summary>
        [DefaultValue(true)] public bool SplitMenu { get; set; } = true;
        public float TextPoints { get; set; } = 9.25f;
        public bool Bold { get; set; } = true;

        public ModernButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Tag = "NoScaling";
        }

        private int ArrowWidth => Menu != null ? Modern.Px(22) : 0;

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hovered = false; Down = false; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Down = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Menu != null && e.Button == MouseButtons.Left && (!SplitMenu || e.X >= Width - ArrowWidth))
            {
                Menu.Show(this, new Point(0, Height));
                return;
            }

            base.OnMouseDown(e);
            Down = true;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            if (Menu != null && (!SplitMenu || PointToClient(MousePosition).X >= Width - ArrowWidth)) return;

            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            Modern.Prepare(G);

            Color Parent_ = Parent?.BackColor ?? Modern.Bg;
            Color Fill, Border, Fore;

            switch (Style)
            {
                case ModernButtonStyle.Primary:
                    Fill = Down ? Modern.Accent : (Hovered ? Modern.AccentHover : Modern.Accent);
                    Border = Fill; Fore = Color.Black;
                    break;
                case ModernButtonStyle.Outline:
                    Fill = Down ? Modern.Pressed : (Hovered ? Modern.Hover : Parent_);
                    Border = Modern.Accent; Fore = Modern.Accent;
                    break;
                case ModernButtonStyle.Danger:
                    Fill = Down ? Modern.DangerLine : (Hovered ? Modern.DangerBg : Parent_);
                    Border = Modern.DangerLine; Fore = Modern.Danger;
                    break;
                case ModernButtonStyle.Ghost:
                    Fill = Down ? Modern.Pressed : (Hovered ? Modern.Hover : Parent_);
                    Border = Fill; Fore = Modern.Text2;
                    break;
                default:
                    Fill = Down ? Modern.Pressed : (Hovered ? Modern.Hover : Modern.Field);
                    Border = Modern.FieldBorder; Fore = Modern.Text;
                    break;
            }

            if (!Enabled)
            {
                Fill = Style == ModernButtonStyle.Primary ? Color.FromArgb(40, 48, 28) : Parent_;
                Border = Modern.Line;
                Fore = Modern.Faint;
            }

            G.Clear(Parent_);
            Modern.Box(G, ClientRectangle, Fill, Border);

            Rectangle Content = new Rectangle(0, 0, Width - ArrowWidth, Height);
            Font F = Modern.Font(TextPoints, FontStyle.Regular, Bold);

            if (Icon != IconKind.None && string.IsNullOrEmpty(Text))
            {
                int Sz = Math.Min(Modern.Px(18), Math.Min(Content.Width, Content.Height) - 4);
                Modern.Icon(G, Icon, new Rectangle(Content.X + (Content.Width - Sz) / 2, (Height - Sz) / 2, Sz, Sz), Fore);
            }
            else if (Icon != IconKind.None)
            {
                int Sz = Modern.Px(16), Gap = Modern.Px(8);
                Size T = TextRenderer.MeasureText(Text, F, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                int Total = Sz + Gap + T.Width;
                int X = Content.X + Math.Max(Modern.Px(8), (Content.Width - Total) / 2);

                Modern.Icon(G, Icon, new Rectangle(X, (Height - Sz) / 2, Sz, Sz), Fore);
                Modern.Text_(G, Text, F, new Rectangle(X + Sz + Gap, 0, Content.Right - (X + Sz + Gap) - Modern.Px(4), Height), Fore, TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
            else
                Modern.Text_(G, Text, F, Content, Fore, TextFormatFlags.HorizontalCenter);

            if (Menu != null)
            {
                using (var P = new Pen(Style == ModernButtonStyle.Primary ? Color.FromArgb(60, 0, 0, 0) : Modern.FieldBorder))
                    G.DrawLine(P, Width - ArrowWidth, Modern.Px(8), Width - ArrowWidth, Height - Modern.Px(8));

                int Sz = Modern.Px(14);
                Modern.Icon(G, IconKind.Chevron, new Rectangle(Width - ArrowWidth + (ArrowWidth - Sz) / 2, (Height - Sz) / 2, Sz, Sz), Fore);
            }
        }
    }

    /// <summary>Panel with optional 1px lines on some sides (used for the header, side bars and the list frame).</summary>
    public class LinePanel : Panel
    {
        public AnchorStyles Lines { get; set; } = AnchorStyles.None;
        public Color LineColor { get; set; } = Modern.Line;
        public Color? FillBorderColor { get; set; } // full border

        public LinePanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Tag = "NoScaling";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var P = new Pen(LineColor))
            {
                if (Lines.HasFlag(AnchorStyles.Top)) e.Graphics.DrawLine(P, 0, 0, Width, 0);
                if (Lines.HasFlag(AnchorStyles.Bottom)) e.Graphics.DrawLine(P, 0, Height - 1, Width, Height - 1);
                if (Lines.HasFlag(AnchorStyles.Left)) e.Graphics.DrawLine(P, 0, 0, 0, Height);
                if (Lines.HasFlag(AnchorStyles.Right)) e.Graphics.DrawLine(P, Width - 1, 0, Width - 1, Height);
            }

            if (FillBorderColor is Color C)
                using (var P = new Pen(C))
                    e.Graphics.DrawRectangle(P, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>A text field look: caption on the left, then the hosted controls laid out from left to right.</summary>
    public class FieldBox : Panel
    {
        public string Caption { get; set; }
        public IconKind CaptionIcon { get; set; } = IconKind.None;

        /// <summary>Hosted controls with their width in unscaled pixels (-1 = take the rest).</summary>
        public readonly List<(Control Control, int Width)> Parts = new List<(Control, int)>();

        private bool Focused_;

        public FieldBox()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Modern.Field;
            Tag = "NoScaling";
            Cursor = Cursors.IBeam;
        }

        public void AddPart(Control C, int Width)
        {
            Parts.Add((C, Width));
            Controls.Add(C);

            C.GotFocus += (s, e) => { Focused_ = true; Invalidate(); };
            C.LostFocus += (s, e) => { Focused_ = false; Invalidate(); };
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            foreach (var (C, _) in Parts)
                if (C is TextBox) { C.Focus(); break; }
        }

        private int CaptionWidth()
        {
            int W = Modern.Px(12);

            if (CaptionIcon != IconKind.None) W += Modern.Px(16 + 8);
            if (!string.IsNullOrEmpty(Caption)) W += TextRenderer.MeasureText(Caption, Modern.Font(8.5f), Size.Empty, TextFormatFlags.NoPadding).Width + Modern.Px(10);

            return W;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            int X = CaptionWidth(), Right = Width - Modern.Px(6);
            int Fixed = 0, Gap = Modern.Px(6);

            foreach (var (C, W) in Parts)
                if (W >= 0 && C.Visible) Fixed += Modern.Px(W) + Gap;

            foreach (var (C, W) in Parts)
            {
                if (!C.Visible) continue;

                int PW = W >= 0 ? Modern.Px(W) : Math.Max(Modern.Px(20), Right - X - Fixed);
                int PH = C is TextBox ? C.PreferredSize.Height : (C is Label ? C.PreferredSize.Height : Math.Min(Height - Modern.Px(8), Modern.Px(26)));

                C.SetBounds(X, (Height - PH) / 2, PW, PH);

                X += PW + Gap;
                if (W >= 0) Fixed -= PW + Gap;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            Modern.Prepare(G);

            Modern.Box(G, ClientRectangle, Modern.Field, Focused_ ? Color.FromArgb(70, 70, 70) : Modern.FieldBorder);

            int X = Modern.Px(12);

            if (CaptionIcon != IconKind.None)
            {
                int Sz = Modern.Px(16);
                Modern.Icon(G, CaptionIcon, new Rectangle(X, (Height - Sz) / 2, Sz, Sz), Modern.Sub);
                X += Sz + Modern.Px(8);
            }

            if (!string.IsNullOrEmpty(Caption))
                Modern.Text_(G, Caption, Modern.Font(8.5f), new Rectangle(X, 0, Width - X, Height), Modern.Sub, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Label that ignores the theme engine (it is not a Label subclass).</summary>
    public class ModernLabel : Control
    {
        public float Points { get; set; } = 9f;
        public bool Bold { get; set; }
        public Color Color { get; set; } = Modern.Text;
        public bool Upper { get; set; }

        public ModernLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Tag = "NoScaling";
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Modern.Bg);
            Modern.Prepare(e.Graphics);
            Modern.Text_(e.Graphics, Upper ? Text?.ToUpperInvariant() : Text, Modern.Font(Points, FontStyle.Regular, Bold), ClientRectangle, Color, TextFormatFlags.NoPadding);
        }
    }
}
