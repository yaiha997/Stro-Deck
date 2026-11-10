using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace StroDeck
{
    internal class DeckButton
    {
        public string Label = "Button";
        public string Icon = "\u2022";
        public string Color = "#39FF14";
        public string Action = "app";
        public string Target = "";
    }

    internal class DeckConfig
    {
        public int Cols = 4;
        public int Rows = 3;
        public string Title = "STRO-DECK";
        public string Background = "";
        public string BackgroundPos = "center";
        public string ClockStyle = "bold";
        public string ClockFormat = "24";
        public string CastIp = "";
        public bool CastOn = false;
        public bool StartInTray = false;
        public List<DeckButton> Buttons = new List<DeckButton>();

        public void Normalize()
        {
            if (Cols < 1) Cols = 1;
            if (Cols > 8) Cols = 8;
            if (Rows < 1) Rows = 1;
            if (Rows > 8) Rows = 8;
            int need = Cols * Rows;
            while (Buttons.Count < need)
                Buttons.Add(new DeckButton { Label = "Empty", Icon = "+", Action = "none", Target = "" });
            while (Buttons.Count > need)
                Buttons.RemoveAt(Buttons.Count - 1);
        }

        static DeckButton Btn(string label, string icon, string color, string action, string target)
        {
            return new DeckButton { Label = label, Icon = icon, Color = color, Action = action, Target = target };
        }

        public static DeckConfig Defaults()
        {
            DeckConfig c = new DeckConfig();
            c.Buttons.AddRange(new DeckButton[]
            {
                Btn("Chrome",     "\U0001F310", "#4FC3F7", "app",          "chrome.exe"),
                Btn("Files",      "\U0001F4C1", "#FFB74D", "app",          "explorer.exe"),
                Btn("Notepad",    "\U0001F4DD", "#B0BEC5", "app",          "notepad.exe"),
                Btn("Task Mgr",   "\U0001F9E0", "#FF8A65", "app",          "taskmgr.exe"),
                Btn("Terminal",   "\u2328\uFE0F", "#81C784", "app",        "cmd.exe"),
                Btn("Settings",   "\u2699\uFE0F", "#90A4AE", "app",        "ms-settings:"),
                Btn("Play/Pause", "\u23EF\uFE0F", "#BA68C8", "media",      "play"),
                Btn("Next",       "\u23ED\uFE0F", "#BA68C8", "media",      "next"),
                Btn("Prev",       "\u23EE\uFE0F", "#BA68C8", "media",      "prev"),
                Btn("Vol +",      "\U0001F50A", "#F06292", "media",        "volup"),
                Btn("Vol -",      "\U0001F509", "#F06292", "media",        "voldown"),
                Btn("Spotify",    "img:icon_20261106_105745_944.png", "#81C784", "app", "spotify:")
            });
            return c;
        }

        static string B64E(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s == null ? "" : s));
        }

        static string B64D(string s)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
            catch { return ""; }
        }

        static int ParseInt(string s, int fallback)
        {
            int v;
            if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        public static DeckConfig Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    DeckConfig d = Defaults();
                    d.Normalize();
                    Save(d, path);
                    return d;
                }
                DeckConfig c = new DeckConfig();
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("cols=")) c.Cols = ParseInt(line.Substring(5), c.Cols);
                    else if (line.StartsWith("rows=")) c.Rows = ParseInt(line.Substring(5), c.Rows);
                    else if (line.StartsWith("title=")) c.Title = B64D(line.Substring(6));
                    else if (line.StartsWith("bgpos=")) c.BackgroundPos = B64D(line.Substring(6));
                    else if (line.StartsWith("clock=")) c.ClockStyle = B64D(line.Substring(6));
                    else if (line.StartsWith("clockfmt=")) c.ClockFormat = B64D(line.Substring(9));
                    else if (line.StartsWith("castip=")) c.CastIp = B64D(line.Substring(7));
                    else if (line.StartsWith("caston=")) c.CastOn = line.Substring(7).Trim() == "1";
                    else if (line.StartsWith("starttray=")) c.StartInTray = line.Substring(10).Trim() == "1";
                    else if (line.StartsWith("bg=")) c.Background = B64D(line.Substring(3));
                    else if (line.StartsWith("b|"))
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 6)
                        {
                            c.Buttons.Add(new DeckButton
                            {
                                Label = B64D(p[1]),
                                Icon = B64D(p[2]),
                                Color = B64D(p[3]),
                                Action = B64D(p[4]),
                                Target = B64D(p[5])
                            });
                        }
                    }
                }
                c.Normalize();
                return c;
            }
            catch
            {
                DeckConfig d = Defaults();
                d.Normalize();
                return d;
            }
        }

        public static void Save(DeckConfig c, string path)
        {
            c.Normalize();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Stro-Deck config (edited by the app)");
            sb.AppendLine("cols=" + c.Cols.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("rows=" + c.Rows.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("title=" + B64E(c.Title));
            sb.AppendLine("bg=" + B64E(c.Background));
            sb.AppendLine("bgpos=" + B64E(c.BackgroundPos));
            sb.AppendLine("clock=" + B64E(c.ClockStyle));
            sb.AppendLine("clockfmt=" + B64E(c.ClockFormat));
            sb.AppendLine("castip=" + B64E(c.CastIp));
            sb.AppendLine("caston=" + (c.CastOn ? "1" : "0"));
            sb.AppendLine("starttray=" + (c.StartInTray ? "1" : "0"));
            foreach (DeckButton b in c.Buttons)
            {
                sb.AppendLine("b|" + B64E(b.Label) + "|" + B64E(b.Icon) + "|" + B64E(b.Color) + "|" +
                              B64E(b.Action) + "|" + B64E(b.Target));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }

    internal static class Runner
    {
        const int INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        static readonly ushort[] Modifiers = { 0x11, 0x12, 0x10, 0x5B };

        static bool IsExtended(ushort vk)
        {
            if (vk >= 0x21 && vk <= 0x2E) return true;
            if (vk >= 0xA6 && vk <= 0xB7) return true;
            if (vk == 0x5B || vk == 0x5C) return true;
            return false;
        }

        static void SendKeyRaw(ushort vk, bool up)
        {
            INPUT[] inp = new INPUT[1];
            inp[0].type = INPUT_KEYBOARD;
            inp[0].u.ki.wVk = vk;
            inp[0].u.ki.wScan = 0;
            inp[0].u.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0u) | (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0u);
            inp[0].u.ki.time = 0;
            inp[0].u.ki.dwExtraInfo = IntPtr.Zero;
            SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
        }

        static ushort Vk(string key)
        {
            string k = (key ?? "").Trim().ToLowerInvariant();
            switch (k)
            {
                case "ctrl": case "control": return 0x11;
                case "alt": return 0x12;
                case "shift": return 0x10;
                case "win": case "windows": return 0x5B;
                case "esc": case "escape": return 0x1B;
                case "enter": case "return": return 0x0D;
                case "tab": return 0x09;
                case "space": return 0x20;
                case "backspace": case "back": return 0x08;
                case "delete": case "del": return 0x2E;
                case "insert": return 0x2D;
                case "up": return 0x26;
                case "down": return 0x28;
                case "left": return 0x25;
                case "right": return 0x27;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup": return 0x21;
                case "pagedown": return 0x22;
                case "printscreen": case "prtsc": return 0x2C;
                case "pause": case "play": case "playpause": case "play/pause": return 0xB3;
                case "next": case "nexttrack": return 0xB0;
                case "prev": case "prevtrack": return 0xB1;
                case "stop": case "stoptrack": return 0xB2;
                case "mute": return 0xAD;
                case "volup": case "volumeup": case "vol+": return 0xAF;
                case "voldown": case "volumedown": case "vol-": return 0xAE;
                case "browserback": return 0xA6;
                case "browserforward": return 0xA7;
                case "browserhome": return 0xAC;
            }
            if (k.Length == 1)
            {
                char ch = char.ToUpperInvariant(k[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) return (ushort)ch;
            }
            if (k.Length >= 2 && k[0] == 'f')
            {
                int f;
                if (int.TryParse(k.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out f) &&
                    f >= 1 && f <= 24)
                    return (ushort)(0x6F + f);
            }
            return 0;
        }

        public static void SendCombo(string combo)
        {
            string[] tokens = (combo ?? "").Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            List<ushort> mods = new List<ushort>();
            List<ushort> keys = new List<ushort>();
            foreach (string t in tokens)
            {
                ushort vk = Vk(t);
                if (vk == 0) continue;
                if (Array.IndexOf(Modifiers, vk) >= 0)
                {
                    if (!mods.Contains(vk)) mods.Add(vk);
                }
                else keys.Add(vk);
            }
            foreach (ushort m in mods) SendKeyRaw(m, false);
            foreach (ushort k in keys)
            {
                SendKeyRaw(k, false);
                System.Threading.Thread.Sleep(18);
                SendKeyRaw(k, true);
            }
            for (int i = mods.Count - 1; i >= 0; i--) SendKeyRaw(mods[i], true);
        }

        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char ch in text)
            {
                INPUT[] down = new INPUT[1];
                down[0].type = INPUT_KEYBOARD;
                down[0].u.ki.wVk = 0;
                down[0].u.ki.wScan = ch;
                down[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
                down[0].u.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));

                INPUT[] up = new INPUT[1];
                up[0].type = INPUT_KEYBOARD;
                up[0].u.ki.wVk = 0;
                up[0].u.ki.wScan = ch;
                up[0].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                up[0].u.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        public static string Run(DeckButton b)
        {
            string act = (b.Action ?? "").Trim().ToLowerInvariant();
            string t = (b.Target ?? "").Trim();
            try
            {
                switch (act)
                {
                    case "app":
                    case "url":
                        if (t.Length == 0) return "No target set";
                        Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
                        return "Opened";
                    case "cmd":
                        if (t.Length == 0) return "No command set";
                        Process.Start(new ProcessStartInfo("cmd.exe", "/c " + t)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        return "Command sent";
                    case "powershell":
                        if (t.Length == 0) return "No script set";
                        Process.Start(new ProcessStartInfo("powershell.exe",
                            "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" +
                            t.Replace("\"", "\\\"") + "\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        return "Script sent";
                    case "media":
                    case "key":
                        if (t.Length == 0) return "No keys set";
                        SendCombo(t);
                        return "Key sent";
                    case "text":
                        TypeText(t);
                        return "Typed";
                    case "lock":
                        LockWorkStation();
                        return "Locked";
                    case "sleep":
                        Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                        return "Sleeping";
                    case "monitoroff":
                        SendMessage(new IntPtr(0xFFFF), 0x0112, new IntPtr(0xF170), new IntPtr(2));
                        return "Monitor off";
                    case "shutdown":
                        Process.Start("shutdown.exe", "/s /t 0");
                        return "Shutting down";
                    case "restart":
                        Process.Start("shutdown.exe", "/r /t 0");
                        return "Restarting";
                    case "none":
                        return "No action";
                }
                return "Unknown action: " + act;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }

    internal class Caster
    {
        public volatile string DeviceIp = "";
        public volatile string Url = "";
        public volatile bool Enabled;
        public volatile string LastStatus = "Off";
        public string CattPath;

        Thread thread;
        volatile bool running;
        volatile bool forceCast;
        AutoResetEvent wake = new AutoResetEvent(false);
        DateTime lastCast = DateTime.MinValue;

        public Caster()
        {
            CattPath = FindCatt();
        }

        public static string FindCatt()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] versions = { "Python313", "Python312", "Python311", "Python310" };
            foreach (string v in versions)
            {
                string p = Path.Combine(local, @"Programs\Python\" + v + @"\Scripts\catt.exe");
                if (File.Exists(p)) return p;
            }
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH");
                if (pathVar != null)
                {
                    foreach (string dir in pathVar.Split(';'))
                    {
                        string d = (dir ?? "").Trim();
                        if (d.Length == 0) continue;
                        string p;
                        try { p = Path.Combine(d, "catt.exe"); }
                        catch { continue; }
                        if (File.Exists(p)) return p;
                    }
                }
            }
            catch { }
            return "catt.exe";
        }

        public void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            try { wake.Set(); } catch { }
        }

        public void Wake()
        {
            try { wake.Set(); } catch { }
        }

        public void CastNow()
        {
            forceCast = true;
            LastStatus = "Casting...";
            Wake();
        }

        void Loop()
        {
            while (running)
            {
                try
                {
                    if (!Enabled)
                    {
                        LastStatus = "Off";
                    }
                    else if (string.IsNullOrEmpty(DeviceIp))
                    {
                        LastStatus = "Set display IP";
                    }
                    else if (string.IsNullOrEmpty(Url))
                    {
                        LastStatus = "Starting server...";
                    }
                    else
                    {
                        Cycle();
                    }
                }
                catch (Exception ex)
                {
                    LastStatus = "Error: " + ex.Message;
                }
                wake.WaitOne(120000, false);
            }
        }

        void Cycle()
        {
            bool force = forceCast;
            forceCast = false;
            if (force)
            {
                Cast();
                return;
            }

            string info = RunCatt("info", 25000);
            if (info == null) return;

            bool active = info.IndexOf("DashCast", StringComparison.OrdinalIgnoreCase) >= 0;
            bool standby = info.IndexOf("is_stand_by: True", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!active) Cast();
            else if (standby && (DateTime.UtcNow - lastCast).TotalMinutes >= 4.0) Cast();
            else LastStatus = "Casting";
        }

        void Cast()
        {
            LastStatus = "Casting...";
            string outp = RunCatt("cast_site \"" + Url + "\"", 45000);
            if (outp != null)
            {
                lastCast = DateTime.UtcNow;
                LastStatus = "Casting";
            }
        }

        string RunCatt(string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(CattPath);
                psi.Arguments = "-d " + DeviceIp + " " + args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        LastStatus = "Timeout";
                        return null;
                    }
                    if (p.ExitCode != 0)
                    {
                        LastStatus = FirstLine(stderr, stdout);
                        return null;
                    }
                    return stdout + stderr;
                }
            }
            catch
            {
                LastStatus = "catt not found";
                return null;
            }
        }

        static string FirstLine(string a, string b)
        {
            string text = !string.IsNullOrEmpty(a) ? a : b;
            if (string.IsNullOrEmpty(text)) return "Display unreachable";
            string[] lines = text.Replace("\r", "").Split('\n');
            foreach (string l in lines)
            {
                string t = (l ?? "").Trim();
                if (t.Length == 0) continue;
                if (t.Length > 70) t = t.Substring(0, 70) + "...";
                return t;
            }
            return "Display unreachable";
        }
    }

    internal class DeckServer
    {
        TcpListener listener;
        Thread thread;
        public volatile bool Running;
        public int Port;
        public DeckConfig Config;
        public NowPlaying Now;
        public string LastError = "";
        public string PairingCode { get; private set; }
        readonly byte[] html;
        readonly Func<DeckButton, string> actionRunner;
        readonly string secretPath;
        byte[] authKey;
        readonly object pairingLock = new object();
        readonly Dictionary<string, int> pairingAttempts = new Dictionary<string, int>();
        const string AuthCookie = "strodeck_session";
        const int SessionLifetimeSeconds = 604800;
        const int MaxPairingAttempts = 5;

        static readonly string Stamp = GetStamp();

        static string GetStamp()
        {
            try { return File.GetLastWriteTimeUtc(typeof(DeckServer).Assembly.Location).Ticks.ToString(); }
            catch { return "1"; }
        }

        public DeckServer(int port, DeckConfig config)
            : this(port, config, null, DefaultSecretPath(), null)
        {
        }

        internal DeckServer(int port, DeckConfig config, Func<DeckButton, string> runner,
            string keyPath, string pairingCode)
        {
            Port = port;
            Config = config;
            Now = new NowPlaying();
            html = LoadHtml();
            actionRunner = runner;
            secretPath = keyPath;
            PairingCode = pairingCode ?? GeneratePairingCode();
        }

        static string DefaultSecretPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stro-Deck", "server.key");
        }

        static string GeneratePairingCode()
        {
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            uint value = BitConverter.ToUInt32(bytes, 0) % 100000000U;
            return value.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        }

        static byte[] LoadOrCreateKey(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] created = new byte[32];
                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(created);
                    stream.Write(created, 0, created.Length);
                    stream.Flush(true);
                    return created;
                }
            }
            catch (IOException)
            {
                if (!File.Exists(path)) throw;
                byte[] existing = File.ReadAllBytes(path);
                if (existing.Length != 32) throw new InvalidDataException("The local authorization key is invalid.");
                return existing;
            }
        }

        static byte[] Hmac(byte[] key, byte[] data)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key)) return hmac.ComputeHash(data);
        }

        static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null) return false;
            int diff = left.Length ^ right.Length;
            int length = Math.Max(left.Length, right.Length);
            for (int i = 0; i < length; i++)
            {
                byte a = i < left.Length ? left[i] : (byte)0;
                byte b = i < right.Length ? right[i] : (byte)0;
                diff |= a ^ b;
            }
            return diff == 0;
        }

        static bool FixedTimeEquals(string left, string right)
        {
            using (SHA256 sha = SHA256.Create())
                return FixedTimeEquals(sha.ComputeHash(Encoding.UTF8.GetBytes(left ?? "")),
                    sha.ComputeHash(Encoding.UTF8.GetBytes(right ?? "")));
        }

        static string Base64Url(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static byte[] FromBase64Url(string value)
        {
            string s = value.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        string CreateSessionToken()
        {
            byte[] payload = new byte[40];
            byte[] expiry = BitConverter.GetBytes(DateTime.UtcNow.AddSeconds(SessionLifetimeSeconds).Ticks);
            Buffer.BlockCopy(expiry, 0, payload, 0, expiry.Length);
            byte[] nonce = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(nonce);
            Buffer.BlockCopy(nonce, 0, payload, 8, nonce.Length);
            byte[] signature = Hmac(authKey, payload);
            byte[] token = new byte[payload.Length + signature.Length];
            Buffer.BlockCopy(payload, 0, token, 0, payload.Length);
            Buffer.BlockCopy(signature, 0, token, payload.Length, signature.Length);
            return Base64Url(token);
        }

        bool IsValidSession(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            try
            {
                byte[] value = FromBase64Url(token);
                if (value.Length != 72) return false;
                byte[] payload = new byte[40];
                byte[] supplied = new byte[32];
                Buffer.BlockCopy(value, 0, payload, 0, payload.Length);
                Buffer.BlockCopy(value, payload.Length, supplied, 0, supplied.Length);
                if (!FixedTimeEquals(Hmac(authKey, payload), supplied)) return false;
                long expiryTicks = BitConverter.ToInt64(payload, 0);
                return expiryTicks > DateTime.UtcNow.Ticks &&
                    expiryTicks <= DateTime.UtcNow.AddSeconds(SessionLifetimeSeconds + 60).Ticks;
            }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
        }

        static string CookieValue(string cookieHeader)
        {
            if (string.IsNullOrEmpty(cookieHeader)) return null;
            string[] cookies = cookieHeader.Split(';');
            foreach (string cookie in cookies)
            {
                int equals = cookie.IndexOf('=');
                if (equals > 0 && cookie.Substring(0, equals).Trim().Equals(AuthCookie, StringComparison.Ordinal))
                    return cookie.Substring(equals + 1).Trim();
            }
            return null;
        }

        static string JsonString(string body, string property)
        {
            if (body == null) return null;
            string key = "\"" + property + "\"";
            int i = body.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return null;
            int c = body.IndexOf(':', i + key.Length);
            if (c < 0) return null;
            int q1 = body.IndexOf('"', c + 1);
            if (q1 < 0) return null;
            int q2 = body.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return body.Substring(q1 + 1, q2 - q1 - 1);
        }

        bool IsAllowedHost(string host, IPAddress localAddress)
        {
            if (string.IsNullOrEmpty(host) || host.IndexOf('@') >= 0) return false;
            Uri uri;
            if (!Uri.TryCreate("http://" + host + "/", UriKind.Absolute, out uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != Port ||
                uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                return false;

            IPAddress address;
            if (IPAddress.TryParse(uri.Host, out address))
                return address.AddressFamily == AddressFamily.InterNetwork && address.Equals(localAddress);
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                localAddress.Equals(IPAddress.Loopback);
        }

        bool IsSameOrigin(string origin, string host, IPAddress localAddress)
        {
            if (string.IsNullOrEmpty(origin)) return false;
            Uri uri;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out uri) ||
                !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length != 0 ||
                uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
                return false;
            Uri hostUri;
            if (!Uri.TryCreate("http://" + host + "/", UriKind.Absolute, out hostUri)) return false;
            return uri.Host.Equals(hostUri.Host, StringComparison.OrdinalIgnoreCase) &&
                uri.Port == Port && hostUri.Port == Port && IsAllowedHost(host, localAddress);
        }

        bool AllowPairingAttempt(string remoteAddress, bool failedAttempt)
        {
            lock (pairingLock)
            {
                int attempts;
                pairingAttempts.TryGetValue(remoteAddress, out attempts);
                if (attempts >= MaxPairingAttempts) return false;
                if (failedAttempt)
                {
                    if (attempts == 0 && pairingAttempts.Count >= 1024) return false;
                    pairingAttempts[remoteAddress] = attempts + 1;
                }
                return true;
            }
        }

        static byte[] LoadHtml()
        {
            try
            {
                using (Stream s = typeof(DeckServer).Assembly.GetManifestResourceStream("deck.html"))
                {
                    if (s == null) return Encoding.UTF8.GetBytes("<h1>deck.html missing</h1>");
                    using (MemoryStream ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return Encoding.UTF8.GetBytes("<h1>deck.html missing</h1>");
            }
        }

        public bool Start()
        {
            try { authKey = LoadOrCreateKey(secretPath); }
            catch (Exception ex)
            {
                LastError = "Could not initialize local authorization: " + ex.Message;
                Running = false;
                return false;
            }
            for (int p = Port; p < Port + 20; p++)
            {
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Any, p);
                    l.Start();
                    listener = l;
                    Port = p;
                    Running = true;
                    if (Now != null) Now.Start();
                    thread = new Thread(AcceptLoop);
                    thread.IsBackground = true;
                    thread.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }
            }
            Running = false;
            return false;
        }

        public void Stop()
        {
            Running = false;
            if (Now != null) Now.Stop();
            try { if (listener != null) listener.Stop(); }
            catch { }
        }

        void AcceptLoop()
        {
            while (Running)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch
                {
                    if (!Running) break;
                    continue;
                }
                ThreadPool.QueueUserWorkItem(Handle, client);
            }
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        string StateJson()
        {
            DeckConfig c = Config;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"v\":\"").Append(Stamp).Append("\",\"title\":\"").Append(Esc(c.Title));
            sb.Append("\",\"bg\":\"").Append(Esc(c.Background));
            sb.Append("\",\"bgpos\":\"").Append(Esc(c.BackgroundPos));
            sb.Append("\",\"clock\":\"").Append(Esc(c.ClockStyle));
            sb.Append("\",\"clockfmt\":\"").Append(Esc(c.ClockFormat));
            sb.Append("\",\"cols\":").Append(c.Cols);
            sb.Append(",\"rows\":").Append(c.Rows);
            sb.Append(",\"buttons\":[");
            for (int i = 0; i < c.Buttons.Count; i++)
            {
                DeckButton b = c.Buttons[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(i);
                sb.Append("\",\"label\":\"").Append(Esc(b.Label));
                sb.Append("\",\"icon\":\"").Append(Esc(b.Icon));
                sb.Append("\",\"color\":\"").Append(Esc(b.Color));
                sb.Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static int FindHeaderEnd(byte[] data, int len)
        {
            for (int i = 0; i + 3 < len; i++)
            {
                if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
                    return i;
            }
            return -1;
        }

        static string JsonId(string body)
        {
            if (body == null) return null;
            int i = body.IndexOf("\"id\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int c = body.IndexOf(':', i);
            if (c < 0) return null;
            int q1 = body.IndexOf('"', c + 1);
            if (q1 < 0) return null;
            int q2 = body.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return body.Substring(q1 + 1, q2 - q1 - 1);
        }

        bool IsAuthorizedAction(Dictionary<string, string> headers, string host, IPAddress localAddress)
        {
            string token = headers.ContainsKey("Cookie") ? CookieValue(headers["Cookie"]) : null;
            return headers.ContainsKey("Origin") &&
                IsSameOrigin(headers["Origin"], host, localAddress) &&
                IsValidSession(token);
        }

        void Handle(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 8000;
                    NetworkStream ns = client.GetStream();
                    MemoryStream ms = new MemoryStream();
                    byte[] buf = new byte[4096];
                    int headerEnd = -1;
                    while (headerEnd < 0)
                    {
                        int n = ns.Read(buf, 0, buf.Length);
                        if (n <= 0) return;
                        ms.Write(buf, 0, n);
                        if (ms.Length > 1024 * 1024) return;
                        headerEnd = FindHeaderEnd(ms.GetBuffer(), (int)ms.Length);
                    }
                    byte[] data = ms.ToArray();
                    string head = Encoding.ASCII.GetString(data, 0, headerEnd);
                    string[] lines = head.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;
                    string[] first = lines[0].Split(' ');
                    if (first.Length < 2) return;
                    string method = first[0].ToUpperInvariant();
                    string path = first[1];
                    int qi = path.IndexOf('?');
                    if (qi >= 0) path = path.Substring(0, qi);

                    int contentLength = 0;
                    Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int c = lines[i].IndexOf(':');
                        if (c <= 0) continue;
                        string name = lines[i].Substring(0, c).Trim();
                        string value = lines[i].Substring(c + 1).Trim();
                        headers[name] = value;
                        if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!int.TryParse(value, out contentLength) || contentLength < 0 || contentLength > 8192)
                            {
                                Send(ns, 413, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("request too large"));
                                return;
                            }
                        }
                    }

                    int bodyStart = headerEnd + 4;
                    while ((int)ms.Length - bodyStart < contentLength)
                    {
                        int n = ns.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        ms.Write(buf, 0, n);
                        data = ms.ToArray();
                    }
                    string body = "";
                    if (contentLength > 0)
                    {
                        int avail = Math.Min(contentLength, Math.Max(0, data.Length - bodyStart));
                        body = Encoding.UTF8.GetString(data, bodyStart, avail);
                    }

                    IPAddress localAddress = ((IPEndPoint)client.Client.LocalEndPoint).Address;
                    string host = headers.ContainsKey("Host") ? headers["Host"] : null;
                    if (!IsAllowedHost(host, localAddress))
                    {
                        Send(ns, 403, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("invalid host"));
                    }
                    else if (method == "GET" && (path == "/" || path == "/index.html" || path == "/deck"))
                    {
                        Send(ns, 200, "text/html; charset=utf-8", html);
                    }
                    else if (method == "GET" && path == "/api/state")
                    {
                        Send(ns, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(StateJson()));
                    }
                    else if (method == "POST" && path == "/api/pair")
                    {
                        if (!headers.ContainsKey("Origin") ||
                            !IsSameOrigin(headers["Origin"], host, localAddress))
                        {
                            Send(ns, 403, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Pairing requires the same origin\"}"));
                        }
                        else if (!headers.ContainsKey("Content-Type") ||
                            !headers["Content-Type"].StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                        {
                            Send(ns, 400, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Expected JSON\"}"));
                        }
                        else
                        {
                            string remoteAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                            string code = JsonString(body, "code");
                            bool validCode = FixedTimeEquals(code, PairingCode);
                            if (!AllowPairingAttempt(remoteAddress, !validCode) || !validCode)
                            {
                                Send(ns, 403, "application/json; charset=utf-8",
                                    Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Invalid or unavailable pairing code\"}"));
                            }
                            else
                            {
                                string token = CreateSessionToken();
                                string cookie = AuthCookie + "=" + token +
                                    "; Path=/; Max-Age=" + SessionLifetimeSeconds +
                                    "; HttpOnly; SameSite=Strict";
                                Send(ns, 200, "application/json; charset=utf-8",
                                    Encoding.UTF8.GetBytes("{\"ok\":true}"), cookie);
                            }
                        }
                    }
                    else if (method == "POST" && path == "/api/press")
                    {
                        if (!IsAuthorizedAction(headers, host, localAddress))
                        {
                            Send(ns, 403, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Pair this browser with the desktop app\"}"));
                        }
                        else if (!headers.ContainsKey("Content-Type") ||
                            !headers["Content-Type"].StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                        {
                            Send(ns, 400, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Expected JSON\"}"));
                        }
                        else
                        {
                            string id = JsonId(body);
                            int idx;
                            bool ok = false;
                            string msg = "Unknown button";
                            if (id != null && int.TryParse(id, out idx) && idx >= 0 && idx < Config.Buttons.Count)
                            {
                                DeckButton button = Config.Buttons[idx];
                                if (actionRunner != null)
                                    msg = actionRunner(button);
                                else if (button.Action == "media" &&
                                    (button.Target == "play" || button.Target == "next" || button.Target == "prev"))
                                    msg = Now.Control(button.Target == "play" ? "toggle" : button.Target)
                                        ? "Media control sent" : "No active media session";
                                else
                                    msg = Runner.Run(button);
                                ok = !msg.StartsWith("Error") && !msg.StartsWith("Unknown action") && !msg.StartsWith("No ");
                            }
                            string json = "{\"ok\":" + (ok ? "true" : "false") + ",\"msg\":\"" + Esc(msg) + "\"}";
                            Send(ns, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                        }
                    }
                    else if (method == "GET" && path == "/api/now")
                    {
                        string token = headers.ContainsKey("Cookie") ? CookieValue(headers["Cookie"]) : null;
                        if (!IsValidSession(token))
                            Send(ns, 403, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"on\":0}"));
                        else
                            Send(ns, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(Now.Json()));
                    }
                    else if (method == "GET" && path == "/api/cover")
                    {
                        string token = headers.ContainsKey("Cookie") ? CookieValue(headers["Cookie"]) : null;
                        if (!IsValidSession(token))
                        {
                            Send(ns, 403, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Pair this browser"));
                        }
                        else
                        {
                            byte[] cover = Now.CoverBytes;
                            if (cover == null || cover.Length == 0)
                                Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("no cover"));
                            else
                                Send(ns, 200, Now.CoverMime, cover);
                        }
                    }
                    else if (method == "POST" && path == "/api/media")
                    {
                        if (!IsAuthorizedAction(headers, host, localAddress))
                        {
                            Send(ns, 403, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Pair this browser with the desktop app\"}"));
                        }
                        else if (!headers.ContainsKey("Content-Type") ||
                            !headers["Content-Type"].StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                        {
                            Send(ns, 400, "application/json; charset=utf-8",
                                Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Expected JSON\"}"));
                        }
                        else
                        {
                            string command = JsonString(body, "cmd");
                            if (command != "toggle" && command != "next" && command != "prev")
                            {
                                Send(ns, 400, "application/json; charset=utf-8",
                                    Encoding.UTF8.GetBytes("{\"ok\":false,\"msg\":\"Unknown media command\"}"));
                            }
                            else
                            {
                                bool ok = Now.Control(command);
                                Send(ns, 200, "application/json; charset=utf-8",
                                    Encoding.UTF8.GetBytes("{\"ok\":" + (ok ? "true" : "false") + "}"));
                            }
                        }
                    }
                    else if (method == "GET" && path == "/api/ping")
                    {
                        Send(ns, 200, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("ok"));
                    }
                    else if (method == "GET" && path.StartsWith("/bg/"))
                    {
                        string name = Uri.UnescapeDataString(path.Substring(4));
                        if (name.Length == 0 || name.IndexOf("..") >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
                        {
                            Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                        }
                        else
                        {
                            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backgrounds", name);
                            if (File.Exists(file)) Send(ns, 200, MimeFor(name), File.ReadAllBytes(file));
                            else Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                        }
                    }
                    else if (method == "GET" && path.StartsWith("/icons/"))
                    {
                        string name = Uri.UnescapeDataString(path.Substring(7));
                        if (name.Length == 0 || name.IndexOf("..") >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
                        {
                            Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                        }
                        else
                        {
                            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", name);
                            if (File.Exists(file)) Send(ns, 200, MimeFor(name), File.ReadAllBytes(file));
                            else Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                        }
                    }
                    else
                    {
                        Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("not found"));
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = "Request failed: " + ex.GetType().Name;
            }
        }

        static string MimeFor(string fileName)
        {
            string e = Path.GetExtension(fileName).ToLowerInvariant();
            switch (e)
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
            }
            return "application/octet-stream";
        }

        static void Send(NetworkStream ns, int code, string contentType, byte[] body)
        {
            Send(ns, code, contentType, body, null);
        }

        static void Send(NetworkStream ns, int code, string contentType, byte[] body, string setCookie)
        {
            string status;
            if (code == 200) status = "OK";
            else if (code == 400) status = "Bad Request";
            else if (code == 401) status = "Unauthorized";
            else if (code == 403) status = "Forbidden";
            else if (code == 404) status = "Not Found";
            else if (code == 413) status = "Payload Too Large";
            else status = "Error";
            StringBuilder h = new StringBuilder();
            h.Append("HTTP/1.1 ").Append(code).Append(' ').Append(status).Append("\r\n");
            h.Append("Content-Type: ").Append(contentType).Append("\r\n");
            h.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            h.Append("Cache-Control: no-store\r\n");
            h.Append("X-Content-Type-Options: nosniff\r\n");
            h.Append("Referrer-Policy: no-referrer\r\n");
            if (!string.IsNullOrEmpty(setCookie)) h.Append("Set-Cookie: ").Append(setCookie).Append("\r\n");
            h.Append("Connection: close\r\n\r\n");
            byte[] head = Encoding.ASCII.GetBytes(h.ToString());
            ns.Write(head, 0, head.Length);
            ns.Write(body, 0, body.Length);
            ns.Flush();
        }
    }
}
