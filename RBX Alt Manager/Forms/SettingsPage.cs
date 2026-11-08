using Microsoft.Win32;
using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    /// <summary>
    /// The settings as a page of the main window (replaces the old Settings window).
    /// Same keys in RAMSettings.ini as before, so nothing changes for existing settings.
    /// </summary>
    public class SettingsPage : Panel
    {
        public readonly List<(string Name, Panel Body)> Sections = new List<(string, Panel)>();
        public int Current { get; private set; } = -1;

        private static IniSection General => AccountManager.General;
        private static IniSection Developer => AccountManager.Developer;
        private static IniSection WebServer => AccountManager.WebServer;
        private static void Save() => AccountManager.IniSettings.Save("RAMSettings.ini");

        private static string Bool(bool V) => V ? "true" : "false";

        private readonly List<Action> Refreshers = new List<Action>();

        public SettingsPage()
        {
            BackColor = Modern.Bg;
            Tag = "NoScaling";

            BuildGeneral();
            BuildUpdates();
            BuildJoining();
            BuildStatus();
            BuildSecurity();
            BuildDeveloper();

            ShowSection(0);
        }

        /// <summary>Reads every value again (call when the page is opened).</summary>
        public void Reload()
        {
            foreach (var R in Refreshers)
                try { R(); } catch { }
        }

        public void ShowSection(int Index)
        {
            if (Index < 0 || Index >= Sections.Count) return;

            Current = Index;

            for (int i = 0; i < Sections.Count; i++)
                Sections[i].Body.Visible = i == Index;

            Sections[Index].Body.BringToFront();
            Sections[Index].Body.AutoScrollPosition = Point.Empty;
        }

        #region Sections

        private void BuildGeneral()
        {
            var S = Section("General", "How the manager behaves on this PC.");

            Toggle(S, "Keep sessions alive", "Checks accounts that weren't used for a week once a day, so Roblox doesn't end their session. Never signs anything out.",
                () => General.Get<bool>("AutoCookieRefresh"),
                V =>
                {
                    General.Set("AutoCookieRefresh", Bool(V)); Save();
                    if (AccountManager.Instance?.AutoCookieRefresh != null) AccountManager.Instance.AutoCookieRefresh.Enabled = V;
                });

            Toggle(S, "Multi Roblox", "Lets several Roblox windows run at the same time. Close Roblox first if it is open.",
                () => General.Get<bool>("EnableMultiRbx"),
                V =>
                {
                    General.Set("EnableMultiRbx", Bool(V)); Save();
                    AccountManager.Instance?.UpdateMultiRoblox();
                });

            Toggle(S, "Close Roblox for multi Roblox", "If Roblox is open when the manager starts, it gets closed so multi Roblox works right away (no warning).",
                () => !General.Exists("CloseRobloxForMulti") || General.Get<bool>("CloseRobloxForMulti"), V => { General.Set("CloseRobloxForMulti", Bool(V)); Save(); });

            Toggle(S, "Start with Windows", "Opens the manager when you log in to Windows.", GetStartup, SetStartup);

            Toggle(S, "Warn about unused accounts", "Marks accounts that weren't used for 20+ days (orange) and 30+ days (red) in the list.",
                () => !General.Get<bool>("DisableAgingAlert"), V => { General.Set("DisableAgingAlert", Bool(!V)); Save(); });

            Toggle(S, "Load game pictures", "Turn off to save memory; the server list then shows names only.",
                () => !General.Get<bool>("DisableImages"), V => { General.Set("DisableImages", Bool(!V)); Save(); });

            Number(S, "Pause between group logins", "Seconds between two accounts when you use \"Log in group\". Longer = fewer captchas.", 5, 600,
                () => General.Exists("LoginQueueDelay") ? General.Get<decimal>("LoginQueueDelay") : 20, V => { General.Set("LoginQueueDelay", V.ToString(CultureInfo.InvariantCulture)); Save(); });

            Number(S, "Recent games to remember", "How many games the recent games page keeps.", 1, 30,
                () => General.Get<decimal>("MaxRecentGames"), V => { General.Set("MaxRecentGames", V.ToString(CultureInfo.InvariantCulture)); Save(); });
        }

        private void BuildUpdates()
        {
            var S = Section("Updates", "Keep Virtuality Account Manager up to date.");

            Button(S, "Check for updates now", $"You have build {RBX_Alt_Manager.Classes.AppUpdate.BuildNumber}. Downloads the newest version from GitHub and replaces this one.", "Check now", ModernButtonStyle.Primary,
                () => RBX_Alt_Manager.Classes.AppUpdate.CheckNow());

            Toggle(S, "Check automatically", "Looks for a new version when the app starts and offers to update.",
                () => General.Get<bool>("CheckForUpdates"), V => { General.Set("CheckForUpdates", Bool(V)); Save(); });
        }

        private void BuildJoining()
        {
            var S = Section("Joining", "What happens when you press Join.");

            ModernToggle Async = null;
            FieldBox Delay = null;

            Async = Toggle(S, "Launch all at once", "Starts every selected account at the same time instead of one after another.",
                () => General.Get<bool>("AsyncJoin"),
                V => { General.Set("AsyncJoin", Bool(V)); Save(); if (Delay != null) Delay.Enabled = !V; });

            Delay = Number(S, "Seconds between game starts", "The next account starts this many seconds after the previous one, without waiting for its game to load. 2–3 s = the games load almost at once; raise it if Roblox shows errors.", 0, 60,
                () => General.Exists("LaunchInterval") ? General.Get<decimal>("LaunchInterval") : 3, V => { General.Set("LaunchInterval", V.ToString(CultureInfo.InvariantCulture)); Save(); }, true);

            Refreshers.Add(() => Delay.Enabled = !General.Get<bool>("AsyncJoin"));

            Toggle(S, "All accounts join the same server", "Off by default – every account joins normally, exactly like Roblox does it. Turn on only if you want them bundled: then, with an empty Job ID, the first account picks a server and the others follow it.",
                () => General.Get<bool>("SameServer"), V => { General.Set("SameServer", Bool(V)); Save(); });

            Toggle(S, "Ask before joining twice", "Off by default – joining just works, no questions. Turn on if you want a warning when the same account is already in a game or was started in the last 2 minutes (joining it again closes its open window). Different accounts in several windows are never affected.",
                () => General.Get<bool>("ConfirmDoubleJoin"), V => { General.Set("ConfirmDoubleJoin", Bool(V)); Save(); });

            Toggle(S, "Shuffle picks the emptiest server", "The shuffle button next to Job ID joins the server with the fewest players.",
                () => General.Get<bool>("ShuffleChoosesLowestServer"), V => { General.Set("ShuffleChoosesLowestServer", Bool(V)); Save(); });

            ModernToggle Fps = Toggle(S, "Unlock FPS", "Raises Roblox's 60 FPS limit to the value below.",
                () => General.Get<bool>("UnlockFPS"), V => { General.Set("UnlockFPS", Bool(V)); Save(); });

            Number(S, "Max FPS", "Used when Unlock FPS is on.", 5, 9999,
                () => General.Get<decimal>("MaxFPSValue"), V => { General.Set("MaxFPSValue", V.ToString(CultureInfo.InvariantCulture)); Save(); });

            ModernButton Custom = null;
            ModernLabel CustomDesc = null;

            bool HasCustom() => General.Exists("CustomClientSettings") && File.Exists(General.Get<string>("CustomClientSettings"));

            void UpdateCustom()
            {
                bool On = HasCustom();
                Custom.Text = On ? "Remove" : "Choose file…";
                Custom.Style = On ? ModernButtonStyle.Danger : ModernButtonStyle.Secondary;
                Custom.Invalidate();
                CustomDesc.Text = On ? $"Using {Path.GetFileName(General.Get<string>("CustomClientSettings"))} – Unlock FPS is ignored while this is set." : "Your own ClientAppSettings.json instead of Unlock FPS.";
                Fps.Enabled = !On;
            }

            (Custom, CustomDesc) = Button(S, "Custom client settings", "", "Choose file…", ModernButtonStyle.Secondary, () =>
            {
                if (HasCustom())
                {
                    General.RemoveProperty("CustomClientSettings");
                    Save();
                    UpdateCustom();
                    return;
                }

                using (var Dialog = new OpenFileDialog() { Filter = "JSON (*.json)|*.json|All files (*.*)|*.*", Title = "Choose ClientAppSettings.json" })
                {
                    if (Dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                    if (File.Exists(Dialog.FileName) && File.ReadAllText(Dialog.FileName).TryParseJson<object>(out _))
                    {
                        string Target = Path.Combine(Environment.CurrentDirectory, "CustomClientAppSettings.json");
                        File.Copy(Dialog.FileName, Target, true);
                        General.Set("CustomClientSettings", Target);
                        Save();
                    }
                    else
                        ModernDialog.Show("That file doesn't contain valid JSON.", "Custom client settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                UpdateCustom();
            });

            Refreshers.Add(UpdateCustom);

            Text(S, "Server region format", "How regions show in the server list. Placeholders from ip-api.com, e.g. <city>, <countryCode>.",
                () => General.Get<string>("ServerRegionFormat"), V => { General.Set("ServerRegionFormat", V, "Visit http://ip-api.com/json to see available format options"); Save(); });
        }

        private void BuildStatus()
        {
            var S = Section("Online status", "The online / in game status on the account cards.");

            Toggle(S, "Show online status", "Asks Roblox which accounts are online or in a game.",
                () => General.Get<bool>("ShowPresence"), V => { General.Set("ShowPresence", Bool(V)); Save(); });

            Number(S, "Refresh every", "Minutes between two updates of the list (cards refresh on their own every minute).", 1, 9999,
                () => General.Get<decimal>("PresenceUpdateRate"), V => { General.Set("PresenceUpdateRate", V.ToString(CultureInfo.InvariantCulture)); Save(); });
        }

        private void BuildSecurity()
        {
            var S = Section("Data & security", "Your accounts stay in AccountData.json on this PC, encrypted with your PIN.");

            Toggle(S, "Save passwords", "Keeps the password of each account (encrypted), needed for Re-login and Copy password.",
                () => General.Get<bool>("SavePasswords"), V => { General.Set("SavePasswords", Bool(V)); Save(); });

            Toggle(S, "Encrypt browser logins", "Locks the LoginProfiles folder (the saved browser sessions) with your PIN whenever they aren't in use – strong encryption, same as your accounts. Leave on. Only matters if you have a PIN.",
                () => !General.Exists("EncryptProfiles") || General.Get<bool>("EncryptProfiles"),
                V => { General.Set("EncryptProfiles", Bool(V)); Save(); if (V) RBX_Alt_Manager.Classes.ProfileVault.SealAllInBackground(); });

            ModernLabel BackupDesc = null;
            ModernButton RestoreButton = null;
            var RestoreMenu = new ContextMenuStrip();
            ModernSkin.StyleMenu(RestoreMenu);

            void UpdateBackups()
            {
                var All = Backups.List();
                BackupDesc.Text = "Copies of your accounts (encrypted, same PIN) are made at every start, on close and every 30 min while you work – also outside this folder (AppData\\Local\\Virtuality Manager), so they survive even if this folder gets deleted. "
                    + (All.Count == 0 ? "No copy yet." : $"Newest: {All[0].LastWriteTime:dd.MM.yyyy HH:mm}.");
                BackupDesc.Invalidate();

                RestoreMenu.Items.Clear();
                foreach (var F in All)
                {
                    var File_ = F;
                    RestoreMenu.Items.Add($"{F.LastWriteTime:dd.MM.yyyy  HH:mm}   ·   {F.Length / 1024} KB", null, (s, e) => RestoreBackup(File_));
                }
                ModernSkin.StyleMenu(RestoreMenu);
                RestoreButton.Enabled = All.Count > 0;
            }

            (_, BackupDesc) = Button(S, "Automatic backups", " ", "Open folder", ModernButtonStyle.Secondary, () =>
            {
                Directory.CreateDirectory(Backups.Folder);
                System.Diagnostics.Process.Start("explorer.exe", Backups.Folder);
            });

            Button(S, "Safe copies", "The copies outside the app folder. A new Virtuality Account Manager finds them by itself and offers to put them back.", "Open folder", ModernButtonStyle.Secondary, () =>
            {
                Directory.CreateDirectory(Backups.SafeFolder);
                System.Diagnostics.Process.Start("explorer.exe", Backups.SafeFolder);
            });

            (RestoreButton, _) = Button(S, "Restore a backup", "Puts an older copy back. The current state is saved first, then the manager restarts.", "Choose…", ModernButtonStyle.Secondary, () => { });
            RestoreButton.Menu = RestoreMenu;
            RestoreButton.SplitMenu = false;

            Refreshers.Add(UpdateBackups);
            UpdateBackups();

            Button(S, "Change encryption", "Choose again how AccountData.json is protected. You will be asked for a new PIN.", "Change…", ModernButtonStyle.Danger, () =>
            {
                if (Utilities.YesNoPrompt("Settings", "Change encryption", "Do you really want to change how your account data is encrypted?", false))
                    AccountManager.Instance.ResetEncryption(true);
            });
        }

        private void BuildDeveloper()
        {
            var S = Section("Developer", "Only needed for scripts that talk to the manager.");

            Toggle(S, "Developer mode", "Shows extra developer options in the menus.",
                () => Developer.Get<bool>("DevMode"), V => { Developer.Set("DevMode", Bool(V)); Save(); });

            Toggle(S, "Web server", "Local API for scripts. Takes effect after a restart.",
                () => Developer.Get<bool>("EnableWebServer"),
                V => { Developer.Set("EnableWebServer", Bool(V)); Save(); ModernDialog.Show("Restart the manager to apply this.", "Web server", MessageBoxButtons.OK, MessageBoxIcon.Information); });

            Number(S, "Port", "Port of the web server.", 1, 65535,
                () => WebServer.Get<decimal>("WebServerPort"), V => { WebServer.Set("WebServerPort", V.ToString(CultureInfo.InvariantCulture)); Save(); });

            Text(S, "Password", "Letters and numbers only.", () => WebServer.Get("Password"),
                V => { WebServer.Set("Password", V); Save(); }, true, V => Regex.Replace(V, "[^0-9a-zA-Z ]", ""));

            Toggle(S, "Every request needs the password", null, () => WebServer.Get<bool>("EveryRequestRequiresPassword"), V => { WebServer.Set("EveryRequestRequiresPassword", Bool(V)); Save(); });
            Toggle(S, "Allow GetCookie", "Scripts can read login cookies. Leave off unless you really need it.", () => WebServer.Get<bool>("AllowGetCookie"), V => { WebServer.Set("AllowGetCookie", Bool(V)); Save(); });
            Toggle(S, "Allow GetAccounts", null, () => WebServer.Get<bool>("AllowGetAccounts"), V => { WebServer.Set("AllowGetAccounts", Bool(V)); Save(); });
            Toggle(S, "Allow LaunchAccount", null, () => WebServer.Get<bool>("AllowLaunchAccount"), V => { WebServer.Set("AllowLaunchAccount", Bool(V)); Save(); });
            Toggle(S, "Allow editing accounts", null, () => WebServer.Get<bool>("AllowAccountEditing"), V => { WebServer.Set("AllowAccountEditing", Bool(V)); Save(); });

            Toggle(S, "Allow external connections", "Other devices in your network can use the API. Needs admin rights and a restart.",
                () => WebServer.Get<bool>("AllowExternalConnections"),
                V => { WebServer.Set("AllowExternalConnections", Bool(V)); Save(); ModernDialog.Show("Restart the manager (as admin) to apply this.", "Web server", MessageBoxButtons.OK, MessageBoxIcon.Information); });
        }

        #endregion

        private void RestoreBackup(FileInfo Backup)
        {
            if (ModernDialog.Show($"Put back the copy from {Backup.LastWriteTime:dd.MM.yyyy HH:mm}?\n\nYour current accounts are saved first (as \"Before restore …\" in the Backups folder). Then the manager restarts and asks for the PIN of that copy.", "Restore a backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                AccountManager.SavingBlocked = true;
                Backups.Restore(Backup);
                System.Diagnostics.Process.Start(Application.ExecutablePath, $"-after {System.Diagnostics.Process.GetCurrentProcess().Id}");
                Application.Exit();
            }
            catch (Exception x)
            {
                AccountManager.SavingBlocked = false;
                ModernDialog.Show($"The backup could not be restored. Nothing was changed.\n\n{x.Message}", "Restore a backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Start with Windows

        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "Virtuality Account Manager";

        private static bool GetStartup()
        {
            try
            {
                using (var Key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return Key?.GetValue(RunName) is string P && P.Trim('"') == Application.ExecutablePath;
            }
            catch { return false; }
        }

        private static void SetStartup(bool On)
        {
            try
            {
                using (var Key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (Key == null) return;

                    if (On) Key.SetValue(RunName, $"\"{Application.ExecutablePath}\"");
                    else if (Key.GetValue(RunName) != null) Key.DeleteValue(RunName);
                }
            }
            catch (Exception x) { ModernDialog.Show(x.Message, "Start with Windows", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        #endregion

        #region Rows

        private class SectionPanel : Panel
        {
            public readonly List<Control> Rows = new List<Control>();
            public ModernLabel Title, Sub;

            public SectionPanel()
            {
                AutoScroll = true;
                BackColor = Modern.Bg;
                Dock = DockStyle.Fill;
                Tag = "NoScaling";
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);

                if (Title == null || Sub == null) return; // layout runs while the panel is still being built

                int G = Modern.Gutter, W = Math.Max(Modern.Px(420), ClientSize.Width - G * 2 - Modern.PageReserveRight), Y = G + AutoScrollPosition.Y;

                // title + subtitle sit exactly like the group title on the accounts page
                Title.SetBounds(G, Y - Modern.Px(4), W, Modern.Px(28));
                Sub.SetBounds(G, Y + Modern.RowH - Modern.Px(16), W, Modern.Px(16));
                Y += Modern.RowH + Modern.Px(14);

                foreach (var R in Rows)
                {
                    int RH = R is SettingRow SR ? SR.HeightFor(W) : R.Height;
                    R.SetBounds(G, Y, W, RH);
                    Y += RH;
                }

                AutoScrollMinSize = new Size(0, Y - AutoScrollPosition.Y + G);
            }
        }

        private class SettingRow : Panel
        {
            public ModernLabel Title, Desc;
            public Control Input;
            public int InputWidth;

            public SettingRow(string T, string D)
            {
                BackColor = Modern.Bg;
                Tag = "NoScaling";
                DoubleBuffered = true;

                Title = new ModernLabel() { Text = T, Points = 10.5f, Bold = true, Color = Modern.Text };
                Desc = new ModernLabel() { Text = D ?? string.Empty, Points = 9f, Color = Modern.Sub, Wrap = true };

                Controls.Add(Title);
                Controls.Add(Desc);

                Height = string.IsNullOrEmpty(D) ? Modern.Px(54) : Modern.Px(66);
            }

            private int DescHeight(int TW) =>
                string.IsNullOrWhiteSpace(Desc.Text) ? 0 : TextRenderer.MeasureText(Desc.Text, Modern.Font(9f), new Size(Math.Max(Modern.Px(80), TW), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;

            /// <summary>Row height for a width: long descriptions wrap instead of being cut off.</summary>
            public int HeightFor(int W)
            {
                int TW = W - Modern.Px(InputWidth) - Modern.Px(24), DH = DescHeight(TW);
                return DH == 0 ? Modern.Px(56) : Math.Max(Modern.Px(66), Modern.Px(14) + Modern.Px(22) + Modern.Px(2) + DH + Modern.Px(14));
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                if (Input == null || Title == null || Desc == null) return;

                int IW = Modern.Px(InputWidth), IH = Input is ModernToggle ? Modern.Px(22) : ModernSkin.Row;
                Input.SetBounds(Width - IW, (Height - IH) / 2, IW, IH);

                int TW = Width - IW - Modern.Px(24), DH = DescHeight(TW);

                if (DH > 0)
                {
                    int Block = Modern.Px(22) + Modern.Px(2) + DH, Top = (Height - Block) / 2;
                    Title.SetBounds(0, Top, TW, Modern.Px(22));
                    Desc.SetBounds(0, Top + Modern.Px(24), TW, DH + Modern.Px(2));
                }
                else
                    Title.SetBounds(0, (Height - Modern.Px(22)) / 2, TW, Modern.Px(22));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var P = new Pen(Modern.Line)) e.Graphics.DrawLine(P, 0, Height - 1, Width, Height - 1);
            }
        }

        private SectionPanel Section(string Name, string Description)
        {
            var S = new SectionPanel();
            S.Title = new ModernLabel() { Text = Name, Points = 15f, Bold = true };
            S.Sub = new ModernLabel() { Text = Description, Points = 9f, Color = Modern.Sub };
            S.Controls.Add(S.Title);
            S.Controls.Add(S.Sub);

            Modern.DarkScrollbars(S);
            Controls.Add(S);
            Sections.Add((Name, S));
            return S;
        }

        private SettingRow AddRow(SectionPanel S, string Title, string Desc, Control Input, int Width)
        {
            var R = new SettingRow(Title, Desc) { Input = Input, InputWidth = Width };
            R.Controls.Add(Input);
            S.Rows.Add(R);
            S.Controls.Add(R);
            return R;
        }

        private ModernToggle Toggle(SectionPanel S, string Title, string Desc, Func<bool> Get, Action<bool> Set)
        {
            var T = new ModernToggle();
            bool Loading = false;

            void Load() { Loading = true; T.Checked = Get(); Loading = false; }

            Load();
            Refreshers.Add(Load);
            T.CheckedChanged += (s, e) => { if (!Loading) Set(T.Checked); };

            var Row = AddRow(S, Title, Desc, T, 40);
            Row.Title.Click += (s, e) => { if (T.Enabled) T.Checked = !T.Checked; };
            Row.Title.Cursor = Cursors.Hand;

            return T;
        }

        private FieldBox Number(SectionPanel S, string Title, string Desc, decimal Min, decimal Max, Func<decimal> Get, Action<decimal> Set, bool Decimals = false)
        {
            var Box = new TextBox() { BorderStyle = BorderStyle.None, BackColor = Modern.Field, ForeColor = Modern.Text, Font = Modern.Font(9.75f), TextAlign = HorizontalAlignment.Right };
            var Field = new FieldBox();
            Field.AddPart(Box, -1);

            bool Loading = false;

            void Load() { Loading = true; Box.Text = Get().ToString(CultureInfo.InvariantCulture); Loading = false; }

            bool TryValue(out decimal V) => decimal.TryParse(Box.Text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out V) && V >= Min && V <= Max && (Decimals || V == Math.Floor(V));

            Box.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(Decimals && (e.KeyChar == '.' || e.KeyChar == ','))) e.Handled = true; };
            Box.TextChanged += (s, e) =>
            {
                if (Loading) return;
                bool Ok = TryValue(out decimal V);
                Box.ForeColor = Ok ? Modern.Text : Modern.Danger;
                if (Ok) Set(V);
            };
            Box.Leave += (s, e) => { if (!TryValue(out _)) Load(); Box.ForeColor = Modern.Text; };

            Load();
            Refreshers.Add(Load);

            AddRow(S, Title, string.IsNullOrEmpty(Desc) ? $"{Min} – {Max}" : $"{Desc}  ({Min} – {Max})", Field, 120);
            return Field;
        }

        private FieldBox Text(SectionPanel S, string Title, string Desc, Func<string> Get, Action<string> Set, bool Secret = false, Func<string, string> Filter = null)
        {
            var Box = new TextBox() { BorderStyle = BorderStyle.None, BackColor = Modern.Field, ForeColor = Modern.Text, Font = Modern.Font(9.75f), UseSystemPasswordChar = Secret };
            var Field = new FieldBox();
            Field.AddPart(Box, -1);

            bool Loading = false;

            void Load() { Loading = true; Box.Text = Get() ?? string.Empty; Loading = false; }

            Box.TextChanged += (s, e) =>
            {
                if (Loading) return;

                if (Filter != null)
                {
                    string F = Filter(Box.Text);
                    if (F != Box.Text) { int C = Box.SelectionStart; Box.Text = F; Box.SelectionStart = Math.Min(C, F.Length); return; }
                }

                Set(Box.Text);
            };

            Load();
            Refreshers.Add(Load);

            AddRow(S, Title, Desc, Field, 240);
            return Field;
        }

        private (ModernButton, ModernLabel) Button(SectionPanel S, string Title, string Desc, string ButtonText, ModernButtonStyle Style, Action Click)
        {
            var B = new ModernButton() { Text = ButtonText, Style = Style };
            B.Click += (s, e) => { try { Click(); } catch (Exception x) { ModernDialog.Show(x.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error); } };

            var Row = AddRow(S, Title, string.IsNullOrEmpty(Desc) ? " " : Desc, B, 150);
            return (B, Row.Desc);
        }

        #endregion
    }
}
