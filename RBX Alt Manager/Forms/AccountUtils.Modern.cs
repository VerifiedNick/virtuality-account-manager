using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    // Account tools as a page of the main window: one row per task, all on the same grid.
    public partial class AccountUtils
    {
        private bool ModernBuilt;
        private ModernLabel ForLabel;
        private readonly List<(ModernLabel Caption, Control[] Row, int[] Widths)> Rows = new List<(ModernLabel, Control[], int[])>();

        public void UseModernLayout()
        {
            if (ModernBuilt) return;
            ModernBuilt = true;

            SuspendLayout();

            label1.Visible = label2.Visible = label3.Visible = label4.Visible = false;

            ForLabel = new ModernLabel() { Points = 9.5f, Color = Modern.Sub };
            Controls.Add(ForLabel);

            textBox3.Text = string.Empty;
            textBox5.Text = string.Empty;
            textBox1.UseSystemPasswordChar = true;
            textBox2.UseSystemPasswordChar = true;
            textBox5.UseSystemPasswordChar = true;

            var Current = ModernSkin.Field(textBox1, "Current", "required for password + e-mail");
            var New = ModernSkin.Field(textBox2, "New");
            var Email = ModernSkin.Field(textBox3, null, "New e-mail address");
            var Display = ModernSkin.Field(DisplayName, null, "New display name");
            var Target = ModernSkin.Field(Username, null, "Username of another player");
            var Pin = ModernSkin.Field(textBox5, "PIN");

            button1.Text = "Change password";
            button3.Text = "Change e-mail";
            SetDisplayName.Text = "Set display name";
            AddFriend.Text = "Send friend request";
            Block.Text = "Block / unblock";
            button7.Text = "Unlock";
            button2.Text = "Sign out of all other sessions";

            WhoFollow.DropDownStyle = ComboBoxStyle.DropDownList;
            WhoFollow.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            foreach (Control C in new Control[] { button1, button2, button3, button7, Block, SetDisplayName, AddFriend, WhoFollow })
                C.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            ModernLabel Cap(string T)
            {
                var L = new ModernLabel() { Text = T, Points = 7.75f, Bold = true, Color = Modern.Sub, Upper = true };
                Controls.Add(L);
                return L;
            }

            Rows.Add((Cap("Password"), new Control[] { Current, New, button1 }, new[] { -1, -1, 170 }));
            Rows.Add((Cap("E-mail (uses the current password above)"), new Control[] { Email, button3 }, new[] { -1, 170 }));
            Rows.Add((Cap("Display name"), new Control[] { Display, SetDisplayName }, new[] { -1, 170 }));
            Rows.Add((Cap("Other player"), new Control[] { Target, AddFriend, Block }, new[] { -1, 170, 170 }));
            Rows.Add((Cap("Who can follow · PIN lock"), new Control[] { WhoFollow, Pin, button7 }, new[] { -1, 200, 170 }));
            Rows.Add((Cap("Sessions"), new Control[] { button2 }, new[] { 260 }));

            Resize += (s, e) => LayoutModern();
            VisibleChanged += (s, e) => { if (Visible) RefreshFor(); };

            ResumeLayout(false);
            LayoutModern();
        }

        public void RefreshFor()
        {
            if (ForLabel == null) return;

            var A = AccountManager.SelectedAccount;
            ForLabel.Text = A == null ? "Select an account in the list first – every action here uses the selected account." : $"Every action here uses the selected account: {A.Username}";
            ForLabel.Color = A == null ? Modern.Warn : Modern.Sub;
            ForLabel.Invalidate();
        }

        private void LayoutModern()
        {
            if (!ModernBuilt || ClientSize.Width < Modern.Px(300)) return;

            int G = Modern.Gutter, W = Math.Min(ClientSize.Width - G * 2, Modern.Px(900)), H = ModernSkin.Row, Gap = ModernSkin.Gap;
            int Y = G;

            ForLabel.SetBounds(G, Y, W, Modern.Px(20));
            Y = ForLabel.Bottom + Modern.Px(18);

            foreach (var (Caption, Row, Widths) in Rows)
            {
                Caption.SetBounds(G, Y, W, Modern.Px(16));
                Y = Caption.Bottom + Modern.Px(6);

                int Fixed = 0, Fills = 0;
                for (int i = 0; i < Row.Length; i++)
                    if (Widths[i] > 0) Fixed += Modern.Px(Widths[i]); else Fills++;

                int FillW = Fills > 0 ? (W - Fixed - Gap * (Row.Length - 1)) / Fills : 0, X = G;

                for (int i = 0; i < Row.Length; i++)
                {
                    int CW = Widths[i] > 0 ? Modern.Px(Widths[i]) : FillW;

                    if (Row[i] is ComboBox Combo)
                        Combo.SetBounds(X, Y + (H - Combo.Height) / 2, CW, Combo.Height);
                    else
                        Row[i].SetBounds(X, Y, CW, H);

                    X += CW + Gap;
                }

                Y += H + Modern.Px(18);
            }
        }
    }
}
