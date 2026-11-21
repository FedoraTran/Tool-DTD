using System;
using System.Collections;
using System.Globalization;
using BepInEx.Unity.IL2CPP.Utils;
using CodeStage.AntiCheat.ObscuredTypes;
using Il2CppInterop.Runtime;
using DR.Save;
using DTDMod.Patches;
using UnityEngine;
using UnityEngine.Events;

namespace DTDMod
{
    /// <summary>
    /// MonoBehaviour chạy trên luồng game: thực thi lệnh từ trainer, cập nhật snapshot
    /// tiền tệ và chạy các cheat dạng ghi dữ liệu định kỳ (<see cref="Ticks"/>).
    /// </summary>
    public class ModBehaviour : MonoBehaviour
    {
        public ModBehaviour(IntPtr ptr) : base(ptr) { }

        float _nextSlowTick;

        void Start()
        {
            // Hai mốc cho RenderSmoothing: ngay trước khi vẽ, và cuối khung hình (đã vẽ xong).
            Application.add_onBeforeRender(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnBeforeRender)));
            this.StartCoroutine(EndOfFrameLoop());
        }

        void FixedUpdate() => RenderSmoothing.FixedSteps++;

        static void OnBeforeRender() => Ticks.Guard("RenderSmoothing.BeforeRender", RenderSmoothing.OnBeforeRender);

        IEnumerator EndOfFrameLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                Ticks.Guard("RenderSmoothing.EndOfFrame", RenderSmoothing.OnEndOfFrame);
            }
        }

        void OnGUI() => Ticks.Guard("FishEsp.OnGUI", FishEsp.OnGUI);

        void Update()
        {
            Ticks.Guard("RenderSmoothing", RenderSmoothing.OnUpdate);

            while (PipeServer.MainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Plugin.LogError("Lenh trainer loi: " + ex); }
            }

            GameSnapshot.InDive = Time.frameCount - PlayerCheats.LastTickFrame < 30;
            Ticks.Guard("MealsKeeper", MealsKeeper.Tick); // mỗi frame: món vừa bị trừ được trả lại ngay

            if (Time.unscaledTime < _nextSlowTick) return;
            _nextSlowTick = Time.unscaledTime + 0.5f;

            Currency.RefreshSnapshot();
            Ticks.Guard("JungleClock", JungleClock.RefreshSnapshot);
            Ticks.Guard("StaffStats", StaffStats.Tick);
            Ticks.Guard("WasabiRefill", WasabiRefill.Tick);
            Ticks.Guard("LandWalk", LandWalk.Tick);
            Ticks.Guard("WeightRefresher", WeightRefresher.Tick);
            Ticks.Guard("FishFreezer", FishFreezer.Tick);
            Ticks.Guard("MomoFeedReset", MomoFeedReset.Tick);
            Ticks.Guard("JungleStopTime", JungleClock.StopTimeTick);
            Ticks.Guard("SmoothTargets", SmoothTargets.Tick);
            Ticks.Guard("FishEsp", FishEsp.Tick);
        }

        /// <summary>Chạy sau mọi Update của game, nên giá trị FPS của mod là giá trị cuối cùng trong frame.</summary>
        void LateUpdate()
        {
            Ticks.Guard("FpsUnlock", FpsUnlock.Apply);
        }
    }

    /// <summary>
    /// Đặt tiền tệ. Mỗi hàm chạy trên luồng game và trả về dòng trả lời cho trainer:
    /// "OK &lt;giá trị thực tế&gt;" hoặc "ERR &lt;mã lỗi&gt;" (trainer dịch mã lỗi sang VI/EN).
    /// </summary>
    static class Currency
    {
        static PlayerInfoSave GetPlayerInfo()
        {
            var save = SaveSystem.GetGameSave();
            return save?.playerInfo;
        }

        static JDLC.SaveDataJungle GetJungleSave() => SaveSystem.GetGameSave()?.JDLCContents;

        public static void RefreshSnapshot()
        {
            try
            {
                var pi = GetPlayerInfo();
                if (pi == null)
                {
                    GameSnapshot.SaveLoaded = false;
                    GameSnapshot.Gold = GameSnapshot.Bei = GameSnapshot.ChefFlame = -1;
                    GameSnapshot.Doni = GameSnapshot.JungleChefFlame = -1;
                    return;
                }
                GameSnapshot.Gold = pi.gold.GetDecrypted();
                GameSnapshot.Bei = pi.bei.GetDecrypted();
                GameSnapshot.ChefFlame = pi.ChefFlame.GetDecrypted();
                GameSnapshot.SaveLoaded = true;
            }
            catch
            {
                GameSnapshot.SaveLoaded = false;
            }

            try
            {
                var jp = GetJungleSave()?.junglePlayerInfo;
                GameSnapshot.Doni = jp?.jungleGold ?? -1;
                GameSnapshot.JungleChefFlame = jp?.jungleChefFlame ?? -1;
            }
            catch
            {
                GameSnapshot.Doni = GameSnapshot.JungleChefFlame = -1;
            }
        }

        public static string Set(string key, int value)
        {
            string reply;
            switch (key)
            {
                case "setdoni": reply = SetDoni(value); break;
                case "setjunglechefflame": reply = SetJungleChefFlame(value); break;
                default: reply = SetMain(key, value); break;
            }
            Plugin.LogInfo($"{key} {value}: {reply}");
            RefreshSnapshot();
            return reply;
        }

        static string SetMain(string key, int value)
        {
            var pi = GetPlayerInfo();
            if (pi == null) return "ERR nosave";

            var obscured = new ObscuredInt(value);
            switch (key)
            {
                case "setgold": pi.gold = obscured; break;
                case "setbei": pi.bei = obscured; break;
                case "setchefflame": pi.ChefFlame = obscured; break;
                default: return "ERR unknown";
            }
            RefreshUi();
            return "OK " + value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Doni (tiền của Jungle DLC) = JunglePlayerInfoSave.jungleGold.</summary>
        static string SetDoni(int value)
        {
            if (GetPlayerInfo() == null) return "ERR nosave";
            var jungle = GetJungleSave();
            if (jungle?.junglePlayerInfo == null) return "ERR nojunglesave";

            jungle.SetJungleGold(value);
            // Cộng 0 qua đúng hàm của game: HUD Doni được làm mới, và game tự áp giới hạn tối đa nếu có.
            try { SingletonNoMono<JDLC.JungleCommonDefine>.Instance?.AddPlayerGoods(JDLC.JungleGoodsType.Gold, 0, 0, false); }
            catch (Exception ex) { Plugin.LogWarn("Khong lam moi duoc HUD Doni: " + ex.Message); }
            return "OK " + jungle.junglePlayerInfo.jungleGold.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Chef Flame riêng của Jungle DLC (dùng để nghiên cứu món ở quán Jungle).</summary>
        static string SetJungleChefFlame(int value)
        {
            if (GetPlayerInfo() == null) return "ERR nosave";
            var jungle = GetJungleSave();
            var info = jungle?.junglePlayerInfo;
            if (info == null) return "ERR nojunglesave";

            info.jungleChefFlame = value;
            jungle.UpdateJungleSave(); // đánh dấu save cần ghi
            return "OK " + info.jungleChefFlame.ToString(CultureInfo.InvariantCulture);
        }

        static void RefreshUi()
        {
            try
            {
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<LobbyGoldInfoPanelUI>()))
                    o.TryCast<LobbyGoldInfoPanelUI>()?.RefreshGold();

                int bei = GetPlayerInfo()?.bei.GetDecrypted() ?? 0;
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BeiInfoPanelUI>()))
                    o.TryCast<BeiInfoPanelUI>()?.SetBeiTextNow(bei);
            }
            catch (Exception ex)
            {
                Plugin.LogWarn("Khong lam moi duoc UI tien: " + ex.Message);
            }
        }
    }
}
