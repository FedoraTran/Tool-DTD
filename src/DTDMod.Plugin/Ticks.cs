using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;

namespace DTDMod
{
    /// <summary>
    /// Các cheat không thể làm bằng Harmony (mục tiêu bị inline hoặc gộp mã máy),
    /// nên ghi thẳng vào dữ liệu game theo chu kỳ từ <see cref="ModBehaviour"/>.
    /// </summary>
    static class Ticks
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Guard(string name, Action body)
        {
            try { body(); }
            catch (Exception ex)
            {
                if (Reported.Add(name)) Plugin.LogError($"{name} loi: {ex}");
            }
        }
    }

    /// <summary>
    /// Đặt chỉ số nhân viên lên mức tối đa ngay trong StaffData.statusData.
    /// Save chỉ lưu level (chỉ số được tính lại khi tải), và mod nhớ giá trị gốc để trả lại khi tắt.
    /// </summary>
    static class StaffStats
    {
        class Original { public int? Charm, Cook, Dispatch; public float? Serving; }

        static readonly Dictionary<IntPtr, Original> Originals = new Dictionary<IntPtr, Original>();

        public static void Tick()
        {
            bool any = ModState.MaxAppeal || ModState.MaxCooking || ModState.MaxProcure || ModState.MaxServing;
            if (!any && Originals.Count == 0) return;
            if (!Singleton<SushiBarStaffDataManager>.hasInstance) return;
            var mgr = Singleton<SushiBarStaffDataManager>.Instance;
            var list = mgr?.m_StaffList;
            if (list == null) return;

            IntPtr dave = mgr.DataDave?.Pointer ?? IntPtr.Zero;
            bool changed = false;
            foreach (var kv in list)
            {
                var staff = kv.Value;
                if (staff == null || staff.Pointer == dave) continue; // Dave có tốc độ riêng (Walk Speed)
                if (!Originals.TryGetValue(staff.Pointer, out var orig))
                {
                    if (!any) continue;
                    Originals[staff.Pointer] = orig = new Original();
                }

                var sd = staff.statusData;
                bool c = false;
                c |= ApplyInt(ModState.MaxAppeal, ref sd.charm, ref orig.Charm);
                c |= ApplyInt(ModState.MaxCooking, ref sd.cook, ref orig.Cook);
                c |= ApplyInt(ModState.MaxProcure, ref sd.dispatch, ref orig.Dispatch);
                c |= ApplyFloat(ModState.MaxServing, ref sd.serving, ref orig.Serving);
                if (c)
                {
                    staff.statusData = sd;
                    changed = true;
                }
            }

            if (!any) Originals.Clear();
            if (changed)
            {
                mgr.UpdateStaffValues();
                ModState.Hit("MaxStaffStats");
            }
        }

        static bool ApplyInt(bool enabled, ref int value, ref int? original)
        {
            int max = ModState.MaxStatValue;
            if (enabled)
            {
                if (value == max) return false;
                original = value; // cũng bắt được giá trị mới sau khi lên level
                value = max;
                return true;
            }
            if (original == null) return false;
            bool c = value == max;
            if (c) value = original.Value;
            original = null;
            return c;
        }

        static bool ApplyFloat(bool enabled, ref float value, ref float? original)
        {
            float max = ModState.MaxStatValue;
            if (enabled)
            {
                if (value == max) return false;
                original = value;
                value = max;
                return true;
            }
            if (original == null) return false;
            bool c = value == max;
            if (c) value = original.Value;
            original = null;
            return c;
        }
    }

    /// <summary>Giữ hũ wasabi của quán chính và chi nhánh luôn đầy.</summary>
    static class WasabiRefill
    {
        static readonly SushiBar.Place[] Places = { SushiBar.Place.Main, SushiBar.Place.Branch };

        public static void Tick()
        {
            if (!ModState.UnlimitedWasabi) return;
            var op = SushiBarContext.Operation;
            if (op == null) return;
            foreach (var place in Places)
            {
                // Ngoài giờ mở quán (trên thuyền, lúc lặn…) hoặc chưa có chi nhánh, dữ liệu của quán chưa được tạo:
                // IsMaxWasabi/WasabiGraters ném NullReferenceException. Đó là bình thường, không phải lỗi, nên bỏ qua.
                try
                {
                    var graters = op.WasabiGraters(place);
                    if (graters == null || op.IsMaxWasabi(place)) continue;
                    graters.RefillWasabi();
                    ModState.Hit("UnlimitedWasabi.Refill");
                }
                catch
                {
                    ModState.Hit("UnlimitedWasabi.NotOpen." + place);
                }
            }
        }
    }

    /// <summary>
    /// Walk Speed ở những nơi tốc độ là field (không có hàm nào để patch), mỗi nơi một hệ số riêng:
    /// thuyền/nhiệm vụ lén lút (LobbyPlayer), nông trại, trại cá, minigame đuổi Momo.
    /// Nhớ giá trị gốc từng object; nếu game tự đổi giá trị (vd. nâng cấp) thì lấy đó làm gốc mới.
    /// Khựng khi đi nhanh ở FPS cao không nằm ở đây mà ở nhịp vật lý: xem FpsUnlock (GraphicsPatches.cs).
    /// </summary>
    static class LandWalk
    {
        struct Scaled { public float Base, Applied; }

        static readonly Dictionary<(IntPtr, int), Scaled> Fields = new Dictionary<(IntPtr, int), Scaled>();

        // Hệ số đã áp lần trước cho từng nơi: còn khác 1 thì vẫn phải quét để trả lại giá trị gốc.
        static float _boat = 1f, _farm = 1f, _fishFarm = 1f, _chase = 1f;

        public static void Tick()
        {
            if (Fields.Count > 128) Fields.Clear();

            float m = ModState.WalkSpeedBoat;
            if (m != 1f || _boat != 1f)
            {
                ForEach<LobbyPlayer>(p => Scale(p.Pointer, 0, p.m_MoveSpeed, v => p.m_MoveSpeed = v, m, "WalkSpeed.Boat"));
                _boat = m;
            }

            m = ModState.WalkSpeedFarm;
            if (m != 1f || _farm != 1f)
            {
                ForEach<Farm.FarmPlayerView>(p =>
                {
                    Scale(p.Pointer, 1, p.m_Speed_Min, v => p.m_Speed_Min = v, m, "WalkSpeed.Farm");
                    Scale(p.Pointer, 2, p.m_Speed_Max, v => p.m_Speed_Max = v, m, "WalkSpeed.Farm");
                });
                _farm = m;
            }

            m = ModState.WalkSpeedFishFarm;
            if (m != 1f || _fishFarm != 1f)
            {
                ForEach<FishFarm.FishFarmPlayerView>(p => Scale(p.Pointer, 3, p.Dave_Speed, v => p.Dave_Speed = v, m, "WalkSpeed.FishFarm"));
                _fishFarm = m;
            }

            m = ModState.WalkSpeedChase;
            if (m != 1f || _chase != 1f)
            {
                ForEach<MiniGame.Chase.ChaserDave>(d =>
                {
                    var data = d.MoveValue?.MoveData;
                    if (data != null) Scale(data.Pointer, 4, data.defaultMoveSpeed, v => data.defaultMoveSpeed = v, m, "WalkSpeed.Chase");
                });
                _chase = m;
            }
        }

        static void ForEach<T>(Action<T> body) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<T>()))
            {
                var t = o.TryCast<T>();
                if (t != null) body(t);
            }
        }

        static void Scale(IntPtr obj, int field, float current, Action<float> set, float mult, string hit)
        {
            var key = (obj, field);
            if (!Fields.TryGetValue(key, out var entry) || Math.Abs(current - entry.Base * entry.Applied) > 0.0001f)
                entry = new Scaled { Base = current, Applied = 1f }; // lần đầu thấy, hoặc game vừa tự đổi giá trị
            float wanted = entry.Base * mult;
            entry.Applied = mult;
            Fields[key] = entry;
            if (Math.Abs(current - wanted) <= 0.0001f) return;
            set(wanted);
            ModState.Hit(hit);
        }
    }

    /// <summary>Bật/tắt No Weight thì tính lại phạt quá tải ngay, không phải đợi nhặt món tiếp theo.</summary>
    static class WeightRefresher
    {
        static bool _last;

        public static void Tick()
        {
            bool now = ModState.NoWeight;
            if (now == _last) return;
            _last = now;
            if (!SingletonNoMono<LootBox>.hasInstance) return;
            SingletonNoMono<LootBox>.Instance?.RefreshOverweight();
        }
    }

    /// <summary>
    /// Đóng băng cá tại chỗ bằng cờ dừng "Stun" trên từng con (FishAISystem.AddFishStopReason).
    /// Không dùng tín hiệu FreezeAll của game: đó là cơ chế ngưng thời gian khi cắt cảnh/vật lộn với cá lớn,
    /// và IsFishStop.IsCanNotDead() = (lý do &amp; 0x290: FreezeAll | Hooked | GlobalStopByGroupController)
    /// khiến cá không chết được. Với Stun, cá đứng yên, không cắn được (CanDamagerAvailable) nhưng vẫn
    /// giết/bắt bình thường bằng súng móc, cận chiến, drone.
    /// </summary>
    static class FishFreezer
    {
        const IsFishStop.FishStopReason Reason = IsFishStop.FishStopReason.Stun;

        /// <summary>Cá mod đã gắn cờ, để khi tắt chỉ gỡ đúng những con đó.</summary>
        static readonly Dictionary<IntPtr, DR.AI.FishAISystem> Frozen = new Dictionary<IntPtr, DR.AI.FishAISystem>();

        public static void Tick()
        {
            if (ModState.FreezeFish)
            {
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<DR.AI.FishAISystem>()))
                {
                    var fish = o.TryCast<DR.AI.FishAISystem>();
                    if (fish == null) continue;
                    try
                    {
                        // Game tự gỡ Stun khi hết hiệu ứng choáng thật: lần quét sau gắn lại.
                        if (!fish.HasFishStopReason(Reason))
                        {
                            fish.AddFishStopReason(Reason);
                            ModState.Hit("FreezeFish");
                        }
                        Frozen[fish.Pointer] = fish;
                    }
                    catch { /* cá đang bị hủy */ }
                }
                if (Frozen.Count > 2000) Frozen.Clear();
                return;
            }

            if (Frozen.Count == 0) return;
            foreach (var fish in Frozen.Values)
            {
                try
                {
                    if (fish != null) fish.RemoveFishStopReason(Reason);
                }
                catch { /* cá đã bị hủy khi đổi khu vực */ }
            }
            Frozen.Clear();
        }
    }

    /// <summary>
    /// Feed Momo Multiple Times: lúc bật thì xóa "ngày cho ăn gần nhất" trong save (MomoFriendshipPanel.Show
    /// chỉ so chuỗi ngày hôm nay với giá trị này), lúc tắt thì trả lại. Trong lúc bật, setter bị chặn
    /// (SushiBarInfoSave_SetMomoPreviousFeedDay) nên cho ăn bao nhiêu lần cũng không ghi lại ngày.
    /// </summary>
    static class MomoFeedReset
    {
        /// <summary>Đang tự ghi: cho phép đi qua patch chặn setter.</summary>
        public static bool OwnWrite;

        static IntPtr _info;
        static string _savedDay;
        static bool _cleared;

        public static void Tick()
        {
            var info = DR.Save.SaveSystem.GetGameSave()?.SushiBarInfo;
            if (info == null) return;
            if (info.Pointer != _info)
            {
                // Vừa tải save khác: trạng thái cũ không còn áp dụng.
                _info = info.Pointer;
                _cleared = false;
            }

            if (ModState.FeedMomoMultiple)
            {
                if (_cleared) return;
                _savedDay = info.MomoPreviousFeedDay.GetDecrypted() ?? "";
                Write(info, "");
                _cleared = true;
                ModState.Hit("FeedMomoMultiple.Reset");
            }
            else if (_cleared)
            {
                Write(info, _savedDay ?? "");
                _cleared = false;
            }
        }

        static void Write(SushiBarInfoSave info, string day)
        {
            OwnWrite = true;
            try { info.MomoPreviousFeedDay = new CodeStage.AntiCheat.ObscuredTypes.ObscuredString(day); }
            finally { OwnWrite = false; }
        }
    }

    /// <summary>
    /// Đồng hồ trong ngày của Jungle DLC (JDLC.JungleTimeController).
    /// Ngày chia thành khung giờ (sáng / chiều tối / đêm); CurTime là số giây trong game tính từ đầu khung,
    /// GetMaxTime() là độ dài khung đó (giây). TimerRoutine cộng giờ mỗi frame trừ khi IsStopAutoTimeFlow.
    /// </summary>
    static class JungleClock
    {
        /// <summary>Lý do dừng giờ mà menu debug của nhà phát triển dùng (SROptions JungleLobbyOptions.PauseTimer).</summary>
        const JDLC.JungleTimeController.StopUseTimeReason StopReason = JDLC.JungleTimeController.StopUseTimeReason.SROption;

        const float SecondsPerHour = 3600f;

        static bool _stopAdded;

        /// <summary>Bộ điều khiển giờ, chỉ khi đang ở một khung giờ hợp lệ của Jungle (ngoài Jungle trả về null).</summary>
        static JDLC.JungleTimeController Controller()
        {
            if (!Singleton<JDLC.JungleTimeController>.hasInstance) return null;
            var c = Singleton<JDLC.JungleTimeController>.Instance;
            if (c == null || !c.IsInit) return null;
            var section = c.CurrentTimeSection;
            if (section != JDLC.JungleTimeSection.Jungle_Day && section != JDLC.JungleTimeSection.Jungle_Evening &&
                section != JDLC.JungleTimeSection.Jungle_Night) return null;
            return c;
        }

        static int ClockMinutes(JDLC.JungleTimeController c)
        {
            var t = c.GetClockTimeNow();
            return ((t.hour % 24 + 24) % 24) * 60 + t.minute;
        }

        static string Format(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

        public static void RefreshSnapshot()
        {
            var c = Controller();
            GameSnapshot.JungleClock = c == null ? -1 : ClockMinutes(c);
        }

        /// <summary>Stop Time: gắn/gỡ lý do dừng giờ. Controller mới (đổi cảnh) không có lý do này nên lần quét sau gắn lại.</summary>
        public static void StopTimeTick()
        {
            bool want = ModState.JungleStopTime;
            if (!want && !_stopAdded) return;
            if (!Singleton<JDLC.JungleTimeController>.hasInstance)
            {
                _stopAdded = false;
                return;
            }
            var c = Singleton<JDLC.JungleTimeController>.Instance;
            var reasons = c?.stopReasons;
            if (reasons == null) return;

            // Lý do dừng là cờ bit: gắn lại khi đã có cũng vô hại, nên đọc lỗi thì coi như chưa có.
            bool has;
            try { has = reasons.HasFlag(StopReason); }
            catch { has = !want; }
            if (want)
            {
                if (!has)
                {
                    c.AddStopReason(StopReason);
                    ModState.Hit("JungleStopTime");
                }
                _stopAdded = true;
            }
            else
            {
                if (has) c.RemoveStopReason(StopReason);
                _stopAdded = false;
            }
        }

        /// <summary>
        /// Advance/Rewind 1 Hour. Giữ trong khung giờ hiện tại giống SkipCurrentTimeSection của game: tiến tới sát cuối khung
        /// thì để game tự chuyển khung như khi hết giờ tự nhiên; lùi tối đa về đầu khung.
        /// Trả lời: "OK hh:mm" hoặc "OK hh:mm limit" (đã chạm đầu/cuối khung), "ERR nojungle".
        /// </summary>
        public static string Shift(int hours)
        {
            var c = Controller();
            if (c == null) return "ERR nojungle";

            float max = c.GetMaxTime();
            float cur = c.CurTime;
            float target = Math.Clamp(cur + hours * SecondsPerHour, 0f, Math.Max(0f, max - 1f));
            float delta = target - cur;
            bool limit = Math.Abs(delta) < Math.Abs(hours * SecondsPerHour) - 0.5f;
            if (Math.Abs(delta) >= 0.5f)
            {
                c.AddTime(delta);
                // Giống TimerRoutine: cập nhật ánh sáng/môi trường, kim đồng hồ trên HUD.
                c.RefreshEnvironment(true, false, null);
                c.SetTimeCursor();
                c.SyncGameEnvironmentValue();
                ModState.Hit(hours > 0 ? "JungleAdvanceHour" : "JungleRewindHour");
            }
            int now = ClockMinutes(c);
            GameSnapshot.JungleClock = now;
            return "OK " + Format(now) + (limit ? " limit" : "");
        }
    }

    /// <summary>
    /// Gom transform cần làm mượt cho RenderSmoothing (GraphicsPatches.cs): camera chính và Dave của cảnh hiện tại
    /// (lặn, thuyền, quán sushi, nông trại, trại cá, đuổi Momo, làng Jungle). Chỉ khi đang mở khóa FPS.
    /// </summary>
    static class SmoothTargets
    {
        static readonly List<UnityEngine.Transform> Targets = new List<UnityEngine.Transform>();

        public static void Tick()
        {
            Targets.Clear();
            if (!DTDMod.Patches.FpsUnlock.Active)
            {
                DTDMod.Patches.RenderSmoothing.SetTargets(Targets, null);
                return;
            }

            var diver = DTDMod.Patches.PlayerCheats.Current;
            if (diver != null && GameSnapshot.InDive) Targets.Add(diver.transform);
            Add<LobbyPlayer>(p => p.m_Rigidbody != null ? p.m_Rigidbody.transform : p.transform);
            Add<SushiBarPlayerHanlder>(h => h.m_Rigidbody2D != null ? h.m_Rigidbody2D.transform : h.transform);
            Add<Farm.FarmPlayerView>(p => p.m_Rigidbody != null ? p.m_Rigidbody.transform : p.transform);
            Add<FishFarm.FishFarmPlayerView>(p => p.transform);
            Add<MiniGame.Chase.ChaserDave>(d => d.m_Rigidbody2D != null ? d.m_Rigidbody2D.transform : d.transform);
            Add<JDLC.JVillageDaveController>(d => d.transform);

            var cam = UnityEngine.Camera.main;
            DTDMod.Patches.RenderSmoothing.SetTargets(Targets, cam != null ? cam.transform : null);
        }

        static void Add<T>(Func<T, UnityEngine.Transform> pick) where T : UnityEngine.Component
        {
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<T>()))
            {
                var c = o.TryCast<T>();
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var t = pick(c);
                if (t != null) Targets.Add(t);
            }
        }
    }

    /// <summary>
    /// Unlimited Meals cho cả quán chính (SushiBarMenuManager) và quán Jungle (JDLC.JungleSushiBarMenuManager).
    /// Quán chính trừ số phần trong SushiBarMenuManager.Order (đã có patch trả lại ngay), còn quán Jungle trừ ở chỗ
    /// không có hàm riêng để patch, nên ở đây quét mỗi frame: nhớ số phần cao nhất của từng ô menu (theo món),
    /// bị trừ thì trả lại, lưu save và báo giao diện cập nhật. Đổi món/thêm phần thì lấy số mới làm mốc.
    /// </summary>
    static class MealsKeeper
    {
        static readonly Dictionary<IntPtr, (int Recipe, int Max)> Seen = new Dictionary<IntPtr, (int, int)>();

        public static void Tick()
        {
            if (!ModState.UnlimitedMeals)
            {
                if (Seen.Count > 0) Seen.Clear();
                return;
            }
            if (Seen.Count > 256) Seen.Clear();

            if (SingletonNoMono<SushiBarMenuManager>.hasInstance)
            {
                var mgr = SingletonNoMono<SushiBarMenuManager>.Instance;
                var menu = mgr?.m_Menu;
                if (menu != null)
                    foreach (var kv in menu)
                    {
                        var slot = kv.Value;
                        if (slot == null || !Keep(slot.Pointer, slot.recipeID, slot.nowPlateCount, out int max)) continue;
                        slot.nowPlateCount = max;
                        slot.isSoldOut = false;
                        slot.UpdateSaveData(false);
                        NotifyMain(mgr, slot);
                        ModState.Hit("UnlimitedMeals.Keep");
                    }
            }

            if (SingletonNoMono<JDLC.JungleSushiBarMenuManager>.hasInstance)
            {
                var menu = SingletonNoMono<JDLC.JungleSushiBarMenuManager>.Instance?.menu;
                if (menu != null)
                    foreach (var kv in menu)
                    {
                        var slot = kv.Value;
                        if (slot == null || !Keep(slot.Pointer, slot.recipeID, slot.nowPlateCount, out int max)) continue;
                        slot.nowPlateCount = max;
                        slot.isSoldOut = false;
                        slot.UpdateSaveData(false);
                        DREventTriggerManager.JungleMenuEventTrigger(JDLC.JungleMenuEvent.Type.Update, slot);
                        ModState.Hit("UnlimitedMeals.Jungle");
                    }
            }
        }

        /// <summary>true nếu ô menu vừa bị trừ phần (max = số cần trả lại).</summary>
        static bool Keep(IntPtr slot, int recipe, int count, out int max)
        {
            max = count;
            if (recipe <= 0) { Seen.Remove(slot); return false; }  // ô trống
            if (!Seen.TryGetValue(slot, out var seen) || seen.Recipe != recipe || count > seen.Max)
            {
                Seen[slot] = (recipe, count);                       // món mới hoặc vừa nấu thêm
                return false;
            }
            if (count >= seen.Max) return false;
            max = seen.Max;
            return true;
        }

        /// <summary>Báo các bảng menu của quán chính (ISushiBarMenuEventListener) vẽ lại số phần.</summary>
        public static void NotifyMain(SushiBarMenuManager mgr, SushibarMenuSlot slot)
        {
            var listeners = mgr?.m_EventListeners;
            if (listeners == null) return;
            for (int i = 0; i < listeners.Count; i++)
            {
                try { listeners[i]?.OnEventUpdate(slot); }
                catch { /* bảng menu đã bị hủy */ }
            }
        }
    }
}
