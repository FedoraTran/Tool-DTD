using System;
using System.Collections.Generic;
using DTDMod.Patches;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DTDMod
{
    /// <summary>
    /// Fish Detector: khi lặn, vẽ đường từ Dave tới cá/tôm/sinh vật bắt được (DR.AI.FishAISystem) kèm tên và khoảng cách.
    /// Danh sách cá làm mới mỗi 0.5 giây (tick chậm); vị trí đọc lại mỗi khung hình trong OnGUI.
    /// Tên lấy theo ngôn ngữ game: DataManager.GetText(FishInfoData.NameTextID).
    /// Màu theo DR.AI.EnumFishType: thường = xanh, phòng thủ = vàng, tấn công = đỏ, đặc biệt (sứa, cá nóc, lươn) = tím, boss = đỏ đậm.
    /// </summary>
    static class FishEsp
    {
        sealed class Entry
        {
            public DR.AI.FishAISystem Fish;
            public Transform T;
            public string Name;
            public Color Color;
        }

        const int MaxShown = 40;
        const float MaxDistance = 150f;

        static readonly List<Entry> Entries = new List<Entry>();
        static readonly Dictionary<int, string> NameCache = new Dictionary<int, string>();
        static readonly List<(Entry E, float Dist)> Visible = new List<(Entry, float)>();

        static GUIStyle _label;
        static GUIStyle _shadow;
        static int _fontSize;

        static readonly Color Normal = new Color(0.35f, 0.9f, 1f);
        static readonly Color Defence = new Color(1f, 0.85f, 0.3f);
        static readonly Color Attacker = new Color(1f, 0.38f, 0.38f);
        static readonly Color Special = new Color(0.85f, 0.55f, 1f);
        static readonly Color Boss = new Color(1f, 0.15f, 0.25f);

        static bool ShouldDraw => ModState.FishEsp && GameSnapshot.InDive && PlayerCheats.Current != null;

        /// <summary>Tick chậm: quét lại cá trong cảnh.</summary>
        public static void Tick()
        {
            Entries.Clear();
            if (!ShouldDraw) return;

            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<DR.AI.FishAISystem>()))
            {
                var fish = o.TryCast<DR.AI.FishAISystem>();
                if (fish == null || !fish.gameObject.activeInHierarchy) continue;
                try
                {
                    if (IsDead(fish)) continue;
                    var info = fish._FishInfoData;
                    Entries.Add(new Entry
                    {
                        Fish = fish,
                        T = fish.transform,
                        Name = NameOf(fish, info),
                        Color = ColorOf(info),
                    });
                }
                catch { /* cá đang bị hủy */ }
            }
        }

        static bool IsDead(DR.AI.FishAISystem fish)
        {
            try { return fish.IsCorpseRP != null && fish.IsCorpseRP.Value; }
            catch { return false; }
        }

        static string NameOf(DR.AI.FishAISystem fish, DR.FishInfoData info)
        {
            int tid = fish.FishDataTID;
            if (NameCache.TryGetValue(tid, out var cached)) return cached;
            string name = null;
            try
            {
                if (info != null && !string.IsNullOrEmpty(info.NameTextID) && Singleton<DataManager>.hasInstance)
                    name = Singleton<DataManager>.Instance.GetText(info.NameTextID);
            }
            catch { }
            if (string.IsNullOrWhiteSpace(name) || name == info?.NameTextID) name = info?.FishName;
            if (string.IsNullOrWhiteSpace(name)) name = "#" + tid;
            NameCache[tid] = name;
            return name;
        }

        static Color ColorOf(DR.FishInfoData info)
        {
            switch (info?.FishType ?? 0)
            {
                case 1: return Defence;                    // k01_DefenceFish
                case 2: case 6: return Attacker;           // k02_AttackerFish, k06_Special_AttackerFish
                case 3: case 4: case 5: return Special;    // sứa, cá nóc, lươn
                case 7: return Boss;
                default: return Normal;
            }
        }

        /// <summary>Gọi từ ModBehaviour.OnGUI.</summary>
        public static void OnGUI()
        {
            if (Entries.Count == 0 || !ShouldDraw) return;
            var ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint) return;
            var cam = Camera.main;
            var player = PlayerCheats.Current;
            if (cam == null || player == null) return;

            EnsureStyles();
            Vector3 davePos = player.transform.position;
            Vector3 daveScreen = cam.WorldToScreenPoint(davePos);
            if (daveScreen.z <= 0f) return;
            var from = new Vector2(daveScreen.x, Screen.height - daveScreen.y);

            Visible.Clear();
            foreach (var e in Entries)
            {
                if (e.T == null || e.Fish == null) continue;
                float d = Vector2.Distance(davePos, e.T.position);
                if (d <= MaxDistance) Visible.Add((e, d));
            }
            Visible.Sort((a, b) => a.Dist.CompareTo(b.Dist));

            var oldColor = GUI.color;
            int shown = 0;
            foreach (var (e, dist) in Visible)
            {
                if (shown++ >= MaxShown) break;
                Vector3 sp = cam.WorldToScreenPoint(e.T.position);
                if (sp.z <= 0f) continue;
                var to = new Vector2(sp.x, Screen.height - sp.y);

                var line = e.Color;
                line.a = 0.55f;
                DrawLine(from, to, line, Math.Max(1.5f, _fontSize / 9f));

                string text = $"{e.Name} {dist:0}m";
                var size = _label.CalcSize(new GUIContent(text));
                // Cá ngoài màn hình: nhãn nằm sát mép, đường vẫn chỉ về phía con cá.
                float x = Mathf.Clamp(to.x - size.x / 2f, 4f, Screen.width - size.x - 4f);
                float y = Mathf.Clamp(to.y - size.y - 6f, 4f, Screen.height - size.y - 4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 1f, y + 1f, size.x, size.y), text, _shadow);
                _label.normal.textColor = e.Color;
                GUI.Label(new Rect(x, y, size.x, size.y), text, _label);
            }
            GUI.color = oldColor;
        }

        static void EnsureStyles()
        {
            int size = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 1080f * 15f), 12, 30);
            if (_label != null && size == _fontSize) return;
            _fontSize = size;
            _label = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, wordWrap = false };
            _shadow = new GUIStyle(_label);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
