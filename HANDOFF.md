# HANDOFF: DTD Trainer (Dave the Diver mod)

Tài liệu bàn giao cho phiên làm việc mới. Đọc hết file này trước khi sửa code. Cập nhật lần cuối: 2026-10-13.

## 1. Bối cảnh và mong muốn của người dùng

- Người dùng nói **tiếng Việt**, luôn trả lời bằng tiếng Việt.
- Mục tiêu: trainer cho Dave the Diver (game chơi đơn), **mô phỏng Wand (trước là WeMod)** cả về giao diện lẫn cách hoạt động.
- Các quyết định đã chốt, **không được đảo lại**:
  - Tool là app `.exe` riêng, không có menu trong game.
  - **Giống Wand: KHÔNG được thêm bất cứ gì vào thư mục game.** Tool chỉ hoạt động trong `Tool-DTD`, tự tìm game (chỉ đọc), và phải chạy được trên máy khác (người dùng test trên nhiều máy).
  - `DTDTrainer.exe` nằm ngay ở `D:\Code\Tool-DTD\`; mã nguồn cũng ở đây, **không** để trong thư mục game.
  - Mỗi lần mở tool, **mọi mod về OFF**; không lưu trạng thái mod.
  - Mở game **thẳng từ Steam** vẫn phải dùng được tool. Khi không có tool kết nối thì plugin để mọi mod OFF.
  - Đóng tool thì mod trong game tự tắt.
  - **Không** phím tắt, **không** âm báo, **không** ô tìm kiếm, **không** nhãn "Đang hiện x/26".
  - Giao diện song ngữ: **tiếng Việt mặc định** (để dùng), tiếng Anh (để dev). Nút 🌐 VI/EN trên header đổi ngay, giữ trạng thái mod.
  - Tên mod giữ tiếng Anh ở cả hai ngôn ngữ (giống danh sách WeMod).
  - Thanh tốc độ 1x–5x (mặc định 1x, không dưới 1), có nút − / +, bước 0.25.
  - Icon tool: `icondtd.ico` (mặt Dave pixel-art).
- **Người dùng tự thiết kế lại giao diện** (theme đại dương, thẻ bo góc kèm emoji, thanh lọc dạng viên thuốc, chế độ `--render` trong `Program.cs`). Giữ nguyên thiết kế của họ; chỉ sửa lỗi hoặc bố cục, và nói rõ đã đổi gì.
- Người dùng có thể sửa file song song. Thấy file thay đổi ngoài ý muốn thì đọc lại, đừng ghi đè.

## 2. Môi trường

| Mục | Giá trị |
|---|---|
| Game | `D:\Steam\steamapps\common\Dave the Diver` (Steam AppID 1868140), v1.0.6.2113, có bản Việt hóa của The Red Team |
| Engine | Unity 6000.0.52f1, **IL2CPP**, metadata v31 |
| Mod loader | BepInEx 6 BE #788 IL2CPP x64 (gói ở `vendor\`), dựng trong `Tool-DTD\runtime\` (core, dotnet, interop, unity-libs, plugins) |
| Log plugin | `Tool-DTD\runtime\BepInEx\LogOutput.log` |
| Bộ nạp | **chưa có trong tool** (xem mục 7). Thư mục game đã được dọn sạch (không còn winhttp.dll / doorstop_config.ini / BepInEx / dotnet) |
| Save | `%USERPROFILE%\AppData\LocalLow\nexon\DAVE THE DIVER\SteamSData` |
| Máy | Windows 11, laptop RTX 5060, màn laptop 2560×1600 @ 240Hz; màn rời ASUS ROG XG27ACMES 2K 255Hz |
| SDK | .NET SDK 10 (plugin target net6.0, trainer net10.0-windows) |

## 3. Kiến trúc

```
DTDTrainer.exe (WinForms, .NET 10)  ── named pipe "DTDTrainer.Pipe" ──►  DTDMod.dll (plugin BepInEx trong game)
```

- **Giao thức pipe** (theo dòng, UTF-8): `PING` → `PONG`; `SET <Key> <value>`; `ACTION SetGold|SetBei|SetChefFlame|SetDoni|SetJungleChefFlame <n>` và `ACTION JungleAdvanceHour|JungleRewindHour`; `GET` → `STATE saveLoaded=..;gold=..;bei=..;chefFlame=..;doni=..;jungleChefFlame=..;jungleClock=<phút trong ngày, -1 = không ở Jungle>`; `DIAG` → danh sách patch, số lần can thiệp (hits), trạng thái.
- `RESET` → tắt mọi cheat (nút "Tắt hết" của trainer).
- `ACTION` chạy trên luồng game và **chờ kết quả** (tối đa 3 giây): `OK <số thực tế>` / `OK hh:mm [limit]` hoặc `ERR nosave|nojunglesave|nojungle|number`. Trainer dịch mã lỗi (`MainForm.DescribeError`).
- Pipe nhận nhiều client cùng lúc. Khi **client cuối cùng** ngắt kết nối thì plugin gọi `ModState.ResetAll()`.
- Key trong `SET` = tên trường `public static volatile` trong `ModState.cs` (bool, float hoặc int).
- Trainer gửi `SET` cho mọi mod ngay khi vừa kết nối (`_desired`), nên mở tool trước hay sau game đều được.

### Cấu trúc mã nguồn

```
build.ps1                      build plugin rồi trainer (nhúng DTDMod.dll vào exe, self-contained) → DTDTrainer.exe ở gốc; -Install chép plugin vào runtime\
runtime/                       (tool tự dựng, không đưa vào git) BepInEx\core, dotnet\, BepInEx\plugins\DTDMod, interop, unity-libs, log
src/DTDLoader/                 thử nghiệm: DLL (C, build bằng gcc w64devkit) khởi động BepInEx từ runtime\ khi đã có trong tiến trình game trước il2cpp_init
src/DTDMod.Plugin/
  Plugin.cs                    nạp plugin; áp TỪNG lớp patch riêng + FoldGuard; log "Patch OK=N, loi=M"
  FoldGuard.cs                 từ chối patch hàm bị gộp mã máy (đọc MethodAddressToToken.db)
  PipeServer.cs                máy chủ pipe, hàng đợi lệnh cho luồng game
  ModState.cs                  trạng thái cheat (tên trường = key giao thức), ResetAll, hits
  ModBehaviour.cs              Update: RenderSmoothing + chạy lệnh trong hàng đợi + tick 0.5 giây; LateUpdate: FpsUnlock; FixedUpdate: đếm nhịp; OnGUI: FishEsp;
                               Start: Application.onBeforeRender + coroutine WaitForEndOfFrame; Currency (Gold/Bei/Flame/Doni/Flame Jungle)
  Ticks.cs                     cheat ghi dữ liệu định kỳ: StaffStats, WasabiRefill, LandWalk, WeightRefresher, FishFreezer, MomoFeedReset, JungleClock (dừng/tua giờ)
  Patches/PlayerPatches.cs     oxy/drone/bẫy cua/đạn/pin/bộ lọc Jungle (PlayerCharacter.Update postfix), tốc độ bơi, tốc độ quán sushi, làng Jungle, stamina
  Patches/InventoryPatches.cs  Unlimited Meals (SushiBarMenuManager.Order), cat food (IngredientsStorage.Decrease), wasabi, No Weight
  Patches/StaffPatches.cs      No Level Limit
  Patches/GamePatches.cs       kiên nhẫn khách, nấu tức thì, camera, Momo (setter MomoPreviousFeedDay)
  Patches/GraphicsPatches.cs   FPS Limit (FpsUnlock + chặn VSync watchdog) + RenderSmoothing (nội suy hiển thị camera/Dave khi FPS > 50)
  FishEsp.cs                   Fish Detector: OnGUI (IMGUI) vẽ đường từ Dave tới cá/tôm, tên + khoảng cách
  CompilerPolyfills.cs         NullableAttribute nội bộ (interop DLL công khai bản trùng tên)
