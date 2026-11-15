// wavelink-remote-play
// Steam Remote Play records one playback device. Elgato Wave Link spreads your programs over several
// (System, Game, Browser, Voice chat...), so most of them never reach the stream. While Steam is recording,
// this tool points the programs on those devices at the one Steam records, so they arrive there in whatever
// layout the stream has, and copies over what is left. Afterwards everything goes back. See README.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IntPtr client);
    void UnregisterEndpointNotificationCallback(IntPtr client);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    void OpenPropertyStore(int access, out IntPtr props);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out int state);
}

[Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioClient
{
    void Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, ref Guid sessionGuid);
    void GetBufferSize(out uint frames);
    void GetStreamLatency(out long latency);
    void GetCurrentPadding(out uint frames);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
    void GetMixFormat(out IntPtr format);
    void GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    void Start();
    void Stop();
    void Reset();
    void SetEventHandle(IntPtr handle);
    void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}

[Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioCaptureClient
{
    void GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
    void ReleaseBuffer(uint frames);
    void GetNextPacketSize(out uint frames);
}

[Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioRenderClient
{
    void GetBuffer(uint frames, out IntPtr data);
    void ReleaseBuffer(uint frames, uint flags);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr notify);
    void UnregisterControlChangeNotify(IntPtr notify);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float db, ref Guid context);
    void SetMasterVolumeLevelScalar(float level, ref Guid context);
    void GetMasterVolumeLevel(out float db);
    void GetMasterVolumeLevelScalar(out float level);
}

