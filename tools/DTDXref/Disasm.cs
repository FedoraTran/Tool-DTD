using System.Reflection;
using System.Reflection.PortableExecutable;
using Iced.Intel;
using Il2CppInterop.Common.Maps;

/// <summary>
/// Dịch ngược mã máy của method IL2CPP ngay từ file GameAssembly.dll (không cần chạy game).
/// Lệnh call được chú thích tên method đích nếu tra được trong MethodAddressToToken.db.
/// </summary>
static class Disasm
{
    const int MaxInstructions = 400;

    public static void Run(string gameDir, MethodAddressToTokenMap map, List<MethodInfo> methods, Func<MethodBase, string> desc)
    {
        // Bảng địa chỉ -> method (một địa chỉ có thể ứng với nhiều method nếu bị gộp mã).
        var addrOf = new Dictionary<(Module, int), long>();
        var nameAt = new Dictionary<long, string>();
        foreach (var (addr, m) in (IEnumerable<(long, MethodBase)>)map)
        {
            if (m == null) continue;
            addrOf[(m.Module, m.MetadataToken)] = addr;
            nameAt.TryAdd(addr, desc(m));
        }

        using var fs = File.OpenRead(Path.Combine(gameDir, "GameAssembly.dll"));
        using var pe = new PEReader(fs);
        ulong imageBase = pe.PEHeaders.PEHeader!.ImageBase;

        foreach (var m in methods)
        {
            if (!addrOf.TryGetValue((m.Module, m.MetadataToken), out long rva))
            {
                Console.WriteLine($"?? {desc(m)} (khong co dia chi)");
                continue;
            }
            Console.WriteLine($"== {desc(m)}  @ RVA 0x{rva:X}");

            var block = pe.GetSectionData((int)rva);
            var bytes = block.GetContent().ToArray();
            var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(bytes), imageBase + (ulong)rva);
            var formatter = new NasmFormatter();
            formatter.Options.FirstOperandCharIndex = 8;
            var output = new StringOutput();

            for (int i = 0; i < MaxInstructions && decoder.IP < imageBase + (ulong)rva + (ulong)bytes.Length; i++)
            {
                decoder.Decode(out var ins);
                formatter.Format(ins, output);
                string text = output.ToStringAndReset();

                string note = "";
                if (ins.FlowControl is FlowControl.Call or FlowControl.UnconditionalBranch && ins.Op0Kind is OpKind.NearBranch64)
                {
                    long target = (long)(ins.NearBranchTarget - imageBase);
                    if (nameAt.TryGetValue(target, out var name)) note = "   ; " + name;
                }
                Console.WriteLine($"   {ins.IP - imageBase:X8}  {text}{note}");

                // Dừng ở ret/int3 cuối hàm (int3 là phần đệm giữa các hàm).
                if (ins.Mnemonic is Mnemonic.Int3) break;
                if (ins.FlowControl == FlowControl.Return && i > 0)
                {
                    // Có thể còn nhánh khác phía sau ret; xem thêm vài lệnh tới khi gặp int3.
                    decoder.Decode(out var next);
                    if (next.Mnemonic == Mnemonic.Int3 || next.IsInvalid) break;
                    formatter.Format(next, output);
                    Console.WriteLine($"   {next.IP - imageBase:X8}  {output.ToStringAndReset()}");
                }
            }
        }
    }
}
