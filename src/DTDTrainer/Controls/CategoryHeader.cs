using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>
/// Tiêu đề phân nhóm mod phong cách WeMod/Wand: biểu tượng, tên nhóm và mũi tên thu gọn/mở rộng.
/// </summary>
sealed class CategoryHeader : Control
{
    public string CategoryId { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Icon { get; }
    public int Count { get; set; }

    bool _collapsed;
    bool _hover;

    public event EventHandler CollapsedChanged;

    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            if (_collapsed == value) return;
            _collapsed = value;
            Invalidate();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CategoryHeader(CategoryInfo info, int count)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        CategoryId = info.Id;
        Title = info.Name;
        Subtitle = L.English ? null : info.NameEn;
        Icon = info.Icon;
        Count = count;
        Height = 36;
        Margin = new Padding(0, 10, 0, 4);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Collapsed = !Collapsed;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Biểu tượng nhóm
        int x = 6;
        var iconRect = new Rectangle(x, (Height - 22) / 2, 22, 22);
        TextRenderer.DrawText(g, Icon, Theme.IconNormal, iconRect, Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        x += 26;

        // Tên nhóm
        Color titleColor = _hover ? Color.White : Color.FromArgb(220, 230, 245);
        var nameSize = TextRenderer.MeasureText(Title, Theme.Section, Size.Empty, TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, Title, Theme.Section, new Point(x, (Height - nameSize.Height) / 2), titleColor, TextFormatFlags.NoPrefix);
        x += nameSize.Width + 6;

        // Tên tiếng Anh phụ (nếu có)
        if (!string.IsNullOrEmpty(Subtitle) && Subtitle != Title)
        {
            var subSize = TextRenderer.MeasureText($"({Subtitle})", Theme.Small, Size.Empty, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, $"({Subtitle})", Theme.Small, new Point(x, (Height - subSize.Height) / 2), Theme.TextMuted, TextFormatFlags.NoPrefix);
            x += subSize.Width + 8;
        }

        // Mũi tên thu gọn bên phải
        int rx = Width - 24;
        string chevron = _collapsed ? MatIcons.ChevronUp : MatIcons.ChevronDown;
        var chevRect = new Rectangle(rx, (Height - 22) / 2, 22, 22);
        TextRenderer.DrawText(g, chevron, Theme.IconNormal, chevRect, _hover ? Theme.Accent : Theme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
