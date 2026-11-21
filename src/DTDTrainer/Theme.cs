using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DTDTrainer;

/// <summary>Bảng mã ký hiệu Google Material Icons chuẩn.</summary>
public static class MatIcons
{
    public const string Play = "\ue037";           // play_arrow
    public const string Pause = "\ue034";          // pause
    public const string Check = "\ue5ca";          // check
    public const string CheckCircle = "\ue86c";    // check_circle
    public const string Close = "\ue5cd";          // close
    public const string Minimize = "\ue15b";       // remove
    public const string Maximize = "\ue3c6";       // crop_square
    public const string Search = "\ue8b6";         // search
    public const string Settings = "\ue8b8";       // settings
    public const string Tune = "\ue429";           // tune
    public const string Star = "\ue838";           // star
    public const string Notifications = "\ue7f4";  // notifications
    public const string Pin = "\ue8e6";            // push_pin
    public const string Bolt = "\uea0b";           // bolt
    public const string Walk = "\ue566";           // directions_walk
    public const string Swim = "\ueb43";           // pool
    public const string Shield = "\ue32a";         // shield
    public const string Heart = "\ue87d";          // favorite
    public const string Air = "\ue554";            // air
    public const string Battery = "\ue1a3";        // battery_charging_full
    public const string Bag = "\ue8cc";            // shopping_bag
    public const string Gold = "\ue850";           // monetization_on
    public const string Restaurant = "\ue56c";     // restaurant
    public const string Grid = "\ue871";           // dashboard / grid_view
    public const string Person = "\ue7fd";         // person
    public const string Stats = "\ue8e5";          // trending_up
    public const string Science = "\uea4b";        // science
    public const string Gamepad = "\ue338";        // sports_esports
    public const string Display = "\ue30c";        // tv
    public const string Translate = "\ue894";      // translate
    public const string Power = "\ue8ac";          // power_settings_new
    public const string Folder = "\ue2c7";         // folder
    public const string Info = "\ue88e";           // info
    public const string ChevronDown = "\ue313";    // keyboard_arrow_down
    public const string ChevronUp = "\ue316";      // keyboard_arrow_up
    public const string Add = "\ue145";            // add
    public const string Remove = "\ue15b";         // remove
    public const string Help = "\ue8fd";           // help_outline
    public const string Videocam = "\ue04b";       // videocam
    public const string Refresh = "\ue5d5";        // refresh
}

/// <summary>Hệ thống giao diện, phông chữ Google Inter và Google Material Icons.</summary>
static class Theme
{
    private static readonly PrivateFontCollection _fonts = new();
    private static readonly List<IntPtr> _allocatedMemory = new();
    public static readonly FontFamily InterFamily;
    public static readonly FontFamily MaterialFamily;

    // Nền chính và các bề mặt
    public static readonly Color Background = Color.FromArgb(14, 18, 26);         // #0E121A - Nền chính
    public static readonly Color HeaderBg = Color.FromArgb(10, 14, 22);           // #0A0E16 - Thanh tiêu đề trên cùng
    public static readonly Color Surface = Color.FromArgb(18, 24, 35);            // #121823 - Thẻ card mod
    public static readonly Color SurfaceHover = Color.FromArgb(24, 32, 47);       // #18202F - Thẻ khi rê chuột
    public static readonly Color SurfaceActive = Color.FromArgb(18, 36, 48);      // #122430 - Thẻ khi mod đang bật
    public static readonly Color PinnedActiveStart = Color.FromArgb(58, 24, 68);  // #3A1844 - Tím gradient hàng ghim
    public static readonly Color PinnedActiveEnd = Color.FromArgb(18, 28, 52);    // #121C34 - Xanh tím indigo
    public static readonly Color Chip = Color.FromArgb(24, 32, 46);               // #18202E - Khung ô nhập
    public static readonly Color ChipHover = Color.FromArgb(34, 46, 66);          // #222E42 - Khung khi rê chuột
    public static readonly Color SearchBg = Color.FromArgb(20, 27, 39);           // #141B27 - Ô tìm kiếm

    // Viền
    public static readonly Color Border = Color.FromArgb(30, 40, 58);             // #1E283A - Viền bình thường
    public static readonly Color BorderHover = Color.FromArgb(46, 60, 86);        // #2E3C56 - Viền khi rê chuột
    public static readonly Color BorderActive = Color.FromArgb(0, 229, 195);      // #00E5C3 - Viền ngọc lam khi bật
    public static readonly Color BorderSubtle = Color.FromArgb(22, 29, 42);       // #161D2A - Viền mờ