[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionManager2
{
    void GetAudioSessionControl(ref Guid session, uint flags, out IntPtr control);
    void GetSimpleAudioVolume(ref Guid session, uint flags, out IntPtr volume);
    void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
}

[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionEnumerator
{
    void GetCount(out int count);
    void GetSession(int index, out IAudioSessionControl2 session);
}

[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl2
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
    void GetGroupingParam(out Guid group);
    void SetGroupingParam(ref Guid group, ref Guid context);
    void RegisterAudioSessionNotification(IntPtr notify);
    void UnregisterAudioSessionNotification(IntPtr notify);
    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetProcessId(out uint pid);
}

// The interface behind Windows' "App volume and device preferences" page: which playback device a single
// program uses. It isn't documented; the layout is the one EarTrumpet uses. Two ids, same methods: Windows
// changed the id with 21H2.
[Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
interface IAudioPolicyConfig
{
    int s01(); int s02(); int s03(); int s04(); int s05(); int s06(); int s07(); int s08(); int s09(); int s10();
    int s11(); int s12(); int s13(); int s14(); int s15(); int s16(); int s17(); int s18(); int s19();
    [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint pid, int flow, int role, IntPtr deviceId);
    [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint pid, int flow, int role, [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
    [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
}

[Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
interface IAudioPolicyConfigOld
{
    int s01(); int s02(); int s03(); int s04(); int s05(); int s06(); int s07(); int s08(); int s09(); int s10();
    int s11(); int s12(); int s13(); int s14(); int s15(); int s16(); int s17(); int s18(); int s19();
    [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint pid, int flow, int role, IntPtr deviceId);
    [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint pid, int flow, int role, [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
    [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorCom { }

class Endpoint
{
    public string Guid;   // e.g. {471386ae-d671-4b9b-8249-349f336c17b3}
    public string Name;   // e.g. System
    public string Device; // e.g. Elgato Wave XLR Pro
    public string FullId { get { return "{0.0.0.00000000}." + Guid; } }
    public override string ToString() { return Name + " (" + Device + ")"; }
}

static class Relay
{
    const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    const int AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = unchecked((int)0x80000000);
    const int AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    const uint AUDCLNT_BUFFERFLAGS_SILENT = 2;

    static readonly Guid IidAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    static readonly Guid IidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    static readonly Guid IidRender = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
    static readonly Guid IidEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    static readonly Guid IidSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    static bool lastActive;
    static bool verbose;
    static volatile bool stop;          // asks the worker thread to finish
    static volatile float Level;        // peak of the last 100 ms, 0..1 (for --verbose)
    static volatile string Status = ""; // one line for the window
    static volatile bool Relaying;      // a session is copying audio right now

    // Undo each source device's own volume fader, so the stream gets full level whatever the fader says.
    // (Wave Link's channel faders are exactly these Windows device volumes.)
    static volatile bool FullVolume = true;
    static volatile bool OpenBigPicture;    // open Steam Big Picture on the PC when a stream starts
    static volatile bool Surround = true;   // while streaming, programs play straight into Steam's device
    static volatile string Warning = "";    // one line for the window, when the setup keeps the stream stereo

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);

    static void Log(string msg)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
        Console.WriteLine(line);
        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wavelink-remote-play.log"), line + Environment.NewLine); } catch { }
    }

    // ---- device lookup (read from the registry, no COM enumeration needed) ----

    static List<Endpoint> ActiveRenderEndpoints()
    {
        var list = new List<Endpoint>();
        using (var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"))
        {
            if (root == null) return list;
            foreach (string name in root.GetSubKeyNames())
            {
                using (var k = root.OpenSubKey(name))
                {
                    if (k == null || !(k.GetValue("DeviceState") is int) || (int)k.GetValue("DeviceState") != 1) continue;
                    using (var p = k.OpenSubKey("Properties"))
                    {
                        if (p == null) continue;
                        string n = p.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                        string d = p.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                        if (n == null) continue;
                        list.Add(new Endpoint { Guid = name, Name = n, Device = d ?? "" });
                    }
                }
            }
        }
        return list;
    }

    static string NormGuid(string s)
    {
        return s.Trim().Trim('{', '}').ToLowerInvariant();
    }

    static Endpoint ByGuid(List<Endpoint> all, string wanted)
    {
        string w = NormGuid(wanted);
        foreach (var e in all) if (NormGuid(e.Guid) == w) return e;
        return null;
    }

    static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }

    // ---- stream detection ----

    static string LogPath()
    {
        string steam = null;
        try { steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string; } catch { }
        if (string.IsNullOrEmpty(steam)) steam = @"C:\Program Files (x86)\Steam";
        return Path.Combine(steam.Replace('/', '\\'), "logs", "streaming_log.txt");
    }

    // Active if the last marker in Steam's streaming log is "Starting", not "Stopped".
    static bool StreamActive(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                int n = (int)Math.Min(fs.Length, 262144);
                fs.Seek(-n, SeekOrigin.End);
                var buf = new byte[n];
                int got = fs.Read(buf, 0, n);
                string text = Encoding.UTF8.GetString(buf, 0, got);
                // Starting from the desktop writes "Starting desktop stream". Starting a game from the library
                // doesn't: there Steam logs "Streaming started to ..." and switches the activity to Game.
                // Switching between the two shows up as a "Stopped desktop stream" right before a new activity
                // line, so only a "Stopped" with nothing after it counts as the end.
                // (SecureDesktop is deliberately not a start marker: a real session end looks like "Stopped"
                // followed by a SecureDesktop activity, with nothing after it.)
                int a = Math.Max(
                    Math.Max(text.LastIndexOf(">>> Starting desktop stream", StringComparison.Ordinal),
                             text.LastIndexOf("Streaming started to ", StringComparison.Ordinal)),
                    Math.Max(text.LastIndexOf("setting activity to k_EStreamActivityGame", StringComparison.Ordinal),
                             text.LastIndexOf("setting activity to k_EStreamActivityDesktop", StringComparison.Ordinal)));
                // "Lost Video Stream" is what Steam writes when the whole session ends. It never shows up while you
                // just switch between desktop and game, so it's a reliable end marker for game streams, which
                // otherwise end without any "Stopped".
                int b = Math.Max(text.LastIndexOf(">>> Stopped desktop stream", StringComparison.Ordinal),
                                 text.LastIndexOf("Lost Video Stream", StringComparison.Ordinal));
                if (a < 0 && b < 0) return lastActive; // markers older than what we read: keep the last state
                lastActive = a > b;
                // Steam writes an "Audio source" line about once a minute while streaming. If the newest stream
                // line (that or a start marker) is much older than that, Steam probably crashed mid-stream and
                // never wrote an end. The file's own timestamp isn't used: starting a game on the PC makes Steam
                // write unrelated lines into this log and would keep the "stream" alive.
                if (lastActive)
                {
                    DateTime t = LastStamp(text, "Audio source [");
                    foreach (string m in new[] { "Streaming started to ", ">>> Starting desktop stream", "setting activity to k_EStreamActivity" })
                    {
                        DateTime x = LastStamp(text, m);
                        if (x > t) t = x;
                    }
                    if (t != DateTime.MinValue && (DateTime.Now - t).TotalSeconds > 100) lastActive = false;
                }
            }
        }
        catch { }
        return lastActive;
    }

    // Time of the newest log line containing the marker ("[2026-11-03 12:22:42][...] text"), or MinValue.
    static DateTime LastStamp(string text, string marker)
    {
        int i = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return DateTime.MinValue;
        int s = text.LastIndexOf('\n', i) + 1;
        DateTime t;
        if (text.Length >= s + 20 && text[s] == '[' &&
            DateTime.TryParseExact(text.Substring(s + 1, 19), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out t))
            return t;
        return DateTime.MinValue;
    }

    // The last "Game Recording - game stopped" line of Steam's streaming log, or null. Steam writes it whenever a
    // game it knows about exits while streaming.
    static string LastGameStop(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                int n = (int)Math.Min(fs.Length, 262144);
                fs.Seek(-n, SeekOrigin.End);
                var buf = new byte[n];
                int got = fs.Read(buf, 0, n);
                string text = Encoding.UTF8.GetString(buf, 0, got);
                int i = text.LastIndexOf("Game Recording - game stopped", StringComparison.Ordinal);
                if (i < 0) return null;
                int s = text.LastIndexOf('\n', i) + 1;
                int e = text.IndexOf('\n', i);
                return (e < 0 ? text.Substring(s) : text.Substring(s, e - s)).Trim();
            }
        }
        catch { return null; }
    }

    // ---- stream detection, the direct way ----
    //
    // While a Remote Play stream runs, steam.exe keeps a loopback capture open on the device it records
    // ("Recording on device: ..." in its log). Windows lists that as an active audio session of steam.exe on that
    // device, from the moment the client connects until it disconnects, also through alt-tab and game starts.
    // That is exactly the condition this tool cares about - "Steam is recording the target right now" - so it is
    // the primary signal. Steam's log is only the fallback for when the sessions can't be read.

    static readonly Dictionary<uint, bool> steamPids = new Dictionary<uint, bool>();

    static bool IsSteam(uint pid)
    {
        if (pid == 0) return false;
        bool known;
        if (steamPids.TryGetValue(pid, out known)) return known;
        bool isSteam = false;
        try
        {
            using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                isSteam = string.Equals(p.ProcessName, "steam", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        if (steamPids.Count > 64) steamPids.Clear(); // pids get reused, don't remember them forever
        steamPids[pid] = isSteam;
        return isSteam;
    }

    // The processes that have an active audio session on a playback device. null: couldn't tell.
    static List<uint> ActiveSessionPids(Endpoint dev)
    {
        IMMDeviceEnumerator en = null;
        IMMDevice d = null;
        object mgr = null;
        IAudioSessionEnumerator list = null;
        try
        {
            en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            en.GetDevice(dev.FullId, out d);
            Guid iid = IidSessionManager2;
            d.Activate(ref iid, 23, IntPtr.Zero, out mgr);
            ((IAudioSessionManager2)mgr).GetSessionEnumerator(out list);
            int n;
            list.GetCount(out n);
            var pids = new List<uint>();
            for (int i = 0; i < n; i++)
            {
                IAudioSessionControl2 c;
                list.GetSession(i, out c);
                try
                {
                    int state;
                    c.GetState(out state);
                    if (state == 1) // active
                    {
                        uint pid;
                        c.GetProcessId(out pid);
                        pids.Add(pid);
                    }
                }
                finally { Marshal.ReleaseComObject(c); }
            }
            return pids;
        }
        catch { return null; }
        finally
        {
            if (list != null) Marshal.ReleaseComObject(list);
            if (mgr != null) Marshal.ReleaseComObject(mgr);
            if (d != null) Marshal.ReleaseComObject(d);
            if (en != null) Marshal.ReleaseComObject(en);
        }
    }

    // true/false: steam.exe has / doesn't have an active audio session on the device. null: couldn't tell.
    static bool? SteamRecording(Endpoint dev)
    {
        var pids = ActiveSessionPids(dev);
        if (pids == null) return null;
        foreach (uint pid in pids) if (IsSteam(pid)) return true;
        return false;
    }

    static bool detectBySession; // which signal answered last (for the grace period and the log)

    static bool StreamActiveNow(Endpoint target, string logPath)
    {
        bool? rec = SteamRecording(target);
        detectBySession = rec.HasValue;
        return rec.HasValue ? rec.Value : StreamActive(logPath);
    }

    static bool IsSteamDevice(Endpoint e)
    {
        return e.Device.IndexOf("Steam Streaming Speakers", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Wave Link's playback devices: every device of the driver that has one called "System". Empty when
    // there is no such driver (no Wave Link), or when "System" isn't unique and we'd have to guess.
    static List<Endpoint> ChannelDevices(List<Endpoint> all)
    {
        var systems = all.FindAll(e => e.Name == "System");
        if (systems.Count != 1) return new List<Endpoint>();
        return all.FindAll(e => e.Device == systems[0].Device);
    }

    // Where to play when nothing tells us: Steam's own device if there is one, else the one called "System".
    static Endpoint GuessTarget(List<Endpoint> all)
    {
        var steam = all.FindAll(IsSteamDevice);
        if (steam.Count == 1) return steam[0];
        var systems = all.FindAll(e => e.Name == "System");
        return systems.Count == 1 ? systems[0] : null;
    }

    // The automatic target: the playback device steam.exe is recording right now, or null.
    // Normally that is "Steam Streaming Speakers". It is a Wave Link channel instead when Steam itself is
    // assigned to that channel in Wave Link - Windows then tells Steam that this is the default device.
    static Endpoint FindRecorded(string logPath)
    {
        var all = ActiveRenderEndpoints();
        Endpoint found = null;
        bool readable = false;
        foreach (var e in all)
        {
            bool? rec = SteamRecording(e);
            if (!rec.HasValue) continue;
            readable = true;
            if (rec.Value && (found == null || IsSteamDevice(e))) found = e;
        }
        detectBySession = readable;
        if (readable) return found;
        return StreamActive(logPath) ? GuessTarget(all) : null;
    }

    static string Layout(int channels)
    {
        return channels == 2 ? "stereo" : channels == 6 ? "5.1" : channels == 8 ? "7.1" : channels + " ch";
    }

    // ---- audio ----

    static IAudioClient OpenClient(IMMDeviceEnumerator en, string id)
    {
        IMMDevice dev;
        en.GetDevice(id, out dev);
        object o;
        Guid iid = IidAudioClient;
        dev.Activate(ref iid, 23, IntPtr.Zero, out o);
        Marshal.ReleaseComObject(dev);
        return (IAudioClient)o;
    }

    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint ms);

    class Src
    {
        public Endpoint E;
        public IAudioClient Client;
        public IAudioCaptureClient Capture;
        public int Channels;
        public float[] Ring;    // queued audio, interleaved in the target's channel layout
        public int Read, Count; // in frames
        public bool Primed;     // has enough queued to be mixed without gaps
        public IAudioEndpointVolume Vol; // the device's volume fader, may be null
        public float Gain = 1f, GainTarget = 1f;
    }

    // Converts interleaved frames from sch channels to tch channels.
    static void ToLayout(float[] src, int sch, float[] dst, int tch, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            int si = i * sch, di = i * tch;
            if (sch == tch) { for (int c = 0; c < tch; c++) dst[di + c] = src[si + c]; }
            else if (sch == 1) { for (int c = 0; c < tch; c++) dst[di + c] = src[si]; }
            else if (tch == 2)
            {
                // surround -> stereo: fronts, plus centre and the rear/side pairs a bit quieter (LFE dropped)
                float l = src[si], r = src[si + 1];
                if (sch > 2) { l += 0.707f * src[si + 2]; r += 0.707f * src[si + 2]; }
                for (int c = 4; c + 1 < sch; c += 2) { l += 0.707f * src[si + c]; r += 0.707f * src[si + c + 1]; }
                dst[di] = l; dst[di + 1] = r;
            }
            else { for (int c = 0; c < tch; c++) dst[di + c] = c < sch ? src[si + c] : 0f; }
        }
    }

    static void Push(Src s, float[] data, int frames, int tch, int cap)
    {
        for (int i = 0; i < frames; i++)
        {
            if (s.Count == cap) { s.Read = (s.Read + 1) % cap; s.Count--; } // full: drop the oldest frame
            int w = ((s.Read + s.Count) % cap) * tch, d = i * tch;
            for (int c = 0; c < tch; c++) s.Ring[w + c] = data[d + c];
            s.Count++;
        }
    }

    static IAudioEndpointVolume OpenVolume(IMMDeviceEnumerator en, string id)
    {
        IMMDevice dev;
        en.GetDevice(id, out dev);
        object o;
        Guid iid = IidEndpointVolume;
        dev.Activate(ref iid, 23, IntPtr.Zero, out o);
        Marshal.ReleaseComObject(dev);
        return (IAudioEndpointVolume)o;
    }

    // One relay session: mixes the sources into the target. Returns when keepRunning() says false, throws on
    // device errors.
    static void RunSession(List<Endpoint> sources, Endpoint target, Func<bool> keepRunning)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        IAudioClient ren = null;
        IntPtr tfmt = IntPtr.Zero;
        var srcs = new List<Src>();
        timeBeginPeriod(1); // 1 ms timer, so the short sleeps below really are short
        try
        {
            ren = OpenClient(en, target.FullId);
            ren.GetMixFormat(out tfmt);
            int tch = Marshal.ReadInt16(tfmt, 2);
            int rate = Marshal.ReadInt32(tfmt, 4);
            if (Marshal.ReadInt16(tfmt, 14) != 32) throw new Exception("The target device doesn't use 32-bit float audio.");
            Guid session = Guid.Empty;
            ren.Initialize(0, 0, 2000000, 0, tfmt, ref session);
            object o;
            Guid g = IidRender;
            ren.GetService(ref g, out o);
            var render = (IAudioRenderClient)o;
            uint bufFrames;
            ren.GetBufferSize(out bufFrames);

            int cap = rate / 2; // half a second per source at most
            var names = new List<string>();
            foreach (var e in sources)
            {
                IntPtr sfmt = IntPtr.Zero;
                IAudioClient c = null;
                try
                {
                    c = OpenClient(en, e.FullId);
                    c.GetMixFormat(out sfmt);
                    int sch = Marshal.ReadInt16(sfmt, 2), srate = Marshal.ReadInt32(sfmt, 4), sbits = Marshal.ReadInt16(sfmt, 14);
                    if (srate != rate || sbits != 32)
                    {
                        Log("Skipping " + e + ": " + srate + " Hz, " + sbits + " bit doesn't match the target.");
                        Marshal.ReleaseComObject(c);
                        continue;
                    }
                    Guid s2 = Guid.Empty;
                    c.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 2000000, 0, sfmt, ref s2);
                    Guid gc = IidCapture;
                    object co;
                    c.GetService(ref gc, out co);
                    c.Start();
                    IAudioEndpointVolume vol = null;
                    try { vol = OpenVolume(en, e.FullId); } catch { }
                    srcs.Add(new Src { E = e, Client = c, Capture = (IAudioCaptureClient)co, Channels = sch, Ring = new float[cap * tch], Vol = vol });
                    names.Add(e.Name + (sch != tch ? " (" + sch + " ch)" : ""));
                }
                catch (Exception ex)
                {
                    Log("Skipping " + e + ": " + ex.Message);
                    if (c != null) try { Marshal.ReleaseComObject(c); } catch { }
                }
                finally { if (sfmt != IntPtr.Zero) Marshal.FreeCoTaskMem(sfmt); }
            }
            if (srcs.Count == 0) throw new Exception("None of the source devices could be opened.");

            // Gain per source: whatever undoes the device's own volume fader, so the stream gets full level.
            // Loopback audio comes after that fader, so at -9 dB we multiply by +9 dB (capped at +30 dB).
            Action updateGains = delegate
            {
                foreach (var s in srcs)
                {
                    float comp = 1f;
                    if (FullVolume && s.Vol != null)
                        try { float db; s.Vol.GetMasterVolumeLevel(out db); comp = (float)Math.Min(31.6, Math.Pow(10, -db / 20.0)); } catch { }
                    s.GainTarget = comp;
                }
            };
            updateGains();
            foreach (var s in srcs) s.Gain = s.GainTarget;

            ren.Start();
            Relaying = true;
            Log("Relaying " + string.Join(", ", names.ToArray()) + " -> " + target.Name + " (" + tch + " ch, " + rate + " Hz)");
            Status = "Relaying " + srcs.Count + (srcs.Count == 1 ? " channel" : " channels") + " into "
                + (IsSteamDevice(target) ? "Steam's device" : target.Name) + " (" + Layout(tch) + ")";

            int prime = rate / 50;            // 20 ms queued before a source joins the mix
            int want = rate * 30 / 1000;      // keep 30 ms in the target's buffer
            int maxQueue = rate * 100 / 1000; // more than this queued means we fell behind: catch up
            var tmp = new float[0];
            var conv = new float[0];
            var mix = new float[0];
            float peak = 0, peak5 = 0;
            var nextLevel = DateTime.UtcNow.AddMilliseconds(100);
            var nextLevelLog = DateTime.UtcNow.AddSeconds(5);
            while (keepRunning())
            {
                if (DateTime.UtcNow >= nextLevel)
                {
                    Level = peak;
                    if (peak > peak5) peak5 = peak;
                    peak = 0;
                    nextLevel = DateTime.UtcNow.AddMilliseconds(100);
                    updateGains();
                }
                if (verbose && DateTime.UtcNow >= nextLevelLog)
                {
                    Log("Level (last 5 s, 0..1): " + peak5.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                    peak5 = 0;
                    nextLevelLog = DateTime.UtcNow.AddSeconds(5);
                }

                // 1. collect what the sources have played since the last round
                int alive = 0;
                foreach (var s in srcs)
                {
                    if (s.Capture == null) continue;
                    try
                    {
                        uint packet;
                        s.Capture.GetNextPacketSize(out packet);
                        while (packet > 0)
                        {
                            IntPtr data; uint frames, flags; ulong dp, qp;
                            s.Capture.GetBuffer(out data, out frames, out flags, out dp, out qp);
                            int n = (int)frames;
                            if (conv.Length < n * tch) conv = new float[n * tch];
                            if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0) Array.Clear(conv, 0, n * tch);
                            else
                            {
                                if (tmp.Length < n * s.Channels) tmp = new float[n * s.Channels];
                                Marshal.Copy(data, tmp, 0, n * s.Channels);
                                ToLayout(tmp, s.Channels, conv, tch, n);
                                // apply the gain, gliding to a new value over this packet so changes don't click
                                float g0 = s.Gain, g1 = s.GainTarget;
                                for (int i = 0; i < n; i++)
                                {
                                    float gn = g0 + (g1 - g0) * i / n;
                                    for (int c = 0; c < tch; c++) conv[i * tch + c] *= gn;
                                }
                                s.Gain = g1;
                            }
                            s.Capture.ReleaseBuffer(frames);
                            Push(s, conv, n, tch, cap);
                            s.Capture.GetNextPacketSize(out packet);
                        }
                        alive++;
                    }
                    catch (Exception ex)
                    {
                        // one channel device going away shouldn't take the others down
                        Log("Lost " + s.E + ": " + ex.Message);
                        try { Marshal.ReleaseComObject(s.Client); } catch { }
                        s.Capture = null; s.Client = null; s.Count = 0; s.Primed = false;
                    }
                    if (!s.Primed && s.Count >= prime) s.Primed = true;
                }
                if (alive == 0) throw new Exception("All source devices are gone.");

                // 2. top the target's buffer up with the mix (silence where nothing is playing)
                uint pad;
                ren.GetCurrentPadding(out pad);
                int toWrite = Math.Min((int)(bufFrames - pad), want - (int)pad);
                if (toWrite > 0)
                {
                    int len = toWrite * tch;
                    if (mix.Length < len) mix = new float[len];
                    Array.Clear(mix, 0, len);
                    foreach (var s in srcs)
                    {
                        if (!s.Primed) continue;
                        int n = Math.Min(s.Count, toWrite);
                        for (int i = 0; i < n; i++)
                        {
                            int r = ((s.Read + i) % cap) * tch, d = i * tch;
                            for (int c = 0; c < tch; c++) mix[d + c] += s.Ring[r + c];
                        }
                        s.Read = (s.Read + n) % cap;
                        s.Count -= n;
                        if (s.Count == 0) s.Primed = false; // ran dry (or stopped playing): queue up again first
                        else if (s.Count > maxQueue) { int drop = s.Count - prime; s.Read = (s.Read + drop) % cap; s.Count -= drop; }
                    }
                    for (int i = 0; i < len; i++)
                    {
                        float v = mix[i];
                        if (v > 1f) mix[i] = v = 1f; else if (v < -1f) mix[i] = v = -1f;
                        if (v < 0) v = -v;
                        if (v > peak) peak = v;
                    }
                    IntPtr dst;
                    render.GetBuffer((uint)toWrite, out dst);
                    Marshal.Copy(mix, 0, dst, len);
                    render.ReleaseBuffer((uint)toWrite, 0);
                }
                Thread.Sleep(3); // loopback delivers nothing during silence, so poll instead of waiting on events
            }
        }
        finally
        {
            Relaying = false;
            timeEndPeriod(1);
            foreach (var s in srcs)
            {
                try { if (s.Client != null) s.Client.Stop(); } catch { }
                try { if (s.Client != null) Marshal.ReleaseComObject(s.Client); } catch { }
            }
            try { if (ren != null) ren.Stop(); } catch { }
            if (tfmt != IntPtr.Zero) Marshal.FreeCoTaskMem(tfmt);
            if (ren != null) Marshal.ReleaseComObject(ren);
            Marshal.ReleaseComObject(en);
        }
    }

    // ---- relay worker (used by the console mode and by the window) ----

    // target == null means automatic: play into whatever device Steam records.
    static void Worker(Func<List<Endpoint>> getSources, Endpoint target, bool always)
    {
        string path = LogPath();
        Log(always ? "Running continuously." : "Waiting for a Remote Play stream (until Steam records " + (target != null ? target.Name : "a playback device") + ")");
        Status = always ? "Starting..." : "Waiting for a Remote Play stream";
        bool sessionStarted = false; // true from the first relay of a stream until it has really ended
        int hits = 0;
        var lastRelayEnd = DateTime.MinValue;
        var lastChores = DateTime.MinValue;
        var lastStreamSign = DateTime.UtcNow; // last time anything looked like a stream (or the worker started)
        Endpoint cur = null;
        while (!stop)
        {
            // two positive checks in a row, half a second apart, so a blip can't start anything
            bool go = always;
            if (always) cur = target ?? GuessTarget(ActiveRenderEndpoints());
            else
            {
                Endpoint seen = target != null ? (StreamActiveNow(target, path) ? target : null) : FindRecorded(path);
                if (seen != null) { hits++; cur = seen; lastStreamSign = DateTime.UtcNow; } else hits = 0;
                go = hits >= 2;
            }
            List<Endpoint> sources = null;
            if (go)
            {
                Endpoint c = cur;
                if (c != null) sources = getSources().FindAll(e => NormGuid(e.Guid) != NormGuid(c.Guid));
                if (c == null || sources.Count == 0)
                {
                    Status = c == null ? "Found no device to play into." : "Nothing to relay into " + c.Name + ".";
                    hits = 0;
                    for (int i = 0; i < 20 && !stop; i++) Thread.Sleep(100);
                    continue;
                }
            }
            if (go)
            {
                hits = 0;
                Endpoint tgt = cur;
                Log(always ? "Starting relay." : "Stream active (" + (detectBySession ? "Steam is recording " + tgt.Name : "according to Steam's log") + "), starting relay.");
                if (!sessionStarted)
                {
                    sessionStarted = true;
                    if (OpenBigPicture) LaunchBigPicture();
                }
                bool mayMove = !always;
                string why = always ? "" : StereoReason(tgt);
                if (why != Warning) { Warning = why; if (why != "") Log(why); }
                var lastMove = DateTime.MinValue;
                var lastCheck = DateTime.UtcNow;
                var lastSeenActive = DateTime.UtcNow;
                bool active = true;
                bool ended = false;
                string gameStopSeen = LastGameStop(path); // only game endings after this point count
                var lastGsCheck = DateTime.UtcNow;
                try
                {
                    RunSession(sources, tgt, () =>
                    {
                        if (stop) return false;
                        if (mayMove && Surround && (DateTime.UtcNow - lastMove).TotalSeconds >= 0.5)
                        {
                            lastMove = DateTime.UtcNow;
                            MoveApps(sources, tgt);
                        }
                        if (OpenBigPicture && (DateTime.UtcNow - lastGsCheck).TotalSeconds >= 2)
                        {
                            lastGsCheck = DateTime.UtcNow;
                            string gs = LastGameStop(path);
                            if (gs != null && gs != gameStopSeen)
                            {
                                // When the game you started from Big Picture ends, Steam falls back to the normal
                                // window. Put Big Picture back after a moment.
                                gameStopSeen = gs;
                                ThreadPool.QueueUserWorkItem(delegate { Thread.Sleep(2000); LaunchBigPicture(); });
                            }
                        }
                        if (!always && (DateTime.UtcNow - lastCheck).TotalSeconds >= 0.5)
                        {
                            lastCheck = DateTime.UtcNow;
                            active = StreamActiveNow(tgt, path);
                            if (active) lastSeenActive = DateTime.UtcNow;
                        }
                        // Steam's recording stays open through alt-tab and game starts, so 3 seconds without it
                        // means the stream is over. The log fallback needs 15: there a "Stopped" is followed
                        // by a new "Starting" a few seconds later when the stream only switches modes.
                        return active || (DateTime.UtcNow - lastSeenActive).TotalSeconds < (detectBySession ? 3 : 15);
                    });
                    ended = true;
                    if (!stop) Log("Steam stopped recording, relay paused.");
                }
                catch (Exception e)
                {
                    // Steam rebuilds its virtual device when a session restarts; just reconnect.
                    Log("Error (0x" + Marshal.GetHRForException(e).ToString("X8") + "): " + e.Message + " - reconnecting");
                    Status = "Error, reconnecting: " + e.Message;
                    Thread.Sleep(500);
                }
                Level = 0;
                if (ended) lastRelayEnd = DateTime.UtcNow; // only when Steam stopped recording, not on a reconnect after an error
                if (!stop) Status = always ? "Reconnecting..." : "Waiting for a Remote Play stream";
            }
            else
            {
                // The relay itself follows Steam's recording to the second. The session as a whole (Big Picture,
                // games moved for surround) is only called over after 20 quiet seconds: launching a game from the
                // library makes Steam end its stream and start a new one 5 to 10 seconds later, and that
                // shouldn't undo anything.
                if (sessionStarted && (DateTime.UtcNow - lastRelayEnd).TotalSeconds >= 20)
                {
                    sessionStarted = false;
                    Log("Remote Play session over.");
                    RestoreApps();
                    if (OpenBigPicture) CloseBigPicture();
                }
                if ((DateTime.UtcNow - lastChores).TotalSeconds >= 3)
                {
                    lastChores = DateTime.UtcNow;
                    string why = StereoReason(null);
                    // Steam puts the default device back when a stream ends. If it crashed mid-stream it didn't.
                    if (why == "" && !sessionStarted && (DateTime.UtcNow - lastStreamSign).TotalSeconds >= 10 && DefaultIsSteamDevice())
                        why = "Windows' default output is still Steam's device. Set it back to hear things at the PC.";
                    Warning = why;
                }
                // Leftovers from an earlier run: only once it's clear that no stream is running, or a game would
                // be pulled off Steam's device and pushed back a second later.
                if (!sessionStarted && (DateTime.UtcNow - lastStreamSign).TotalSeconds >= 5) RestoreLeftovers();
                for (int i = 0; i < 5 && !stop; i++) Thread.Sleep(100);
            }
        }
        if (!restarting) RestoreApps();
        Status = "Stopped";
        Level = 0;
    }

    // ---- straight into the stream ----
    //
    // A Wave Link channel is a stereo device, so a game on such a channel can only ever be stereo, and Steam
    // doesn't record it anyway. Steam's own device has as many channels as the client asked for. While a
    // stream runs, the programs assigned to the chosen devices are pointed at the device Steam records (the
    // same per-program setting Wave Link uses), and pointed back when the stream is over. Wave Link's own
    // channel list is not touched. What can't be moved this way - programs that picked their device
    // themselves, hardware inputs - is still copied over by the relay, in stereo.

    [DllImport("combase.dll", PreserveSig = false)]
    static extern void RoGetActivationFactory([MarshalAs(UnmanagedType.HString)] string id, [In] ref Guid iid, [Out, MarshalAs(UnmanagedType.IInspectable)] out object factory);
    [DllImport("combase.dll", PreserveSig = false)]
    static extern void WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string s, int length, out IntPtr h);
    [DllImport("combase.dll")]
    static extern int WindowsDeleteString(IntPtr h);

    const string RouteClass = "Windows.Media.Internal.AudioPolicyConfig";
    const string RoutePrefix = @"\\?\SWD#MMDEVAPI#", RouteSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    static bool RouteCall(Func<IAudioPolicyConfig, int> now, Func<IAudioPolicyConfigOld, int> old)
    {
        object f = null;
        try
        {
            try
            {
                Guid iid = typeof(IAudioPolicyConfig).GUID;
                RoGetActivationFactory(RouteClass, ref iid, out f);
                return now((IAudioPolicyConfig)f) == 0;
            }
            catch
            {
                if (f != null) { try { Marshal.ReleaseComObject(f); } catch { } f = null; }
                Guid iid = typeof(IAudioPolicyConfigOld).GUID;
                RoGetActivationFactory(RouteClass, ref iid, out f);
                return old((IAudioPolicyConfigOld)f) == 0;
            }
        }
        catch { return false; }
        finally { if (f != null) try { Marshal.ReleaseComObject(f); } catch { } }
    }

    // The device a program is assigned to, in Windows' long form. "" = none (it follows the default device),
    // null = couldn't ask.
    static string GetAppDevice(uint pid)
    {
        string s = null;
        bool ok = RouteCall(f => f.GetPersistedDefaultAudioEndpoint(pid, 0, 0, out s), f => f.GetPersistedDefaultAudioEndpoint(pid, 0, 0, out s));
        return ok ? (s ?? "") : null;
    }

    // Assigns a program to a device (long form as above), or with "" back to the default device.
    static bool SetAppDevice(uint pid, string device)
    {
        IntPtr h = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(device)) WindowsCreateString(device, device.Length, out h);
            bool ok = true;
            for (int role = 0; role < 2; role++) // console and multimedia, like Windows' own settings page
            {
                int r = role;
                ok &= RouteCall(f => f.SetPersistedDefaultAudioEndpoint(pid, 0, r, h), f => f.SetPersistedDefaultAudioEndpoint(pid, 0, r, h));
            }
            return ok;
        }
        catch { return false; }
        finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
    }

    // The name of the device steam.exe itself is assigned to, or null. Wave Link does this when Steam sits in
    // one of its channels. Steam then records that channel instead of its own device, in stereo.
    static string SteamPinned()
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("steam"))
            {
                string id;
                using (p) id = GetAppDevice((uint)p.Id);
                if (string.IsNullOrEmpty(id)) continue;
                foreach (var e in ActiveRenderEndpoints())
                    if (id.IndexOf(e.Guid, StringComparison.OrdinalIgnoreCase) >= 0) return e.Name;
                return "a playback device";
            }
        }
        catch { }
        return null;
    }

    // One line for the window when the stream is stereo although it wouldn't have to be. "" otherwise.
    // recorded: the device Steam is recording right now, or null when no stream runs.
    static string StereoReason(Endpoint recorded)
    {
        string pin = SteamPinned();
        if (pin != null) return "Steam sits in Wave Link's \"" + pin + "\" channel: stereo only. Remove it from that channel.";
        // Steam records a Wave Link channel without being assigned to it: Wave Link's lock put the default
        // device back before Steam started recording.
        if (recorded != null && ChannelDevices(ActiveRenderEndpoints()).Exists(e => NormGuid(e.Guid) == NormGuid(recorded.Guid)))
            return "Steam records \"" + recorded.Name + "\": stereo only. Switch off the device lock in Wave Link's settings.";
        return "";
    }

    static bool DefaultIsSteamDevice()
    {
        IMMDeviceEnumerator en = null;
        IMMDevice d = null;
        try
        {
            en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            en.GetDefaultAudioEndpoint(0, 1, out d); // playback, multimedia
            string id;
            d.GetId(out id);
            foreach (var e in ActiveRenderEndpoints())
                if (IsSteamDevice(e) && id.IndexOf(e.Guid, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        catch { return false; }
        finally
        {
            if (d != null) Marshal.ReleaseComObject(d);
            if (en != null) Marshal.ReleaseComObject(en);
        }
    }

    class MovedApp { public string Name, Orig, From; }
    // Everything below is guarded by lock (movedApps).
    static readonly Dictionary<uint, MovedApp> movedApps = new Dictionary<uint, MovedApp>();
    // process name -> the device to put it back on. Also kept in the settings file, so a program that was
    // moved gets put back even if this tool was closed in between.
    static readonly Dictionary<string, string> leftovers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<uint, int> moveTries = new Dictionary<uint, int>();
    static int moving;
    static volatile bool restarting; // the worker is only being restarted, leave moved programs where they are

    static void LoadLeftovers()
    {
        var s = LoadSettings();
        lock (movedApps)
            foreach (var kv in s)
                if (kv.Key.StartsWith("moved_")) leftovers[kv.Key.Substring(6)] = kv.Value;
    }

    static void Forget(string name)
    {
        lock (movedApps) leftovers.Remove(name);
        UpdateSettings(d => { d.Remove("moved_" + name); });
    }

    // Points the programs that are assigned to one of the "from" devices at the target. Runs on a pool
    // thread, the caller is the audio loop.
    static void MoveApps(List<Endpoint> from, Endpoint target)
    {
        if (Interlocked.Exchange(ref moving, 1) == 1) return;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string to = RoutePrefix + target.FullId + RouteSuffix;
                uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var dev in from)
                {
                    // If Steam records a Wave Link channel, everything is stereo anyway and the relay does the
                    // job. Moving programs from one channel to another would only mess up the mix at the PC.
                    if (dev.Device == target.Device) continue;
                    var pids = ActiveSessionPids(dev);
                    if (pids == null) continue;
                    foreach (uint pid in pids)
                    {
                        if (pid == 0 || pid == me) continue;
                        // A program we already moved shows up here again when something put it back, which Wave
                        // Link does now and then. Point it at the stream again, without counting tries.
                        bool again;
                        lock (movedApps)
                        {
                            again = movedApps.ContainsKey(pid);
                            if (!again)
                            {
                                int tries;
                                moveTries.TryGetValue(pid, out tries);
                                if (tries >= 5) continue;
                                if (moveTries.Count > 256) moveTries.Clear();
                                moveTries[pid] = tries + 1;
                            }
                        }
                        // Only programs that are assigned to this device, which is what sitting in a Wave Link
                        // channel means. Not something that merely listens to it, like a recorder.
                        string orig = GetAppDevice(pid);
                        if (orig == null || orig.IndexOf(dev.Guid, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string name;
                        try { using (var p = System.Diagnostics.Process.GetProcessById((int)pid)) name = p.ProcessName; } catch { continue; }
                        if (!SetAppDevice(pid, to)) { Log("Couldn't point " + name + " at " + target.Name + "."); continue; }
                        if (again) { Log(name + " was back on " + dev.Name + ", pointed it at the stream again."); continue; }
                        lock (movedApps) { movedApps[pid] = new MovedApp { Name = name, Orig = orig, From = NormGuid(dev.Guid) }; leftovers[name] = orig; }
                        UpdateSettings(d => { d["moved_" + name] = orig; });
                        Log(name + " now plays straight into " + target + ".");
                    }
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref moving, 0); }
        });
    }

    // Puts the moved programs back on their own device. A program that has exited meanwhile stays on the
    // leftovers list and is put back by RestoreLeftovers when it runs again.
    // With a device guid: only the programs that came from that device.
    static void RestoreApps(string onlyFrom = null)
    {
        var list = new List<KeyValuePair<uint, MovedApp>>();
        lock (movedApps)
        {
            foreach (var kv in movedApps)
                if (onlyFrom == null || kv.Value.From == onlyFrom) list.Add(kv);
            foreach (var kv in list) { movedApps.Remove(kv.Key); moveTries.Remove(kv.Key); }
            if (onlyFrom == null) moveTries.Clear();
        }
        foreach (var kv in list)
        {
            try { using (System.Diagnostics.Process.GetProcessById((int)kv.Key)) { } } catch { continue; } // gone
            if (!SetAppDevice(kv.Key, kv.Value.Orig)) continue;
            Forget(kv.Value.Name);
            Log(kv.Value.Name + " is back on its own device.");
        }
    }

    // For the window: device guid -> the programs from that device that currently play straight into the stream.
    static Dictionary<string, string> MovedNames()
    {
        var d = new Dictionary<string, string>();
        lock (movedApps)
            foreach (var m in movedApps.Values)
            {
                string cur;
                d[m.From] = d.TryGetValue(m.From, out cur) ? cur + ", " + m.Name : m.Name;
            }
        return d;
    }

    static void RestoreLeftovers()
    {
        List<KeyValuePair<string, string>> list;
        lock (movedApps)
        {
            if (leftovers.Count == 0) return;
            list = new List<KeyValuePair<string, string>>(leftovers);
        }
        foreach (var kv in list)
        {
            bool done = false;
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(kv.Key))
                    using (p) if (SetAppDevice((uint)p.Id, kv.Value)) done = true;
            }
            catch { }
            if (!done) continue;
            Forget(kv.Key);
            Log(kv.Key + " is back on its own device.");
        }
    }

    static void LaunchBigPicture() { SteamUrl("steam://open/bigpicture", "Opened Steam Big Picture."); }

    static void CloseBigPicture() { SteamUrl("steam://close/bigpicture", "Closed Steam Big Picture."); }

    static void SteamUrl(string url, string done)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            Log(done);
        }
        catch (Exception e) { Log("Steam URL failed (" + url + "): " + e.Message); }
    }

    // ---- settings and autostart (window mode) ----

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "wavelink-remote-play";
    const string AppName = "Remote Play Audio for Wave Link";

    static string SettingsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wavelink-remote-play", "settings.ini");
    }

    static Dictionary<string, string> LoadSettings()
    {
        var d = new Dictionary<string, string>();
        try
        {
            foreach (string line in File.ReadAllLines(SettingsPath()))
            {
                int i = line.IndexOf('=');
                if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
        }
        catch { }
        return d;
    }

    static readonly object settingsLock = new object();

    // Read-modify-write of the settings file. Keys that aren't touched stay as they are.
    static void UpdateSettings(Action<Dictionary<string, string>> change)
    {
        lock (settingsLock)
        {
            var d = LoadSettings();
            change(d);
            try
            {
                string p = SettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                var sb = new StringBuilder();
                foreach (var kv in d) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
                File.WriteAllText(p, sb.ToString());
            }
            catch { }
        }
    }

    // Earlier versions were called steam-audio-relay. If that one was set to start with Windows, this one
    // takes over its entry.
    static void MigrateAutostart()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (k == null || k.GetValue("steam-audio-relay") == null) return;
            k.DeleteValue("steam-audio-relay", false);
            k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --minimized");
        }
    }

    static bool AutostartEnabled()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue(RunValue) != null;
    }

    static void SetAutostart(bool on)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (k == null) return;
            if (on) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --minimized");
            else k.DeleteValue(RunValue, false);
        }
    }

    // ---- the window ----

    static readonly Color CBg = Color.FromArgb(27, 27, 27), CCard = Color.FromArgb(42, 42, 42), CCardHi = Color.FromArgb(56, 56, 56),
        CText = Color.FromArgb(242, 242, 242), CSub = Color.FromArgb(160, 160, 160), CAccent = Color.FromArgb(76, 194, 255),
        CTrack = Color.FromArgb(70, 70, 70), CGreen = Color.FromArgb(47, 209, 91), CRed = Color.FromArgb(255, 110, 110),
        CRedBg = Color.FromArgb(74, 38, 38), CAmber = Color.FromArgb(255, 190, 70);
    static readonly Font FTitle = new Font("Segoe UI Semibold", 13f), FName = new Font("Segoe UI Semibold", 9.75f),
        FText = new Font("Segoe UI", 9f), FSmall = new Font("Segoe UI", 8.25f), FCaps = new Font("Segoe UI Semibold", 8f);
    const ControlStyles Painted = ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static GraphicsPath Round(RectangleF r, float rad)
    {
        float d = rad * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void FillRound(Graphics g, Color c, RectangleF r, float rad)
    {
        using (var p = Round(r, rad))
        using (var b = new SolidBrush(c)) g.FillPath(b, p);
    }

    // The icon is built into the exe (see build.cmd); fall back to the stock one if that ever fails.
    static Icon AppIcon(Size? size)
    {
        try
        {
            using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))
                if (s != null) return size.HasValue ? new Icon(s, size.Value) : new Icon(s);
        }
        catch { }
        return SystemIcons.Application;
    }

    // On/off switch.
    class Toggle : Control
    {
        bool on;
        public event EventHandler Changed; // only fires on clicks, not when On is set from code
        public bool On { get { return on; } set { if (on != value) { on = value; Invalidate(); } } }
        public Toggle() { SetStyle(Painted, true); Size = new Size(44, 24); Cursor = Cursors.Hand; BackColor = CCard; }
        protected override void OnClick(EventArgs e)
        {
            on = !on;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            if (on) FillRound(g, CAccent, r, r.Height / 2);
            else using (var p = Round(r, r.Height / 2)) using (var pen = new Pen(CSub, 1.5f)) g.DrawPath(pen, p);
            float d = Height - 10, x = on ? Width - d - 5 : 5;
            using (var b = new SolidBrush(on ? Color.FromArgb(20, 20, 20) : CSub)) g.FillEllipse(b, x, 5, d, d);
        }
    }

    class PillButton : Control
    {
        public bool Primary = true;
        public PillButton() { SetStyle(Painted, true); Size = new Size(96, 32); Cursor = Cursors.Hand; BackColor = CBg; }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            if (Primary) FillRound(g, CAccent, r, r.Height / 2);
            else using (var p = Round(r, r.Height / 2)) using (var pen = new Pen(CSub, 1.3f)) g.DrawPath(pen, p);
            TextRenderer.DrawText(g, Text, FName, ClientRectangle, Primary ? Color.FromArgb(15, 15, 15) : CText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    static Color BadgeColor(string name, int index)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("game")) return Color.FromArgb(214, 84, 84);
        if (n.Contains("browser") || n.Contains("chrome")) return Color.FromArgb(142, 92, 247);
        if (n.Contains("voice") || n.Contains("chat")) return Color.FromArgb(232, 190, 30);
        if (n.Contains("sound")) return Color.FromArgb(245, 138, 31);
        if (n.Contains("alert")) return Color.FromArgb(84, 132, 164);
        if (n.Contains("music")) return Color.FromArgb(47, 191, 113);
        var pal = new[] { Color.FromArgb(61, 139, 253), Color.FromArgb(226, 85, 161), Color.FromArgb(47, 191, 113), Color.FromArgb(108, 122, 137) };
        return pal[index % pal.Length];
    }

    // One channel: badge, name, and the switch that puts it into the stream.
    class ChannelRow : Control
    {
        public Endpoint E;
        public string Key;
        public Toggle T = new Toggle();
        public bool FullName; // show "name (device)", for setups without Wave Link
        Color badge;
        string note = "";
        public string Note { set { if (note != value) { note = value; Invalidate(); } } }
        public ChannelRow(Endpoint e, Color badgeColor)
        {
            SetStyle(Painted, true);
            E = e; Key = NormGuid(e.Guid); badge = badgeColor;
            Size = new Size(580, 46);
            T.Location = new Point(580 - 44 - 14, 11);
            Controls.Add(T);
            T.Changed += delegate { Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(CBg);
            FillRound(g, CCard, new RectangleF(0, 0, Width, Height), 8);
            FillRound(g, T.On ? badge : CTrack, new RectangleF(8, 8, 30, 30), 7);
            TextRenderer.DrawText(g, E.Name.Substring(0, 1).ToUpperInvariant(), FName, new Rectangle(8, 8, 30, 30), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, FullName ? E.ToString() : E.Name, FName, new Rectangle(48, 0, note.Length > 0 ? 250 : 460, Height), T.On ? CText : CSub, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (note.Length > 0)
                TextRenderer.DrawText(g, note, FSmall, new Rectangle(300, 0, 210, Height), CGreen, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        }
    }

    // A setting: title, one line of explanation, switch on the right.
    class OptionRow : Control
    {
        public Toggle T = new Toggle();
        string title, sub;
        public OptionRow(string title, string sub)
        {
            SetStyle(Painted, true);
            this.title = title; this.sub = sub;
            Size = new Size(580, 50);
            T.Location = new Point(580 - 44 - 14, 13);
            Controls.Add(T);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(CBg);
            FillRound(g, CCard, new RectangleF(0, 0, Width, Height), 8);
            TextRenderer.DrawText(g, title, FName, new Rectangle(14, 6, 480, 20), CText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, sub, FSmall, new Rectangle(14, 26, 490, 18), CSub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    class MainForm : Form
    {
        Panel body = new Panel();
        PictureBox pic = new PictureBox();
        Label lblTitle = new Label(), lblStatus = new Label(), lblWarn = new Label(), lblMix = new Label(), lblMixSub = new Label(), lblOpt = new Label(), lblRefresh = new Label(), lblNone = new Label();
        PillButton btnToggle = new PillButton();
        List<ChannelRow> rows = new List<ChannelRow>();
        OptionRow optLocal = new OptionRow("Keep the sound on at the PC", "For when someone sits at the PC during the stream. The stream is stereo then, not surround."),
            optAuto = new OptionRow("Start with Windows", "Starts minimized to the tray."),
            optBig = new OptionRow("Big Picture", "Open Steam Big Picture when a stream starts, close it when it ends.");
        NotifyIcon tray = new NotifyIcon();
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        bool startHidden, reallyExit, wantRun, preview;
        int ticks;
        Thread worker;

        static Label Caption(Label l, string text, Font f, Color c)
        {
            l.Text = text; l.Font = f; l.ForeColor = c; l.BackColor = CBg; l.AutoSize = true;
            return l;
        }

        public MainForm(bool hidden, bool previewOnly)
        {
            startHidden = hidden;
            preview = previewOnly;
            Text = AppName;
            Icon = AppIcon(null);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = CBg;
            ForeColor = CText;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = previewOnly ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
            if (previewOnly) Location = new Point(-4000, -4000);

            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.BackColor = CBg;
            Controls.Add(body);

            pic.SetBounds(20, 16, 36, 36);
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            try { pic.Image = AppIcon(new Size(48, 48)).ToBitmap(); } catch { }
            Caption(lblTitle, AppName, FTitle, CText).Location = new Point(64, 12);
            Caption(lblStatus, "", FText, CSub).Location = new Point(66, 38);
            Caption(lblWarn, "", FSmall, CAmber).Location = new Point(22, 57);
            btnToggle.Location = new Point(504, 18);
            btnToggle.Click += delegate
            {
                if (worker != null) { wantRun = false; StopWorker(); }
                else { wantRun = true; StartWorker(); }
            };
            Caption(lblMix, "IN THE STREAM", FCaps, CSub).Location = new Point(22, 74);
            Caption(lblMixSub, "The channels the other side should hear. This changes nothing in Wave Link.", FSmall, CSub).Location = new Point(22, 92);
            Caption(lblRefresh, "Refresh", FSmall, CAccent).Location = new Point(552, 74);
            lblRefresh.Cursor = Cursors.Hand;
            lblRefresh.Click += delegate { BuildRows(); if (wantRun) StartWorker(); };
            Caption(lblNone, "No playback devices found.", FText, CSub);
            lblNone.Visible = false;
            Caption(lblOpt, "OPTIONS", FCaps, CSub);
            body.Controls.AddRange(new Control[] { pic, lblTitle, lblStatus, lblWarn, btnToggle, lblMix, lblMixSub, lblRefresh, lblNone, lblOpt, optLocal, optAuto, optBig });

            optLocal.T.Changed += delegate
            {
                bool v = optLocal.T.On;
                Surround = !v;
                UpdateSettings(d => { d["keeplocal"] = v ? "1" : "0"; });
                if (v) ThreadPool.QueueUserWorkItem(delegate { RestoreApps(); });
            };
            optAuto.T.Changed += delegate
            {
                try { SetAutostart(optAuto.T.On); }
                catch (Exception e) { Status = "Couldn't change autostart: " + e.Message; }
            };
            optBig.T.Changed += delegate
            {
                OpenBigPicture = optBig.T.On;
                bool v = optBig.T.On;
                UpdateSettings(d => { d["bigpicture"] = v ? "1" : "0"; });
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { ShowWindow(); });
            menu.Items.Add("Exit", null, delegate { reallyExit = true; Close(); });
            tray.ContextMenuStrip = menu;
            tray.Icon = AppIcon(SystemInformation.SmallIconSize);
            tray.Text = AppName;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.Visible = !previewOnly;

            var s = LoadSettings();
            optLocal.T.On = s.ContainsKey("keeplocal") && s["keeplocal"] == "1";
            Surround = !optLocal.T.On;
            optBig.T.On = s.ContainsKey("bigpicture") && s["bigpicture"] == "1";
            OpenBigPicture = optBig.T.On;
            if (!previewOnly) try { MigrateAutostart(); } catch { }
            try { optAuto.T.On = AutostartEnabled(); } catch { }
            BuildRows();

            timer.Interval = 60;
            timer.Tick += delegate { Tick(); };
            timer.Start();

            if (previewOnly)
            {
                // sample state so a screenshot shows what the window looks like in use
                int n = 0;
                foreach (var r in rows) if (r.T.On) n++;
                Status = "Relaying " + n + " channels into Steam's device (7.1)";
                Relaying = true;
                Tick();
                return;
            }

            LoadLeftovers();
            wantRun = true;
            StartWorker();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } // dark title bar
        }

        void Tick()
        {
            bool relaying = Relaying && (worker != null || preview);
            lblStatus.Text = Status;
            lblStatus.ForeColor = relaying ? CGreen : CSub;
            string w = preview ? "" : Warning;
            if (lblWarn.Text != w) lblWarn.Text = w;
            bool running = worker != null || preview;
            string t = running ? "Stop" : "Start";
            if (btnToggle.Text != t) { btnToggle.Primary = !running; btnToggle.Text = t; }
            if (preview) return;
            if (++ticks % 8 == 0)
            {
                // which programs currently play straight into the stream, per channel
                var moved = MovedNames();
                foreach (var r in rows)
                {
                    string names;
                    r.Note = moved.TryGetValue(r.Key, out names) ? "direct: " + names : "";
                }
            }
            if (ticks % 16 == 0)
            {
                string tt = "Remote Play Audio: " + Status;
                tray.Text = tt.Length > 63 ? tt.Substring(0, 63) : tt;
            }
        }

        // The rows: Wave Link's devices, all switched on. Without Wave Link every playback device is offered,
        // switched off until the user picks. Steam's own devices are never rows.
        void BuildRows()
        {
            foreach (var r in rows) { body.Controls.Remove(r); r.Dispose(); }
            rows.Clear();
            var s = preview ? new Dictionary<string, string>() : LoadSettings(); // screenshots show the defaults
            var all = ActiveRenderEndpoints();
            var list = ChannelDevices(all);
            bool channels = list.Count > 0;
            if (!channels) list = all;
            list = list.FindAll(e => !e.Device.StartsWith("Steam Streaming", StringComparison.OrdinalIgnoreCase));
            list.Sort((a, b) => string.Compare(a.Name + a.Device, b.Name + b.Device, StringComparison.OrdinalIgnoreCase));
            int i = 0;
            foreach (var e in list)
            {
                var row = new ChannelRow(e, BadgeColor(e.Name, i++));
                string k = row.Key;
                row.FullName = !channels;
                row.T.On = s.ContainsKey("on_" + k) ? s["on_" + k] == "1" : channels;
                row.T.Changed += delegate
                {
                    bool on = row.T.On;
                    UpdateSettings(d => { d["on_" + k] = on ? "1" : "0"; });
                    if (!on) ThreadPool.QueueUserWorkItem(delegate { RestoreApps(k); });
                    if (wantRun) StartWorker();
                };
                rows.Add(row);
                body.Controls.Add(row);
            }
            Relayout();
        }

        void Relayout()
        {
            body.AutoScrollPosition = Point.Empty;
            int y = 116;
            foreach (var r in rows) { r.Location = new Point(20, y); y += 50; }
            lblNone.Visible = rows.Count == 0;
            if (rows.Count == 0) { lblNone.Location = new Point(22, y + 4); y += 34; }
            y += 14;
            lblOpt.Location = new Point(22, y); y += 22;
            foreach (var o in new[] { optLocal, optAuto, optBig }) { o.Location = new Point(20, y); y += 54; }
            y += 12;
            int max = Screen.FromControl(this).WorkingArea.Height - 80;
            bool scroll = y > max;
            body.AutoScrollMinSize = scroll ? new Size(0, y) : Size.Empty;
            ClientSize = new Size(620 + (scroll ? SystemInformation.VerticalScrollBarWidth : 0), Math.Min(y, max));
        }

        void StartWorker()
        {
            restarting = true; // programs that were moved stay where they are across the restart
            StopWorker();
            restarting = false;
            var list = new List<Endpoint>();
            foreach (var r in rows) if (r.T.On) list.Add(r.E);
            if (list.Count == 0) { Status = "No channel is switched on."; return; }
            stop = false;
            worker = new Thread(delegate() { Worker(delegate { return list; }, null, false); });
            worker.IsBackground = true;
            worker.Start();
        }

        void StopWorker()
        {
            stop = true;
            if (worker != null) worker.Join(3000);
            worker = null;
            Status = "Stopped";
            Level = 0;
        }

        void ShowWindow()
        {
            startHidden = false;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void SetVisibleCore(bool value)
        {
            // started with --minimized: live in the tray, don't show the window
            if (startHidden)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyExit && !preview && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            timer.Stop();
            if (!preview) StopWorker();
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosing(e);
        }
    }

    static int RunUi(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // --screenshot <file.png>: draw the window into a picture and exit (for the README, nothing is relayed)
        string shot = Arg(args, "--screenshot");
        if (shot != null)
        {
            using (var f = new MainForm(false, true))
            {
                f.Show();
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            return 0;
        }

        bool created;
        using (var mutex = new Mutex(true, "wavelink-remote-play", out created))
        {
            if (!created)
            {
                MessageBox.Show(AppName + " is already running (look in the tray).", AppName);
                return 1;
            }
            Application.Run(new MainForm(Array.IndexOf(args, "--minimized") >= 0, false));
            return 0;
        }
    }

    // ---- console mode ----

    static void PrintList(List<Endpoint> all)
    {
        Console.WriteLine("Active playback devices (name, device, id):");
        foreach (var e in all) Console.WriteLine("  " + e.Name + "  |  " + e.Device + "  |  " + e.Guid);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Run without arguments to open the window. Command line:");
        Console.WriteLine("Usage: wavelink-remote-play-cli.exe [--target <id>] [--source <id,id>] [--always] [--selftest] [--list] [--verbose]");
        Console.WriteLine("  --list      show playback devices and their ids");
        Console.WriteLine("  --target    device to play into (id from --list). Default: whatever device Steam records.");
        Console.WriteLine("  --source    device(s) to copy from, comma separated. Default: Wave Link's devices.");
        Console.WriteLine("  --always    don't wait for a Steam stream, relay until closed");
        Console.WriteLine("  --selftest  open both devices, run for 2 seconds, exit");
        Console.WriteLine("  --verbose   log the audio level every 5 seconds (to check that signal arrives)");
        Console.WriteLine("  --raw       don't undo the source devices' volume faders (by default the stream gets full level)");
        Console.WriteLine("  --keep-local  keep the sound on at the PC: only copy it into the stream, in stereo. By default the programs");
        Console.WriteLine("              on the source devices play straight into Steam's device while streaming (surround, silent at the PC).");
        Console.WriteLine("  --minimized (window mode) start in the tray");
    }

    [STAThread]
    static int Main(string[] args)
    {
        bool cli = false;
        foreach (string f in new[] { "--list", "--help", "-h", "--selftest", "--target", "--source", "--always", "--verbose", "--raw", "--keep-local" })
            if (Array.IndexOf(args, f) >= 0) cli = true;
        if (!cli) return RunUi(args);

        AttachConsole(-1); // so the windowed exe can print into the terminal it was started from

        if (Array.IndexOf(args, "--help") >= 0 || Array.IndexOf(args, "-h") >= 0) { PrintUsage(); return 0; }

        var all = ActiveRenderEndpoints();
        if (Array.IndexOf(args, "--list") >= 0) { PrintList(all); return 0; }

        verbose = Array.IndexOf(args, "--verbose") >= 0;
        FullVolume = Array.IndexOf(args, "--raw") < 0;
        Surround = Array.IndexOf(args, "--keep-local") < 0;
        bool always = Array.IndexOf(args, "--always") >= 0;
        bool selftest = Array.IndexOf(args, "--selftest") >= 0;

        // target: an explicit id, or automatic (whatever Steam records)
        Endpoint target = null;
        string tgtArg = Arg(args, "--target");
        if (tgtArg != null && tgtArg != "auto")
        {
            target = ByGuid(all, tgtArg);
            if (target == null) { Log("No active playback device with id " + tgtArg + ". Try --list."); return 3; }
        }

        bool created;
        using (var mutex = new Mutex(true, "wavelink-remote-play", out created))
        {
            if (!created) { Log("Already running."); return 1; }

            // sources: explicit ids (comma separated), or Wave Link's devices
            Func<List<Endpoint>> getSources;
            string srcArg = Arg(args, "--source");
            if (srcArg != null && srcArg != "auto")
            {
                var fixedSources = new List<Endpoint>();
                foreach (string id in srcArg.Split(','))
                {
                    var e = ByGuid(all, id);
                    if (e == null) { Log("No active playback device with id " + id + ". Try --list."); return 3; }
                    fixedSources.Add(e);
                }
                getSources = delegate { return fixedSources; };
            }
            else
            {
                getSources = delegate { return ChannelDevices(ActiveRenderEndpoints()); };
                if (getSources().Count == 0) { Log("Found no Wave Link devices (no single playback device called \"System\"). Pass --source <id,id>."); return 3; }
            }
            Log("To: " + (target != null ? target.ToString() : "whatever device Steam records"));
            LoadLeftovers();

            if (selftest)
            {
                Endpoint st = target ?? GuessTarget(all);
                if (st == null) { Log("The self-test needs --target <id>."); return 3; }
                var until = DateTime.UtcNow.AddSeconds(2);
                try { RunSession(getSources().FindAll(e => NormGuid(e.Guid) != NormGuid(st.Guid)), st, () => DateTime.UtcNow < until); Log("Self-test ok."); return 0; }
                catch (Exception e) { Log("Self-test failed: " + e.Message); return 2; }
            }

            Worker(getSources, target, always);
            return 0;
        }
    }
}
