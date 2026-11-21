# DTD Trainer: mod cho Dave the Diver

Trainer kiểu **Wand (WeMod)** cho Dave the Diver (Steam, Unity 6 IL2CPP). Gồm hai phần:

- **`DTDTrainer.exe`**: app điều khiển giao diện tối, có nút Play, bộ lọc theo nhóm, công tắc, thanh trượt và ô nhập số kèm nút Apply.
- **`DTDMod.dll`**: plugin BepInEx 6 chạy bên trong game, nhận lệnh từ trainer qua named pipe `DTDTrainer.Pipe`.

Giống Wand: **tool không ghi gì vào thư mục game.** Mọi thứ của mod nằm trong `Tool-DTD`:

```
Tool-DTD\
  DTDTrainer.exe        trainer (đã kèm .NET, máy khác không cần cài gì)
  vendor\               gói BepInEx 6 BE #788 + unity-libs (thư viện Unity cho máy không có mạng)
  runtime\              tool tự dựng: BepInEx\core, dotnet\, BepInEx\plugins\DTDMod, interop, log
```

**Tự động hóa hoàn toàn (Auto-Inject):** Tool tự động chèn bộ nạp `runtime\DTDLoader.dll` vào tiến trình `DaveTheDiver.exe` (kể cả khi game đã mở từ trước hoặc vừa bấm Play). Không cần can thiệp hay chép file nào vào thư mục game.

## Cách dùng