    // Màu nhấn (Neon Cyan & Ngọc lam đại dương)
    public static readonly Color Accent = Color.FromArgb(0, 229, 195);            // #00E5C3 - Xanh ngọc lam neon
    public static readonly Color AccentGlow = Color.FromArgb(0, 245, 212);        // #00F5D4 - Sáng rực
    public static readonly Color AccentDark = Color.FromArgb(0, 168, 142);        // #00A88E
    public static readonly Color AccentGradientEnd = Color.FromArgb(0, 180, 216); // #00B4D8 - Xanh dương đại dương
    public static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);     // #A855F7 - Tím PRO
    public static readonly Color StarCyan = Color.FromArgb(56, 189, 248);         // #38BDF8 - Sao đánh dấu

    // Màu trạng thái & tiền tệ
    public static readonly Color Gold = Color.FromArgb(255, 183, 3);              // #FFB703 - Vàng sushi
    public static readonly Color Danger = Color.FromArgb(244, 63, 94);            // #F43F5E - Đỏ cảnh báo
    public static readonly Color Warning = Color.FromArgb(251, 191, 36);          // #FBBF24 - Vàng cam
    public static readonly Color Success = Color.FromArgb(16, 185, 129);          // #10B981 - Xanh lá thành công
    public static readonly Color ToastBg = Color.FromArgb(16, 23, 34);            // #101722 - Nền thông báo nổi
    public static readonly Color ToastBorder = Color.FromArgb(28, 38, 56);        // #1C2638

    // Màu chữ
    public static readonly Color Text = Color.FromArgb(248, 250, 252);            // #F8FAFC - Trắng sáng
    public static readonly Color TextDim = Color.FromArgb(148, 163, 184);         // #94A3B8 - Xám sáng vừa
    public static readonly Color TextMuted = Color.FromArgb(90, 105, 126);        // #5A697E - Xám tối
    public static readonly Color TextDark = Color.FromArgb(10, 22, 24);           // #0A1618 - Chữ đậm trên nút nhấn

    // Phông chữ Google Inter
    public static readonly Font HeroTitle;
    public static readonly Font Title;
    public static readonly Font Header;
    public static readonly Font Section;
    public static readonly Font Body;
    public static readonly Font BodyBold;
    public static readonly Font Small;
    public static readonly Font SmallBold;
    public static readonly Font Badge;
    public static readonly Font Button;
    public static readonly Font ButtonLarge;
    public static readonly Font ButtonXLarge;
    public static readonly Font Keycap;

    // Phông chữ Google Material Icons
    public static readonly Font IconSmall;
    public static readonly Font IconNormal;
    public static readonly Font IconMedium;
    public static readonly Font IconLarge;
    public static readonly Font IconXLarge;

    static Theme()
    {
        LoadFont("Inter.ttf");
        LoadFont("MaterialIcons-Regular.ttf");

        InterFamily = _fonts.Families.FirstOrDefault(f => f.Name.StartsWith("Inter", StringComparison.OrdinalIgnoreCase))
                      ?? FontFamily.GenericSansSerif;

        MaterialFamily = _fonts.Families.FirstOrDefault(f => f.Name.Contains("Material", StringComparison.OrdinalIgnoreCase))
                         ?? FontFamily.GenericSansSerif;

        HeroTitle = new Font(InterFamily, 22f, FontStyle.Bold);
        Title = new Font(InterFamily, 14f, FontStyle.Bold);
        Header = new Font(InterFamily, 11f, FontStyle.Bold);
        Section = new Font(InterFamily, 10f, FontStyle.Bold);
        Body = new Font(InterFamily, 9.25f, FontStyle.Regular);
        BodyBold = new Font(InterFamily, 9.25f, FontStyle.Bold);
        Small = new Font(InterFamily, 8.25f, FontStyle.Regular);
        SmallBold = new Font(InterFamily, 8.25f, FontStyle.Bold);
        Badge = new Font(InterFamily, 7.5f, FontStyle.Bold);
        Button = new Font(InterFamily, 9.5f, FontStyle.Bold);
        ButtonLarge = new Font(InterFamily, 10.5f, FontStyle.Bold);
        ButtonXLarge = new Font(InterFamily, 11.5f, FontStyle.Bold);
        Keycap = new Font(InterFamily, 8.5f, FontStyle.Bold);

        IconSmall = new Font(MaterialFamily, 11f, FontStyle.Regular);
        IconNormal = new Font(MaterialFamily, 13.5f, FontStyle.Regular);
        IconMedium = new Font(MaterialFamily, 16f, FontStyle.Regular);
        IconLarge = new Font(MaterialFamily, 20f, FontStyle.Regular);
        IconXLarge = new Font(MaterialFamily, 24f, FontStyle.Regular);
    }

    public static Font IconFont(float size) => new(MaterialFamily, size, FontStyle.Regular);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

    private const uint FR_PRIVATE = 0x10;

    private static void LoadFont(string name)
    {
        string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", name);
        if (!File.Exists(local))
            local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", name);
        if (File.Exists(local))
        {
            try
            {
                AddFontResourceEx(local, FR_PRIVATE, IntPtr.Zero);
                _fonts.AddFontFile(local);
                return;
            }
            catch { }
        }

        try
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream(name);
            if (stream != null)
            {
                byte[] data = new byte[stream.Length];
                stream.ReadExactly(data);
                IntPtr p = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, p, data.Length);
                _allocatedMemory.Add(p);
                uint cFonts = 0;
                AddFontMemResourceEx(p, (uint)data.Length, IntPtr.Zero, ref cFonts);
                _fonts.AddMemoryFont(p, data.Length);
            }
        }
        catch { }
    }

    /// <summary>Tạo đường dẫn hình chữ nhật bo tròn góc mềm mại.</summary>
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Tô chuyển sắc mượt mà với kiểm tra kích thước an toàn.</summary>
    public static void FillGradient(Graphics g, RectangleF r, Color start, Color end, float angle = 90f)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var brush = new LinearGradientBrush(r, start, end, angle);
        using var path = RoundRect(r, 6);
        g.FillPath(brush, path);
    }
}
