using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DTDMod.Patches
{
    /// <summary>
    /// Mở giới hạn FPS. Game khóa 60 FPS qua Application.targetFrameRate và đặt lại liên tục
    /// (TimeManager.Init, CameraResolution.UpdateVSyncValue mỗi khi đổi cài đặt/màn hình).
    /// Ngoài ra "VSync watchdog" của CameraResolution thấy FPS lệch tần số màn hình (hay gặp với
    /// màn VRR/G-Sync của laptop) thì coi v-sync là không tin cậy, ghi tắt v-sync vào cài đặt và về 60.
    /// Khi bật: tắt v-sync, đặt targetFrameRate theo lựa chọn, áp lại sau mỗi lần game tự đặt,
    /// và tạm dừng watchdog để nó không sửa cài đặt đã lưu của người chơi.
    ///
    /// KHÔNG đổi nhịp vật lý (FixedUpdate 50 lần/giây): nhiều chỗ trong game phụ thuộc nhịp này, vd. đạn/súng móc/lựu đạn
    /// được phóng bằng một lần AddForce kiểu Force (GameObjectExtention.AddForce_Fixed) nên vận tốc = lực × fixedDeltaTime;
    /// tăng nhịp lên 240 thì đạn bay chậm 4,8 lần. Chống khựng ở FPS cao: xem <see cref="RenderSmoothing"/>.
    /// </summary>
    static class FpsUnlock
    {
        public const int MatchMonitor = -1;

        static bool _active;
        static int _monitorHz = 60;
        static float _nextMonitorCheck;

        public static bool Enabled => ModState.FpsLimit != 0;

        /// <summary>Đang mở khóa FPS (đã áp dụng ở frame gần nhất).</summary>
        public static bool Active => _active;

        /// <summary>Tần số màn hình đang hiển thị game, đọc lại mỗi giây (người chơi có thể kéo game sang màn khác).</summary>
        static int MonitorHz()
        {
            if (Time.unscaledTime < _nextMonitorCheck) return _monitorHz;
            _nextMonitorCheck = Time.unscaledTime + 1f;
            var rate = Screen.currentResolution.refreshRateRatio;
            if (rate.denominator != 0)
            {
                int hz = (int)Math.Round(rate.numerator / (double)rate.denominator);
                if (hz >= 30) _monitorHz = hz;
            }
            return _monitorHz;
        }

        public static void Apply()
        {
            int want = ModState.FpsLimit;
            if (want == MatchMonitor) want = MonitorHz();
            if (want > 0)
            {
                _active = true;
                if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
                if (Application.targetFrameRate != want)
                {
                    Application.targetFrameRate = want;
                    ModState.Hit("FpsLimit");
                }
                return;
            }

            if (!_active) return;
            _active = false;
            // Trả quyền lại cho game: tính lại theo cài đặt v-sync của người chơi.
            if (Singleton<CameraResolution>.hasInstance) Singleton<CameraResolution>.Instance.UpdateVSyncValue();
            else Application.targetFrameRate = 60;
        }
    }

    [HarmonyPatch(typeof(CameraResolution), nameof(CameraResolution.UpdateVSyncValue))]
    static class CameraResolution_UpdateVSyncValue
    {
        static void Postfix()
        {
            if (FpsUnlock.Enabled) FpsUnlock.Apply();
        }
    }

    [HarmonyPatch(typeof(CameraResolution), nameof(CameraResolution.UpdateVSyncWatchdog))]
    static class CameraResolution_UpdateVSyncWatchdog
    {
        static bool Prefix() => !FpsUnlock.Enabled;
    }

    /// <summary>
    /// Chống khựng khi FPS cao mà không đổi mô phỏng của game. Camera (CameraManager.FixedUpdate, Cinemachine ở chế độ
    /// FixedUpdate…) và Dave (Rigidbody hoặc CharacterController2D) chỉ đổi vị trí ở nhịp vật lý 50Hz, nên ở 240 FPS
    /// cứ ~5 khung hình mới nhích một lần. Ở đây làm giống Interpolate của Unity, nhưng cho cả camera lẫn Dave:
    /// ghi lại vị trí sau mỗi nhịp, hiển thị ở vị trí nội suy giữa hai nhịp gần nhất (chậm sau một nhịp, như Unity),
    /// rồi trả lại vị trí thật ở cuối khung hình, trước nhịp vật lý kế tiếp. Logic game trong FixedUpdate luôn thấy vị trí thật.
    ///
    /// Tự đứng ngoài với transform bị thứ khác dời theo từng khung hình (vd. Cinemachine ở LateUpdate, Dave đi trong Update):
    /// những transform đó vốn đã mượt, can thiệp chỉ gây giành giật. Camera bám theo Dave mỗi khung hình thì bám theo
    /// vị trí Dave đã nội suy nên cũng mượt.
    /// </summary>
    static class RenderSmoothing
    {
        sealed class Track
        {
            public Transform T;
            public IntPtr Ptr;
            public bool IsCamera;
            public Vector3 Prev, Cur;
            public bool HasKeys;
            public Vector3 True, Applied;
            public bool Pending;
            public bool PerFrame; // bị dời ngoài nhịp vật lý: không can thiệp nữa
            public string Name;

            public void StandAside(string why)
            {
                if (PerFrame) return;
                PerFrame = true;
                Pending = false;
                Plugin.LogInfo($"Lam muot: dung ngoai '{Name}' ({why}).");
            }
        }

        /// <summary>Dời quá xa trong một nhịp thì coi là dịch chuyển tức thời (đổi cảnh, cắt cảnh): không nội suy.</summary>
        const float SnapDistance = 3f;

        static readonly List<Track> Tracks = new List<Track>();

        /// <summary>Số nhịp FixedUpdate đã chạy (ModBehaviour.FixedUpdate tăng).</summary>
        public static int FixedSteps;
        static int _lastSteps;

        static bool Enabled => FpsUnlock.Active;

        /// <summary>Gọi từ tick chậm: danh sách transform cần làm mượt (camera chính + Dave của cảnh hiện tại).</summary>
        public static void SetTargets(List<Transform> targets, Transform camera)
        {
            if (!Enabled)
            {
                Clear();
                return;
            }

            var wanted = new HashSet<IntPtr>();
            foreach (var t in targets) if (t != null) wanted.Add(t.Pointer);
            if (camera != null) wanted.Add(camera.Pointer);

            for (int i = Tracks.Count - 1; i >= 0; i--)
            {
                var k = Tracks[i];
                if (k.T != null && wanted.Remove(k.Ptr)) continue; // còn dùng: giữ nguyên trạng thái
                Restore(k);
                Tracks.RemoveAt(i);
            }

            if (camera != null && wanted.Contains(camera.Pointer))
            {
                Tracks.Add(new Track { T = camera, Ptr = camera.Pointer, IsCamera = true, Name = "camera " + camera.name });
                wanted.Remove(camera.Pointer);
            }
            foreach (var t in targets)
                if (t != null && wanted.Remove(t.Pointer)) Tracks.Add(new Track { T = t, Ptr = t.Pointer, Name = t.name });
        }

        static void Clear()
        {
            foreach (var k in Tracks) Restore(k);
            Tracks.Clear();
        }

        static void Restore(Track k)
        {
            if (!k.Pending) return;
            k.Pending = false;
            if (k.T != null && k.T.position == k.Applied) k.T.position = k.True;
        }

        /// <summary>Đầu Update (sau các nhịp FixedUpdate của khung hình): lấy mốc mới và đặt vị trí nội suy.</summary>
        public static void OnUpdate()
        {
            int steps = FixedSteps - _lastSteps;
            _lastSteps = FixedSteps;
            if (Tracks.Count == 0) return;
            if (!Enabled)
            {
                Clear();
                return;
            }

            float dt = Time.fixedDeltaTime;
            float alpha = dt > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / dt) : 1f;

            for (int i = 0; i < Tracks.Count; i++)
            {
                var k = Tracks[i];
                if (k.T == null || k.PerFrame) continue;
                Vector3 pos = k.T.position;

                if (steps > 0)
                {
                    if (!k.HasKeys || (pos - k.Cur).sqrMagnitude > SnapDistance * SnapDistance)
                    {
                        k.Prev = k.Cur = pos;
                        k.HasKeys = true;
                    }
                    else
                    {
                        k.Prev = k.Cur;
                        k.Cur = pos;
                    }
                }
                else if (!k.HasKeys)
                {
                    k.Prev = k.Cur = pos;
                    k.HasKeys = true;
                }
                else if (pos != k.Cur)
                {
                    // Dời giữa hai nhịp vật lý: thứ khác điều khiển theo khung hình, vốn đã mượt.
                    k.StandAside("doi vi tri ngoai nhip vat ly");
                    continue;
                }

                Vector3 render = Vector3.LerpUnclamped(k.Prev, k.Cur, alpha);
                if (render == pos) continue;
                k.True = pos;
                k.Applied = render;
                k.Pending = true;
                k.T.position = render;
            }
        }

        /// <summary>
        /// Ngay trước khi vẽ (sau LateUpdate): nếu có thứ ghim transform về đúng vị trí thật của nhịp (vd. Cinemachine ghi lại
        /// trạng thái mỗi LateUpdate) thì đặt lại vị trí nội suy; nếu bị dời sang chỗ khác thì đứng ngoài.
        /// </summary>
        public static void OnBeforeRender()
        {
            foreach (var k in Tracks)
            {
                if (!k.Pending || k.T == null) continue;
                Vector3 pos = k.T.position;
                if (pos == k.Applied) continue;
                if (pos == k.True) k.T.position = k.Applied;
                else k.StandAside("bi doi trong LateUpdate");
            }
        }

        /// <summary>Cuối khung hình (đã vẽ xong): trả vị trí thật trước nhịp vật lý kế tiếp.</summary>
        public static void OnEndOfFrame()
        {
            foreach (var k in Tracks)
            {
                if (!k.Pending) continue;
                k.Pending = false;
                if (k.T == null) continue;
                if (k.T.position == k.Applied)
                {
                    k.T.position = k.True;
                    ModState.Hit(k.IsCamera ? "Smooth.Camera" : "Smooth.Dave");
                }
                else k.StandAside("bi doi sau khi ve");
            }
        }

        /// <summary>Cho lệnh DIAG: các transform đang theo dõi, "~" = đang đứng ngoài.</summary>
        public static string Describe()
        {
            try // gọi từ luồng pipe trong lúc luồng game có thể đang sửa danh sách
            {
                var parts = new List<string>();
                foreach (var k in Tracks.ToArray()) parts.Add((k.PerFrame ? "~" : "") + k.Name);
                return string.Join(",", parts);
            }
            catch { return "?"; }
        }
    }
}
