using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DTDTrainer;

/// <summary>
/// Tìm thư mục game (chỉ đọc), quản lý runtime của mod trong Tool-DTD\runtime và mở game qua Steam.
/// Giống Wand: tool KHÔNG ghi gì vào thư mục game. Mọi thứ của mod (BepInEx, .NET runtime, plugin, log, interop)
/// nằm trong runtime\ cạnh DTDTrainer.exe, nên chép cả thư mục Tool-DTD sang máy khác là dùng được.
/// </summary>
static class ModInstaller
{
    public const string SteamAppId = "1868140";
    const string GameExe = "DaveTheDiver.exe";
    const string PluginFile = "DTDMod.dll";
    const string LoaderFile = "DTDLoader.dll";

    /// <summary>Thư mục chứa trainer (Tool-DTD).</summary>
    public static string ToolDir => AppContext.BaseDirectory;
    public static string RuntimeDir => Path.Combine(ToolDir, "runtime");
    static string BepInExDir => Path.Combine(RuntimeDir, "BepInEx");
    static string PluginPath => Path.Combine(BepInExDir, "plugins", "DTDMod", PluginFile);
    public static string LoaderPath => Path.Combine(RuntimeDir, LoaderFile);
    public static string LogPath => Path.Combine(BepInExDir, "LogOutput.log");

