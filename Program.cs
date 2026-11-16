using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CelestellarisInstaller;

static class Program {
    // Optional unattended mode (for testing/CI): Install-Celestellaris.exe --silent --dir "<Celeste folder>" [--payload "<folder>"]
    public static string SilentDir, PayloadOverride; public static int ExitCode;
    [STAThread]
    static void Main(string[] args) {
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--dir" && i + 1 < args.Length) SilentDir = args[++i];
            else if (args[i] == "--payload" && i + 1 < args.Length) PayloadOverride = args[++i];
        }
        ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); Environment.Exit(ExitCode);
    }
}

class MainForm : Form {
    // ---- Edit these for a different mod ------------------------------------------------
    const string ModName = "Celestellaris";
    // Every *.zip in this folder (next to the installer) is copied into Celeste/Mods.
    // Put your own mods that are NOT on the public mod database here (e.g. KirbyHelperMechanics).
    const string PayloadDir = "payload";
    // -------------------------------------------------------------------------------------
    const string DbUrl = "https://maddie480.ovh/celeste/everest_update.yaml";
    const string EverestFeed = "https://maddie480.ovh/celeste/everest-versions";
    const string Mirror = "https://celestemodupdater.0x0a.de/banana-mirror/";

    readonly TextBox path = new() { Dock = DockStyle.Fill };
    readonly Button browse = new() { Text = "Browse...", AutoSize = true };
    readonly Button go = new() { Text = "Install " + ModName, Dock = DockStyle.Fill, Height = 44 };
    readonly ProgressBar bar = new() { Dock = DockStyle.Fill };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    static readonly HttpClient http = MakeClient();
    static HttpClient MakeClient() {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("CelestellarisInstaller/1.0");
        return c;
    }

    string title = ModName + " installer";

