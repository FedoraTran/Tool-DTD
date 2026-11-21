using System.Text.Json;

namespace DTDTrainer;

/// <summary>
/// Cài đặt lưu cạnh file exe (settings.json). Cố ý KHÔNG lưu trạng thái mod:
/// mỗi lần mở tool mọi mod bắt đầu ở OFF.
/// </summary>
sealed class TrainerSettings
{
    public string GamePath { get; set; }
    /// <summary>"vi" (mặc định) hoặc "en".</summary>
    public string Language { get; set; } = "vi";
    public Dictionary<string, long> Numbers { get; set; } = new();

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static TrainerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<TrainerSettings>(File.ReadAllText(FilePath)) ?? new TrainerSettings();
        }
        catch { /* file hỏng: dùng mặc định */ }
        return new TrainerSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options)); }
        catch { /* thư mục chỉ đọc: bỏ qua */ }
    }
}
