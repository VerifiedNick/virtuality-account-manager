using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace RBX_Alt_Manager.Classes
{
    public class NBTabControl : TabControl
    {
        // https://dotnetrix.co.uk/tabcontrol.htm

        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private Container components = null;

        public NBTabControl()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                if (components != null)
                    components.Dispose();

            base.Dispose(disposing);
        }

        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() =>
            components = new System.ComponentModel.Container();
        #endregion

        #region Interop

        [StructLayout(LayoutKind.Sequential)]
        private struct NMHDR
        {
            public IntPtr HWND;
            public uint idFrom;
            public int code;
            public override String ToString()
            {
                return String.Format("Hwnd: {0}, ControlID: {1}, Code: {2}", HWND, idFrom, code);
            }
        }

        private const int TCN_FIRST = 0 - 550;
        private const int TCN_SELCHANGING = (TCN_FIRST - 2);

        private const int WM_USER = 0x400;
        private const int WM_NOTIFY = 0x4E;
        private const int WM_REFLECT = WM_USER + 0x1C00;

        #endregion

        #region BackColor Manipulation

        private Color m_Backcolor = Color.Empty;
        [Browsable(true), Description("The background color used to display text and graphics in a control.")]
        public override Color BackColor
        {
            get
            {
                if (m_Backcolor.Equals(Color.Empty))
                {
                    if (Parent == null)
                        return Control.DefaultBackColor;
                    else
                        return Parent.BackColor;
                }
                return m_Backcolor;
            }
            set
            {
                if (m_Backcolor.Equals(value)) return;
                m_Backcolor = value;
                Invalidate();

                base.OnBackColorChanged(EventArgs.Empty);
            }
        }

        public bool ShouldSerializeBackColor() => !m_Backcolor.Equals(Color.Empty);

        public override void ResetBackColor()
        {
            m_Backcolor = Color.Empty;
            Invalidate();
        }

        #endregion

        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            Invalidate();
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        private int HoverIndex = -1;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int Index = -1;
            for (int i = 0; i < TabCount; i++)
                if (GetTabRect(i).Contains(e.Location)) { Index = i; break; }

            if (Index != HoverIndex) { HoverIndex = Index; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoverIndex = -1;
            Invalidate();
        }

        /// <summary>When the tabs are chosen from outside (page navigation of the main window) the strip is hidden.</summary>
        private bool HideStrip_;
        public bool HideStrip { get => HideStrip_; set { HideStrip_ = value; PerformLayout(); Invalidate(); } }

        /// <summary>Height of the tab strip (tabs + line under them).</summary>
        private int StripHeight => HideStrip_ ? 0 : IsHandleCreated && TabCount > 0 ? GetTabRect(0).Bottom + Modern.Px(1) : 0;

        /// <summary>Pages fill the whole area below the tabs (no 3D frame, no inset).</summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                if (HideStrip_) return new Rectangle(0, 0, Width, Height);
                if (!IsHandleCreated || TabCount == 0 || Alignment != TabAlignment.Top) return base.DisplayRectangle;

                int Top = StripHeight;
                return new Rectangle(0, Top, Width, Math.Max(0, Height - Top));
            }
        }

        // Virtuality look: flat tab strip, white text on the selected tab with a purple bar under it
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics G = e.Graphics;
            G.Clear(Modern.Bg);

            if (TabCount <= 0 || HideStrip_) return;

            Modern.Prepare(G);

            int Bottom = StripHeight;
            using (var P = new Pen(Modern.Line)) G.DrawLine(P, 0, Bottom - 1, Width, Bottom - 1);

            for (int i = 0; i < TabCount; i++)
            {
                Rectangle R = GetTabRect(i);
                bool Sel = i == SelectedIndex, Hot = i == HoverIndex;

                if (Hot && !Sel)
                    using (var B = new SolidBrush(Modern.Hover)) G.FillRectangle(B, R.X, R.Y, R.Width, Bottom - 1 - R.Y);

                Color C = !TabPages[i].Enabled ? Modern.Faint : (Sel ? Modern.Text : (Hot ? Modern.Text2 : Modern.Sub));
                Modern.Text_(G, TabPages[i].Text, Modern.Font(9.25f, FontStyle.Regular, Sel), new Rectangle(R.X, R.Y, R.Width, Bottom - 1 - R.Y), C, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

                if (Sel)
                    using (var B = new SolidBrush(Modern.Accent)) G.FillRectangle(B, R.X, Bottom - Modern.Px(2) - 1, R.Width, Modern.Px(2));
            }
        }

        [Description("Occurs as a tab is being changed.")]
        public event SelectedTabPageChangeEventHandler SelectedIndexChanging;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (WM_REFLECT + WM_NOTIFY))
            {
                NMHDR hdr = (NMHDR)(Marshal.PtrToStructure(m.LParam, typeof(NMHDR)));
                if (hdr.code == TCN_SELCHANGING)
                {
                    TabPage tp = TestTab(PointToClient(Cursor.Position));
                    if (tp != null)
                    {
                        TabPageChangeEventArgs e = new TabPageChangeEventArgs(SelectedTab, tp);
                        if (SelectedIndexChanging != null)
                            SelectedIndexChanging(this, e);
                        if (e.Cancel || tp.Enabled == false)
                        {
                            m.Result = new IntPtr(1);
                            return;
                        }
                    }
                }
            }
            base.WndProc(ref m);
        }

        private TabPage TestTab(Point pt)
        {
            for (int index = 0; index <= TabCount - 1; index++)
                if (GetTabRect(index).Contains(pt.X, pt.Y))
                    return TabPages[index];

            return null;
        }
    }

    public class TabPageChangeEventArgs : EventArgs
    {
        private TabPage _Selected = null;
        private TabPage _PreSelected = null;
        public bool Cancel = false;

        public TabPage CurrentTab => _Selected;
        public TabPage NextTab => _PreSelected;

        public TabPageChangeEventArgs(TabPage CurrentTab, TabPage NextTab)
        {
            _Selected = CurrentTab;
            _PreSelected = NextTab;
        }
    }

    public delegate void SelectedTabPageChangeEventHandler(Object sender, TabPageChangeEventArgs e);
}