using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using SushiBar.Customer;

namespace DTDMod.Patches
{
    // ---------------- Khách không bao giờ hết kiên nhẫn ----------------

    [HarmonyPatch(typeof(SushiBarCustomer), nameof(SushiBarCustomer.Angry))]
    static class SushiBarCustomer_Angry
    {
        static bool Prefix()
        {
            if (!ModState.UnlimitedPatience) return true;
            ModState.Hit("UnlimitedPatience");
            return false;
        }
    }

    /// <summary>Khách đặc biệt (VIP) tự xử lý trước khi gọi base.Angry, nên chặn riêng.</summary>
    [HarmonyPatch(typeof(SushiBarSpecialCustomer), nameof(SushiBarSpecialCustomer.Angry))]
    static class SushiBarSpecialCustomer_Angry
    {
        static bool Prefix()
        {
            if (!ModState.UnlimitedPatience) return true;
            ModState.Hit("UnlimitedPatience.Special");
            return false;
        }
    }

    // KHÔNG kéo dài GetOrderWaitTime: đó là thời gian khách "chọn món" (hiện "..." trong WaitForOrder, đếm tới giá trị này
    // rồi mới gọi món), không phải thời gian kiên nhẫn. Nhân lên thì khách cứ "..." mãi, không gọi món.
    // Thời gian chờ món/nước (WaitForServing, WaitForDrinkServing) đọc thẳng từ dữ liệu khách, hết giờ thì gọi Angry:
    // chặn Angry ở trên là đủ để khách không bỏ về.

    // ---------------- Nấu ăn tức thì ----------------

    static class InstantCook
    {
        // Không trả về 0 để tránh chia cho 0 trong thanh tiến trình của game.
        public const float Time = 0.05f;
    }

    [HarmonyPatch(typeof(SushiBarStaffBase), nameof(SushiBarStaffBase.CalcCookingTime))]
    static class SushiBarStaffBase_CalcCookingTime
    {
        static void Postfix(ref float __result)
        {
            if (!ModState.InstantCooking) return;
            __result = InstantCook.Time;
            ModState.Hit("InstantCooking");
        }
    }

    [HarmonyPatch(typeof(GameFormulaManager), nameof(GameFormulaManager.CalcCookingTime))]
    static class GameFormulaManager_CalcCookingTime
    {
        static void Postfix(ref float __result)
        {
            if (!ModState.InstantCooking) return;
            __result = InstantCook.Time;
            ModState.Hit("InstantCooking.Formula");
        }
    }

    // ---------------- Chụp ảnh dễ lấy nét ----------------

    /// <summary>Hạ tỉ lệ phần thân mục tiêu phải nằm trong khung về gần 0 và bỏ đếm lỗi lấy nét.</summary>
    [HarmonyPatch(typeof(PhotoZonePanel), nameof(PhotoZonePanel.Init))]
    static class PhotoZonePanel_Init
    {
        static void Postfix(Il2CppSystem.Collections.Generic.List<PhotoZoneObjective> objs)
        {
            if (!ModState.EasyCameraFocus || objs == null) return;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o != null && o.passPercentage > 0.01f) o.passPercentage = 0.01f;
            }
            ModState.Hit("EasyCameraFocus");
        }
    }

    [HarmonyPatch(typeof(PhotoZonePanel), nameof(PhotoZonePanel.UpateFocusFailCount))]
    static class PhotoZonePanel_UpateFocusFailCount
    {
        static bool Prefix()
        {
            if (!ModState.EasyCameraFocus) return true;
            ModState.Hit("EasyCameraFocus.Fail");
            return false;
        }
    }

    // ---------------- Cho Momo ăn nhiều lần ----------------

    /// <summary>
    /// Game ghi "ngày cho ăn gần nhất" để chặn lần thứ hai trong ngày. MomoFriendshipPanel.SuccessFeeding
    /// tự ghi thẳng giá trị này (không qua SaveData.SaveMomoFeedingData), nên chặn ngay tại setter.
    /// Độ thân thiết vẫn được cộng bình thường. Xóa ngày đã lưu khi bật: xem MomoFeedReset (Ticks.cs).
    /// </summary>
    [HarmonyPatch(typeof(SushiBarInfoSave), nameof(SushiBarInfoSave.MomoPreviousFeedDay), MethodType.Setter)]
    static class SushiBarInfoSave_SetMomoPreviousFeedDay
    {
        static bool Prefix()
        {
            if (!ModState.FeedMomoMultiple || MomoFeedReset.OwnWrite) return true;
            ModState.Hit("FeedMomoMultiple");
            return false;
        }
    }
}
