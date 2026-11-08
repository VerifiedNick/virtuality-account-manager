using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>
    /// Roblox login in a normal Chrome / Edge window (own temporary profile, no automation flags), so Roblox's
    /// "press and hold" human check works. The program only fills in username + password and waits until the
    /// login cookie exists; you click "Log in" and do the captcha / 2-step code yourself. Nothing is sent anywhere
    /// except to Roblox (in the browser) and to the account list on this PC.
    /// </summary>
    public static class CleanLogin
    {
        public static string FindBrowser()
        {
            var Candidates = new System.Collections.Generic.List<string>();

            if (AccountManager.General != null && AccountManager.General.Exists("LoginBrowserPath")) Candidates.Add(AccountManager.General.Get("LoginBrowserPath"));

            // the account manager is a 32 bit program: "Program Files" would point to "Program Files (x86)", so the 64 bit folder is read separately
            string PF64 = Environment.GetEnvironmentVariable("ProgramW6432") ?? @"C:\Program Files";
            string PF86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Candidates.Add(Path.Combine(PF64, @"Google\Chrome\Application\chrome.exe"));
            Candidates.Add(Path.Combine(PF86, @"Google\Chrome\Application\chrome.exe"));
            Candidates.Add(Path.Combine(Local, @"Google\Chrome\Application\chrome.exe"));
            Candidates.Add(Path.Combine(PF86, @"Microsoft\Edge\Application\msedge.exe"));
            Candidates.Add(Path.Combine(PF64, @"Microsoft\Edge\Application\msedge.exe"));

            return Candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
        }

        private static int FreePort()
        {
            var L = new TcpListener(IPAddress.Loopback, 0);
            L.Start();
            int Port = ((IPEndPoint)L.LocalEndpoint).Port;
            L.Stop();
            return Port;
        }

        /// <summary>Minimal DevTools connection (only used to fill the form, read the password field and read the cookie).</summary>
        private class Cdp : IDisposable
        {
            private readonly ClientWebSocket Socket = new ClientWebSocket();
            private readonly ConcurrentDictionary<int, TaskCompletionSource<JObject>> Pending = new ConcurrentDictionary<int, TaskCompletionSource<JObject>>();
            private int NextId;

            public static async Task<Cdp> Connect(string Url)
            {
                var C = new Cdp();
                C.Socket.Options.Proxy = null;
                await C.Socket.ConnectAsync(new Uri(Url), CancellationToken.None);
                _ = C.Receive();
                return C;
            }

            public bool Open => Socket.State == WebSocketState.Open;

            private async Task Receive()
            {
                var Buffer = new byte[1 << 16];
                var Text = new StringBuilder();

                try
                {
                    while (Socket.State == WebSocketState.Open)
                    {
                        var R = await Socket.ReceiveAsync(new ArraySegment<byte>(Buffer), CancellationToken.None);

                        if (R.MessageType == WebSocketMessageType.Close) break;

                        Text.Append(Encoding.UTF8.GetString(Buffer, 0, R.Count));

                        if (!R.EndOfMessage) continue;

                        string Message = Text.ToString();
                        Text.Clear();

                        if (Message.TryParseJson(out JObject Obj) && Obj["id"] != null && Pending.TryRemove(Obj["id"].Value<int>(), out var Tcs))
                            Tcs.TrySetResult(Obj);
                    }
                }
                catch { }

                foreach (var P in Pending.Values) P.TrySetResult(null);
            }

            public async Task<JObject> Call(string Method, object Params = null, int TimeoutMs = 5000)
            {
                int Id = Interlocked.Increment(ref NextId);
                var Tcs = new TaskCompletionSource<JObject>();
                Pending[Id] = Tcs;

                byte[] Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { id = Id, method = Method, @params = Params ?? new { } }));
                await Socket.SendAsync(new ArraySegment<byte>(Data), WebSocketMessageType.Text, true, CancellationToken.None);

                var Done = await Task.WhenAny(Tcs.Task, Task.Delay(TimeoutMs));
                Pending.TryRemove(Id, out _);

                return Done == Tcs.Task ? Tcs.Task.Result : null;
            }

            public async Task<string> Eval(string Expression)
            {
                var R = await Call("Runtime.evaluate", new { expression = Expression, returnByValue = true });
                return R?["result"]?["result"]?["value"]?.ToString();
            }

            public void Dispose()
            {
                try { Socket.Abort(); Socket.Dispose(); } catch { }
            }
        }

        private static string Js(string Value) => JsonConvert.ToString(Value ?? string.Empty);

        /// <summary>
        /// Opens the login, fills it in and waits (max. 15 minutes) for the login cookie. Returns (cookie, password) or null when the window was closed.
        /// </summary>
        public static readonly string ProfilesFolder = Path.Combine(Environment.CurrentDirectory, "LoginProfiles");

        /// <summary>
        /// Each account keeps its own browser profile in "LoginProfiles\&lt;UserId&gt;", so Roblox sees the same device on every re-login
        /// (fewer extra checks). Caches are not kept. New accounts start in a temporary "new-…" folder that is renamed after the login.
        /// </summary>
        public static string ProfileFor(long UserId) => Path.Combine(ProfilesFolder, UserId > 0 ? UserId.ToString() : "new-" + Guid.NewGuid().ToString("N").Substring(0, 8));

        /// <summary>After a new account logged in, its temporary profile folder becomes its own one.</summary>
        public static void AdoptProfile(string Profile, long UserId)
        {
            if (string.IsNullOrEmpty(Profile) || UserId <= 0 || !Path.GetFileName(Profile).StartsWith("new-")) return;

            string Target = ProfileFor(UserId);

            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(1500);

                    try
                    {
                        if (Directory.Exists(Target)) { Directory.Delete(Profile, true); return; } // the account already had one
                        Directory.Move(Profile, Target);
                        return;
                    }
                    catch { }
                }
            });
        }

        private static void DropCaches(string Profile)
        {
            foreach (string Sub in new[] { @"Default\Cache", @"Default\Code Cache", @"Default\GPUCache", @"Default\Service Worker\CacheStorage", @"Default\Service Worker\ScriptCache", "GrShaderCache", "ShaderCache", "GraphiteDawnCache", @"Default\DawnGraphiteCache", @"Default\DawnWebGPUCache", "component_crx_cache", "OptimizationGuidePredictionModels" })
            {
                try { string D = Path.Combine(Profile, Sub); if (Directory.Exists(D)) Directory.Delete(D, true); } catch { }
            }
        }

        public static async Task<(string Cookie, string Password)?> Run(string Browser, string Username, string Password, string Profile, Action<string> Status = null)
        {
            int Port = FreePort();
            Directory.CreateDirectory(Profile);

            string Args = $"--user-data-dir=\"{Profile}\" --remote-debugging-port={Port} --remote-allow-origins=* --no-first-run --no-default-browser-check --disable-sync --disk-cache-size=1 --media-cache-size=1 --window-size=900,820 \"https://www.roblox.com/login\"";
            Process Proc = Process.Start(new ProcessStartInfo(Browser, Args) { UseShellExecute = false });

            using var Http = new HttpClient(new HttpClientHandler() { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };

            Cdp Page = null;
            string PageId = null;
            bool Filled = false;
            string TypedPassword = Password;
            DateTime Ends = DateTime.Now.AddMinutes(15);

            try
            {
                while (DateTime.Now < Ends)
                {
                    await Task.Delay(1000);

                    JArray Targets;

                    try { Targets = JArray.Parse(await Http.GetStringAsync($"http://127.0.0.1:{Port}/json/list")); }
                    catch
                    {
                        if (Proc.HasExited && (DateTime.Now - Proc.StartTime).TotalSeconds > 10) return null; // window closed
                        continue;
                    }

                    var Tab = Targets.FirstOrDefault(t => t.Value<string>("type") == "page" && (t.Value<string>("url") ?? "").Contains("roblox.com")) ?? Targets.FirstOrDefault(t => t.Value<string>("type") == "page");

                    if (Tab == null) { if (Targets.Count == 0) return null; continue; }

                    string Url = Tab.Value<string>("url") ?? string.Empty;

                    if (Page == null || !Page.Open || PageId != Tab.Value<string>("id"))
                    {
                        Page?.Dispose();
                        Page = await Cdp.Connect(Tab.Value<string>("webSocketDebuggerUrl"));
                        PageId = Tab.Value<string>("id");
                    }

                    // 1) fill in the login form once (you press "Log in" yourself)
                    if (!Filled && Url.Contains("/login"))
                    {
                        string R = await Page.Eval($@"(function(){{
                            var set=function(id,v){{var e=document.getElementById(id);if(!e||!v)return false;var s=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;s.call(e,v);e.dispatchEvent(new Event('input',{{bubbles:true}}));return true;}};
                            var a=set('login-username',{Js(Username)});var b=set('login-password',{Js(Password)});return (a||b||{(string.IsNullOrEmpty(Username) ? "true" : "false")})?'ok':'wait';}})()");

                        if (R == "ok") { Filled = true; Status?.Invoke("Filled in – press \"Log in\" and do the check"); }
                    }

                    // remember a password typed by hand (new accounts)
                    if (Url.Contains("/login"))
                    {
                        string Typed = await Page.Eval("(document.getElementById('login-password')||{}).value||''");
                        if (!string.IsNullOrEmpty(Typed)) TypedPassword = Typed;
                    }

                    // 2) logged in? Roblox sets .ROBLOSECURITY only after a successful login
                    var Cookies = await Page.Call("Network.getAllCookies");
                    var Sec = (Cookies?["result"]?["cookies"] as JArray)?.FirstOrDefault(c => c.Value<string>("name") == ".ROBLOSECURITY" && (c.Value<string>("domain") ?? "").EndsWith("roblox.com"));

                    if (Sec != null && !string.IsNullOrEmpty(Sec.Value<string>("value")))
                        return (Sec.Value<string>("value"), TypedPassword);
                }

                return null;
            }
            finally
            {
                Page?.Dispose();

                try
                {
                    var Version = JObject.Parse(await Http.GetStringAsync($"http://127.0.0.1:{Port}/json/version"));
                    using (var B = await Cdp.Connect(Version.Value<string>("webSocketDebuggerUrl")))
                        await B.Call("Browser.close", null, 2000);
                }
                catch { }

                try { if (!Proc.HasExited && !Proc.WaitForExit(3000)) Proc.Kill(); } catch { }

                _ = Task.Run(async () => // keep the profile (= same device for Roblox), only throw away the caches
                {
                    await Task.Delay(2000);
                    DropCaches(Profile);
                });
            }
        }
    }
}
