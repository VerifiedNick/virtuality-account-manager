using System;
using System.IO;
using System.Reflection;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>Puts a shortcut to the running exe into the user's Start menu (Programs), so Windows lists the app like an installed program. Per user, no admin rights; silently skipped on any problem.</summary>
    internal static class StartMenuShortcut
    {
        public static void Ensure()
        {
            try
            {
                string Exe = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(Exe) || !File.Exists(Exe)) return;

                string Dir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string Link = Path.Combine(Dir, "Virtuality Manager.lnk");

                Type Shell = Type.GetTypeFromProgID("WScript.Shell");
                if (Shell == null) return;

                dynamic Wsh = Activator.CreateInstance(Shell);

                // already there and pointing at this exe: nothing to do
                if (File.Exists(Link))
                {
                    dynamic Old = Wsh.CreateShortcut(Link);
                    if (string.Equals((string)Old.TargetPath, Exe, StringComparison.OrdinalIgnoreCase)) return;
                }

                dynamic S = Wsh.CreateShortcut(Link);
                S.TargetPath = Exe;
                S.WorkingDirectory = Path.GetDirectoryName(Exe);
                S.IconLocation = Exe + ",0";
                S.Description = "Virtuality Account Manager";
                S.Save();
            }
            catch { }
        }
    }
}
