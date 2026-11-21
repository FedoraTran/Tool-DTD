using System.Globalization;

namespace DTDTrainer;

/// <summary>
/// Ngôn ngữ giao diện: tiếng Việt (mặc định, để dùng) hoặc tiếng Anh (để phát triển).
/// Chuỗi được viết ngay tại chỗ dùng theo cặp <c>L.T("tiếng Việt", "English")</c>,
/// nên khi sửa giao diện luôn thấy và cập nhật cả hai bản cùng lúc.
/// </summary>
static class L
{
    public static bool English { get; set; }

    /// <summary>Mã lưu trong settings.json: "vi" hoặc "en".</summary>
    public static string Code => English ? "en" : "vi";

    public static string T(string vi, string en) => English ? en : vi;

    /// <summary>Định dạng số theo ngôn ngữ (vi: 1.234.567, en: 1,234,567).</summary>
    public static CultureInfo Numbers => CultureInfo.GetCultureInfo(English ? "en-US" : "vi-VN");
}
