using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>Panel that paints itself and all children into one off-screen buffer first (WS_EX_COMPOSITED), so resizing parts of the window shows whole frames instead of parts drawn one after another.</summary>
    public class BufferedPanel : Panel
    {
        public BufferedPanel() { DoubleBuffered = true; }

        protected override CreateParams CreateParams
        {
            get
            {
                var P = base.CreateParams;
                P.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return P;
            }
        }
    }
}
