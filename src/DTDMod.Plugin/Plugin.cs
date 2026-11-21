using System;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace DTDMod
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "tool.dtd.trainer";
        public const string Name = "DTD Trainer Mod";
        public const string Version = "1.0.0";

        static ManualLogSource _log;

        public static int PatchesOk;
        public static int PatchesFailed;
        public static string PatchReport = "";

        public static void LogInfo(string msg) => _log?.LogInfo(msg);
        public static void LogWarn(string msg) => _log?.LogWarning(msg);
        public static void LogError(string msg) => _log?.LogError(msg);

        public override void Load()
        {
            _log = Log;
            FoldGuard.Init();
            ApplyPatches();
            AddComponent<ModBehaviour>();
            PipeServer.Start();
            Log.LogInfo($"{Name} {Version} da tai. Patch OK={PatchesOk}, loi={PatchesFailed}. Pipe: {PipeServer.PipeName}");
        }

        /// <summary>
        /// Áp từng lớp patch riêng lẻ: một method đổi tên sau bản cập nhật game
        /// chỉ làm hỏng đúng tính năng đó chứ không kéo theo tất cả.
        /// </summary>
        void ApplyPatches()
        {
            var harmony = new Harmony(Guid);
            var report = new StringBuilder();
            var patchClasses = typeof(Plugin).Assembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                .OrderBy(t => t.FullName);

            foreach (var type in patchClasses)
            {
                try
                {
                    var target = ResolveTarget(type);
                    if (target == null) throw new MissingMethodException("Khong tim thay method dich");
                    int shared = FoldGuard.SharedCount(target);
                    if (shared > 1)
                        throw new InvalidOperationException($"{target.DeclaringType?.Name}.{target.Name} dung chung ma may voi {shared - 1} ham khac, patch se pha game");

                    harmony.CreateClassProcessor(type).Patch();
                    PatchesOk++;
                    report.Append(type.Name).Append(":ok,");
                }
                catch (Exception ex)
                {
                    PatchesFailed++;
                    report.Append(type.Name).Append(":FAIL,");
                    Log.LogError($"Patch {type.Name} bi bo qua: {ex.GetBaseException().Message}");
                }
            }
            PatchReport = report.ToString();
        }

        static MethodBase ResolveTarget(Type patchClass)
        {
            Type declaring = null;
            string name = null;
            MethodType? kind = null;
            Type[] args = null;
            foreach (HarmonyPatch attr in patchClass.GetCustomAttributes(typeof(HarmonyPatch), false))
            {
                declaring ??= attr.info.declaringType;
                name ??= attr.info.methodName;
                kind ??= attr.info.methodType;
                args ??= attr.info.argumentTypes;
            }
            if (declaring == null || name == null) return null;
            if (kind == MethodType.Getter) return AccessTools.PropertyGetter(declaring, name);
            if (kind == MethodType.Setter) return AccessTools.PropertySetter(declaring, name);
            return AccessTools.Method(declaring, name, args);
        }
    }
}
