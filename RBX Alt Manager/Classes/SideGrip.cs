using System;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>Thin drag handle on the right edge of the side bar (like Discord): drag to make it wider or narrower, double click to collapse / expand.</summary>
    public class SideGrip : Control
    {
        private bool Hot, Dragging;
        private int StartScreenX, StartWidth, Target;

        /// <summary>While dragging: the width the mouse points at (the owner only shows a preview line, nothing is relaid out).</summary>
        public Action<int> WidthRequested = _ => { };
        /// <summary>Mouse released: the final width. The owner applies it once.</summary>
        public Action<int> DragFinished = _ => { };
        public Action Toggled = () => { };
        public Func<int> CurrentWidth = () => 0;

        public SideGrip()
        {
            Cursor = Cursors.SizeWE;
            Tag = "NoScaling";
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);

            if (!Hot && !Dragging) return;

            // thin accent line while hovering or dragging, so it is clear the edge can be grabbed
            using (var B = new SolidBrush(Modern.Accent))
                e.Graphics.FillRectangle(B, Width - Math.Max(2, Modern.Px(2)), 0, Math.Max(2, Modern.Px(2)), Height);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hot = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left) return;

            Dragging = true;
            StartScreenX = Cursor.Position.X;
            StartWidth = CurrentWidth();
            Target = StartWidth;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!Dragging) return;

            int W = StartWidth + Cursor.Position.X - StartScreenX;
            if (W == Target) return;

            Target = W;
            WidthRequested(W);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!Dragging) return;

            Dragging = false;
            Capture = false;
            Invalidate();
            DragFinished(Target);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            Toggled();
        }
    }
}
