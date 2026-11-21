namespace DTDTrainer;

enum CheatKind { Toggle, Number, Slider, Choice, Button }

/// <summary>Nhóm mod: tên đầy đủ (tiêu đề nhóm) và tên ngắn (nút lọc) cho cả hai ngôn ngữ.</summary>
sealed record CategoryInfo(string Id, string NameVi, string NameEn, string ShortVi, string ShortEn, string Icon)
{
    public string Name => L.T(NameVi, NameEn);
    public string Short => L.T(ShortVi, ShortEn);
}

/// <summary>
/// Một cheat trong danh sách. <see cref="Key"/> trùng với tên trường trong ModState của plugin
/// (Toggle/Slider/Choice) hoặc tên ACTION (Number/Button). Tên mod giữ tiếng Anh ở cả hai ngôn ngữ
/// (giống danh sách của Wand/WeMod); mô tả, nhãn lựa chọn và nhãn nút có hai bản.
/// </summary>
sealed record CheatDef(
    string Key,
    string Name,
    string Category,
    CheatKind Kind,
    string DescriptionVi,
    string DescriptionEn,
    string SnapshotKey = null,
    int[] Options = null,
    string[] OptionLabelsVi = null,
    string[] OptionLabelsEn = null,
    string Icon = "⚡",
    string ButtonVi = null,
    string ButtonEn = null,
    string Hotkey = null,
    bool DefaultPinned = false)
{
    public string Description => L.T(DescriptionVi, DescriptionEn);
    public string[] OptionLabels => L.English ? OptionLabelsEn : OptionLabelsVi;
    public string ButtonLabel => L.T(ButtonVi, ButtonEn);
}

static class Cheats
{
    public static readonly CategoryInfo[] CategoryList =
    {
        new("Player", "Nhân vật", "Player", "Nhân vật", "Player", MatIcons.Person),
        new("Inventory", "Kho đồ & Túi", "Inventory", "Kho đồ", "Inventory", MatIcons.Bag),
        new("Stats", "Quán Sushi & Chỉ số", "Sushi & Staff", "Sushi", "Sushi", MatIcons.Stats),
        new("Weapons", "Vũ khí", "Weapons", "Vũ khí", "Weapons", MatIcons.Shield),
        new("Enemies", "Sinh vật & Cá", "Marine Life", "Cá", "Fish", MatIcons.Heart),
        new("Game", "Cơ chế Game", "Game Mechanics", "Game", "Game", MatIcons.Gamepad),
        new("Physics", "Tốc độ di chuyển", "Movement Speed", "Tốc độ", "Speed", MatIcons.Walk),
        new("Graphics", "Đồ họa & Hiển thị", "Graphics & Display", "Đồ họa", "Graphics", MatIcons.Display),
    };

    public static readonly string[] Categories = CategoryList.Select(c => c.Id).ToArray();

    public static CategoryInfo GetCategory(string id)
        => CategoryList.FirstOrDefault(c => c.Id == id) ?? new CategoryInfo(id, id, id, id, id, MatIcons.Bolt);

