using HarmonyLib;

namespace DTDMod.Patches
{
    // ---------------- Unlimited Meals: số phần món trên menu ----------------

    /// <summary>
    /// Khi khách gọi món, Order() chọn một ô menu rồi trừ thẳng nowPlateCount (ghi field, không qua hàm nào).
    /// Chụp số phần của mọi ô trước Order và trả lại sau đó.
    /// </summary>
    [HarmonyPatch(typeof(SushiBarMenuManager), nameof(SushiBarMenuManager.Order))]
    static class SushiBarMenuManager_Order
    {
        static void Prefix(SushiBarMenuManager __instance, out System.Collections.Generic.Dictionary<int, int> __state)
        {
            __state = null;
            if (!ModState.UnlimitedMeals) return;
            var menu = __instance.m_Menu;
            if (menu == null) return;
            __state = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var kv in menu)
                if (kv.Value != null) __state[kv.Key] = kv.Value.nowPlateCount;
        }

        static void Postfix(SushiBarMenuManager __instance, System.Collections.Generic.Dictionary<int, int> __state)
        {
            if (__state == null) return;
            var menu = __instance.m_Menu;
            if (menu == null) return;
            foreach (var kv in menu)
            {
                var slot = kv.Value;
                if (slot == null || !__state.TryGetValue(kv.Key, out int before) || slot.nowPlateCount >= before) continue;
                slot.nowPlateCount = before;
                slot.isSoldOut = false;
                slot.UpdateSaveData(false);
                MealsKeeper.NotifyMain(__instance, slot); // Order đã báo giao diện số đã trừ: báo lại số đúng
                ModState.Hit("UnlimitedMeals");
            }
        }
    }

    // ---------------- Thức ăn của Momo ----------------

    /// <summary>
    /// Chụp lại số lượng thức ăn (nguyên liệu loại Feed) trước khi game trừ và trả lại sau đó.
    /// Cách này giữ nguyên giá trị trả về và mọi logic phụ của Decrease (thông báo, nhiệm vụ...).
    /// </summary>
    static class IngredientKeeper
    {
        /// <summary>Frame đang bán nguyên liệu: không hoàn trả, tránh biến thành tiền vô hạn.</summary>
        public static int SellingFrame = -1;

        public static int[] Snapshot(IngredientsStorage storage, int ingredientsID)
        {
            if (!ModState.UnlimitedCatFood) return null;
            if (SellingFrame == UnityEngine.Time.frameCount) return null;
            var data = Find(storage, ingredientsID);
            if (data == null || data.type != IngredientsType.Feed) return null;
            var counts = data.counts;
            if (counts == null) return null;
            var copy = new int[counts.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = counts[i];
            return copy;
        }

        public static void Restore(IngredientsStorage storage, int ingredientsID, int[] before)
        {
            if (before == null) return;
            var data = Find(storage, ingredientsID);
            var counts = data?.counts;
            if (counts == null) return;
            bool changed = false;
            for (int i = 0; i < before.Length && i < counts.Length; i++)
            {
                if (counts[i] < before[i]) { counts[i] = before[i]; changed = true; }
            }
            if (!changed) return;
            data.UpdateSaveData();
            ModState.Hit("UnlimitedCatFood");
        }

        static IngredientsData Find(IngredientsStorage storage, int id)
        {
            var dict = storage?.m_Storage;
            if (dict == null) return null;
            return dict.TryGetValue(id, out var data) ? data : null;
        }
    }

    [HarmonyPatch(typeof(IngredientsStorage), nameof(IngredientsStorage.Decrease),
        new[] { typeof(int), typeof(SushiBar.Place), typeof(int), typeof(bool) })]
    static class IngredientsStorage_DecreaseAtPlace
    {
        static void Prefix(IngredientsStorage __instance, int ingredientsID, out int[] __state)
            => __state = IngredientKeeper.Snapshot(__instance, ingredientsID);

        static void Postfix(IngredientsStorage __instance, int ingredientsID, int[] __state)
            => IngredientKeeper.Restore(__instance, ingredientsID, __state);
    }

    [HarmonyPatch(typeof(IngredientsStorage), nameof(IngredientsStorage.Decrease),
        new[] { typeof(int), typeof(int), typeof(bool) })]
    static class IngredientsStorage_Decrease
    {
        static void Prefix(IngredientsStorage __instance, int ingredientsID, out int[] __state)
            => __state = IngredientKeeper.Snapshot(__instance, ingredientsID);

        static void Postfix(IngredientsStorage __instance, int ingredientsID, int[] __state)
            => IngredientKeeper.Restore(__instance, ingredientsID, __state);
    }

    [HarmonyPatch(typeof(IngredientsDetailSellMiddle), nameof(IngredientsDetailSellMiddle.Sell))]
    static class IngredientsDetailSellMiddle_Sell
    {
        static void Prefix() => IngredientKeeper.SellingFrame = UnityEngine.Time.frameCount;
    }

    [HarmonyPatch(typeof(Dredge.Sushibar.DredgeTravellingMerchantPanel), nameof(Dredge.Sushibar.DredgeTravellingMerchantPanel.SellAll_DredgeIngredient))]
    static class DredgeTravellingMerchantPanel_SellAll
    {
        static void Prefix() => IngredientKeeper.SellingFrame = UnityEngine.Time.frameCount;
    }

    // ---------------- Wasabi ----------------
    // Không cho nấu ăn trừ wasabi; phần nạp đầy nằm ở WasabiRefill (Ticks.cs).

    [HarmonyPatch(typeof(SushiBarContext.OperationData), nameof(SushiBarContext.OperationData.UsingWasabiInCooking))]
    static class OperationData_UsingWasabi
    {
        static bool Prefix()
        {
            if (!ModState.UnlimitedWasabi) return true;
            ModState.Hit("UnlimitedWasabi");
            return false;
        }
    }

    // ---------------- Không giới hạn cân nặng ----------------
    // Getter weight/isOverweightState bị gộp mã máy với hàm khác nên không patch;
    // chặn ở hai chỗ game thực sự quyết định: được nhặt thêm không, và có bị phạt quá tải không.

    [HarmonyPatch(typeof(LootBox), nameof(LootBox.CheckOverloadedState))]
    static class LootBox_CheckOverloadedState
    {
        static bool Prefix(ref bool __result)
        {
            if (!ModState.NoWeight) return true;
            __result = false;
            ModState.Hit("NoWeight.Pickup");
            return false;
        }
    }

    [HarmonyPatch(typeof(LootBox), nameof(LootBox.RefreshOverweight), new[] { typeof(float) })]
    static class LootBox_RefreshOverweight
    {
        static void Prefix(ref float targetWeight)
        {
            if (!ModState.NoWeight) return;
            targetWeight = 0f;
            ModState.Hit("NoWeight.Debuff");
        }
    }
}