    /// <summary>Plugin được nhúng sẵn trong exe (build.ps1 build plugin trước rồi mới build trainer).</summary>
    static readonly Lazy<byte[]> BundledPlugin = new(() =>
    {
        using var s = typeof(ModInstaller).Assembly.GetManifestResourceStream(PluginFile);
        if (s == null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    });

    /// <summary>Bộ nạp DTDLoader.dll được nhúng sẵn trong exe.</summary>
    static readonly Lazy<byte[]> BundledLoader = new(() =>
    {
        using var s = typeof(ModInstaller).Assembly.GetManifestResourceStream(LoaderFile);
        if (s == null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    });

    // ------------------------------------------------------------------ Game (chỉ đọc)

    public static bool IsGameDir(string dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, GameExe));

    public static bool IsGameRunning() => GameStartTime() != null;

    /// <summary>Thời điểm game khởi động, null nếu game không chạy.</summary>
    public static DateTime? GameStartTime()
    {
        foreach (var p in Process.GetProcessesByName("DaveTheDiver"))
        {
            using (p)
            {
                try { return p.StartTime; } catch { return DateTime.Now; }
            }
        }
        return null;
    }

    /// <summary>Tìm game trong các thư viện Steam (registry + libraryfolders.vdf).</summary>
    public static string DetectGameDir()
    {
        var roots = new List<string>();
        foreach (var (hive, key, name) in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        })
        {
            try
            {
                if (hive.OpenSubKey(key)?.GetValue(name) is string p && Directory.Exists(p)) roots.Add(p);
            }
            catch { }
        }

        var libraries = new List<string>(roots);
        foreach (var root in roots)
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string dir = Path.Combine(lib, "steamapps", "common", "Dave the Diver");
            if (IsGameDir(dir)) return Path.GetFullPath(dir);
        }
        return null;
    }

    public static void LaunchGame()
        => Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId) { UseShellExecute = true });

    // ------------------------------------------------------------------ Runtime trong Tool-DTD

    public static bool IsBepInExReady
        => File.Exists(Path.Combine(BepInExDir, "core", "BepInEx.Unity.IL2CPP.dll"))
        && File.Exists(Path.Combine(RuntimeDir, "dotnet", "coreclr.dll"))
        && File.Exists(LoaderPath);

    public static bool IsPluginInstalled => File.Exists(PluginPath);
    public static bool IsLoaderInstalled => File.Exists(LoaderPath);

    /// <summary>Interop do BepInEx sinh ra ở lần đầu game chạy kèm mod (cần để build plugin và cho DTDXref).</summary>
    public static bool HasInterop => File.Exists(Path.Combine(BepInExDir, "interop", "Assembly-CSharp.dll"));

    /// <summary>Plugin trong runtime khác bản đi kèm trainer (cần cập nhật).</summary>
    public static bool IsPluginOutdated
    {
        get
        {
            var bundled = BundledPlugin.Value;
            if (bundled == null || !IsPluginInstalled) return false;
            try { return !File.ReadAllBytes(PluginPath).AsSpan().SequenceEqual(bundled); }
            catch (IOException) { return false; }
        }
    }

    /// <summary>Bộ nạp DTDLoader.dll trong runtime khác bản đi kèm trainer (cần cập nhật).</summary>
    public static bool IsLoaderOutdated
    {
        get
        {
            var bundled = BundledLoader.Value;
            if (bundled == null || !IsLoaderInstalled) return false;
            try { return !File.ReadAllBytes(LoaderPath).AsSpan().SequenceEqual(bundled); }
            catch (IOException) { return false; }
        }
    }

    /// <summary>
    /// Dựng/cập nhật runtime: giải nén BepInEx + .NET runtime từ vendor\ (nếu thiếu), chép thư viện Unity đi kèm
    /// (vendor\unity-libs, để máy không có mạng vẫn dùng được) và ghi plugin + loader nhúng trong exe.
    /// Trả về mô tả những gì đã làm, hoặc chuỗi rỗng nếu không cần làm gì.
    /// </summary>
    public static string EnsureRuntime()
    {
        var done = new List<string>();

        var loader = BundledLoader.Value;
        if (loader != null && (!IsLoaderInstalled || IsLoaderOutdated))
        {
            try
            {
                Directory.CreateDirectory(RuntimeDir);
                File.WriteAllBytes(LoaderPath, loader);
                done.Add(L.T("đã chuẩn bị DTDLoader.dll", "prepared DTDLoader.dll"));
            }
            catch (IOException)
            {
                // Game đang chạy giữ file: bỏ qua
            }
        }

        if (!IsBepInExReady)
        {
            string zip = new[] { "vendor", "BepInEx" }
                .Select(d => Path.Combine(ToolDir, d))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "BepInEx-Unity.IL2CPP-win-x64-*.zip"))
                .FirstOrDefault()
                ?? throw new FileNotFoundException(L.T(@"Thiếu gói BepInEx (vendor\BepInEx-Unity.IL2CPP-win-x64-*.zip) cạnh trainer.",
                                                     @"Missing BepInEx package (vendor\BepInEx-Unity.IL2CPP-win-x64-*.zip) next to the trainer."));
            ExtractRuntime(zip);
            done.Add(L.T("đã chuẩn bị BepInEx trong runtime", "prepared BepInEx in runtime"));
        }

        string bundledLibs = Path.Combine(ToolDir, "vendor", "unity-libs");
        string libs = Path.Combine(BepInExDir, "unity-libs");
        if (Directory.Exists(bundledLibs) && !Directory.Exists(libs))
        {
            CopyDirectory(bundledLibs, libs);
            done.Add(L.T("đã chép thư viện Unity", "copied Unity libraries"));
        }

        var plugin = BundledPlugin.Value;
        if (plugin != null && (!IsPluginInstalled || IsPluginOutdated))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PluginPath));
                File.WriteAllBytes(PluginPath, plugin);
                done.Add(L.T("đã cập nhật DTDMod.dll", "updated DTDMod.dll"));
            }
            catch (IOException)
            {
                // Game đang chạy kèm mod giữ file plugin: lần sau mở tool lúc game tắt sẽ cập nhật.
            }
        }
        return string.Join(", ", done);
    }

    /// <summary>Chỉ lấy BepInEx\ và dotnet\ từ gói; bỏ winhttp.dll / doorstop_config.ini (bộ nạp đặt trong thư mục game).</summary>
    static void ExtractRuntime(string zip)
    {
        string root = Path.GetFullPath(RuntimeDir) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zip);
        foreach (var entry in archive.Entries)
        {
            string name = entry.FullName.Replace('/', '\\');
            if (!name.StartsWith(@"BepInEx\", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith(@"dotnet\", StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.GetFullPath(Path.Combine(RuntimeDir, name));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // chặn đường dẫn ../ trong zip
            if (name.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    static void CopyDirectory(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }

    // ------------------------------------------------------------------ Dọn bản cài cũ trong thư mục game

    /// <summary>Các file bản trainer cũ đã cài vào thư mục game (BepInEx + bộ nạp Doorstop).</summary>
    static readonly string[] LegacyFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" };
    static readonly string[] LegacyDirs = { "BepInEx", "dotnet" };

    /// <summary>Thư mục game còn bản cài cũ của tool (nhận ra qua BepInEx\plugins\DTDMod).</summary>
    public static bool HasLegacyInstall(string gameDir)
        => IsGameDir(gameDir) && Directory.Exists(Path.Combine(gameDir, "BepInEx", "plugins", "DTDMod"));

    /// <summary>
    /// Gỡ bản cài cũ khỏi thư mục game. Interop / thư viện Unity / cache / cấu hình BepInEx đã sinh được chuyển sang
    /// runtime (nếu runtime chưa có) để khỏi phải sinh lại. Chỉ chạy khi thấy dấu hiệu bản cài của tool.
    /// </summary>
    public static void CleanLegacyInstall(string gameDir)
    {
        if (!HasLegacyInstall(gameDir)) return;
        if (IsGameRunning()) throw new InvalidOperationException(L.T("Hãy tắt game trước.", "Close the game first."));

        string oldBepInEx = Path.Combine(gameDir, "BepInEx");
        Directory.CreateDirectory(BepInExDir);
        foreach (var keep in new[] { "interop", "unity-libs", "cache", "config" })
        {
            string from = Path.Combine(oldBepInEx, keep), to = Path.Combine(BepInExDir, keep);
            if (Directory.Exists(from) && !Directory.Exists(to)) CopyDirectory(from, to);
        }

        foreach (var f in LegacyFiles)
        {
            string path = Path.Combine(gameDir, f);
            if (!File.Exists(path)) continue;
            // changelog.txt là tên chung: chỉ xóa nếu đúng là changelog của BepInEx.
            if (f == "changelog.txt" && !File.ReadAllText(path).Contains("BepInEx", StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(path);
        }
        foreach (var d in LegacyDirs)
        {
            string path = Path.Combine(gameDir, d);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }
}