1. Mở `DTDTrainer.exe` (ngay trong thư mục này). Trainer tự tìm thư mục game trong thư viện Steam (chỉ đọc). Nếu không thấy, bấm **⚙ Cài đặt → Chọn…**.
   - Trainer tự dựng/cập nhật `runtime\` từ `vendor\`, bộ nạp DLL và plugin nhúng sẵn trong exe.
2. Mở game:
   - Bấm **▶ Play** trên trainer (tool mở game qua Steam và tự chèn mod khi game lên), **HOẶC**
   - Mở game thẳng từ Steam lúc nào tùy thích: trainer tự phát hiện tiến trình `DaveTheDiver.exe` đang chạy và tự động chèn mod vào ngay!
   - Lần đầu trên một máy (hoặc sau khi game cập nhật), BepInEx mất 1-3 phút sinh `runtime\BepInEx\interop`.
3. Khi đèn trạng thái báo **Mods đang hoạt động**, bật/tắt mod ngay trên tool (game vẫn chạy khi bạn chuyển sang cửa sổ tool).

Lưu ý:

- **Mỗi lần mở trainer, mọi mod bắt đầu ở OFF** (thanh trượt về 1.00x). Trainer chỉ nhớ thư mục game, ngôn ngữ và số đã nhập ở ô Set.
- Đóng trainer thì mọi mod trong game tự tắt. Game vẫn chạy bình thường, mở lại trainer là dùng tiếp.
- **Dọn thư mục game** (trong Cài đặt) xóa BepInEx mà bản tool cũ từng cài vào thư mục game (chỉ khi thấy `BepInEx\plugins\DTDMod`). **Thư mục Mod** mở `runtime\` (log ở `runtime\BepInEx\LogOutput.log`).
- **Ngôn ngữ:** nút **🌐 VI / EN** trên header đổi giữa tiếng Việt (mặc định) và tiếng Anh ngay lập tức, các mod đang bật vẫn giữ nguyên. Tool nhớ ngôn ngữ đã chọn.

## Danh sách mod

| Nhóm | Mod | Cách hoạt động |
|---|---|---|
| Player | Unlimited Oxygen | Oxy (cũng là máu) luôn đầy |
| | Unlimited Stamina (Sushi Bar) | Thể lực khi chạy trong quán không cạn |
| | Unlimited Purification Filter Charge (Jungle DLC) | Bộ lọc (khiên chống ô nhiễm) khi lặn vùng nước độc của Jungle luôn đầy |
| Inventory | Unlimited Item Battery | Booster, đèn UV, máy hút… không hao pin |
| | Unlimited Meals | Số phần món trên menu không giảm khi khách gọi |
| | Unlimited Wasabi | Hũ wasabi luôn đầy |
| | Unlimited Drones | Số drone nâng hàng không giảm trong lượt lặn |
| | Unlimited Crab Traps | Số bẫy cua không giảm trong lượt lặn |
| | Unlimited Shop Items | Đồ phụ trợ mua ở cửa hàng mang theo khi lặn (viên/bình oxy phụ, hộp đạn, kit nâng cấp súng, lưới, bom gây ngủ, thiết bị lắp đặt, phấn ngụy trang, bình xịt làm mát…) dùng không bị trừ |
| | No Weight | Nhặt không giới hạn, không bị chậm khi quá tải |
| Stats | No Level Limit | Nhân viên lên cấp vượt mức tối đa |
| | Max Appeal / Cooking / Procure / Serving | Chỉ số mọi nhân viên = 999, tắt thì trả lại như cũ |
| | Set Gold / Bei / Chef Flame | Nhập số rồi bấm Apply |
| | Set Doni (Jungle DLC) | Tiền của Jungle DLC. Game có giới hạn tối đa riêng, nhập lớn hơn sẽ bị đưa về mức đó |
| | Set Chef Flame (Jungle DLC) | Chef Flame của quán Jungle (riêng với quán chính) |
| Weapons | Unlimited Ammo | Súng không hết đạn |
| Enemies | Freeze Fish in Place | Cá đứng yên, không cắn được Dave, nhưng vẫn giết/bắt bình thường bằng súng móc, cận chiến, drone |
| | Fish Detector (ESP) | Khi lặn, vẽ đường từ Dave tới cá/tôm gần nhất (tối đa 40 con trong 150 m) kèm tên và khoảng cách; màu theo loại (thường, phòng thủ, tấn công, đặc biệt, boss) |
| Game | Unlimited Customer Patience | Khách (kể cả VIP) không bỏ về |
| | Instant Cooking | Nấu món gần như tức thì |
| | Easy Camera Focus | Chụp ảnh nhiệm vụ: chỉ cần một phần mục tiêu trong khung; bật trước khi mở máy ảnh |
| | Unlimited Cat Food | Thức ăn cho Momo không bị trừ |
| | Feed Momo Multiple Times | Cho Momo ăn nhiều lần trong ngày |
| | Stop Time (Jungle DLC) | Đồng hồ trong ngày ở Jungle đứng yên (hành động tốn thời gian của game vẫn tính giờ) |
| | Advance 1 Hour (Jungle DLC) | Nút **+1 giờ**: tua đồng hồ Jungle, trong khung giờ hiện tại (ngày / chiều tối / đêm); chạm cuối khung thì game tự chuyển khung |
| | Rewind 1 Hour (Jungle DLC) | Nút **−1 giờ**: lùi đồng hồ Jungle, tối đa về đầu khung giờ hiện tại. Thẻ hiện giờ Jungle đang là mấy giờ |
| Physics | Swim Speed Multiplier | Nhân tốc độ bơi, 1x-5x (nút − / + bước 0.25x) |
| | Walk Speed: Sushi Bar | Tốc độ đi bộ trong quán sushi (cả Jungle và sự kiện), 1x-5x |
| | Walk Speed: Boat | Tốc độ đi bộ trên thuyền (và nhiệm vụ lén lút), 1x-5x |
| | Walk Speed: Farm | Tốc độ đi bộ ở nông trại, 1x-5x |
| | Walk Speed: Fish Farm | Tốc độ đi bộ ở trại cá, 1x-5x |
| | Walk Speed: Jungle Village | Tốc độ đi bộ ở làng Jungle (DLC), 1x-5x |
| | Walk Speed: Momo Chase | Tốc độ đi bộ trong minigame đuổi Momo, 1x-5x |
| Graphics | FPS Limit | Mặc định (60) / 120 / 144 / 240 / 255 / Theo màn hình (tự lấy tần số của màn đang hiển thị game). Tắt v-sync của game và chặn "VSync watchdog" tự khóa về 60. Chống khựng khi di chuyển: camera và Dave chỉ đổi vị trí theo nhịp vật lý 50 lần/giây, nên tool hiển thị chúng ở vị trí nội suy giữa hai nhịp (chỉ phần hình, gameplay giữ nguyên như bản gốc). FPS thực tế không vượt được tần số màn hình nếu driver đang ép v-sync/G-Sync |

Set Gold/Bei/Chef Flame/Doni ghi thẳng vào save. Nên sao lưu save trước khi dùng. Save nằm ở `%USERPROFILE%\AppData\LocalLow\nexon\DAVE THE DIVER\SteamSData`.

## Build

Cần .NET SDK 10 và `runtime\BepInEx\interop` (BepInEx sinh ra ở lần đầu game chạy kèm mod).

```powershell
.\build.ps1             # build plugin + trainer -> DTDTrainer.exe ở thư mục này (plugin nhúng sẵn trong exe)
.\build.ps1 -Install    # build rồi chép luôn plugin vào runtime\BepInEx\plugins\DTDMod
```

Mang sang máy khác: chép `DTDTrainer.exe` và `vendor\`. Muốn khỏi chờ sinh interop lần đầu thì chép thêm `runtime\` (khoảng 290 MB). Exe đã kèm .NET nên máy đó không cần cài gì.

## Cấu trúc mã nguồn

```
src/DTDMod.Plugin/     plugin BepInEx (net6.0)
  Plugin.cs            nạp plugin, áp từng patch riêng lẻ + FoldGuard
  FoldGuard.cs         từ chối patch hàm bị "gộp mã máy" (xem bên dưới)
  PipeServer.cs        giao thức với trainer (PING/SET/ACTION/GET/DIAG)
  ModState.cs          trạng thái cheat (tên trường = khóa giao thức)
  ModBehaviour.cs      vòng lặp trên luồng game, đặt tiền tệ
  Ticks.cs             cheat dạng ghi dữ liệu định kỳ (chỉ số nhân viên, wasabi, đóng băng cá…)
  Patches/*.cs         các patch Harmony
src/DTDTrainer/        app WinForms (net10.0-windows)
tools/DTDXref/         công cụ dò ngược offline (xref + kiểm tra gộp mã)
vendor/                gói BepInEx 6 BE #788 (IL2CPP x64)
```

## Ghi chú cho người sửa mod

- **Song ngữ.** Mọi chuỗi giao diện viết tại chỗ dạng `L.T("tiếng Việt", "English")` (xem `src/DTDTrainer/Lang.cs`); mô tả mod nằm ở `DescriptionVi`/`DescriptionEn` trong `Cheats.cs`. Thêm chữ mới thì viết luôn cả hai bản.
- **Gộp mã máy (identical code folding).** `GameAssembly.dll` gộp các hàm có mã máy giống hệt nhau. Ví dụ `StaffData.get_Charm` dùng chung địa chỉ với 207 hàm khác. Harmony patch theo địa chỉ native, nên patch vào đó sẽ phá luôn các hàm kia. `FoldGuard` tự từ chối những patch như vậy lúc nạp; xem log `runtime\BepInEx\LogOutput.log`. Trước khi thêm patch mới, kiểm tra bằng:
  ```
  dotnet run --project tools\DTDXref -- --fold StaffData::get_Charm     # đọc interop trong runtime\, GameAssembly.dll trong thư mục game (chỉ đọc)
  dotnet run --project tools\DTDXref -- --disasm IsFishStop::IsCanNotDead   # dịch ngược mã máy x64
  dotnet run --project tools\DTDXref -- --enum "IsFishStop+FishStopReason"   # giá trị enum
  ```
- **Inline.** Getter/setter nhỏ thường bị inline. Nếu `DTDXref Type::Method` không có dòng `usedBy` nào thì patch nhiều khả năng vô tác dụng; khi đó ghi thẳng vào dữ liệu (xem `Ticks.cs`).
- **Nhịp vật lý: không được đổi.** Game chạy FixedUpdate 50 lần/giây, và nhiều chỗ phụ thuộc nhịp này. Ví dụ đạn, súng móc, lựu đạn được phóng bằng một lần `AddForce` kiểu Force (`GameObjectExtention.AddForce_Fixed`), nên vận tốc = lực × fixedDeltaTime; tăng nhịp lên 240 thì đạn bay chậm 4,8 lần. Khựng ở FPS cao được xử lý bằng `RenderSmoothing` (GraphicsPatches.cs): nội suy vị trí hiển thị của camera chính và Dave giữa hai nhịp, trả vị trí thật ở cuối khung hình. Lệnh `DIAG` có mục `smooth=` liệt kê transform đang làm mượt (`~` = đứng ngoài vì bị dời theo từng khung hình).
- **Không dùng `XrefScanner.XrefScan` lúc chạy.** Nó làm game crash (AccessViolation) trên Unity 6. Dùng `tools\DTDXref` thay thế.
- Khi game cập nhật: BepInEx tự sinh lại interop ở lần chạy đầu. Nếu log báo `Patch … bi bo qua`, dùng `DTDXref` để tìm tên method mới rồi build lại.

## Trạng thái kiểm thử (game v1.0.6.2113)

Đã kiểm chứng:
- Cả 26 patch áp dụng được và qua FoldGuard.
- Mở game thẳng từ Steam thì mod được nạp và trainer tự kết nối, dù mở trainer trước hay sau game.
- Mở lại trainer thì mọi mod về OFF.
- Bật/tắt từ trainer tới được game.
- Set Gold ghi đúng.
- Đóng trainer thì mọi mod tự tắt.

Chưa kiểm chứng trong lúc chơi thật (lặn, mở quán): hiệu quả thực tế của từng mod. Nếu mod nào không ăn, gửi `runtime\BepInEx\LogOutput.log` để sửa.
