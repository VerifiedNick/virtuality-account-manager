using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    // Account control (Nexus) as a clear page: accounts on the left, actions on the right,
    // one grid (gutter 16, rows 34 px, gaps 8) and readable captions instead of the old tiny labels.
    public partial class AccountControl
    {
        private bool ModernBuilt;
        private ModernButton ExecTab, AutoTab;
        private ModernLabel CommandCaption, ScriptCaption, OutputCaption, ScriptHint;
        private FieldBox PlaceBox, JobBox, CommandBox;
        private LinePanel AccountsFrame, OutputFrame;

        public void UseModernLayout()
        {
            if (ModernBuilt) return;
            ModernBuilt = true;

            SuspendLayout();
            ACTabs.HideStrip = true;
            ScriptTabs.HideStrip = true;

            ModernLabel Caption(Control Parent, string T)
            {
                var L = new ModernLabel() { Text = T, Points = 8.25f, Bold = true, Color = Modern.Sub, Upper = true };
                Parent.Controls.Add(L);
                return L;
            }

            // ---------- Control panel ----------
            var Moving = new List<Control>(ControlsPanel.Controls.Cast<Control>());
            foreach (var C in Moving) { ControlsPanel.Controls.Remove(C); C.Anchor = AnchorStyles.Top | AnchorStyles.Left; ControlPage.Controls.Add(C); }
            CPanel.Visible = false;

            label1.Visible = label2.Visible = label3.Visible = false;
            ShowScriptBox.Visible = false;
            OutputToggle.Visible = false;

            // the script area and the output were collapsible strips; now they are always shown in their own place
            foreach (var C in new Control[] { ScriptTabs }) { ScriptLayoutPanel.Controls.Remove(C); ControlPage.Controls.Add(C); C.Dock = DockStyle.None; }
            foreach (var C in new Control[] { OutputPanel, ClearOutputButton, SaveOutputToFileCheck }) { OutputFlowPanel.Controls.Remove(C); C.Dock = DockStyle.None; }
            ScriptLayoutPanel.Visible = OutputFlowPanel.Visible = false;

            AccountsFrame = ModernSkin.Frame(AccountsView);
            AccountsFrame.Parent?.Controls.Remove(AccountsFrame);
            ControlPage.Controls.Add(AccountsFrame);

            OutputFrame = new LinePanel() { BackColor = Modern.Field, FillBorderColor = Modern.FieldBorder, Padding = new Padding(Modern.Px(8)) };
            OutputPanel.Dock = DockStyle.Fill;
            OutputPanel.AutoScroll = true;
            OutputFrame.Controls.Add(OutputPanel);
            ControlPage.Controls.AddRange(new Control[] { OutputFrame, ClearOutputButton, SaveOutputToFileCheck });

            PlaceBox = ModernSkin.Field(PlaceIdText, "Place ID", "game to keep them in");
            JobBox = ModernSkin.Field(JobIdText, "Job ID", "server (optional)");
            CommandBox = ModernSkin.Field(CommandText, "Command", "e.g. teleport, rejoin …");

            AutoRelaunchCheckBox.Text = "Rejoin automatically when an account leaves the game";
            SendCommand.Text = "Send";
            SendCommand.AccessibleDescription = "Primary";
            ExecuteButton.Text = "Run on checked accounts";
            ExecuteButton.AccessibleDescription = "Primary";
            ClearButton.Text = "Clear";
            ClearAutoExecScript.Text = "Clear";
            ClearOutputButton.Text = "Clear output";
            SaveOutputToFileCheck.Text = "Save output to a file";

            CommandCaption = Caption(ControlPage, "Keep the checked accounts in");
            ScriptCaption = Caption(ControlPage, "Script");
            OutputCaption = Caption(ControlPage, "Output from the accounts");
            ScriptHint = new ModernLabel() { Text = "Check the accounts on the left (box in front of the name) – commands and scripts go only to them.", Points = 9f, Color = Modern.Sub };
            ControlPage.Controls.Add(ScriptHint);

            // Executor / Auto execute as two tab buttons
            ExecTab = new ModernButton() { Text = "Executor" };
            AutoTab = new ModernButton() { Text = "Auto execute (on join)" };
            ExecTab.Click += (s, e) => { ScriptTabs.SelectedIndex = 0; MarkScriptTabs(); };
            AutoTab.Click += (s, e) => { ScriptTabs.SelectedIndex = 1; MarkScriptTabs(); };
            ControlPage.Controls.Add(ExecTab);
            ControlPage.Controls.Add(AutoTab);
            MarkScriptTabs();

            foreach (var Box in new[] { ScriptBox, AutoExecuteScriptBox })
            {
                Box.Dock = DockStyle.None;
                Box.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                Box.Font = new Font("Consolas", 10.5f);
                Box.BorderStyle = BorderStyle.FixedSingle;
                DarkCode(Box);
            }

            ExecutionPage.Resize += (s, e) => LayoutScriptPage(ExecutionPage, ScriptBox, ExecuteButton, ClearButton);
            AutoexecPage.Resize += (s, e) => LayoutScriptPage(AutoexecPage, AutoExecuteScriptBox, null, ClearAutoExecScript);
            ControlPage.Resize += (s, e) => LayoutControlPage();

            // ---------- Settings ----------
            var SettingsItems = SettingsLayoutPanel.Controls.Cast<Control>().ToList();
            foreach (var C in SettingsItems) { SettingsLayoutPanel.Controls.Remove(C); C.Anchor = AnchorStyles.Top | AnchorStyles.Left; SettingsTab.Controls.Add(C); }
            foreach (var C in tableLayoutPanel1.Controls.Cast<Control>().ToList()) { tableLayoutPanel1.Controls.Remove(C); C.Anchor = AnchorStyles.Top | AnchorStyles.Left; SettingsTab.Controls.Add(C); }
            SettingsLayoutPanel.Visible = false;
            tableLayoutPanel1.Visible = false;
            SettingsTab.AutoScroll = true;

            StartOnLaunch.Text = "Start account control when the manager starts";
            AllowExternalConnectionsCB.Text = "Allow connections from other devices";
            InternetCheckCB.Text = "Check the internet connection before launching";
            UsePresenceCB.Text = "Use Roblox online status to find accounts that left";
            RLLabel.Text = "Delay between rejoins (seconds per account)";
            LDLabel.Text = "Launcher delay (seconds)";
            PortLabel.Text = "Port";
            AutoMinimizeCB.Text = "Minimize Roblox automatically every";
            label8.Text = "seconds";
            AutoCloseCB.Text = "Close Roblox automatically";
            ACLabel.Text = "Close interval (minutes)";
            MaxInstanceLabel.Text = "Max Roblox windows";
            MinimizeRoblox.Text = "Minimize all Roblox windows now";
            CloseRoblox.Text = "Close all Roblox windows now";

            var Rows = new (Control Left, Control Value, Control Extra)[]
            {
                (StartOnLaunch, null, null),
                (AllowExternalConnectionsCB, null, null),
                (InternetCheckCB, null, null),
                (UsePresenceCB, null, null),
                (RLLabel, ModernSkin.Field(RelaunchDelayNumber), null),
                (LDLabel, ModernSkin.Field(LauncherDelayNumber), null),
                (PortLabel, ModernSkin.Field(PortNumber), null),
                (AutoMinimizeCB, ModernSkin.Field(AutoMinIntervalNum), label8),
                (AutoCloseCB, AutoCloseType, null),
                (ACLabel, ModernSkin.Field(AutoCloseIntervalNum), null),
                (MaxInstanceLabel, ModernSkin.Field(MaxInstancesNum), null),
            };

            AutoCloseType.DropDownStyle = ComboBoxStyle.DropDownList;

            SettingsTab.Resize += (s, e) =>
            {
                int G = Modern.Gutter, Y = G + SettingsTab.AutoScrollPosition.Y, H = ModernSkin.Row, ValueX = G + Modern.Px(440), TextX = G + Modern.Px(24);

                foreach (var (Left, Value, Extra) in Rows)
                {
                    int LH = Left is CheckBox ? Modern.Px(22) : Left.PreferredSize.Height;
                    if (Left is CheckBox) Left.SetBounds(G, Y + (H - LH) / 2, Modern.Px(420), LH);
                    else { ((Label)Left).AutoSize = false; Left.SetBounds(TextX, Y + (H - LH) / 2, Modern.Px(396), LH); }

                    if (Value is ComboBox Combo) Combo.SetBounds(ValueX, Y + (H - Combo.Height) / 2, Modern.Px(160), Combo.Height);
                    else Value?.SetBounds(ValueX, Y, Modern.Px(120), H);

                    if (Extra != null) { int EH = Extra.PreferredSize.Height; ((Label)Extra).AutoSize = false; Extra.SetBounds(ValueX + Modern.Px(128), Y + (H - EH) / 2, Modern.Px(90), EH); }

                    Y += H + Modern.Px(6);
                }

                Y += Modern.Px(12);
                MinimizeRoblox.SetBounds(G, Y, Modern.Px(260), H);
                CloseRoblox.SetBounds(G + Modern.Px(268), Y, Modern.Px(260), H);
            };

            // ---------- Help ----------
            foreach (var L in new[] { label4, label5, label6, label7 }) { L.AutoSize = false; L.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
            NexusDL.Text = "Download Nexus.lua";
            NexusDocsButton.Text = "Open the documentation";
            NexusDL.Anchor = NexusDocsButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            HelpPage.Resize += (s, e) =>
            {
                int G = Modern.Gutter, W = Math.Min(HelpPage.ClientSize.Width - G * 2, Modern.Px(760)), Y = G;

                foreach (var L in new[] { label4, label5, label7, label6 })
                {
                    Size Need = TextRenderer.MeasureText(L.Text, L.Font, new Size(W, 0), TextFormatFlags.WordBreak);
                    L.SetBounds(G, Y, W, Need.Height + Modern.Px(4));
                    Y = L.Bottom + Modern.Px(14);
                }

                NexusDL.SetBounds(G, Y + Modern.Px(6), Modern.Px(200), ModernSkin.Row);
                NexusDocsButton.SetBounds(G + Modern.Px(208), Y + Modern.Px(6), Modern.Px(220), ModernSkin.Row);
            };

            ModernSkin.SkinList(AccountsView);
            ResumeLayout(true);

            LayoutControlPage();
        }

        private void MarkScriptTabs()
        {
            if (ExecTab == null) return;

            ExecTab.Active = ScriptTabs.SelectedIndex == 0;
            AutoTab.Active = ScriptTabs.SelectedIndex == 1;
            ScriptHint.Visible = ScriptTabs.SelectedIndex == 0;
        }

        /// <summary>Code editor in the app colors (comments grey, strings purple) instead of the bright default ones.</summary>
        private static void DarkCode(FastColoredTextBoxNS.FastColoredTextBox Box)
        {
            try
            {
                var H = Box.SyntaxHighlighter;
                H.CommentStyle = new FastColoredTextBoxNS.TextStyle(new SolidBrush(Modern.Sub), null, FontStyle.Italic);
                H.StringStyle = new FastColoredTextBoxNS.TextStyle(new SolidBrush(Modern.AccentText), null, FontStyle.Regular);
                H.NumberStyle = new FastColoredTextBoxNS.TextStyle(new SolidBrush(Color.FromArgb(230, 180, 80)), null, FontStyle.Regular);
                H.KeywordStyle = new FastColoredTextBoxNS.TextStyle(new SolidBrush(Color.FromArgb(120, 170, 255)), null, FontStyle.Regular);
                H.FunctionsStyle = new FastColoredTextBoxNS.TextStyle(new SolidBrush(Color.FromArgb(110, 210, 200)), null, FontStyle.Regular);
                Box.OnSyntaxHighlight(new FastColoredTextBoxNS.TextChangedEventArgs(Box.Range));
            }
            catch { }
        }

        private void LayoutScriptPage(TabPage Page, Control Editor, Control Main, Control Clear)
        {
            int H = ModernSkin.Row, Gap = ModernSkin.Gap, W = Page.ClientSize.Width;
            int EditorH = Math.Max(Modern.Px(60), Page.ClientSize.Height - H - Gap);

            Editor.SetBounds(0, 0, W, EditorH);
            Main?.SetBounds(0, EditorH + Gap, Modern.Px(220), H);
            Clear.SetBounds(W - Modern.Px(110), EditorH + Gap, Modern.Px(110), H);
        }

        private void LayoutControlPage()
        {
            if (!ModernBuilt || ControlPage.ClientSize.Width < Modern.Px(400)) return;

            int G = Modern.Gutter, H = ModernSkin.Row, Gap = ModernSkin.Gap, W = ControlPage.ClientSize.Width, Hh = ControlPage.ClientSize.Height;

            // left: the controlled accounts
            int ListW = Math.Max(Modern.Px(260), (W - G * 3) * 2 / 5);
            AccountsFrame.SetBounds(G, G, ListW, Hh - G * 2);

            // right: one column of actions
            int X = AccountsFrame.Right + G, RW = W - X - G, Y = G;

            AutoRelaunchCheckBox.SetBounds(X, Y + (H - Modern.Px(22)) / 2, RW, Modern.Px(22));
            Y += H + Modern.Px(10);

            CommandCaption.SetBounds(X, Y, RW, Modern.Px(18)); Y += Modern.Px(18) + Modern.Px(6);
            int Half = (RW - Gap) / 2;
            PlaceBox.SetBounds(X, Y, Half, H);
            JobBox.SetBounds(X + Half + Gap, Y, RW - Half - Gap, H);
            Y += H + Gap;
            CommandBox.SetBounds(X, Y, RW - Modern.Px(100) - Gap, H);
            SendCommand.SetBounds(X + RW - Modern.Px(100), Y, Modern.Px(100), H);
            Y += H + Modern.Px(18);

            ScriptCaption.SetBounds(X, Y, RW, Modern.Px(18)); Y += Modern.Px(18) + Modern.Px(6);
            ExecTab.SetBounds(X, Y, Modern.Px(130), H);
            AutoTab.SetBounds(ExecTab.Right + Gap, Y, Modern.Px(190), H);
            Y += H + Gap;

            int Rest = Hh - Y - G;
            int ScriptH = Math.Max(Modern.Px(150), Rest * 55 / 100);
            ScriptTabs.SetBounds(X, Y, RW, ScriptH);
            Y += ScriptH + Modern.Px(6);
            ScriptHint.SetBounds(X, Y, RW, Modern.Px(20));
            Y += Modern.Px(20) + Modern.Px(10);

            OutputCaption.SetBounds(X, Y, RW, Modern.Px(18)); Y += Modern.Px(18) + Modern.Px(6);
            int OutH = Math.Max(Modern.Px(60), Hh - G - Y - H - Gap);
            OutputFrame.SetBounds(X, Y, RW, OutH);
            Y += OutH + Gap;
            ClearOutputButton.SetBounds(X, Y, Modern.Px(130), H);
            SaveOutputToFileCheck.SetBounds(ClearOutputButton.Right + Modern.Px(14), Y + (H - Modern.Px(22)) / 2, Modern.Px(240), Modern.Px(22));
        }
    }
}
