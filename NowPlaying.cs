using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StroDeck
{
    /// <summary>
    /// Reads the current media session through Windows SMTC (native, in-process,
    /// no helper process) and exposes play/pause/skip controls.
    /// </summary>
    internal class NowPlaying
    {
        readonly object sync = new object();

        string title = "";
        string artist = "";
        string album = "";
        string trackKey = "";
        bool on;
        bool playing;
        double pos;
        double end;
        byte[] cover;
        string coverMime = "image/jpeg";
        long coverStamp;
        DateTime posAt = DateTime.UtcNow;

        Thread thread;
        volatile bool running;
        object manager;
        volatile object session;

        public void Start()
        {
            if (thread != null && thread.IsAlive)
            {
                running = true;
                return;
            }
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
        }

        void Loop()
        {
            while (running)
            {
                try { Poll(); }
                catch (Exception ex) { Log("poll", ex); }
                for (int i = 0; i < 14 && running; i++) Thread.Sleep(100);
            }
        }

        static void Log(string where, Exception ex)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media.log");
                FileInfo fi = new FileInfo(file);
                if (fi.Exists && fi.Length > 200000) fi.Delete();
                File.AppendAllText(file, DateTime.Now.ToString("u") + "  " + where + "  " +
                    ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            }
            catch { }
        }

        // ---------- snapshot for HTTP ----------

        public byte[] CoverBytes
        {
            get { lock (sync) { return cover; } }
        }

        public string CoverMime
        {
            get { lock (sync) { return coverMime; } }
        }

        public string Json()
        {
            lock (sync)
            {
                double p = pos;
                if (playing && end > 0)
                {
                    p += (DateTime.UtcNow - posAt).TotalSeconds;
                    if (p > end) p = end;
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"on\":").Append(on ? "1" : "0");
                sb.Append(",\"playing\":").Append(playing ? "1" : "0");
                sb.Append(",\"title\":\"").Append(Esc(title));
                sb.Append("\",\"artist\":\"").Append(Esc(artist));
                sb.Append("\",\"album\":\"").Append(Esc(album));
                sb.Append("\",\"pos\":").Append(p.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(",\"end\":").Append(end.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(",\"cover\":").Append(coverStamp);
                sb.Append('}');
                return sb.ToString();
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

        // ---------- controls ----------

        public bool Control(string cmd)
        {
            if (cmd != "toggle" && cmd != "next" && cmd != "prev") return false;
            object s = session;
            if (s == null) return false;

            string name = "TryTogglePlayPauseAsync";
            if (cmd == "next") name = "TrySkipNextAsync";
            else if (cmd == "prev") name = "TrySkipPreviousAsync";

            try
            {
                MethodInfo m = s.GetType().GetMethod(name);
                if (m == null) return false;
                object r = Await(m.Invoke(s, null), m.ReturnType);
                bool ok = r is bool ? (bool)r : true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Thread.Sleep(400); Poll(); }
                    catch { }
                });
                return ok;
            }
            catch
            {
                return false;
            }
        }

        // ---------- polling ----------

        void Poll()
        {
            EnsureManager();
            if (manager == null)
            {
                SetIdle();
                return;
            }

            IEnumerable sessions;
            try
            {
                sessions = (IEnumerable)manager.GetType().GetMethod("GetSessions").Invoke(manager, null);
            }
            catch
            {
                manager = null;
                session = null;
                SetIdle();
                return;
            }

            object best = null;
            bool bestSpotify = false;
            bool bestPlaying = false;
            try
            {
                foreach (object s in sessions)
                {
                    Type st = s.GetType();
                    string app = Convert.ToString(st.GetProperty("SourceAppUserModelId").GetValue(s, null)) ?? "";
                    bool spot = app.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isPlaying = false;
                    try
                    {
                        object info = st.GetMethod("GetPlaybackInfo").Invoke(s, null);
                        object status = info.GetType().GetProperty("PlaybackStatus").GetValue(info, null);
                        isPlaying = Convert.ToString(status) == "Playing";
                    }
                    catch { }

                    bool take = best == null
                        || (spot && !bestSpotify)
                        || (spot == bestSpotify && isPlaying && !bestPlaying);
                    if (take)
                    {
                        best = s;
                        bestSpotify = spot;
                        bestPlaying = isPlaying;
                    }
                }
            }
            catch { }

            if (best == null)
            {
                session = null;
                SetIdle();
                return;
            }

            try
            {
                Type bt = best.GetType();
                object info = bt.GetMethod("GetPlaybackInfo").Invoke(best, null);
                object status = info.GetType().GetProperty("PlaybackStatus").GetValue(info, null);
                bool isPlaying = Convert.ToString(status) == "Playing";

                MethodInfo propsMethod = bt.GetMethod("TryGetMediaPropertiesAsync");
                object p = Await(propsMethod.Invoke(best, null), propsMethod.ReturnType);
                if (p == null)
                {
                    SetIdle();
                    return;
                }
                Type pt = p.GetType();
                string t = Convert.ToString(pt.GetProperty("Title").GetValue(p, null)) ?? "";
                string a = Convert.ToString(pt.GetProperty("Artist").GetValue(p, null)) ?? "";
                string al = Convert.ToString(pt.GetProperty("AlbumTitle").GetValue(p, null)) ?? "";

                if (t.Trim().Length == 0)
                {
                    session = null;
                    SetIdle();
                    return;
                }

                double pp = 0, pe = 0;
                try
                {
                    object timeline = bt.GetMethod("GetTimelineProperties").Invoke(best, null);
                    Type tt = timeline.GetType();
                    pp = ToSeconds(tt.GetProperty("Position").GetValue(timeline, null));
                    pe = ToSeconds(tt.GetProperty("EndTime").GetValue(timeline, null));
                }
                catch { }

                string key = t + "\n" + a + "\n" + al;
                byte[] cov = null;
                string mime = "image/jpeg";
                lock (sync)
                {
                    if (key == trackKey)
                    {
                        cov = cover;
                        mime = coverMime;
                    }
                }
                if (cov == null)
                {
                    try
                    {
                        PropertyInfo thumbProp = pt.GetProperty("Thumbnail");
                        object thumb = thumbProp.GetValue(p, null);
                        if (thumb != null)
                        {
                            byte[] fetched = FetchCover(thumb, thumbProp.PropertyType, out mime);
                            if (fetched != null) cov = fetched;
                        }
                    }
                    catch (Exception ex) { Log("thumb", ex); }
                }

                session = best;
                lock (sync)
                {
                    title = t;
                    artist = a;
                    album = al;
                    on = true;
                    playing = isPlaying;
                    pos = pp;
                    end = pe;
                    posAt = DateTime.UtcNow;
                    if (key != trackKey)
                    {
                        trackKey = key;
                        cover = cov;
                        coverMime = mime;
                        coverStamp++;
                    }
                    else if (cov != null && cover == null)
                    {
                        cover = cov;
                        coverMime = mime;
                        coverStamp++;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("poll-props", ex);
                SetIdle();
            }
        }

        void SetIdle()
        {
            lock (sync)
            {
                on = false;
                playing = false;
                title = "";
                artist = "";
                album = "";
                trackKey = "";
            }
        }

        static double ToSeconds(object ticks)
        {
            try
            {
                if (ticks == null) return 0;
                Type t = ticks.GetType();
                PropertyInfo sec = t.GetProperty("TotalSeconds");
                if (sec != null) return Convert.ToDouble(sec.GetValue(ticks, null));
                PropertyInfo tm = t.GetProperty("Ticks");
                if (tm != null) return Convert.ToInt64(tm.GetValue(ticks, null)) / 10000000.0;
            }
            catch { }
            return 0;
        }

        static byte[] FetchCover(object thumb, Type thumbType, out string mime)
        {
            mime = "image/jpeg";
            try
            {
                if (thumb == null) { Log("cover", new Exception("thumb null")); return null; }
                MethodInfo openRead = thumbType.GetMethod("OpenReadAsync");
                if (openRead == null) { Log("cover", new Exception("OpenReadAsync missing on " + thumbType.FullName)); return null; }
                object stream = Await(openRead.Invoke(thumb, null), openRead.ReturnType);
                if (stream == null) { Log("cover", new Exception("stream null")); return null; }

                Type ext = ExtensionsType();
                if (ext == null) return null;
                MethodInfo asStream = null;
                Type streamExt = WinRTAssembly() == null ? null : WinRTAssembly().GetType("System.IO.WindowsRuntimeStreamExtensions", false);
                if (streamExt != null)
                {
                    foreach (MethodInfo m in streamExt.GetMethods())
                    {
                        if (m.Name != "AsStreamForRead") continue;
                        if (m.GetParameters().Length != 1) continue;
                        asStream = m;
                        break;
                    }
                }
                if (asStream == null) { Log("cover", new Exception("AsStreamForRead missing")); return null; }

                Stream s = (Stream)asStream.Invoke(null, new object[] { stream });
                if (s == null) { Log("cover", new Exception("stream cast null")); return null; }

                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, n);
                        if (ms.Length > 8 * 1024 * 1024) break;
                    }
                    data = ms.ToArray();
                }
                try { s.Dispose(); } catch { }
                if (data.Length < 16) return null;

                if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) mime = "image/png";
                else if (data[0] == 0xFF && data[1] == 0xD8) mime = "image/jpeg";
                else if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46) mime = "image/webp";
                else if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) mime = "image/gif";
                else if (data[0] == 0x42 && data[1] == 0x4D) mime = "image/bmp";
                return data;
            }
            catch (Exception ex)
            {
                Log("cover", ex);
                return null;
            }
        }

        // ---------- WinRT plumbing (reflection only, no SDK reference needed) ----------

        void EnsureManager()
        {
            if (manager != null) return;
            try
            {
                Type mt = Type.GetType(
                    "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime", false);
                if (mt == null)
                {
                    Log("type", new Exception("SMTC manager type not found"));
                    return;
                }
                MethodInfo request = mt.GetMethod("RequestAsync");
                if (request == null) return;
                manager = Await(request.Invoke(null, null), request.ReturnType);
            }
            catch (Exception ex)
            {
                Log("manager", ex);
                manager = null;
            }
        }

        static Type extensionsType;
        static Assembly winrtAssembly;
        static bool winrtTried;

        static Assembly WinRTAssembly()
        {
            if (winrtAssembly != null) return winrtAssembly;
            if (winrtTried) return null;
            winrtTried = true;
            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string[] candidates = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System.Runtime.WindowsRuntime.dll"),
                    Path.Combine(win, @"Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll"),
                    Path.Combine(win, @"Microsoft.NET\Framework\v4.0.30319\System.Runtime.WindowsRuntime.dll")
                };
                foreach (string c in candidates)
                {
                    if (!File.Exists(c)) continue;
                    winrtAssembly = Assembly.LoadFrom(c);
                    if (winrtAssembly != null) return winrtAssembly;
                }
            }
            catch (Exception ex) { Log("winrt-load", ex); }
            return null;
        }

        static Type ExtensionsType()
        {
            if (extensionsType != null) return extensionsType;
            try
            {
                Type t = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime", false);
                if (t != null) return extensionsType = t;
                Assembly asm = WinRTAssembly();
                if (asm != null) extensionsType = asm.GetType("System.WindowsRuntimeSystemExtensions", false);
            }
            catch (Exception ex) { Log("ext-load", ex); }
            return extensionsType;
        }

        static object Await(object op, Type declared)
        {
            if (op == null || declared == null || !declared.IsGenericType) return null;
            Type resultType = declared.GetGenericArguments()[0];

            Type ext = ExtensionsType();
            if (ext == null)
            {
                Log("ext", new Exception("System.WindowsRuntimeSystemExtensions not found"));
                return null;
            }

            MethodInfo asTask = null;
            foreach (MethodInfo m in ext.GetMethods())
            {
                if (m.Name != "AsTask" || !m.IsGenericMethod) continue;
                if (m.GetGenericArguments().Length != 1) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 1) continue;
                Type p0 = ps[0].ParameterType;
                if (!p0.IsGenericType) continue;
                if (p0.GetGenericTypeDefinition().FullName == "Windows.Foundation.IAsyncOperation`1")
                {
                    asTask = m;
                    break;
                }
            }
            if (asTask == null) return null;

            try
            {
                object task = asTask.MakeGenericMethod(resultType).Invoke(null, new object[] { op });
                Task t = task as Task;
                if (t == null) return null;
                if (!t.Wait(8000)) return null;
                return task.GetType().GetProperty("Result").GetValue(task, null);
            }
            catch (Exception ex)
            {
                Log("await " + resultType.FullName, ex);
                return null;
            }
        }
    }
}
