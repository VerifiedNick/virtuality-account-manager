using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    /// <summary>
    /// Replaces the grey Windows message boxes with the Virtuality look (black, square, purple).
    /// Same calls as MessageBox.Show, so every old message in the program uses it.
    /// </summary>
    public static class ModernDialog
    {
        public static DialogResult Show(string Text) => Show(null, Text, null, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string Text, string Caption) => Show(null, Text, Caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string Text, string Caption, MessageBoxButtons Buttons) => Show(null, Text, Caption, Buttons, MessageBoxIcon.None);
        public static DialogResult Show(string Text, string Caption, MessageBoxButtons Buttons, MessageBoxIcon Icon) => Show(null, Text, Caption, Buttons, Icon);
        public static DialogResult Show(IWin32Window Owner, string Text, string Caption, MessageBoxButtons Buttons, MessageBoxIcon Icon) => Run(() => Build(Owner, Text, Caption, Buttons, Icon, null, out _));

        /// <summary>Yes / No question with an optional "don't ask again" box.</summary>
        public static bool YesNo(string Caption, string Instruction, string Text, bool OfferRemember, out bool Remember)
        {
            bool R = false;
            DialogResult Result = Run(() => Build(null, string.IsNullOrEmpty(Text) ? Instruction : $"{Instruction}\n\n{Text}", Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, OfferRemember ? "Don't ask again and remember my choice" : null, out R));
            Remember = R;
            return Result == DialogResult.Yes;
        }

        /// <summary>Text input. Returns "/UC" when cancelled (what the old prompt returned).</summary>
        public static string Prompt(string Text, string Caption, string Default = "", bool Big = false)
        {
            string Value = "/UC";
            Run(() => BuildPrompt(Text, Caption, Default, Big, out Value));
            return Value;
        }

        #region Threading

        private static DialogResult Run(Func<DialogResult> Show)
        {
            var Main = AccountManager.Instance;

            try
            {
                if (Main != null && Main.IsHandleCreated && !Main.IsDisposed && Main.InvokeRequired)
                    return (DialogResult)Main.Invoke(Show);

                if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                    return Show();

                // background thread without a window: run the dialog on its own STA thread
                DialogResult Result = DialogResult.None;
                var T = new Thread(() => Result = Show());
                T.SetApartmentState(ApartmentState.STA);
                T.Start();
                T.Join();
                return Result;
            }
            catch (Exception x)
            {
                Program.Logger?.Warn($"[Dialog] {x.Message}");
                return DialogResult.Cancel;
            }
        }

        #endregion

        #region Building

        private class DialogForm : Form
        {
            public Color Accent = Modern.Accent;
            private Point DragStart;

            public DialogForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                BackColor = Modern.Panel;
                ForeColor = Modern.Text;
                Font = Modern.Font(9f);
                ShowInTaskbar = false;
                KeyPreview = true;
                DoubleBuffered = true;
                StartPosition = FormStartPosition.Manual;
                if (AppIcon.Value != null) Icon = AppIcon.Value;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var P = base.CreateParams;
                    P.ClassStyle |= 0x20000; // CS_DROPSHADOW
                    return P;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var P = new Pen(Modern.FieldBorder)) e.Graphics.DrawRectangle(P, 0, 0, Width - 1, Height - 1);
                using (var B = new SolidBrush(Accent)) e.Graphics.FillRectangle(B, 0, 0, Width, Modern.Px(3));
            }

            protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); DragStart = e.Location; }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (e.Button == MouseButtons.Left) Location = new Point(Location.X + e.X - DragStart.X, Location.Y + e.Y - DragStart.Y);
            }
        }

        private static string CaptionOf(string Caption, MessageBoxIcon Icon)
        {
            if (string.IsNullOrWhiteSpace(Caption) || Caption.Trim() == "Roblox Account Manager")
                switch (Icon)
                {
                    case MessageBoxIcon.Error: return "Something went wrong";
                    case MessageBoxIcon.Warning: return "Heads up";
                    case MessageBoxIcon.Question: return "Are you sure?";
                    default: return "Virtuality Manager";
                }

            return Caption.Trim();
        }

        private static (Color Color, string Glyph) Badge(MessageBoxIcon Icon)
        {
            switch (Icon)
            {
                case MessageBoxIcon.Error: return (Modern.Danger, "!"); // Hand / Stop share the value
                case MessageBoxIcon.Warning: return (Modern.Warn, "!");   // Exclamation
                case MessageBoxIcon.Question: return (Modern.AccentText, "?");
                case MessageBoxIcon.Information: return (Modern.AccentText, "i"); // Asterisk
                default: return (Modern.AccentText, "i");
            }
        }

        private static DialogResult Build(IWin32Window Owner, string Text, string Caption, MessageBoxButtons Buttons, MessageBoxIcon Icon, string RememberText, out bool Remember)
        {
            Text = (Text ?? string.Empty).Replace("Roblox Account Manager", "Virtuality Manager").TrimEnd();

            int G = Modern.Gutter + Modern.Px(4), W = Modern.Px(460), BadgeSz = Modern.Px(30), TextX = G + BadgeSz + Modern.Px(14), TextW = W - TextX - G;

            var F = new DialogForm() { Text = CaptionOf(Caption, Icon) };
            var (BadgeColor, Glyph) = Badge(Icon);
            if (Icon == MessageBoxIcon.Error) F.Accent = Modern.Danger;
            else if (Icon == MessageBoxIcon.Warning) F.Accent = Modern.Warn;

            var Title = new ModernLabel() { Text = F.Text, Points = 11.5f, Bold = true, Color = Modern.Text };
            Title.SetBounds(TextX, G, TextW, Modern.Px(24));

            var BadgeBox = new Panel() { Bounds = new Rectangle(G, G - Modern.Px(2), BadgeSz, BadgeSz), BackColor = Modern.Panel };
            BadgeBox.Paint += (s, e) =>
            {
                Modern.Prepare(e.Graphics);
                Rectangle R = new Rectangle(0, 0, BadgeSz, BadgeSz);
                Modern.Box(e.Graphics, R, Color.FromArgb(28, BadgeColor), Color.FromArgb(110, BadgeColor));
                Modern.Text_(e.Graphics, Glyph, Modern.Font(12f, FontStyle.Regular, true), R, BadgeColor, TextFormatFlags.HorizontalCenter);
            };

            F.Controls.Add(BadgeBox);
            F.Controls.Add(Title);

            // message: wraps; very long ones (errors with details) scroll
            Font MF = Modern.Font(9.5f);
            Size Need = TextRenderer.MeasureText(Text, MF, new Size(TextW, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
            int MaxH = Modern.Px(320), Y = Title.Bottom + Modern.Px(8);

            if (Need.Height > MaxH || Text.Contains("   at "))
            {
                var Box = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Modern.Field, ForeColor = Modern.Text2, Font = Modern.Font(9f), Text = Text.Replace("\r\n", "\n").Replace("\n", "\r\n"), TabStop = false };
                var Frame = new LinePanel() { BackColor = Modern.Field, FillBorderColor = Modern.FieldBorder, Padding = new Padding(Modern.Px(8)) };
                int H = Math.Min(MaxH, Need.Height + Modern.Px(24));
                Frame.SetBounds(TextX, Y, TextW, H);
                Box.Dock = DockStyle.Fill;
                Frame.Controls.Add(Box);
                F.Controls.Add(Frame);
                Modern.DarkScrollbars(Box);
                Y = Frame.Bottom;
            }
            else
            {
                var Msg = new Label() { Text = Text, Font = MF, ForeColor = Modern.Text2, BackColor = Color.Transparent, UseMnemonic = false, AutoSize = false };
                Msg.SetBounds(TextX, Y, TextW, Need.Height + Modern.Px(2));
                F.Controls.Add(Msg);
                Y = Msg.Bottom;
            }

            ModernToggle RememberToggle = null;

            if (RememberText != null)
            {
                Y += Modern.Px(14);
                RememberToggle = new ModernToggle();
                RememberToggle.SetBounds(TextX, Y, Modern.Px(40), Modern.Px(22));
                var RL = new ModernLabel() { Text = RememberText, Color = Modern.Text2, Points = 9f };
                RL.SetBounds(RememberToggle.Right + Modern.Px(10), Y, TextW - Modern.Px(50), Modern.Px(22));
                RL.Click += (s, e) => RememberToggle.Checked = !RememberToggle.Checked;
                F.Controls.Add(RememberToggle);
                F.Controls.Add(RL);
                Y = RememberToggle.Bottom;
            }

            // buttons, right aligned; the first one is the main action
            var Defs = new List<(string Text, DialogResult Result)>();

            switch (Buttons)
            {
                case MessageBoxButtons.OKCancel: Defs.Add(("OK", DialogResult.OK)); Defs.Add(("Cancel", DialogResult.Cancel)); break;
                case MessageBoxButtons.YesNo: Defs.Add(("Yes", DialogResult.Yes)); Defs.Add(("No", DialogResult.No)); break;
                case MessageBoxButtons.YesNoCancel: Defs.Add(("Yes", DialogResult.Yes)); Defs.Add(("No", DialogResult.No)); Defs.Add(("Cancel", DialogResult.Cancel)); break;
                case MessageBoxButtons.RetryCancel: Defs.Add(("Retry", DialogResult.Retry)); Defs.Add(("Cancel", DialogResult.Cancel)); break;
                case MessageBoxButtons.AbortRetryIgnore: Defs.Add(("Retry", DialogResult.Retry)); Defs.Add(("Ignore", DialogResult.Ignore)); Defs.Add(("Abort", DialogResult.Abort)); break;
                default: Defs.Add(("OK", DialogResult.OK)); break;
            }

            Y += Modern.Px(22);
            int BH = Modern.Px(34), BW = Modern.Px(96), X = W - G;
            DialogResult Escape = Defs.Count == 1 ? Defs[0].Result : Defs[Defs.Count - 1].Result;
            DialogResult Enter = Defs[0].Result;

            for (int i = Defs.Count - 1; i >= 0; i--)
            {
                var (BText, BResult) = Defs[i];
                var B = new ModernButton() { Text = BText, Style = i == 0 ? ModernButtonStyle.Primary : ModernButtonStyle.Secondary };
                X -= BW;
                B.SetBounds(X, Y, BW, BH);
                B.Click += (s, e) => { F.DialogResult = BResult; F.Close(); };
                F.Controls.Add(B);
                X -= Modern.Px(8);
            }

            // errors can be copied (helps when something has to be looked at later)
            if (Icon == MessageBoxIcon.Error || Icon == MessageBoxIcon.Warning)
            {
                var Copy = new ModernButton() { Text = "Copy text", Style = ModernButtonStyle.Ghost, Bold = false };
                Copy.SetBounds(G - Modern.Px(8), Y, Modern.Px(96), BH);
                Copy.Click += (s, e) => { try { Clipboard.SetText($"{F.Text}\n{Text}"); Copy.Text = "Copied"; } catch { } };
                F.Controls.Add(Copy);
            }

            F.ClientSize = new Size(W, Y + BH + G);

            F.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { F.DialogResult = Escape; F.Close(); }
                else if (e.KeyCode == Keys.Enter) { F.DialogResult = Enter; F.Close(); }
                else if (e.Control && e.KeyCode == Keys.C) { try { Clipboard.SetText($"{F.Text}\n{Text}"); } catch { } }
            };

            Place(F, Owner);

            DialogResult Result = ShowModal(F, Owner);
            if (Result == DialogResult.None || Result == DialogResult.Cancel && !Defs.Exists(d => d.Result == DialogResult.Cancel)) Result = Escape;

            Remember = RememberToggle?.Checked ?? false;
            F.Dispose();

            return Result;
        }

        private static DialogResult BuildPrompt(string Text, string Caption, string Default, bool Big, out string Value)
        {
            int G = Modern.Gutter + Modern.Px(4), W = Modern.Px(Big ? 520 : 420);
            var F = new DialogForm() { Text = string.IsNullOrWhiteSpace(Caption) ? "Virtuality Manager" : Caption };

            var Title = new ModernLabel() { Text = F.Text, Points = 11.5f, Bold = true };
            Title.SetBounds(G, G, W - G * 2, Modern.Px(24));
            F.Controls.Add(Title);

            int Y = Title.Bottom + Modern.Px(6);

            if (!string.IsNullOrEmpty(Text))
            {
                Font MF = Modern.Font(9.5f);
                Size Need = TextRenderer.MeasureText(Text, MF, new Size(W - G * 2, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                var Msg = new Label() { Text = Text, Font = MF, ForeColor = Modern.Text2, BackColor = Color.Transparent, UseMnemonic = false };
                Msg.SetBounds(G, Y, W - G * 2, Need.Height + Modern.Px(2));
                F.Controls.Add(Msg);
                Y = Msg.Bottom;
            }

            Y += Modern.Px(12);

            TextBox Input;

            if (Big)
            {
                Input = new TextBox() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, BorderStyle = BorderStyle.None, BackColor = Modern.Field, ForeColor = Modern.Text, Font = Modern.Font(9.25f), Text = Default ?? string.Empty, Dock = DockStyle.Fill };
                var Frame = new LinePanel() { BackColor = Modern.Field, FillBorderColor = Modern.FieldBorder, Padding = new Padding(Modern.Px(8)) };
                Frame.SetBounds(G, Y, W - G * 2, Modern.Px(300));
                Frame.Controls.Add(Input);
                F.Controls.Add(Frame);
                Modern.DarkScrollbars(Input);
                Y = Frame.Bottom;
            }
            else
            {
                Input = new TextBox() { BorderStyle = BorderStyle.None, BackColor = Modern.Field, ForeColor = Modern.Text, Font = Modern.Font(9.75f), Text = Default ?? string.Empty };
                var Field = new FieldBox();
                Field.SetBounds(G, Y, W - G * 2, Modern.RowH);
                Field.AddPart(Input, -1);
                F.Controls.Add(Field);
                Y = Field.Bottom;
            }

            Y += Modern.Px(20);
            int BH = Modern.Px(34), BW = Modern.Px(96);

            var Cancel = new ModernButton() { Text = "Cancel" };
            Cancel.SetBounds(W - G - BW, Y, BW, BH);
            Cancel.Click += (s, e) => { F.DialogResult = DialogResult.Cancel; F.Close(); };

            var Ok = new ModernButton() { Text = "OK", Style = ModernButtonStyle.Primary };
            Ok.SetBounds(Cancel.Left - Modern.Px(8) - BW, Y, BW, BH);
            Ok.Click += (s, e) => { F.DialogResult = DialogResult.OK; F.Close(); };

            F.Controls.Add(Ok);
            F.Controls.Add(Cancel);
            F.ClientSize = new Size(W, Y + BH + G);

            F.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { F.DialogResult = DialogResult.Cancel; F.Close(); }
                else if (e.KeyCode == Keys.Enter && (!Big || e.Control)) { e.SuppressKeyPress = true; F.DialogResult = DialogResult.OK; F.Close(); }
            };

            F.Shown += (s, e) => { Input.Focus(); Input.SelectAll(); };

            Place(F, null);

            DialogResult Result = ShowModal(F, null);
            Value = Result == DialogResult.OK ? Input.Text : "/UC";
            F.Dispose();

            return Result;
        }

        private static void Place(Form F, IWin32Window Owner)
        {
            Form Anchor = Owner as Form;

            if (Anchor == null || !Anchor.Visible)
            {
                Anchor = AccountManager.Instance;
                if (Anchor != null && (!Anchor.Visible || Anchor.WindowState == FormWindowState.Minimized || Anchor.IsDisposed)) Anchor = null;
            }

            Rectangle Area = Anchor != null ? Screen.FromControl(Anchor).WorkingArea : Screen.PrimaryScreen.WorkingArea;
            Rectangle Around = Anchor?.Bounds ?? Area;

            int X = Around.Left + (Around.Width - F.Width) / 2, Y = Around.Top + (Around.Height - F.Height) / 2;
            F.Location = new Point(Math.Max(Area.Left, Math.Min(X, Area.Right - F.Width)), Math.Max(Area.Top, Math.Min(Y, Area.Bottom - F.Height)));
        }

        private static DialogResult ShowModal(Form F, IWin32Window Owner)
        {
            IWin32Window Parent = Owner;

            if (Parent == null && AccountManager.Instance is Form Main && Main.Visible && !Main.IsDisposed && !Main.InvokeRequired)
                Parent = Main;

            if (Parent == null) { F.ShowInTaskbar = true; F.TopMost = true; }

            return Parent != null ? F.ShowDialog(Parent) : F.ShowDialog();
        }

        #endregion
    }
}
