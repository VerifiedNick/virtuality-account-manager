using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    public enum AddAccountChoice { None, Browser, Cookie, UserPass, Custom }

    /// <summary>
    /// Shown when "Add Account" is clicked: every way to add an account is visible right away
    /// instead of being hidden behind the small arrow on the button.
    /// </summary>
    public class AddAccountDialog : Form
    {
        public AddAccountChoice Choice { get; private set; } = AddAccountChoice.None;

        private class Option
        {
            public AddAccountChoice Choice;
            public string Title, Text;
        }

        public AddAccountDialog(bool AllowUserPass, bool AllowCustom)
        {
            float S = Program.Scale;

            Text = "Add Account";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            BackColor = ThemeEditor.FormsBackground;
            ForeColor = ThemeEditor.FormsForeground;
            Font = new Font("Segoe UI", 9f * S);

            AccountManager.SetDarkBar(Handle);

            var Options = new List<Option>
            {
                new Option { Choice = AddAccountChoice.Browser, Title = "Log in with browser", Text = "Opens a login window. Sign in like on roblox.com." },
                new Option { Choice = AddAccountChoice.Cookie, Title = "Add with cookie", Text = "Paste one or more .ROBLOSECURITY cookies (one per line)." }
            };

            if (AllowUserPass) Options.Add(new Option { Choice = AddAccountChoice.UserPass, Title = "Several with username:password", Text = "One account per line, logs them in one after another." });
            if (AllowCustom) Options.Add(new Option { Choice = AddAccountChoice.Custom, Title = "Custom (URL + JS)", Text = "Advanced: login page with your own script." });

            int Width = (int)(400 * S), Margin = (int)(16 * S), CardHeight = (int)(66 * S), Gap = (int)(8 * S);
            int Y = Margin;

            var Header = new Label
            {
                Text = "How do you want to add the account?",
                Font = new Font("Segoe UI Semibold", 11f * S),
                AutoSize = false,
                Location = new Point(Margin, Y),
                Size = new Size(Width - Margin * 2, (int)(26 * S)),
                ForeColor = ThemeEditor.FormsForeground,
                BackColor = Color.Transparent
            };
            Controls.Add(Header);
            Y += Header.Height + Gap;

            foreach (var Opt in Options)
            {
                var Card = new OptionCard(Opt.Title, Opt.Text) { Location = new Point(Margin, Y), Size = new Size(Width - Margin * 2, CardHeight) };
                var Picked = Opt.Choice;

                Card.Click += (s, e) => { Choice = Picked; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(Card);

                Y += CardHeight + Gap;
            }

            var Note = new Label
            {
                Text = "Tip: never paste your cookie anywhere else – it logs in without password or 2FA.",
                AutoSize = false,
                Location = new Point(Margin, Y),
                Size = new Size(Width - Margin * 2, (int)(34 * S)),
                ForeColor = SidebarBlend(ThemeEditor.FormsForeground, ThemeEditor.FormsBackground, 0.35f),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.25f * S)
            };
            Controls.Add(Note);
            Y += Note.Height + Margin / 2;

            ClientSize = new Size(Width, Y);
        }

        private static Color SidebarBlend(Color A, Color B, float Amount) => GroupSidebar.Blend(A, B, Amount);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape) Close();
        }

        private class OptionCard : Control
        {
            private readonly string Title, Description;
            private bool Hover;
            private static readonly Color Accent = Color.FromArgb(0, 150, 215);

            public OptionCard(string Title, string Description)
            {
                this.Title = Title;
                this.Description = Description;

                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float S = Program.Scale;
                Graphics G = e.Graphics;

                G.SmoothingMode = SmoothingMode.AntiAlias;
                G.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                G.Clear(Parent?.BackColor ?? ThemeEditor.FormsBackground);

                Color Back = ThemeEditor.FormsBackground, Fore = ThemeEditor.FormsForeground;
                Color Card = GroupSidebar.Blend(Back, Fore, Hover ? 0.14f : 0.07f);
                RectangleF R = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                float D = 8 * S * 2;

                using (var Path = new GraphicsPath())
                {
                    Path.AddArc(R.X, R.Y, D, D, 180, 90);
                    Path.AddArc(R.Right - D, R.Y, D, D, 270, 90);
                    Path.AddArc(R.Right - D, R.Bottom - D, D, D, 0, 90);
                    Path.AddArc(R.X, R.Bottom - D, D, D, 90, 90);
                    Path.CloseFigure();

                    using (var Brush = new SolidBrush(Card)) G.FillPath(Brush, Path);
                    using (var Pen = new Pen(Hover ? Accent : GroupSidebar.Blend(Back, Fore, 0.2f), 1.2f * S)) G.DrawPath(Pen, Path);
                }

                int Pad = (int)(14 * S);

                using (var TitleFont = new Font("Segoe UI Semibold", 10f * S))
                using (var TextFont = new Font("Segoe UI", 8.5f * S))
                {
                    TextRenderer.DrawText(G, Title, TitleFont, new Rectangle(Pad, (int)(8 * S), Width - Pad * 2 - (int)(16 * S), (int)(22 * S)), Hover ? Accent : Fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(G, Description, TextFont, new Rectangle(Pad, (int)(30 * S), Width - Pad * 2 - (int)(16 * S), Height - (int)(32 * S)), GroupSidebar.Blend(Fore, Back, 0.35f), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }

                // little arrow on the right
                using (var Pen = new Pen(Hover ? Accent : GroupSidebar.Blend(Fore, Back, 0.4f), 2f * S))
                {
                    int X = Width - (int)(20 * S), Mid = Height / 2, A = (int)(5 * S);
                    G.DrawLines(Pen, new[] { new Point(X - A / 2, Mid - A), new Point(X + A / 2, Mid), new Point(X - A / 2, Mid + A) });
                }
            }
        }
    }
}
