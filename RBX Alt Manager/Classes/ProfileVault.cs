using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Encrypts the per-account browser logins in "LoginProfiles" when they are not in use.
    /// Each profile folder "LoginProfiles\&lt;UserId&gt;" is packed and encrypted into "LoginProfiles\&lt;UserId&gt;.vault"
    /// with the same strength as the account data (Argon2 key from the PIN + libsodium SecretBox). While a login
    /// window is open the folder is unpacked; afterwards (and when the manager closes or locks) it is sealed again
    /// and the plain folder is removed. Without the PIN the ".vault" files are unreadable.
    ///
    ///   sealed (.vault)  ──Unseal()──▶  plain folder (browser uses it)  ──Seal()──▶  sealed (.vault)
    ///
    /// No PIN set (data not encrypted) → nothing is sealed, exactly like the account data.
    /// A ".vault" that can't be opened (PIN was changed, file damaged) is dropped; that account just needs a fresh re-login.
    /// </summary>
    public static class ProfileVault
    {
        /// <summary>Returns the PIN key bytes, or null when there is no PIN / the feature is off.</summary>
        public static Func<byte[]> KeyProvider;

        private const string Ext = ".vault";

        private static byte[] Key()
        {
            try { return KeyProvider?.Invoke(); } catch { return null; }
        }

        public static bool Enabled => Key() != null;

        private static string VaultOf(string ProfileFolder) => ProfileFolder + Ext;
        private static bool IsNewProfile(string Folder) => Path.GetFileName(Folder).StartsWith("new-");

        /// <summary>Before a login window opens: unpack the account's sealed profile so the browser can use it.</summary>
        public static void Unseal(string ProfileFolder)
        {
            var K = Key();
            if (K == null || string.IsNullOrEmpty(ProfileFolder)) return;
            if (Directory.Exists(ProfileFolder)) return; // already in use

            string Vault = VaultOf(ProfileFolder);
            if (!File.Exists(Vault)) return; // nothing sealed yet (new account)

            string Tmp = ProfileFolder + ".unzip.tmp";

            try
            {
                byte[] Zip = Cryptography.DecryptBytes(File.ReadAllBytes(Vault), K);

                if (File.Exists(Tmp)) File.Delete(Tmp);
                File.WriteAllBytes(Tmp, Zip);
                ZipFile.ExtractToDirectory(Tmp, ProfileFolder);
            }
            catch (Exception x)
            {
                // wrong PIN (changed) or damaged: drop it, the account re-logs in once and a fresh vault is made
                Program.Logger?.Warn($"[Vault] could not open {Path.GetFileName(Vault)} – it will be recreated ({x.Message})");
                try { if (Directory.Exists(ProfileFolder)) Directory.Delete(ProfileFolder, true); } catch { }
                try { File.Delete(Vault); } catch { }
            }
            finally { try { if (File.Exists(Tmp)) File.Delete(Tmp); } catch { } }
        }

        /// <summary>Seal one plain profile folder back into its ".vault" and remove the plain folder.</summary>
        public static void Seal(string ProfileFolder)
        {
            var K = Key();
            if (K == null || string.IsNullOrEmpty(ProfileFolder) || !Directory.Exists(ProfileFolder)) return;
            if (IsNewProfile(ProfileFolder)) return; // still a temporary "new-…" folder, gets sealed after it is adopted

            CleanLogin.DropProfileCaches(ProfileFolder); // don't seal the throw-away cache

            string Tmp = ProfileFolder + ".zip.tmp";

            try
            {
                if (File.Exists(Tmp)) File.Delete(Tmp);
                ZipFile.CreateFromDirectory(ProfileFolder, Tmp, CompressionLevel.Fastest, false);

                byte[] Sealed_ = Cryptography.EncryptBytes(File.ReadAllBytes(Tmp), K);
                File.WriteAllBytes(VaultOf(ProfileFolder), Sealed_);

                Directory.Delete(ProfileFolder, true);
            }
            catch (Exception x)
            {
                // a browser may still hold the folder: leave it plain, it is sealed next time
                Program.Logger?.Warn($"[Vault] could not seal {Path.GetFileName(ProfileFolder)} ({x.Message})");
            }
            finally { try { if (File.Exists(Tmp)) File.Delete(Tmp); } catch { } }
        }

        /// <summary>Seal every plain profile folder (manager closes or the lock screen appears).</summary>
        public static void SealAll()
        {
            if (Key() == null) return;

            try
            {
                if (!Directory.Exists(CleanLogin.ProfilesFolder)) return;

                foreach (var Dir in Directory.GetDirectories(CleanLogin.ProfilesFolder))
                    if (!IsNewProfile(Dir)) Seal(Dir);
            }
            catch (Exception x) { Program.Logger?.Warn($"[Vault] seal all: {x.Message}"); }
        }

        /// <summary>Seal everything in the background (after unlock), so folders left plain by a crash get protected.</summary>
        public static void SealAllInBackground()
        {
            if (Key() == null) return;
            _ = Task.Run(() => SealAll());
        }
    }
}
