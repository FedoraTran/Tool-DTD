using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DTDMod
{
    /// <summary>
    /// Trạng thái các cheat. Được ghi từ luồng pipe, đọc từ luồng game,
    /// nên mọi trường đều là volatile và chỉ chứa kiểu nguyên thủy.
    /// Tên trường chính là khóa dùng trong giao thức với trainer.
    /// </summary>
    public static class ModState
    {
        // Player
        public static volatile bool UnlimitedOxygen;
        public static volatile bool UnlimitedStamina;
        public static volatile bool UnlimitedPurificationFilter; // Jungle DLC: bộ lọc (khiên) khi lặn vùng nước ô nhiễm

        // Inventory
        public static volatile bool UnlimitedBattery;
        public static volatile bool UnlimitedMeals;
        public static volatile bool UnlimitedWasabi;
        public static volatile bool UnlimitedDrones;
        public static volatile bool UnlimitedCrabTraps;
        public static volatile bool UnlimitedShopItems;   // đồ phụ trợ dùng một lần mua ở cửa hàng (bình oxy phụ, hộp đạn, lưới, bom…)
        public static volatile bool NoWeight;

        // Stats (nhân viên quán sushi)
        public static volatile bool NoLevelLimit;
        public static volatile bool MaxAppeal;
        public static volatile bool MaxCooking;
        public static volatile bool MaxProcure;
        public static volatile bool MaxServing;

        // Weapons
        public static volatile bool UnlimitedAmmo;

        // Enemies
        public static volatile bool FreezeFish;
        public static volatile bool FishEsp;              // vẽ đường từ Dave tới cá/tôm kèm tên và khoảng cách

        // Game
        public static volatile bool UnlimitedPatience;
        public static volatile bool InstantCooking;
        public static volatile bool EasyCameraFocus;
        public static volatile bool UnlimitedCatFood;
        public static volatile bool FeedMomoMultiple;
        public static volatile bool JungleStopTime;             // Jungle DLC: đồng hồ trong ngày đứng yên

        // Physics (1 = tắt)
        public static volatile float SwimSpeed = 1f;
        // Tốc độ đi bộ trên cạn, tách riêng từng nơi
        public static volatile float WalkSpeedSushi = 1f;     // quán sushi (chính, sự kiện, Jungle)
        public static volatile float WalkSpeedBoat = 1f;      // thuyền, nhiệm vụ lén lút
        public static volatile float WalkSpeedFarm = 1f;      // nông trại
        public static volatile float WalkSpeedFishFarm = 1f;  // trại cá
        public static volatile float WalkSpeedJungle = 1f;    // làng Jungle
        public static volatile float WalkSpeedChase = 1f;     // minigame đuổi Momo

        // Graphics: giới hạn FPS (0 = để game tự quyết, mặc định của game là 60; -1 = theo tần số màn hình)
        public static volatile int FpsLimit;

        public const int MaxStatValue = 999;

        static readonly Dictionary<string, FieldInfo> Fields = BuildFieldMap();

        static Dictionary<string, FieldInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in typeof(ModState).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.IsLiteral) continue;
                if (f.FieldType == typeof(bool) || f.FieldType == typeof(float) || f.FieldType == typeof(int)) map[f.Name] = f;
            }
            return map;
        }

        public static bool TrySet(string name, string value)
        {
            if (!Fields.TryGetValue(name, out var f)) return false;
            if (f.FieldType == typeof(bool))
            {
                bool b = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                f.SetValue(null, b);
                return true;
            }
            if (f.FieldType == typeof(int))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return false;
                f.SetValue(null, Math.Clamp(n, -1, 1000));
                return true;
            }
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
            if (float.IsNaN(v) || v <= 0f) v = 1f;
            f.SetValue(null, Math.Min(v, 20f));
            return true;
        }

        /// <summary>Tắt mọi cheat (giống Wand/WeMod: đóng app là mod ngừng). Các tick sẽ tự trả dữ liệu về như cũ.</summary>
        public static void ResetAll()
        {
            foreach (var f in Fields.Values)
                f.SetValue(null, f.FieldType == typeof(bool) ? false : f.FieldType == typeof(int) ? 0 : (object)1f);
        }

        public static string Describe()
        {
            var sb = new StringBuilder();
            foreach (var kv in Fields)
            {
                object v = kv.Value.GetValue(null);
                sb.Append(kv.Key).Append('=');
                sb.Append(v switch
                {
                    bool b => b ? "1" : "0",
                    float fl => fl.ToString(CultureInfo.InvariantCulture),
                    _ => Convert.ToString(v, CultureInfo.InvariantCulture),
                });
                sb.Append(';');
            }
            return sb.ToString();
        }

        // ---- Chẩn đoán: đếm số lần mỗi patch thực sự can thiệp ----
        static readonly ConcurrentDictionary<string, int> HitCounts = new ConcurrentDictionary<string, int>();

        public static void Hit(string key) => HitCounts.AddOrUpdate(key, 1, (_, n) => n + 1);

        public static string DescribeHits()
        {
            var sb = new StringBuilder();
            foreach (var kv in HitCounts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            return sb.ToString();
        }
    }

    /// <summary>Giá trị game được luồng chính cập nhật để luồng pipe đọc.</summary>
    public static class GameSnapshot
    {
        public static volatile bool SaveLoaded;
        public static volatile bool InDive;
        public static volatile int Gold = -1;
        public static volatile int Bei = -1;
        public static volatile int ChefFlame = -1;
        public static volatile int Doni = -1;             // Jungle DLC
        public static volatile int JungleChefFlame = -1;  // Jungle DLC
        public static volatile int JungleClock = -1;      // giờ trong ngày ở Jungle, tính bằng phút (-1 = không ở Jungle)
    }
}
