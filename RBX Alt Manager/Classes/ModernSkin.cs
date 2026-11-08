using BrightIdeasSoftware;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Restyles the older windows (server list, account control, utilities, dialogs …) in the Virtuality look:
    /// black, square, purple accent, same fonts and field heights as the main window.
    /// Safe to call again and again (theme changes call it); every hook is only added once.
    /// </summary>
    public static class ModernSkin
    {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hWnd, string App, string Id);

        private static readonly ConditionalWeakTable<Control, object> Hooked = new ConditionalWeakTable<Control, object>();

        private static bool FirstTime(Control C)
        {
            if (Hooked.TryGetValue(C, out _)) return false;
            Hooked.Add(C, null);
            return true;
        }

        public static readonly Color SelectionBack = Color.FromArgb(38, 26, 64); // purple tint, white text on it = 15:1

        /// <summary>Black title bar (Windows 10 + 11), on Windows 11 also the caption + border color.</summary>
        public static void DarkTitleBar(Form F)
        {
            void Set()
            {
                try
                {
                    int On = 1;
                    DwmSetWindowAttribute(F.Handle, 20, ref On, 4);
                    DwmSetWindowAttribute(F.Handle, 19, ref On, 4);

                    int Caption = ColorTranslator.ToWin32(Modern.Panel), Border = ColorTranslator.ToWin32(Modern.Line), Title = ColorTranslator.ToWin32(Modern.Text);
                    DwmSetWindowAttribute(F.Handle, 35, ref Caption, 4);
                    DwmSetWindowAttribute(F.Handle, 34, ref Border, 4);
                    DwmSetWindowAttribute(F.Handle, 36, ref Title, 4);
                }
                catch { }
            }

            if (F.IsHandleCreated) Set();
            if (FirstTime(F)) F.HandleCreated += (s, e) => Set();
        }

        /// <summary>Opens a window centered over the main window, always fully on the screen (the old code put it right of the main window = off screen when maximized).</summary>
        public static void PlaceNear(Form F, Form Owner)
        {
            Rectangle Area = Screen.FromControl(Owner ?? F).WorkingArea;
            Rectangle Anchor = Owner != null && Owner.WindowState != FormWindowState.Minimized ? Owner.Bounds : Area;

            int W = Math.Min(F.Width, Area.Width), H = Math.Min(F.Height, Area.Height);
            int X = Anchor.Left + (Anchor.Width - W) / 2, Y = Anchor.Top + (Anchor.Height - H) / 2;

            X = Math.Max(Area.Left, Math.Min(X, Area.Right - W));
            Y = Math.Max(Area.Top, Math.Min(Y, Area.Bottom - H));

            F.StartPosition = FormStartPosition.Manual;
            F.SetBounds(X, Y, W, H);
        }

        /// <summary>Shows a tool window (or brings it back) in a visible place.</summary>
        public static void ShowTool(Form F, Form Owner)
        {
            bool WasVisible = F.Visible;

            if (F.WindowState == FormWindowState.Minimized) F.WindowState = FormWindowState.Normal;
            if (!WasVisible) PlaceNear(F, Owner);

            F.Show();
            F.BringToFront();
            F.Activate();

            if (!WasVisible) PlaceNear(F, Owner);
        }

        public static void Apply(Control Root)
        {
            if (Root == null) return;

            try
            {
                if (Root is Form F)
                {
                    F.BackColor = Modern.Bg;
                    F.ForeColor = Modern.Text;
                    F.Font = Modern.Font(9f);
                    DarkTitleBar(F);

                    if (AppIcon.Value != null && F.FormBorderStyle != FormBorderStyle.None) F.Icon = AppIcon.Value;
                    if (F.ContextMenuStrip != null) StyleMenu(F.ContextMenuStrip);
                }

                foreach (Control C in Root.Controls)
                    Style(C);

                HookAdded(Root);
            }
            catch (Exception x) { Program.Logger?.Warn($"[Skin] {x.Message}"); }
        }

        /// <summary>Controls added later (game tiles, outfit tiles …) get the same look.</summary>
        private static readonly ConditionalWeakTable<Control, object> AddedHooked = new ConditionalWeakTable<Control, object>();

        private static void HookAdded(Control C)
        {
            if (AddedHooked.TryGetValue(C, out _)) return;
            AddedHooked.Add(C, null);
            C.ControlAdded += (s, e) => { try { Style(e.Control); } catch { } };
        }

        private static bool IsOwn(Control C) =>
            C is ModernButton || C is ModernLabel || C is FieldBox || C is ModernToggle || C is LinePanel || C is AccountGrid || C is GroupSidebar || C is AccountCardPanel;

        private static void Style(Control C)
        {
            if (C is FieldBox FB) // the old theme code walks into panels; put the field colors back
            {
                foreach (Control Part in FB.Controls)
                {
                    if (Part is TextBox || Part is NumericUpDown) { Part.BackColor = Modern.Field; Part.ForeColor = Modern.Text; }
                    if (Part is BorderedTextBox BT) BT.BorderColor = Modern.Field;
                }

                FB.Invalidate();
                return;
            }

            if (C is LinePanel && C.HasChildren) // our frames: keep their look, but style what's inside
            {
                foreach (Control Child in C.Controls) Style(Child);
                HookAdded(C);
                return;
            }

            if (C == null || IsOwn(C)) return;
            if (C.ContextMenuStrip != null) StyleMenu(C.ContextMenuStrip);

            switch (C)
            {
                case Button B:
                    B.FlatStyle = FlatStyle.Flat;
                    B.UseVisualStyleBackColor = false;
                    B.ForeColor = Modern.Text;
                    B.Font = Modern.Font(9f, FontStyle.Regular, true);
                    B.Cursor = Cursors.Hand;

                    bool Primary = "Primary".Equals(B.AccessibleDescription);
                    B.BackColor = Primary ? Modern.Accent : Modern.Field;
                    B.FlatAppearance.BorderColor = Primary ? Modern.Accent : Modern.FieldBorder;
                    B.FlatAppearance.BorderSize = 1;
                    B.FlatAppearance.MouseOverBackColor = Primary ? Modern.AccentHover : Modern.Hover;
                    B.FlatAppearance.MouseDownBackColor = Primary ? Modern.Accent : Modern.Pressed;
                    if (Primary) B.ForeColor = Color.White;
                    break;

                case CheckBox CB:
                    SkinCheck(CB);
                    break;

                case RadioButton RB:
                    RB.FlatStyle = FlatStyle.Flat;
                    RB.ForeColor = Modern.Text2;
                    RB.Font = Modern.Font(9f);
                    break;

                case NumericUpDown N:
                    N.BackColor = Modern.Field;
                    N.ForeColor = Modern.Text;
                    N.Font = Modern.Font(9.25f);
                    if (N.BorderStyle == BorderStyle.Fixed3D) N.BorderStyle = BorderStyle.FixedSingle;
                    SkinSpin(N);
                    break;

                case RichTextBox R:
                    R.BackColor = Modern.Field;
                    R.ForeColor = Modern.Text;
                    if (R is BorderedRichTextBox BR) BR.BorderColor = Modern.FieldBorder;
                    break;

                case TextBox T:
                    if (T.Parent is FieldBox) break; // already styled by whoever built the field
                    T.BackColor = Modern.Field;
                    T.ForeColor = Modern.Text;
                    T.Font = Modern.Font(9.25f);
                    if (T is BorderedTextBox BT) BT.BorderColor = Modern.FieldBorder;
                    else if (T.BorderStyle == BorderStyle.Fixed3D) T.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox Combo:
                    Combo.FlatStyle = FlatStyle.Flat;
                    Combo.BackColor = Modern.Field;
                    Combo.ForeColor = Modern.Text;
                    Combo.Font = Modern.Font(9.25f);
                    if (FirstTime(Combo))
                    {
                        if (Combo.IsHandleCreated) SetWindowTheme(Combo.Handle, "DarkMode_CFD", null);
                        Combo.HandleCreated += (s, e) => SetWindowTheme(Combo.Handle, "DarkMode_CFD", null);
                    }
                    break;

                case ListBox L:
                    L.BackColor = Modern.Field;
                    L.ForeColor = Modern.Text;
                    L.BorderStyle = BorderStyle.FixedSingle;
                    Modern.DarkScrollbars(L);
                    break;

                case LinkLabel LL:
                    LL.LinkColor = Modern.AccentText;
                    LL.ActiveLinkColor = Modern.AccentHover;
                    LL.VisitedLinkColor = Modern.AccentText;
                    LL.BackColor = Color.Transparent;
                    LL.Font = Modern.Font(9f);
                    break;

                case Label Lb:
                    Lb.BackColor = Color.Transparent;
                    Lb.ForeColor = Lb.Parent is UserControl ? Modern.Text : Modern.Text2;
                    Lb.Font = Modern.Font(Lb.Parent is UserControl ? 8.75f : 9f, FontStyle.Regular, Lb.Parent is UserControl);
                    break;

                case ObjectListView O:
                    SkinList(O);
                    break;

                case ListView LV:
                    LV.BackColor = Modern.Bg;
                    LV.ForeColor = Modern.Text;
                    LV.BorderStyle = BorderStyle.None;
                    LV.Font = Modern.Font(9.25f);
                    Modern.DarkScrollbars(LV);
                    break;

                case TabControl Tabs:
                    Tabs.BackColor = Modern.Bg;
                    Tabs.ForeColor = Modern.Text;
                    Tabs.Font = Modern.Font(9.25f, FontStyle.Regular, true);
                    Tabs.Padding = new Point(Modern.Px(14), Modern.Px(6));
                    Tabs.Invalidate();
                    break;

                case TabPage Page:
                    Page.UseVisualStyleBackColor = false;
                    Page.BackColor = Modern.Bg;
                    Page.ForeColor = Modern.Text;
                    break;

                case PictureBox P:
                    if (P.Parent is UserControl) P.BackColor = Modern.Field;
                    break;

                case FastColoredTextBoxNS.FastColoredTextBox Code:
                    Code.BackColor = Modern.Field;
                    Code.ForeColor = Modern.Text;
                    Code.IndentBackColor = Modern.Panel;
                    Code.LineNumberColor = Modern.Faint;
                    Code.ServiceLinesColor = Modern.Line;
                    Code.CaretColor = Modern.Text;
                    Code.SelectionColor = Color.FromArgb(90, Modern.Accent);
                    Code.PaddingBackColor = Modern.Field;
                    Code.CurrentLineColor = Color.Transparent;
                    break;

                case UserControl U: // game / outfit tiles
                    U.BackColor = Modern.Panel;
                    U.ForeColor = Modern.Text;
                    if (FirstTime(U))
                    {
                        U.Paint += (s, e) => { using (var Pen = new Pen(Modern.Line)) e.Graphics.DrawRectangle(Pen, 0, 0, U.Width - 1, U.Height - 1); };
                        U.MouseEnter += (s, e) => { U.BackColor = Modern.Hover; };
                        U.MouseLeave += (s, e) => { if (!U.ClientRectangle.Contains(U.PointToClient(Cursor.Position))) U.BackColor = Modern.Panel; };
                    }
                    break;

                case TrackBar TB:
                    TB.BackColor = Modern.Bg;
                    break;

                case ScrollableControl Container: // Panel, FlowLayoutPanel, TableLayoutPanel, GroupBox (via Control below)
                    if (!(Container is SplitContainer)) Container.BackColor = Modern.Bg;
                    Container.ForeColor = Modern.Text;
                    if (Container.AutoScroll) Modern.DarkScrollbars(Container);
                    break;

                case GroupBox G:
                    G.BackColor = Modern.Bg;
                    G.ForeColor = Modern.Sub;
                    G.FlatStyle = FlatStyle.Flat;
                    break;
            }

            if (C is NumericUpDown || C is ComboBox || C is ObjectListView) return;

            if (C is FastColoredTextBoxNS.FastColoredTextBox) return;

            if (C.HasChildren || C is ScrollableControl || C is TabControl || C is UserControl)
            {
                foreach (Control Child in C.Controls)
                    Style(Child);

                HookAdded(C);
            }
        }

        #region Check box

        /// <summary>Square purple check box drawn over the system one (the system one cannot be made dark).</summary>
        public static void SkinCheck(CheckBox CB)
        {
            CB.ForeColor = Modern.Text2;
            CB.Font = Modern.Font(9f);
            CB.Cursor = Cursors.Hand;

            if (CB.Appearance == Appearance.Button) // toggle buttons ("List view") look like the other buttons
            {
                CB.FlatStyle = FlatStyle.Flat;
                CB.BackColor = Modern.Field;
                CB.FlatAppearance.BorderColor = Modern.FieldBorder;
                CB.FlatAppearance.CheckedBackColor = SelectionBack;
                CB.FlatAppearance.MouseOverBackColor = Modern.Hover;
                CB.ForeColor = Modern.Text;
                return;
            }

            if (!FirstTime(CB)) { FitCheck(CB); return; }

            FitCheck(CB);

            bool Hover = false;

            CB.MouseEnter += (s, e) => { Hover = true; CB.Invalidate(); };
            CB.MouseLeave += (s, e) => { Hover = false; CB.Invalidate(); };
            CB.CheckedChanged += (s, e) => CB.Invalidate();
            CB.EnabledChanged += (s, e) => CB.Invalidate();
            CB.TextChanged += (s, e) => FitCheck(CB);

            CB.Paint += (s, e) =>
            {
                Graphics G = e.Graphics;
                G.Clear(BackOf(CB.Parent));
                Modern.Prepare(G);

                int Sz = Modern.Px(16);
                Rectangle Box = new Rectangle(0, (CB.Height - Sz) / 2, Sz, Sz);

                if (CB.Checked)
                {
                    Modern.Box(G, Box, CB.Enabled ? Modern.Accent : Modern.Pressed, CB.Enabled ? Modern.Accent : Modern.FieldBorder);
                    DrawCheck(G, Box, Color.White);
                }
                else
                    Modern.Box(G, Box, Modern.Field, Hover && CB.Enabled ? Color.FromArgb(80, 80, 80) : Modern.FieldBorder);

                Modern.Text_(G, CB.Text, Modern.Font(9f), new Rectangle(Sz + Modern.Px(8), 0, CB.Width - Sz - Modern.Px(8), CB.Height), CB.Enabled ? (CB.Checked ? Modern.Text : Modern.Text2) : Modern.Faint, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            };

            CB.Invalidate();
        }

        private static void FitCheck(CheckBox CB)
        {
            if (!CB.AutoSize && CB.Tag as string != "SkinSized") return;

            CB.AutoSize = false;
            CB.Tag = CB.Tag ?? "SkinSized";

            int TW = TextRenderer.MeasureText(CB.Text ?? string.Empty, Modern.Font(9f), Size.Empty, TextFormatFlags.NoPadding).Width;
            CB.Size = new Size(Modern.Px(16 + 8 + 4) + TW, Math.Max(Modern.Px(22), CB.Height));
        }

        public static void DrawCheck(Graphics G, Rectangle Box, Color C)
        {
            using (var P = new Pen(C, Math.Max(1.6f, Modern.Px(2))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float X = Box.X, Y = Box.Y, W = Box.Width;
                G.DrawLines(P, new[] { new PointF(X + W * 0.24f, Y + W * 0.52f), new PointF(X + W * 0.43f, Y + W * 0.70f), new PointF(X + W * 0.77f, Y + W * 0.32f) });
            }
        }

        private static Color BackOf(Control C)
        {
            while (C != null)
            {
                if (C.BackColor.A == 255) return C.BackColor;
                C = C.Parent;
            }

            return Modern.Bg;
        }

        #endregion

        #region Number field arrows

        /// <summary>The up/down arrows of a NumericUpDown are drawn by Windows in light grey; this paints them dark.</summary>
        private static void SkinSpin(NumericUpDown N)
        {
            if (N.Controls.Count == 0 || !FirstTime(N)) return;

            Control Buttons = N.Controls[0];

            Buttons.Paint += (s, e) =>
            {
                var G = e.Graphics;
                int W = Buttons.Width, H = Buttons.Height;

                using (var B = new SolidBrush(Modern.Field)) G.FillRectangle(B, 0, 0, W, H);
                using (var P = new Pen(Modern.FieldBorder)) { G.DrawLine(P, 0, 0, 0, H); G.DrawLine(P, 0, H / 2, W, H / 2); }

                G.SmoothingMode = SmoothingMode.AntiAlias;
                using (var B = new SolidBrush(N.Enabled ? Modern.Text2 : Modern.Faint))
                {
                    float CX = W / 2f + 0.5f, A = Math.Max(3f, Modern.Px(3.5f));
                    float Up = H / 4f + 1, Dn = H * 3 / 4f;
                    G.FillPolygon(B, new[] { new PointF(CX - A, Up + A / 2), new PointF(CX + A, Up + A / 2), new PointF(CX, Up - A / 2) });
                    G.FillPolygon(B, new[] { new PointF(CX - A, Dn - A / 2), new PointF(CX + A, Dn - A / 2), new PointF(CX, Dn + A / 2) });
                }
            };

            Buttons.Invalidate();
        }

        #endregion

        #region Lists

        public static void SkinList(ObjectListView O)
        {
            O.BackColor = Modern.Bg;
            O.ForeColor = Modern.Text;
            O.BorderStyle = BorderStyle.None;
            O.Font = Modern.Font(9.25f);
            O.GridLines = false;
            O.FullRowSelect = true;
            O.UseAlternatingBackColors = false;
            O.HeaderUsesThemes = false;

            if (O.RowHeight < Modern.Px(28)) O.RowHeight = Modern.Px(28);

            // a column that fills the rest, so no empty (white) header piece is left on the right
            if (O.Columns.Count > 0 && !O.Columns.Cast<ColumnHeader>().OfType<OLVColumn>().Any(c => c.FillsFreeSpace))
                ((O.Columns[0] as OLVColumn) ?? O.AllColumns[0]).FillsFreeSpace = true;

            O.SelectedBackColor = SelectionBack;
            O.SelectedForeColor = Color.White;
            O.UnfocusedSelectedBackColor = SelectionBack;
            O.UnfocusedSelectedForeColor = Color.White;
            O.UseHotItem = false;

            var Header = new HeaderFormatStyle();
            Header.SetBackColor(Modern.Panel);
            Header.SetForeColor(Modern.Sub);
            Header.SetFont(Modern.Font(8f, FontStyle.Regular, true));
            Header.Normal.FrameColor = Modern.Line;
            Header.Normal.FrameWidth = 1;
            Header.Hot.BackColor = Modern.Hover;
            Header.Hot.ForeColor = Modern.Text;
            O.HeaderFormatStyle = Header;

            if (O.EmptyListMsgOverlay is TextOverlay Empty)
            {
                Empty.TextColor = Modern.Sub;
                Empty.BackColor = Modern.Bg;
                Empty.BorderColor = Modern.Bg;
                Empty.BorderWidth = 0;
                Empty.Font = Modern.Font(9.5f);
            }

            Modern.DarkScrollbars(O);
        }

        #endregion

        #region Menus

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

        private static readonly ToolStripProfessionalRenderer MenuRenderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false };

        public static void StyleMenu(ToolStrip Strip)
        {
            if (Strip == null) return;

            Strip.Renderer = MenuRenderer;
            Strip.BackColor = Modern.Panel;
            Strip.ForeColor = Modern.Text;
            Strip.Font = Modern.Font(9f);

            foreach (ToolStripItem Item in Strip.Items)
            {
                Item.ForeColor = Modern.Text;

                if (Item is ToolStripMenuItem Menu && Menu.HasDropDownItems)
                    StyleMenu(Menu.DropDown);
            }
        }

        #endregion

        #region Layout helpers (one grid for every window)

        /// <summary>Height of every input / button row in the tool windows.</summary>
        public static int Row => Modern.Px(34);
        public static int Gap => Modern.Px(8);

        /// <summary>
        /// Lays out a tool bar: controls from the left, controls from the right, all the same height, gap 8, starting at the gutter.
        /// Width &lt; 0 = take the rest, 0 = keep the control's width.
        /// </summary>
        public static void ToolRow(Control Parent, int Y, (Control C, int W)[] Left, (Control C, int W)[] Right = null)
        {
            int G = Modern.Gutter, X = G, R = Parent.ClientSize.Width - G, H = Row;

            if (Right != null)
                for (int i = Right.Length - 1; i >= 0; i--)
                {
                    var (C, W) = Right[i];
                    if (C == null) continue;

                    int CW = W > 0 ? Modern.Px(W) : C.Width;
                    R -= CW;
                    Place(C, R, Y, CW, H);
                    R -= Gap;
                }

            Control Fill = null;

            foreach (var (C, W) in Left)
            {
                if (C == null) continue;
                if (W < 0) { Fill = C; break; }

                int CW = W > 0 ? Modern.Px(W) : C.Width;
                Place(C, X, Y, CW, H);
                X += CW + Gap;
            }

            if (Fill != null)
            {
                // everything after the filler is placed right before the right block
                int Index = Array.FindIndex(Left, p => p.C == Fill), RX = R + Gap;

                for (int i = Left.Length - 1; i > Index; i--)
                {
                    var (C, W) = Left[i];
                    if (C == null) continue;

                    int CW = W > 0 ? Modern.Px(W) : C.Width;
                    RX -= Gap + CW;
                    Place(C, RX, Y, CW, H);
                }

                Place(Fill, X, Y, Math.Max(Modern.Px(60), RX - Gap - X), H);
            }
        }

        private static void Place(Control C, int X, int Y, int W, int H)
        {
            // labels and check boxes keep their own height and sit on the row's center line
            if (C is Label || C is CheckBox CB && CB.Appearance != Appearance.Button)
            {
                int CH = C is CheckBox ? Math.Max(Modern.Px(22), C.Height) : C.PreferredSize.Height;
                C.SetBounds(X, Y + (H - CH) / 2, W, CH);
            }
            else
                C.SetBounds(X, Y, W, H);
        }

        /// <summary>Wraps an existing text box / number box into the same field look as the main window (caption inside on the left).</summary>
        public static FieldBox Field(Control Input, string Caption = null, string Cue = null)
        {
            var Box = new FieldBox() { Caption = Caption };
            Input.Parent?.Controls.Add(Box);

            if (Input is TextBox T)
            {
                T.BorderStyle = BorderStyle.None;
                T.BackColor = Modern.Field;
                T.ForeColor = Modern.Text;
                T.Font = Modern.Font(9.25f);
                T.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                if (T is BorderedTextBox BT) BT.BorderColor = Modern.Field;
                if (Cue != null) SetCue(T, Cue);
            }
            else if (Input is NumericUpDown N)
            {
                N.BorderStyle = BorderStyle.None;
                N.BackColor = Modern.Field;
                N.ForeColor = Modern.Text;
                N.Font = Modern.Font(9.25f);
                N.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                SkinSpin(N);
            }

            Box.AddPart(Input, -1);
            return Box;
        }

        /// <summary>Puts a list / tile panel into a 1px frame (re-parents it, keeps it docked).</summary>
        public static LinePanel Frame(params Control[] Content)
        {
            var F = new LinePanel() { BackColor = Modern.Bg, FillBorderColor = Modern.Line, Padding = new Padding(1) };
            Content[0].Parent?.Controls.Add(F);

            foreach (var C in Content)
            {
                C.Parent?.Controls.Remove(C);
                C.Dock = DockStyle.Fill;
                if (C is ScrollableControl SC) { SC.Margin = Padding.Empty; SC.Padding = new Padding(Modern.Px(8)); }
                F.Controls.Add(C);
            }

            return F;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, string lParam);

        public static void SetCue(TextBox T, string Cue)
        {
            if (T.IsHandleCreated) SendMessage(T.Handle, 0x1501, (IntPtr)1, Cue);
            T.HandleCreated += (s, e) => SendMessage(T.Handle, 0x1501, (IntPtr)1, Cue);
        }

        #endregion
    }

    /// <summary>On/off switch for the settings window.</summary>
    [DefaultEvent("CheckedChanged")]
    public class ModernToggle : Control
    {
        private bool Checked_, Hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => Checked_;
            set { if (Checked_ == value) return; Checked_ = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public ModernToggle()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Tag = "NoScaling";
            Size = new Size(Modern.Px(40), Modern.Px(22));
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled) Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var G = e.Graphics;
            G.Clear(Parent?.BackColor ?? Modern.Bg);
            Modern.Prepare(G);

            Rectangle Track = new Rectangle(0, (Height - Modern.Px(22)) / 2, Modern.Px(40), Modern.Px(22));
            Color Fill = !Enabled ? Modern.Pressed : (Checked_ ? (Hover ? Modern.AccentHover : Modern.Accent) : (Hover ? Modern.Pressed : Modern.Field));
            Modern.Box(G, Track, Fill, Checked_ && Enabled ? Fill : Modern.FieldBorder);

            int K = Track.Height - Modern.Px(8);
            Rectangle Knob = new Rectangle(Checked_ ? Track.Right - Modern.Px(4) - K : Track.X + Modern.Px(4), Track.Y + Modern.Px(4), K, K);

            using (var B = new SolidBrush(!Enabled ? Modern.Faint : (Checked_ ? Color.White : Modern.Sub)))
                G.FillRectangle(B, Knob);
        }
    }
}
