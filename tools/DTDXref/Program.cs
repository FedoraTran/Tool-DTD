// Công cụ dò ngược OFFLINE cho người phát triển mod (không cần mở game, không làm game crash).
// Đọc cache mà BepInEx sinh ra trong Tool-DTD\runtime\BepInEx\interop:
//   MethodXrefScanCache.db  : method nào gọi method nào (phân tích tĩnh lúc sinh interop)
//   MethodAddressToToken.db : địa chỉ mã máy -> method
//
// Cách dùng:
//   dotnet run -- [--game <thư mục game>] Type::Method ...          liệt kê "calls" và "usedBy"
//   dotnet run -- [--game <thư mục game>] --fold Type::Method ...   kiểm tra method có bị gộp mã máy không
//   dotnet run -- [--game <thư mục game>] --disasm Type::Method ... dịch ngược mã máy x64 (kèm tên hàm được gọi)
//   dotnet run -- [--game <thư mục game>] --enum Type ...           in tên và giá trị của enum
//   --runtime <thư mục>: mặc định tự tìm Tool-DTD\runtime (đi ngược từ thư mục hiện tại)
// Type lồng nhau dùng dấu '+', vd. SushiBarContext+OperationData::UsingWasabiInCooking
//
// Quy tắc khi thêm patch Harmony: mục tiêu phải "unique" (không bị gộp mã). Nếu getter/setter
// không có "usedBy" nào thì nhiều khả năng đã bị inline, patch sẽ không có tác dụng.

using System.Reflection;
using System.Runtime.Loader;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Common.Maps;

var argList = args.ToList();
string Option(string name, string fallback)
{
    int i = argList.IndexOf(name);
    if (i < 0 || i + 1 >= argList.Count) return fallback;
    string v = argList[i + 1];
    argList.RemoveRange(i, 2);
    return v;
}
// Thư mục game chỉ dùng để ĐỌC GameAssembly.dll (cho --disasm). BepInEx (core, interop) nằm trong Tool-DTD\runtime.
string gameDir = Option("--game", @"D:\Steam\steamapps\common\Dave the Diver");
string runtimeDir = Option("--runtime", FindRuntimeDir());
bool fold = argList.Remove("--fold");
bool disasm = argList.Remove("--disasm");
bool showEnum = argList.Remove("--enum");

string core = Path.Combine(runtimeDir, "BepInEx", "core");
string interop = Path.Combine(runtimeDir, "BepInEx", "interop");

// Đi ngược từ thư mục chạy tới thư mục có runtime\BepInEx\interop (gốc Tool-DTD).
static string FindRuntimeDir()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "runtime");
            if (Directory.Exists(Path.Combine(candidate, "BepInEx", "interop"))) return candidate;
        }
    return Path.Combine(Directory.GetCurrentDirectory(), "runtime");
}
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    foreach (var dir in new[] { interop, core })
    {
        var p = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(p)) return ctx.LoadFromAssemblyPath(p);
    }
    return null;
};

var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(interop, "Assembly-CSharp.dll"));
var map = new MethodAddressToTokenMap(Path.Combine(interop, "MethodAddressToToken.db"));
var lookup = typeof(MethodAddressToTokenMap).BaseType!.GetMethod("Lookup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
var cache = File.ReadAllBytes(Path.Combine(interop, "MethodXrefScanCache.db"));

// Bản ghi cache: header 16 byte, rồi mỗi phần tử { long Address(RVA), long FoundAt, int Type } = 24 byte.
(long Address, int Type) CacheAt(int i) { int o = 16 + i * 24; return (BitConverter.ToInt64(cache, o), BitConverter.ToInt32(cache, o + 16)); }
MethodBase Resolve(long rva) { try { return (MethodBase)lookup.Invoke(map, new object[] { rva }); } catch { return null; } }
string Desc(MethodBase m) => m == null ? "?" : $"{m.DeclaringType?.FullName}::{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})";

IEnumerable<MethodInfo> FindMethods(string spec)
{
    int sep = spec.IndexOf("::", StringComparison.Ordinal);
    var type = sep > 0 ? asm.GetType(spec[..sep]) : null;
    if (type == null && sep > 0)
    {
        // Không có trong Assembly-CSharp: thử các interop khác (UnityEngine.*, Il2Cppmscorlib…)
        foreach (var dll in Directory.GetFiles(interop, "*.dll"))
        {
            try { type = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll).GetType(spec[..sep]); } catch { }
            if (type != null) break;
        }
    }
    if (type == null) { Console.WriteLine("Khong thay type: " + spec); return Array.Empty<MethodInfo>(); }
    return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
               .Where(x => x.Name == spec[(sep + 2)..]);
}

if (showEnum)
{
    foreach (var name in argList)
    {
        var t = asm.GetType(name);
        if (t == null || !t.IsEnum) { Console.WriteLine("Khong phai enum: " + name); continue; }
        Console.WriteLine("== " + name + (t.IsDefined(typeof(FlagsAttribute), false) ? " [Flags]" : ""));
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            Console.WriteLine($"   {f.Name} = {Convert.ToInt64(f.GetRawConstantValue())} (0x{Convert.ToInt64(f.GetRawConstantValue()):X})");
    }
    return;
}

if (disasm)
{
    Disasm.Run(gameDir, map, argList.Select(FindMethods).SelectMany(x => x).ToList(), Desc);
    return;
}

if (fold)
{
    var byAddr = new Dictionary<long, int>();
    var addrOf = new Dictionary<(Module, int), long>();
    foreach (var (addr, m) in (IEnumerable<(long, MethodBase)>)map)
    {
        if (m == null) continue;
        byAddr[addr] = byAddr.GetValueOrDefault(addr) + 1;
        addrOf[(m.Module, m.MetadataToken)] = addr;
    }
    foreach (var spec in argList)
        foreach (var m in FindMethods(spec))
        {
            if (!addrOf.TryGetValue((m.Module, m.MetadataToken), out long a)) { Console.WriteLine($"??          {Desc(m)} (khong co trong bang)"); continue; }
            int n = byAddr[a];
            Console.WriteLine($"{(n > 1 ? $"GOP MA x{n}" : "unique"),-11} {Desc(m)}");
        }
    return;
}

foreach (var spec in argList)
    foreach (var m in FindMethods(spec))
    {
        Console.WriteLine("== " + Desc(m));
        var a = m.GetCustomAttribute<CachedScanResultsAttribute>();
        if (a == null) { Console.WriteLine("   (khong co du lieu cache)"); continue; }
        for (int i = a.XrefRangeStart; i < a.XrefRangeEnd; i++)
        {
            var d = CacheAt(i);
            if (d.Type == 1) Console.WriteLine("   calls  " + (Resolve(d.Address) is { } t ? Desc(t) : $"rva 0x{d.Address:X}"));
        }
        for (int i = a.RefRangeStart; i < a.RefRangeEnd; i++)
        {
            var d = CacheAt(i);
            Console.WriteLine("   usedBy " + (Resolve(d.Address) is { } t ? Desc(t) : $"rva 0x{d.Address:X}"));
        }
    }