    public MainForm() {
        // Optional branding.json next to the exe: {"title":"...","button":"...","accent":"#RRGGBB","icon":"icon.ico"}
        try {
            var bp = Path.Combine(AppContext.BaseDirectory, "branding.json");
            if (File.Exists(bp)) {
                var b = JsonDocument.Parse(File.ReadAllText(bp)).RootElement;
                if (b.TryGetProperty("title", out var bt)) title = bt.GetString();
                if (b.TryGetProperty("button", out var bb)) go.Text = bb.GetString();
                if (b.TryGetProperty("accent", out var ba)) { go.BackColor = ColorTranslator.FromHtml(ba.GetString()); go.ForeColor = Color.White; go.FlatStyle = FlatStyle.Flat; }
                if (b.TryGetProperty("icon", out var bi) && File.Exists(Path.Combine(AppContext.BaseDirectory, bi.GetString()))) Icon = new Icon(Path.Combine(AppContext.BaseDirectory, bi.GetString()));
            }
        } catch { }
        Text = title; Width = 640; Height = 460; StartPosition = FormStartPosition.CenterScreen;
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(10) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.ColumnStyles.Add(new ColumnStyle());
        t.RowStyles.Add(new RowStyle()); t.RowStyles.Add(new RowStyle()); t.RowStyles.Add(new RowStyle());
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var lbl = new Label { Text = "Celeste folder (found automatically):", AutoSize = true };
        t.Controls.Add(lbl, 0, 0); t.SetColumnSpan(lbl, 2);
        t.Controls.Add(path, 0, 1); t.Controls.Add(browse, 1, 1);
        t.Controls.Add(go, 0, 2); t.SetColumnSpan(go, 2);
        t.Controls.Add(bar, 0, 3); t.SetColumnSpan(bar, 2);
        t.Controls.Add(log, 0, 4); t.SetColumnSpan(log, 2);
        Controls.Add(t);
        browse.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog() == DialogResult.OK) path.Text = d.SelectedPath; };
        go.Click += async (_, _) => await Run();
        Shown += async (_, _) => {
            if (Program.SilentDir != null) { path.Text = Program.SilentDir; await Run(); Close(); return; }
            var f = FindCeleste(); path.Text = f ?? "";
            Log(f == null ? "Could not find Celeste. Click Browse and pick the folder containing Celeste.exe." : "Found Celeste: " + f);
        };
    }

    void Log(string s) {
        if (InvokeRequired) { Invoke(() => Log(s)); return; }
        log.AppendText(s + "\r\n");
        if (Program.SilentDir != null) try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "celestellaris-install.log"), s + "\n"); } catch { }
    }
    void Prog(int p) { if (InvokeRequired) { Invoke(() => Prog(p)); return; } bar.Value = Math.Clamp(p, 0, 100); }

    static bool IsCeleste(string d) => !string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "Celeste.exe"));

    static string FindCeleste() {
        var c = new List<string>();
        try {
            foreach (var k in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" }) {
                var steam = (Registry.GetValue(k, "SteamPath", null) ?? Registry.GetValue(k, "InstallPath", null)) as string;
                if (steam == null) continue;
                c.Add(Path.Combine(steam, "steamapps", "common", "Celeste"));
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        c.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common", "Celeste"));
            }
        } catch { }
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        try { // itch app: any install folder holding Celeste.exe (default or custom itch library)
            var itchApps = Path.Combine(appdata, "itch", "apps");
            if (Directory.Exists(itchApps)) c.AddRange(Directory.GetDirectories(itchApps));
        } catch { }
        try { // GOG Galaxy
            foreach (var root in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games", @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games" }) {
                using var k = Registry.LocalMachine.OpenSubKey(root.Substring(19));
                if (k == null) continue;
                foreach (var sub in k.GetSubKeyNames()) {
                    using var g = k.OpenSubKey(sub);
                    if ((g?.GetValue("gameName") as string) == "Celeste" && g.GetValue("path") is string gp) c.Add(gp);
                }
            }
        } catch { }
        try {
            var epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(epic))
                foreach (var f in Directory.GetFiles(epic, "*.item")) {
                    var j = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
                    if (j.TryGetProperty("DisplayName", out var n) && n.GetString() == "Celeste" && j.TryGetProperty("InstallLocation", out var l)) c.Add(l.GetString());
                }
        } catch { }
        foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady)) {
            c.Add(Path.Combine(d.Name, "Program Files (x86)", "Steam", "steamapps", "common", "Celeste"));
            c.Add(Path.Combine(d.Name, "SteamLibrary", "steamapps", "common", "Celeste"));
            c.Add(Path.Combine(d.Name, "Games", "Celeste"));
        }
        return c.FirstOrDefault(IsCeleste);
    }

    async Task Run() {
        go.Enabled = false;
        try {
            var dir = path.Text.Trim();
            if (!IsCeleste(dir)) { MessageBox.Show("That folder doesn't contain Celeste.exe."); return; }
            var payload = Program.PayloadOverride ?? Path.Combine(AppContext.BaseDirectory, PayloadDir);
            var zips = Directory.Exists(payload) ? Directory.GetFiles(payload, "*.zip") : Array.Empty<string>();
            // Also accept mod zips sitting directly next to the installer (e.g. loose GitHub release downloads).
            if (zips.Length == 0 && Program.PayloadOverride == null)
                zips = Directory.GetFiles(AppContext.BaseDirectory, "*.zip").Where(z => YamlOf(z).Contains("Name:")).ToArray();
            if (zips.Length == 0) throw new Exception("No mod files found in '" + PayloadDir + "' next to the installer. Extract the whole download first.");
            var mods = Path.Combine(dir, "Mods"); Directory.CreateDirectory(mods);

            if (!File.Exists(Path.Combine(dir, "Celeste.Mod.mm.dll"))) await InstallEverest(dir);
            else Log("Everest already installed, skipping.");
            Prog(40);

            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var z in zips) {
                var dest = Path.Combine(mods, Path.GetFileName(z));
                Log("Installing " + Path.GetFileName(z));
                await Task.Run(() => File.Copy(z, dest, true));
                have.Add(ModNameOf(z));
            }
            Prog(60);

            var installedNames = Directory.GetFiles(mods, "*.zip").Select(ModNameOf)
                .Concat(Directory.GetDirectories(mods).Select(Path.GetFileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var needed = zips.SelectMany(DepsOf).Where(n => n != "Everest" && n != "EverestCore" && !installedNames.Contains(n)).Distinct().ToList();
            if (needed.Count > 0) {
                Log("Looking up " + needed.Count + " required helper mods...");
                var db = ParseDb(await http.GetStringAsync(DbUrl));
                int i = 0;
                foreach (var n in needed) {
                    i++; Prog(60 + 38 * i / needed.Count);
                    if (!db.TryGetValue(n, out var info)) { Log("  ! " + n + " not found in database (skipped)"); continue; }
                    Log("  Downloading " + n + " " + info.Version);
                    var file = Path.Combine(mods, n + ".zip");
                    bool ok = false;
                    foreach (var src in new[] { Mirror + info.Mirror + ".zip", info.Url }) {
                        try {
                            await Download(src, file);
                            if (Verify(file, info.Hashes)) { ok = true; break; }
                            Log("  checksum mismatch from " + new Uri(src).Host + ", trying next source");
                        } catch (Exception ex) { Log("  download failed: " + ex.Message); }
                        try { File.Delete(file); } catch { }
                    }
                    if (!ok) throw new Exception(n + " could not be downloaded and verified. Nothing unverified was installed for it.");
                    Log("  verified " + n);
                }
            }
            Prog(100);
            Log("\r\nDone! Launch Celeste and pick " + ModName + " from the map list.");
            if (Program.SilentDir == null) MessageBox.Show(ModName + " is installed. Start Celeste to play.");
        } catch (Exception e) {
            Log("ERROR: " + e.Message); Program.ExitCode = 1;
            if (Program.SilentDir == null) MessageBox.Show(e.Message, "Install failed");
        }
        finally { go.Enabled = true; }
    }

    async Task InstallEverest(string dir) {
        Log("Installing Everest (mod loader)...");
        var json = JsonDocument.Parse(await http.GetStringAsync(EverestFeed)).RootElement;
        var best = json.EnumerateArray().Where(e => e.GetProperty("branch").GetString() == "stable")
            .OrderByDescending(e => e.GetProperty("version").GetInt32()).First();
        var url = best.GetProperty("mainDownload").GetString();
        var tmp = Path.Combine(Path.GetTempPath(), "everest-main.zip");
        await Download(url, tmp);
        // Verify against the SHA-256 GitHub publishes for the release asset.
        var m = Regex.Match(url, @"github\.com/EverestAPI/Everest/releases/download/([^/]+)/main\.zip");
        if (!m.Success) throw new Exception("Unexpected Everest download location; refusing to install it.");
        var rel = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/EverestAPI/Everest/releases/tags/" + m.Groups[1].Value)).RootElement;
        string want = null;
        foreach (var a in rel.GetProperty("assets").EnumerateArray())
            if (a.GetProperty("name").GetString() == "main.zip" && a.TryGetProperty("digest", out var dg)) want = dg.GetString();
        if (want == null || !want.StartsWith("sha256:")) throw new Exception("Could not get a published checksum for Everest; refusing to install it.");
        string got;
        using (var fs = File.OpenRead(tmp)) got = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs));
        if (!got.Equals(want.Substring(7), StringComparison.OrdinalIgnoreCase)) { File.Delete(tmp); throw new Exception("Everest download failed its checksum; nothing was installed."); }
        Log("Everest download verified (SHA-256).");
        Log("Extracting Everest...");
        await Task.Run(() => {
            using var zf = ZipFile.OpenRead(tmp);
            var root = Path.GetFullPath(dir);
            foreach (var e in zf.Entries) {
                var rel = e.FullName; if (rel.StartsWith("main/")) rel = rel.Substring(5);
                if (rel.Length == 0 || rel.EndsWith("/")) continue;
                var dest = Path.GetFullPath(Path.Combine(dir, rel));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // zip-slip guard
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); e.ExtractToFile(dest, true);
            }
        });
        File.Delete(tmp);
        Log("Running Everest patcher...");
        var exe = Path.Combine(dir, "MiniInstaller-win64.exe");
        if (!File.Exists(exe)) exe = Path.Combine(dir, "MiniInstaller-win.exe");
        var p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
        var outp = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
        if (p.ExitCode != 0 || !File.Exists(Path.Combine(dir, "Celeste.Mod.mm.dll")))
            throw new Exception("Everest patching failed:\r\n" + outp.Substring(Math.Max(0, outp.Length - 800)));
        Log("Everest installed.");
    }

    async Task Download(string url, string dest) {
        using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); r.EnsureSuccessStatusCode();
        using var s = await r.Content.ReadAsStreamAsync(); using var f = File.Create(dest); await s.CopyToAsync(f);
    }

    static string YamlOf(string zip) {
        using var z = ZipFile.OpenRead(zip);
        var e = z.GetEntry("everest.yaml") ?? z.GetEntry("everest.yml");
        if (e == null) return "";
        using var r = new StreamReader(e.Open()); return r.ReadToEnd();
    }
    static string ModNameOf(string zip) {
        try { var m = Regex.Match(YamlOf(zip), @"^\s*-?\s*Name:\s*(.+)$", RegexOptions.Multiline); return m.Success ? m.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(zip); }
        catch { return Path.GetFileNameWithoutExtension(zip); }
    }
    static IEnumerable<string> DepsOf(string zip) {
        string y; try { y = YamlOf(zip); } catch { yield break; }
        int i = y.IndexOf("Dependencies:"); if (i < 0) yield break;
        var block = y.Substring(i);
        int o = block.IndexOf("OptionalDependencies:"); if (o >= 0) block = block.Substring(0, o);
        foreach (Match m in Regex.Matches(block, @"-\s*Name:\s*(.+)")) yield return m.Groups[1].Value.Trim();
    }
    static bool Verify(string file, string[] hashes) {
        if (hashes.Length == 0) return false; // no checksum published: refuse
        var h = new System.IO.Hashing.XxHash64();
        using (var s = File.OpenRead(file)) h.Append(s);
        var got = Convert.ToHexString(h.GetCurrentHash()).ToLowerInvariant();
        return hashes.Any(x => x.Equals(got, StringComparison.OrdinalIgnoreCase));
    }
    record Entry(string Version, string Mirror, string Url, string[] Hashes);
    static Dictionary<string, Entry> ParseDb(string y) {
        var d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(y, @"^(?<n>[^\s#][^\n]*?):\r?\n  Version: (?<v>[^\n]+)\r?\n  LastUpdate: [^\n]+\r?\n  MirrorName: '?(?<m>[^'\r\n]+)'?\r?\n  URL: (?<u>[^\r\n]+)\r?\n  xxHash:\r?\n(?<h>(  - [0-9a-fA-F]+\r?\n?)+)", RegexOptions.Multiline)) {
            var hashes = Regex.Matches(m.Groups["h"].Value, @"- ([0-9a-fA-F]+)").Select(x => x.Groups[1].Value).ToArray();
            d[m.Groups["n"].Value.Trim('\'', '"')] = new Entry(m.Groups["v"].Value.Trim(), m.Groups["m"].Value.Trim(), m.Groups["u"].Value.Trim(), hashes);
        }
        return d;
    }
}
