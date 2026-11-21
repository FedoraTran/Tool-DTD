using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DTDMod.Patches
{
    /// <summary>
    /// Các cheat cần ép giá trị mỗi frame trong lúc lặn. Chạy sau PlayerCharacter.Update
    /// nên luôn có player hợp lệ và đúng thời điểm game vừa cập nhật xong.
    /// </summary>
    static class PlayerCheats
    {
        public static int LastTickFrame = -1000;

        /// <summary>Dave đang lặn (null khi không lặn hoặc đã bị hủy).</summary>
        public static PlayerCharacter Current;

        static IntPtr _player;
        static bool _breathApplied;
        static int _droneMax;
        static int _crabMax;

        public static void Tick(PlayerCharacter player)
        {
            LastTickFrame = Time.frameCount;
            Current = player;

            if (player.Pointer != _player)
            {
                // Lượt lặn mới: tạo player mới, trạng thái cũ không còn giá trị.
                _player = player.Pointer;
                _breathApplied = false;
                _droneMax = 0;
                _crabMax = 0;
                SlotMax.Clear();
            }

            Guard("Oxygen", () => Oxygen(player));
            Guard("Drones", () => _droneMax = KeepCount(ModState.UnlimitedDrones, _droneMax,
                player.AvailableLiftDroneCount, v => player.AvailableLiftDroneCount = v, "UnlimitedDrones"));
            Guard("CrabTraps", () => _crabMax = KeepCount(ModState.UnlimitedCrabTraps, _crabMax,
                player.AvailableCrabTrapCount, v => player.AvailableCrabTrapCount = v, "UnlimitedCrabTraps"));
            if (ModState.UnlimitedAmmo) Guard("Ammo", () => Ammo(player));
            if (ModState.UnlimitedBattery) Guard("Battery", () => Battery(player));
            if (ModState.UnlimitedPurificationFilter) Guard("PurificationFilter", () => PurificationFilter(player));
            if (ModState.UnlimitedShopItems) Guard("ShopItems", () => ShopItems(player));
            else if (SlotMax.Count > 0) SlotMax.Clear();
        }

        static void Oxygen(PlayerCharacter player)
        {
            bool want = ModState.UnlimitedOxygen;
            if (want != _breathApplied)
            {
                player.SetBreathInvincible(InvincibleCallerType.Cheat, want);
                _breathApplied = want;
            }
            if (!want) return;

            // Oxy và máu là một thanh duy nhất trong game: giữ luôn đầy.
            var breath = player.BreathHandler;
            if (breath == null) return;
            float max = breath.MaxHP;
            if (max > 0f && breath.HP < max)
            {
                breath.HP = max;
                ModState.Hit("UnlimitedOxygen");
            }
        }

        /// <summary>Ghi nhớ số lượng cao nhất trong lượt lặn và trả lại mỗi khi game trừ đi.</summary>
        static int KeepCount(bool enabled, int remembered, int current, Action<int> set, string hitKey)
        {
            if (!enabled || current >= remembered) return Math.Max(current, enabled ? remembered : 0);
            set(remembered);
            ModState.Hit(hitKey);
            return remembered;
        }

        static void Ammo(PlayerCharacter player)
        {
            var gun = player.CurrentInstanceItemInventory?.gunHandler;
            if (gun == null) return;
            int max = gun.m_MaxAmmo;
            if (max > 0 && gun.GetAmmo() < max)
            {
                gun.ForceSetBulletCount(max);
                ModState.Hit("UnlimitedAmmo.Refill");
            }
        }

        static void Battery(PlayerCharacter player)
        {
            var slots = player.CurrentSubHelperItemInventory?.subHelperSlots;
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var spec = slot?.subHelper;
                if (spec == null || !(spec.IsBatteryType || spec.IsRechargeType)) continue;
                float full = spec.BatteryDuration;
                if (full > 0f && slot.remainTime < full)
                {
                    slot.remainTime = full;
                    ModState.Hit("UnlimitedBattery");
                }
            }
        }

        /// <summary>
        /// Jungle DLC: bộ lọc (khiên chống ô nhiễm) bị trừ khi lặn trong vùng nước độc (HazardLevelDefiner).
        /// Giữ luôn đầy qua setter của game, để HUD (HazardShieldUI) cập nhật theo ReactiveProperty.
        /// Ngoài vùng Jungle MaxHP = 0 nên không làm gì.
        /// </summary>
        static void PurificationFilter(PlayerCharacter player)
        {
            float max = player.HazardShieldMaxHP;
            if (max <= 0f || player.HazardShieldHP >= max) return;
            player.HazardShieldHP = max;
            ModState.Hit("UnlimitedPurificationFilter");
        }

        /// <summary>Số lượng cao nhất đã thấy của từng ô đồ phụ trợ trong lượt lặn (khóa: chỉ số ô).</summary>
        static readonly Dictionary<int, (IntPtr Spec, int Max)> SlotMax = new Dictionary<int, (IntPtr, int)>();

        /// <summary>
        /// Dự phòng cho SubHelperItemInventory_ConsumeInstant (nếu hàm bị inline ở nơi gọi): đồ đếm theo số lượng
        /// bị trừ thì trả lại số cao nhất của lượt lặn. Đồ dùng pin do Unlimited Item Battery lo.
        /// </summary>
        static void ShopItems(PlayerCharacter player)
        {
            var inv = player.CurrentSubHelperItemInventory;
            var slots = inv?.subHelperSlots;
            if (slots == null) return;
            bool restored = false;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var spec = slot?.subHelper;
                if (spec == null || spec.IsBatteryType || spec.IsRechargeType) continue;
                int count = slot.remainCount;
                if (!SlotMax.TryGetValue(i, out var seen) || seen.Spec != spec.Pointer || count > seen.Max)
                {
                    SlotMax[i] = (spec.Pointer, count);
                    continue;
                }
                if (count >= seen.Max) continue;
                slot.remainCount = seen.Max;
                restored = true;
            }
            if (!restored) return;
            inv.RefreshUIData();
            ModState.Hit("UnlimitedShopItems.Refill");
        }

        static readonly HashSet<string> Reported = new HashSet<string>();

        static void Guard(string name, Action body)
        {
            try { body(); }
            catch (Exception ex)
            {
                if (Reported.Add(name)) Plugin.LogError($"Cheat {name} loi: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
    static class PlayerCharacter_Update
    {
        static void Postfix(PlayerCharacter __instance) => PlayerCheats.Tick(__instance);
    }

    [HarmonyPatch(typeof(GunWeaponHandler), nameof(GunWeaponHandler.DecreaseBullet))]
    static class GunWeaponHandler_DecreaseBullet
    {
        static bool Prefix(GunWeaponHandler __instance)
        {
            if (!ModState.UnlimitedAmmo || !__instance.IsPlayersHandler) return true;
            ModState.Hit("UnlimitedAmmo");
            return false;
        }
    }

    /// <summary>
    /// Đồ phụ trợ dùng một lần (mua ở cửa hàng): hàm này trừ remainCount của ô đang chọn, về 0 thì xóa ô.
    /// Chặn hẳn để số lượng giữ nguyên (cũng không bị trừ khỏi kho sau lượt lặn).
    /// </summary>
    [HarmonyPatch(typeof(SubHelperItemInventory), nameof(SubHelperItemInventory.ConsumeInstantSubHelper))]
    static class SubHelperItemInventory_ConsumeInstant
    {
        static bool Prefix()
        {
            if (!ModState.UnlimitedShopItems) return true;
            ModState.Hit("UnlimitedShopItems");
            return false;
        }
    }

    [HarmonyPatch(typeof(SubHelperItemInventory), nameof(SubHelperItemInventory.ConsumeBatterySubHelper))]
    static class SubHelperItemInventory_ConsumeBattery
    {
        static bool Prefix()
        {
            if (!ModState.UnlimitedBattery) return true;
            ModState.Hit("UnlimitedBattery.Consume");
            return false;
        }
    }

    // ---------------- Tốc độ ----------------

    [HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.DetermineMoveSpeed))]
    static class PlayerCharacter_DetermineMoveSpeed
    {
        static void Postfix(ref float __result)
        {
            float m = ModState.SwimSpeed;
            if (m == 1f) return;
            __result *= m;
            ModState.Hit("SwimSpeed");
        }
    }

    // ---- Walk Speed: Dave đi bộ trên cạn ----
    // Quán sushi (chính, sự kiện, Jungle): StaffDave/StaffDaveEvent.CalcMoveSpeed và JungleSushiBarStaffDave
    // đều nhân với DaveMoveValue.speedMultiplier, nên patch một chỗ này là đủ.
    // Thuyền, nông trại, trại cá, minigame đuổi Momo: xem LandWalk trong Ticks.cs.
    // Mỗi nơi một hệ số riêng (ModState.WalkSpeed*).

    [HarmonyPatch(typeof(Common.Contents.DaveMoveValue), nameof(Common.Contents.DaveMoveValue.speedMultiplier), MethodType.Getter)]
    static class DaveMoveValue_SpeedMultiplier
    {
        static void Postfix(ref float __result)
        {
            float m = ModState.WalkSpeedSushi;
            if (m == 1f) return;
            __result *= m;
            ModState.Hit("WalkSpeed.SushiBar");
        }
    }

    /// <summary>Làng Jungle: Move(dir, dt) dời Dave theo dir * tốc độ * dt, nên nhân dt.</summary>
    [HarmonyPatch(typeof(JDLC.JVillageLobbyMove), nameof(JDLC.JVillageLobbyMove.Move))]
    static class JVillageLobbyMove_Move
    {
        static void Prefix(ref float dt)
        {
            float m = ModState.WalkSpeedJungle;
            if (m == 1f) return;
            dt *= m;
            ModState.Hit("WalkSpeed.JungleVillage");
        }
    }

    // ---------------- Stamina ở quán sushi ----------------

    [HarmonyPatch(typeof(Common.Contents.DaveMoveValue), nameof(Common.Contents.DaveMoveValue.Update))]
    static class DaveMoveValue_Update
    {
        static void Postfix(Common.Contents.DaveMoveValue __instance)
        {
            if (!ModState.UnlimitedStamina) return;
            var data = __instance.moveData;
            if (data == null) return;
            float max = data.staminaMax;
            if (__instance.stamina < max) __instance.stamina = max;
            __instance.remainStaminaZeroPenaltyTime = 0f;
            ModState.Hit("UnlimitedStamina");
        }
    }
}
