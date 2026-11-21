using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using Il2CppInterop.Common;

namespace DTDMod
{
    /// <summary>
    /// GameAssembly.dll được build với "identical code folding": các getter/setter tầm thường
    /// (vd. StaffData.get_Charm) dùng chung một địa chỉ mã máy với hàng trăm hàm không liên quan.
    /// Harmony patch theo địa chỉ native nên patch vào đó sẽ phá luôn các hàm kia.
    /// Lớp này đọc bảng địa chỉ do BepInEx sinh ra để phát hiện và từ chối những mục tiêu như vậy.
    /// </summary>
    static class FoldGuard
    {
        static long[] _rvas;
        static long _base;

        public static void Init()
        {
            try
            {
                string path = Path.Combine(Paths.BepInExRootPath, "interop", "MethodAddressToToken.db");
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                br.ReadInt32(); // magic
                br.ReadInt32(); // version
                br.ReadInt32(); // số assembly
                int count = br.ReadInt32();
                int dataOffset = br.ReadInt32();
                fs.Position = dataOffset;
                var rvas = new long[count];
                for (int i = 0; i < count; i++) rvas[i] = br.ReadInt64();

                foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
                {
                    if (string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        _base = m.BaseAddress.ToInt64();
                        break;
                    }
                }
                if (_base != 0) _rvas = rvas;

                // Tự kiểm tra bằng một getter đã biết là bị gộp; nếu không phát hiện được thì bảng hỏng.
                int probe = SharedCount(HarmonyLib.AccessTools.PropertyGetter(typeof(StaffData), nameof(StaffData.Charm)));
                Plugin.LogInfo($"FoldGuard: {count} dia chi, StaffData.get_Charm dung chung voi {probe - 1} ham (mong doi > 0).");
            }
            catch (Exception ex)
            {
                Plugin.LogWarn("FoldGuard khong doc duoc bang dia chi, bo qua kiem tra: " + ex.Message);
            }
        }

        /// <summary>Số method IL2CPP dùng chung địa chỉ mã máy với <paramref name="method"/> (1 = an toàn).</summary>
        public static int SharedCount(MethodBase method)
        {
            if (_rvas == null || method == null) return 1;
            var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
            if (field == null) return 1;
            var methodInfo = (IntPtr)field.GetValue(null);
            if (methodInfo == IntPtr.Zero) return 1;
            long rva = Marshal.ReadIntPtr(methodInfo).ToInt64() - _base;

            int lo = LowerBound(rva);
            int hi = lo;
            while (hi < _rvas.Length && _rvas[hi] == rva) hi++;
            return Math.Max(hi - lo, 1);
        }

        static int LowerBound(long value)
        {
            int lo = 0, hi = _rvas.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_rvas[mid] < value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
    }
}
