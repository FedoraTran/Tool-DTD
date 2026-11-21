using HarmonyLib;

namespace DTDMod.Patches
{
    // Chỉ số nhân viên (Appeal/Cooking/Procure/Serving) không patch qua getter được vì các getter đó
    // bị gộp mã máy với hàng trăm hàm khác; xem StaffStats trong Ticks.cs.

    [HarmonyPatch(typeof(StaffData), nameof(StaffData.IsMaxLevel))]
    static class StaffData_IsMaxLevel
    {
        static void Postfix(ref bool __result)
        {
            if (!ModState.NoLevelLimit || !__result) return;
            __result = false;
            ModState.Hit("NoLevelLimit");
        }
    }
}