src/DTDTrainer/
  Program.cs                   single-instance, đọc ngôn ngữ, chế độ --render (của người dùng; không kết nối game; --render-cat=<Nhóm> để chụp một nhóm)
  MainForm.cs                  header (🌐 VI/EN, Tắt hết, Cài đặt, Play), thanh lọc nhóm, danh sách, cài đặt, chân trang; SwitchLanguage dựng lại UI
  CheatRow.cs                  một thẻ mod: Toggle / Slider (− [thanh] + giá trị) / Choice (dải nút) / Number (ô số + Áp dụng) / Button (nút một lần + giá trị hiện tại)
  Cheats.cs                    định nghĩa 39 mod, 8 nhóm, mô tả VI/EN
  Lang.cs                      L.T("tiếng Việt", "English"), L.Numbers
  ModInstaller.cs              tìm game qua Steam (chỉ đọc), dựng runtime\ từ vendor\ + plugin nhúng, dọn bản cài cũ trong thư mục game, mở game qua Steam
  PipeClient.cs, TrainerSettings.cs (GamePath, Language, Numbers), Theme.cs, Controls/*
tools/DTDXref/                 công cụ dò ngược OFFLINE: xref (calls/usedBy), --fold, --disasm (Iced), --enum
tools/testing/                 script PowerShell để test tự động (mục 6)
vendor/                        BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788.zip, unity-libs\ (thư viện Unity 6000.0.52 cho máy không có mạng)
```

## 4. Danh sách mod và cách làm

| Mod (key) | Cơ chế | Kiểm chứng |
|---|---|---|
| UnlimitedOxygen | `SetBreathInvincible(Cheat)` + giữ HP = MaxHP mỗi frame | chưa thử lặn |
| UnlimitedStamina | postfix `DaveMoveValue.Update`: stamina = max | chưa thử |
| UnlimitedPurificationFilter (Jungle) | mỗi frame lặn: `PlayerCharacter.HazardShieldHP = HazardShieldMaxHP` (khiên chống ô nhiễm; UI `JDLC.HazardShieldUI`) | **mới**, chưa thử |
| UnlimitedBattery | chặn `SubHelperItemInventory.ConsumeBatterySubHelper` + nạp `remainTime` mỗi frame | chưa thử |
| UnlimitedMeals | quán chính: chụp/trả `nowPlateCount` quanh `SushiBarMenuManager.Order` + báo `m_EventListeners.OnEventUpdate` (Order đã báo giao diện số bị trừ). Cả quán chính và **quán Jungle** (`JDLC.JungleSushiBarMenuManager.menu`, trừ ở chỗ không có hàm riêng): `MealsKeeper` mỗi frame nhớ số phần cao nhất theo ô+món, bị trừ thì trả lại + `UpdateSaveData` + báo giao diện (`DREventTriggerManager.JungleMenuEventTrigger(Update)`) | **vừa sửa** (người dùng báo không ăn), chưa thử |
| UnlimitedWasabi | chặn `OperationData.UsingWasabiInCooking` + `RefillWasabi` mỗi 0.5 giây | chạy được ở màn hình tiêu đề |
| UnlimitedDrones / UnlimitedCrabTraps | nhớ số cao nhất trong lượt lặn, trả lại khi bị trừ | chưa thử |
| UnlimitedShopItems | chặn `SubHelperItemInventory.ConsumeInstantSubHelper` (trừ `remainCount` ô đang chọn, về 0 thì xóa ô) + dự phòng giữ `remainCount` cao nhất mỗi frame. Đồ: `SubHelperType` OxygenCapsule, Ammo, GunUpgradeKit, SubO2Tank, InstallDevice, TriggerNet, SleepTriggerBomb, CamouflagePowder, CoolingSpray… | **mới**, chưa thử |
| NoWeight | chặn `LootBox.CheckOverloadedState`, `RefreshOverweight(target=0)` | chạy được ở màn hình tiêu đề |
| NoLevelLimit | postfix `StaffData.IsMaxLevel` → false | chưa thử |
| MaxAppeal/Cooking/Procure/Serving | ghi thẳng `StaffData.statusData` = 999 rồi `UpdateStaffValues`; nhớ giá trị gốc để trả lại (save chỉ lưu level) | chưa thử |
| SetGold / SetBei / SetChefFlame | `PlayerInfoSave.gold/bei/ChefFlame = new ObscuredInt(n)` | **đã kiểm chứng** (ghi và đọc lại đúng) |
| SetDoni (Jungle) | `SaveData.JDLCContents.SetJungleGold(n)` rồi `JungleCommonDefine.AddPlayerGoods(Gold, 0, 0, false)` để làm mới HUD (game tự kẹp về mức tối đa của `ContentsRestrictUtil`, loại 1000) | **mới**, chưa thử |
| SetJungleChefFlame | `junglePlayerInfo.jungleChefFlame = n` + `UpdateJungleSave()` | **mới**, chưa thử |
| UnlimitedAmmo | nạp đạn mỗi frame (`DecreaseBullet` bị inline vào `FireWeapon`) | chưa thử |
| FreezeFish | `FishAISystem.AddFishStopReason(Stun)` cho từng con, gỡ khi tắt | **vừa sửa**, chưa thử |
| FishEsp | tick 0.5 giây quét `DR.AI.FishAISystem` còn sống (`IsCorpseRP`), tên `DataManager.GetText(_FishInfoData.NameTextID)`, màu theo `EnumFishType`; OnGUI vẽ đường (GUI.DrawTexture + RotateAroundPivot) và nhãn, 40 con gần nhất trong 150 m | **mới**, chưa thử |
| UnlimitedPatience | chặn `SushiBarCustomer.Angry` + `SushiBarSpecialCustomer.Angry`. **Đừng** nhân `GetOrderWaitTime`: đó là thời gian khách chọn món ("..." trong `WaitForOrder`), nhân lên thì khách không bao giờ gọi món (lỗi người dùng báo, đã gỡ). Thời gian chờ món/nước đọc thẳng từ dữ liệu (inline), hết giờ thì Animator trigger → `Angry` | chưa thử khi chơi |
| InstantCooking | `CalcCookingTime` → 0.05 | chưa thử |
| EasyCameraFocus | `PhotoZonePanel.Init`: passPercentage 0.01; chặn `UpateFocusFailCount` | chưa thử |
| UnlimitedCatFood | chụp/trả lại số lượng nguyên liệu loại Feed quanh `IngredientsStorage.Decrease` (bỏ qua frame đang bán) | chưa thử |
| FeedMomoMultiple | chặn setter `SushiBarInfoSave.MomoPreviousFeedDay`; khi bật thì xóa ngày đã lưu, khi tắt thì trả lại | **vừa sửa**, đã thử xóa/trả ở màn hình tiêu đề |
| JungleStopTime | `JungleTimeController.AddStopReason(SROption)` (giống menu debug PauseTimer), quét lại mỗi 0.5 giây; chỉ dừng dòng thời gian tự chạy (TimerRoutine), hành động tốn giờ vẫn tính | **mới**, chưa thử |
| JungleAdvanceHour / JungleRewindHour (nút) | `AddTime(±3600)` kẹp trong khung giờ hiện tại [0, GetMaxTime()-1] như `SkipCurrentTimeSection`, rồi `RefreshEnvironment`/`SetTimeCursor`/`SyncGameEnvironmentValue`. Chỉ chạy khi `CurrentTimeSection` là Day/Evening/Night | **mới**, chưa thử |
| SwimSpeed | postfix `PlayerCharacter.DetermineMoveSpeed` | chưa thử |
| WalkSpeedSushi | postfix `DaveMoveValue.speedMultiplier` | chạy được (người dùng dùng 3x) |
| WalkSpeedBoat / Farm / FishFarm / Chase | ghi field tốc độ (`LobbyPlayer.m_MoveSpeed`, `FarmPlayerView.m_Speed_Min/Max`, `FishFarmPlayerView.Dave_Speed`, `ChaserDaveMoveData.defaultMoveSpeed`) | boat/farm/fishfarm chạy được (người dùng dùng 3x) |
| WalkSpeedJungle | prefix `JVillageLobbyMove.Move`: nhân `dt` | chưa thử |
| FpsLimit (int: 0, 120, 144, 240, 255, -1 = theo màn hình) | tắt vSync, đặt `targetFrameRate` trong LateUpdate + postfix `CameraResolution.UpdateVSyncValue`; chặn `UpdateVSyncWatchdog`; **RenderSmoothing** (nội suy hiển thị camera chính + Dave các khu vực) | FPS **đã kiểm chứng** (144/240 đạt; 255 chỉ được 240 trên màn laptop 240Hz). RenderSmoothing: chạy ở màn hình tiêu đề (theo dõi Main Camera, không lỗi); hiệu quả khi di chuyển chờ người dùng thử |

Ngoài ra đã kiểm chứng:
- Plugin nạp **26/26 patch**, FoldGuard hoạt động. (Các mod Jungle không thêm patch Harmony nào: chỉ gọi hàm và ghi dữ liệu.)
- Mở game từ Steam thì tool tự kết nối; đóng tool thì mod tắt; mở lại tool thì mọi mod OFF.
- Đổi VI/EN giữ nguyên trạng thái mod.

## 5. Bài học kỹ thuật (đọc trước khi thêm hoặc sửa patch)

1. **Gộp mã máy (identical code folding).** Getter/setter tầm thường dùng chung địa chỉ với hàng trăm hàm (vd. `StaffData.get_Charm` ×208, `LobbyPlayer.Update` ×10, `LootBox.get_weight` ×65). Patch vào đó sẽ phá luôn các hàm kia. Trước khi patch, luôn chạy:
   `dotnet run --project tools\DTDXref -- --fold Type::Method`. Lúc chạy, FoldGuard cũng từ chối những patch như vậy.
2. **Inline.** Hàm nhỏ bị nhúng thẳng vào nơi gọi, nên patch không có tác dụng (`DecreaseBullet`, `get_weight`). Nếu DTDXref không cho thấy `usedBy` nào, hãy ghi thẳng vào dữ liệu theo chu kỳ (`Ticks.cs`).
3. **Hàm gọi tự chép lại thân hàm phụ.** `MomoFriendshipPanel.SuccessFeeding` tự ghi ngày cho ăn, không gọi `SaveMomoFeedingData`. Hãy patch ở setter thấp nhất và kiểm tra `usedBy`.
4. **Không dùng `XrefScanner.XrefScan` lúc game chạy**: game crash (AccessViolation). Dùng `tools\DTDXref` offline:
   - `--disasm Type::Method`: dịch ngược x64, có ghi tên hàm được gọi.
   - `--enum "Outer+Inner"`: in tên và giá trị enum.
   - Không có tùy chọn: liệt kê calls/usedBy. Type lồng nhau viết bằng `+`.
5. **Cá bất tử:** `IsFishStop.IsCanNotDead()` = `reason & 0x290` (FreezeAll 0x10 | Hooked 0x80 | GlobalStopByGroupController 0x200). Không dùng tín hiệu FreezeAll của `InGameManager.signalManager` để đóng băng.
6. **Khựng khi di chuyển ở FPS > 60:** game chạy FixedUpdate **50 lần/giây** (`SceneLoader.ResetForNewScene` → `TimeManager.SetFixedUpdateFrame(50)`). **Camera** (`CameraManager.FixedUpdate` → `CameraUpdate_Impl` dời `parentTransform`; ngoài ra còn `OrthographicCameraManager`, `VCamManager`, Cinemachine) và Dave (Rigidbody, hoặc `CharacterController2D` khi lặn) đều cập nhật trong FixedUpdate. Ở 240 FPS thì ~5 khung hình mới có một nhịp.
   - **ĐỪNG đổi nhịp vật lý.** Bản trước đã đặt nhịp = FPS và gây lỗi đạn/súng móc bay chậm như slow motion: `GameObjectExtention.AddForce_Fixed` = `Rigidbody2D.AddForce(Force)` một lần (GunBullet.Shoot, GrenadeBullet.Shoot, HarpoonProjectile.Fire, MeleeThrowProjectile…), vận tốc tỉ lệ với fixedDeltaTime. Còn hàng chục chỗ AddForce một lần khác (đồ rơi bật ra, nổ, đẩy lùi, minigame).
   - **Rigidbody Interpolate một mình không đủ**, vì camera vẫn đi theo nhịp 50Hz.
   - Cách hiện tại: `RenderSmoothing` nội suy *hiển thị* cho `Camera.main` và transform của Dave:
     - Đầu Update: lấy mốc nếu khung hình có nhịp vật lý, đặt vị trí lerp(mốc trước, mốc sau, (time - fixedTime)/fixedDeltaTime).
     - `Application.onBeforeRender`: đặt lại nếu bị ghim về vị trí thật.
     - Coroutine `WaitForEndOfFrame`: trả vị trí thật.
     - Transform bị dời giữa hai nhịp thì "đứng ngoài" (log `Lam muot: dung ngoai ...`).
7. **FPS:** game có "VSync watchdog" trong `CameraResolution`: nếu FPS lệch tần số màn hình thì nó tắt v-sync trong save và khóa 60.
8. Game **vẫn chạy Update khi mất focus**.
9. Singleton: dùng `Singleton<T>.hasInstance` trước `.Instance`. `SushiBarStaffDataManager`, `InGameManager`, `CameraResolution` là `Singleton<T>`; `LootBox`, `IngredientsStorage` là `SingletonNoMono<T>`.
10. Tiền tệ là `CodeStage.AntiCheat.ObscuredTypes.ObscuredInt`: dùng `new ObscuredInt(n)` và `.GetDecrypted()`. Không dùng toán tử implicit.
11. Trong plugin có `CompilerPolyfills.cs` vì interop công khai `NullableAttribute` trùng tên.
12. Trainer: `DoubleBufferedFlowLayoutPanel.ScrollToControl` đã bị chặn, nếu không danh sách tự cuộn tới ô nhập Set Gold khi mở tool. Thanh cuộn ngang của danh sách đã tắt.

## 6. Quy trình build và test

```powershell
.\build.ps1 -Install        # tool phải tắt; tạo D:\Code\Tool-DTD\DTDTrainer.exe và chép plugin vào runtime\ (không đụng thư mục game)
```

- Mở game: `Start-Process "steam://rungameid/1868140"`. Chờ log có `da tai. Patch OK=` và `Trainer da ket noi`.
- **Chẩn đoán** (chạy song song với tool, không làm mod tắt): `tools\testing\pipe.ps1 -Commands @("GET","DIAG")`.
- **Chụp cửa sổ**: `tools\testing\shot.ps1 -ProcessName DTDTrainer -Out x.png` (dùng `-ProcessName DaveTheDiver` để đọc bộ đếm FPS ở góc trên trái game).
- **Click giả lập**: `tools\testing\click.ps1 -X -Y` (tọa độ theo cửa sổ). WinForms chỉ nhận click nếu control thực sự nhìn thấy trên màn hình, nên phải đặt tool **TOPMOST** trước (SetWindowPos HWND_TOPMOST), click xong thì trả lại. `resize.ps1` đưa cửa sổ về (0,0) và đổi kích thước.
- Tool đang mở sẽ khóa file exe. Nếu tool đang chạy là của người dùng, nói cho họ biết trước khi đóng.
- Không thể chơi game thay người dùng, nên hiệu quả trong lúc chơi phải nhờ họ thử và gửi `runtime\BepInEx\LogOutput.log`.

## 7. Đã hoàn thành bộ nạp tự động (Auto-Inject) kiểu Wand (WeMod)

- **ĐÃ XONG bộ nạp trực tiếp vào game:**
  - `Injector.cs`: dùng Win32 API (`OpenProcess`, `VirtualAllocEx`, `WriteProcessMemory`, `CreateRemoteThread` gọi `LoadLibraryW`) chèn `runtime\DTDLoader.dll` vào `DaveTheDiver.exe`.
  - Hỗ trợ cả **chèn sớm** (khi game vừa mở trước `il2cpp_init`) và **chèn muộn vào game đang chạy**:
    - `dtdloader.c` phát hiện `GameAssembly.dll` đã nạp, tạo thread riêng, gắn (`il2cpp_thread_attach`) vào IL2CPP domain rồi khởi động CoreCLR + BepInEx.
    - `BepInEx.Unity.IL2CPP.dll` đã được vá để `OnInvokeMethod` kích hoạt chuỗi nạp plugin trên luồng chính (`Update`, `FixedUpdate`, `LateUpdate`, `OnGUI`) ngay khung hình tiếp theo thay vì chỉ chờ `Internal_ActiveSceneChanged`.
  - `DTDTrainer`:
    - Tự động dò thư mục game qua Steam (`ModInstaller.DetectGameDir()`).
    - Nút **▶ Play**: nếu game chưa chạy thì mở qua Steam rồi tự chèn mod; nếu game đang chạy thì chèn mod ngay lập tức.
    - Timer giám sát nền: tự động phát hiện `DaveTheDiver.exe` chạy độc lập từ Steam và tự động chèn mod mà người dùng không cần thao tác gì.
    - Exe tự nhúng `DTDMod.dll` và `DTDLoader.dll`, tự giải nén ra `runtime\`. Thư mục game hoàn toàn sạch, không bị thêm file nào!

- **Chờ người dùng thử:**
  - khựng khi đi nhanh ở FPS > 60 (RenderSmoothing) ở mọi khu vực; xem `DIAG` mục `smooth=` và log `Lam muot: dung ngoai` để biết khu vực nào camera/Dave không được làm mượt;
  - súng/súng móc hết chậm (đã trả nhịp vật lý về 50);
  - Unlimited Shop Items, Fish Detector (ESP);
  - 6 mod Jungle DLC mới: bộ lọc, Set Doni, Set Chef Flame Jungle, Stop Time, ±1 giờ;
  - cho Momo ăn nhiều lần;
  - Freeze Fish rồi giết cá bằng súng móc/cận chiến và dùng drone.
- Các mod "chưa thử" ở mục 4 cần người dùng xác nhận khi chơi.
- Thử FPS 255 / "Theo màn hình" trên màn ROG 255Hz.
- Các mod khác trong danh sách WeMod, chưa làm (chỉ làm nếu người dùng yêu cầu): Oxygen Consumption [%], [Selected Item] Set Amount, (Jungle DLC) Time Speed Multiplier (gợi ý: nhân `JungleTimeController.RealTimeConvertValuePerSecond`; TimerRoutine cộng `deltaTime * [+0xB8]` mỗi frame).
- Thư mục `dist\` trống còn sót lại, người dùng có thể tự xóa.
- Bộ nhớ dự án của Claude: `C:\Users\trand\.claude\projects\D--Steam-steamapps-common-Dave-the-Diver\memory\`.

## 8. Câu mở đầu gợi ý cho phiên mới

> Đọc `D:\Code\Tool-DTD\HANDOFF.md` và `README.md` để nắm dự án DTD Trainer. Trả lời bằng tiếng Việt. Sau đó: <việc cần làm>.