    public static readonly CheatDef[] All =
    {
        // Player
        new("UnlimitedOxygen", "Unlimited Oxygen", "Player", CheatKind.Toggle,
            "Oxy luôn đầy khi lặn. Trong game oxy cũng là máu nên bạn gần như bất tử.",
            "Oxygen stays full while diving. Oxygen is also your health, so you are nearly invincible.",
            Icon: MatIcons.Air, DefaultPinned: true),
        new("UnlimitedStamina", "Unlimited Stamina (Sushi Bar)", "Player", CheatKind.Toggle,
            "Thể lực của Dave khi chạy trong quán sushi không bao giờ cạn.",
            "Dave's stamina never runs out while running in the sushi bar.",
            Icon: MatIcons.Bolt, DefaultPinned: false),
        new("UnlimitedPurificationFilter", "Unlimited Purification Filter Charge (Jungle DLC)", "Player", CheatKind.Toggle,
            "Bộ lọc nước (khiên chống ô nhiễm) khi lặn ở vùng nước độc của Jungle luôn đầy.",
            "The purification filter (pollution shield) stays full while diving in the Jungle's toxic water.",
            Icon: MatIcons.Science, DefaultPinned: false),

        // Inventory
        new("UnlimitedBattery", "Unlimited Item Battery", "Inventory", CheatKind.Toggle,
            "Thiết bị dùng pin (booster, đèn UV, máy hút, túi nâng…) không bị hao pin.",
            "Battery-powered gear (booster, UV light, vacuum, lift bag…) never drains.",
            Icon: MatIcons.Battery, DefaultPinned: false),
        new("UnlimitedMeals", "Unlimited Meals", "Inventory", CheatKind.Toggle,
            "Số phần món trên menu quán sushi không giảm khi khách gọi, món không bao giờ hết.",
            "Menu dishes in the sushi bar are not used up when customers order, so they never run out.",
            Icon: MatIcons.Restaurant, DefaultPinned: false),
        new("UnlimitedWasabi", "Unlimited Wasabi", "Inventory", CheatKind.Toggle,
            "Hũ wasabi ở quán chính và chi nhánh luôn đầy, nấu món không tốn wasabi.",
            "Wasabi at the main restaurant and the branch stays full; cooking does not use wasabi.",
            Icon: MatIcons.Restaurant, DefaultPinned: false),
        new("UnlimitedDrones", "Unlimited Drones", "Inventory", CheatKind.Toggle,
            "Số drone nâng hàng không giảm trong lượt lặn (giữ ở mức cao nhất của lượt đó).",
            "Lift drone count does not go down during a dive (kept at the highest count of that dive).",
            Icon: MatIcons.Bolt, DefaultPinned: false),
        new("UnlimitedCrabTraps", "Unlimited Crab Traps", "Inventory", CheatKind.Toggle,
            "Số bẫy cua không giảm trong lượt lặn.",
            "Crab trap count does not go down during a dive.",
            Icon: MatIcons.Shield, DefaultPinned: false),
        new("UnlimitedShopItems", "Unlimited Shop Items", "Inventory", CheatKind.Toggle,
            "Đồ phụ trợ mua ở cửa hàng mang theo khi lặn (viên/bình oxy phụ, hộp đạn, kit nâng cấp súng, lưới bẫy, bom gây ngủ, thiết bị lắp đặt, phấn ngụy trang, bình xịt làm mát…) dùng không bị trừ. Đồ dùng pin: xem Unlimited Item Battery.",
            "Helper items bought from shops and taken on dives (oxygen capsules/tanks, ammo boxes, gun upgrade kits, trap nets, sleep bombs, install devices, camouflage powder, cooling spray…) are not used up. Battery items: see Unlimited Item Battery.",
            Icon: MatIcons.Bag, DefaultPinned: false),
        new("NoWeight", "No Weight", "Inventory", CheatKind.Toggle,
            "Nhặt đồ không bị giới hạn cân nặng và không bị chậm khi quá tải.",
            "Pick up items with no weight limit and no overweight slowdown.",
            Icon: MatIcons.Bag, DefaultPinned: true),

        // Stats (nhân viên quán sushi)
        new("NoLevelLimit", "No Level Limit", "Stats", CheatKind.Toggle,
            "Nhân viên có thể nâng cấp vượt quá level tối đa.",
            "Staff can be leveled up past their maximum level.",
            Icon: MatIcons.Stats),
        new("MaxAppeal", "Max Appeal", "Stats", CheatKind.Toggle,
            "Appeal của mọi nhân viên = 999. Tắt đi sẽ trả lại giá trị cũ.",
            "Appeal of every staff member = 999. Turning it off restores the original values.",
            Icon: MatIcons.Star),
        new("MaxCooking", "Max Cooking", "Stats", CheatKind.Toggle,
            "Cooking của mọi nhân viên = 999. Tắt đi sẽ trả lại giá trị cũ.",
            "Cooking of every staff member = 999. Turning it off restores the original values.",
            Icon: MatIcons.Restaurant),
        new("MaxProcure", "Max Procure", "Stats", CheatKind.Toggle,
            "Procure của mọi nhân viên = 999. Tắt đi sẽ trả lại giá trị cũ.",
            "Procure of every staff member = 999. Turning it off restores the original values.",
            Icon: MatIcons.Bag),
        new("MaxServing", "Max Serving", "Stats", CheatKind.Toggle,
            "Serving của mọi nhân viên = 999. Tắt đi sẽ trả lại giá trị cũ.",
            "Serving of every staff member = 999. Turning it off restores the original values.",
            Icon: MatIcons.Person),
        new("SetGold", "Set Gold", "Stats", CheatKind.Number,
            "Đặt số Gold. Nhập số rồi bấm Áp dụng (hoặc Enter).",
            "Set your Gold. Type a number and press Apply (or Enter).",
            SnapshotKey: "gold", Icon: MatIcons.Gold),
        new("SetBei", "Set Bei", "Stats", CheatKind.Number,
            "Đặt số Bei.",
            "Set your Bei.",
            SnapshotKey: "bei", Icon: MatIcons.Star),
        new("SetChefFlame", "Set Chef Flame", "Stats", CheatKind.Number,
            "Đặt số Chef Flame.",
            "Set your Chef Flames.",
            SnapshotKey: "chefFlame", Icon: MatIcons.Bolt),
        new("SetDoni", "Set Doni (Jungle DLC)", "Stats", CheatKind.Number,
            "Đặt số Doni (tiền của Jungle DLC). Game có giới hạn tối đa riêng; nhập lớn hơn sẽ bị đưa về mức đó.",
            "Set your Doni (the Jungle DLC currency). The game has its own cap; larger numbers are lowered to it.",
            SnapshotKey: "doni", Icon: MatIcons.Gold),
        new("SetJungleChefFlame", "Set Chef Flame (Jungle DLC)", "Stats", CheatKind.Number,
            "Đặt số Chef Flame của quán Jungle (riêng với Chef Flame ở quán chính).",
            "Set the Jungle restaurant's Chef Flames (separate from the main restaurant's).",
            SnapshotKey: "jungleChefFlame", Icon: MatIcons.Bolt),

        // Weapons
        new("UnlimitedAmmo", "Unlimited Ammo", "Weapons", CheatKind.Toggle,
            "Súng không bao giờ hết đạn.",
            "Guns never run out of ammo.",
            Icon: MatIcons.Shield, DefaultPinned: true),

        // Enemies
        new("FreezeFish", "Freeze Fish in Place", "Enemies", CheatKind.Toggle,
            "Cá và sinh vật biển đứng yên tại chỗ, không cắn được Dave, nhưng vẫn giết/bắt được bình thường bằng súng móc, cận chiến hay drone.",
            "Fish and sea creatures stay in place and cannot bite Dave, but can still be killed or caught normally with the harpoon, melee or drones.",
            Icon: MatIcons.Pause),
        new("FishEsp", "Fish Detector (ESP)", "Enemies", CheatKind.Toggle,
            "Khi lặn, vẽ đường từ Dave tới cá, tôm và sinh vật bắt được gần nhất (tối đa 40 con trong 150 m), kèm tên và khoảng cách. " +
            "Màu: xanh = thường, vàng = phòng thủ, đỏ = tấn công, tím = đặc biệt (sứa, cá nóc, lươn), đỏ đậm = boss.",
            "While diving, draws lines from Dave to the nearest fish, shrimp and catchable creatures (up to 40 within 150 m) with name and distance. " +
            "Colors: blue = normal, yellow = defensive, red = aggressive, purple = special (jellyfish, puffer, eel), dark red = boss.",
            Icon: MatIcons.Search),

        // Game
        new("UnlimitedPatience", "Unlimited Customer Patience", "Game", CheatKind.Toggle,
            "Khách (kể cả khách VIP) không bao giờ bỏ về vì chờ lâu.",
            "Customers (including VIPs) never leave because they waited too long.",
            Icon: MatIcons.Heart),
        new("InstantCooking", "Instant Cooking", "Game", CheatKind.Toggle,
            "Món ăn được nấu xong gần như tức thì.",
            "Dishes finish cooking almost instantly.",
            Icon: MatIcons.Bolt),
        new("EasyCameraFocus", "Easy Camera Focus", "Game", CheatKind.Toggle,
            "Nhiệm vụ chụp ảnh: chỉ cần một phần mục tiêu nằm trong khung là đạt, không bị tính lỗi lấy nét. Bật trước khi mở máy ảnh.",
            "Photo missions: only part of the target needs to be in the frame, and focus failures are not counted. Turn it on before opening the camera.",
            Icon: MatIcons.Videocam),
        new("UnlimitedCatFood", "Unlimited Cat Food", "Game", CheatKind.Toggle,
            "Thức ăn cho Momo không bị trừ khi cho ăn.",
            "Food for Momo is not used up when feeding.",
            Icon: MatIcons.Heart),
        new("FeedMomoMultiple", "Feed Momo Multiple Times", "Game", CheatKind.Toggle,
            "Cho Momo ăn nhiều lần trong cùng một ngày.",
            "Feed Momo multiple times on the same day.",
            Icon: MatIcons.Heart),
        // Jungle DLC: đồng hồ trong ngày (sáng / chiều tối / đêm)
        new("JungleStopTime", "Stop Time (Jungle DLC)", "Game", CheatKind.Toggle,
            "Đồng hồ trong ngày ở Jungle đứng yên. Hành động tốn thời gian của game vẫn tính giờ như bình thường; dùng Rewind 1 Hour để lấy lại.",
            "The Jungle clock stops. Actions that cost time in the game still take time as usual; use Rewind 1 Hour to get it back.",
            Icon: MatIcons.Pause),
        new("JungleAdvanceHour", "Advance 1 Hour (Jungle DLC)", "Game", CheatKind.Button,
            "Tua đồng hồ Jungle thêm 1 giờ. Chỉ tua trong khung giờ hiện tại (ban ngày / chiều tối / đêm): chạm cuối khung thì game tự chuyển như khi hết giờ.",
            "Moves the Jungle clock forward 1 hour, within the current time block (day / evening / night): at the end of the block the game moves on as if time ran out.",
            SnapshotKey: "jungleClock", Icon: MatIcons.Play, ButtonVi: "+1 giờ", ButtonEn: "+1 hour"),
        new("JungleRewindHour", "Rewind 1 Hour (Jungle DLC)", "Game", CheatKind.Button,
            "Lùi đồng hồ Jungle 1 giờ, tối đa về đầu khung giờ hiện tại.",
            "Moves the Jungle clock back 1 hour, down to the start of the current time block.",
            SnapshotKey: "jungleClock", Icon: MatIcons.Refresh, ButtonVi: "−1 giờ", ButtonEn: "−1 hour"),

        // Physics
        new("SwimSpeed", "Swim Speed Multiplier", "Physics", CheatKind.Slider,
            "Nhân tốc độ bơi khi lặn, từ 1x (bình thường) đến 5x.",
            "Multiplies swim speed while diving, from 1x (normal) to 5x.",
            Icon: MatIcons.Swim),
        // Đi bộ trên cạn: mỗi nơi một hệ số riêng (key trùng ModState.WalkSpeed* trong plugin)
        new("WalkSpeedSushi", "Walk Speed: Sushi Bar", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave trong quán sushi (cả quán Jungle và quán sự kiện), từ 1x đến 5x.",
            "Dave's walking speed in the sushi bar (including the Jungle and event bars), from 1x to 5x.",
            Icon: MatIcons.Walk),
        new("WalkSpeedBoat", "Walk Speed: Boat", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave trên thuyền (và nhiệm vụ lén lút), từ 1x đến 5x.",
            "Dave's walking speed on the boat (and stealth missions), from 1x to 5x.",
            Icon: MatIcons.Walk),
        new("WalkSpeedFarm", "Walk Speed: Farm", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave ở nông trại, từ 1x đến 5x.",
            "Dave's walking speed on the farm, from 1x to 5x.",
            Icon: MatIcons.Walk),
        new("WalkSpeedFishFarm", "Walk Speed: Fish Farm", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave ở trại cá, từ 1x đến 5x.",
            "Dave's walking speed at the fish farm, from 1x to 5x.",
            Icon: MatIcons.Walk),
        new("WalkSpeedJungle", "Walk Speed: Jungle Village", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave ở làng Jungle (DLC), từ 1x đến 5x.",
            "Dave's walking speed in the Jungle village (DLC), from 1x to 5x.",
            Icon: MatIcons.Walk),
        new("WalkSpeedChase", "Walk Speed: Momo Chase", "Physics", CheatKind.Slider,
            "Tốc độ đi bộ của Dave trong minigame đuổi theo Momo, từ 1x đến 5x.",
            "Dave's walking speed in the Momo chase minigame, from 1x to 5x.",
            Icon: MatIcons.Walk),

        // Graphics
        new("FpsLimit", "FPS Limit", "Graphics", CheatKind.Choice,
            "Mở giới hạn 60 FPS của game (tắt v-sync, khóa FPS ở mức đã chọn). Mặc định = để game tự quyết (60). " +
            "\"Theo màn hình\" tự lấy tần số của màn đang hiển thị game (vd. 240 trên laptop, 255 trên màn rời).",
            "Unlocks the game's 60 FPS cap (turns off v-sync and caps FPS at the selected level). Default = let the game decide (60). " +
            "\"Match monitor\" uses the refresh rate of the display the game is on (e.g. 240 on the laptop, 255 on the external monitor).",
            Options: new[] { 0, 120, 144, 240, 255, -1 },
            OptionLabelsVi: new[] { "Mặc định", "120", "144", "240", "255", "Theo màn hình" },
            OptionLabelsEn: new[] { "Default", "120", "144", "240", "255", "Match monitor" },
            Icon: MatIcons.Display),
    };
}
