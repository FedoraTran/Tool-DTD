using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>Thanh lọc theo nhóm mod dạng các viên thuốc (Pill tabs) nằm ngang gọn gàng, đẹp mắt.</summary>
sealed class CategoryFilterBar : Control
{
    public sealed record Item(string Id, string Label, string Icon, int Count);

    readonly List<Item> _items = new();
    int _selected = 0;
    int _hover = -1;
    float[] _edges = { 0f };

    // Bố cục theo chiều rộng có sẵn: đủ chỗ thì giữ nguyên thiết kế đầy đủ, thiếu thì thu đệm rồi bỏ icon.
    float _pad = 16f;
    float _gap = 5f;
    bool _showIcons = true;

    public event Action<string> SelectedCategoryChanged;

    public CategoryFilterBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Height = 36;
        Cursor = Cursors.Hand;
        Font = Theme.Button;
        BackColor = Color.Transparent;
    }

    public void SetItems(IEnumerable<Item> items)
    {
        _items.Clear();
        _items.AddRange(items);
        RecomputeEdges();
        Invalidate();
    }

    public void FitWidth(int maxWidth)
    {
        foreach (var (pad, gap, icons) in new[] { (16f, 5f, true), (10f, 4f, true), (10f, 4f, false), (6f, 3f, false) })
        {
            _pad = pad; _gap = gap; _showIcons = icons;
            RecomputeEdges();
            if (maxWidth <= 0 || Width <= maxWidth) break;
        }
        Invalidate();
    }

    bool HasIcon(int i) => _showIcons && !string.IsNullOrEmpty(_items[i].Icon);

    string GetItemLabel(int i)
    {
        if (i < 0 || i >= _items.Count) return "";
        return (i == 0) ? $"{_items[i].Label} ({_items[i].Count})" : _items[i].Label;
    }

    void RecomputeEdges()
    {
        _edges = new float[_items.Count + 1];
        float x = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            string label = GetItemLabel(i);
            float labelW = TextRenderer.MeasureText(label, Theme.Button, Size.Empty, TextFormatFlags.NoPrefix).Width;
            float w = labelW + (HasIcon(i) ? 18 : 0) + _pad;
            _edges[i] = x;
            x += w + _gap;
        }
        _edges[^1] = x;
        Width = (int)Math.Ceiling(x);
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = Math.Clamp(value, 0, Math.Max(_items.Count - 1, 0));
            if (v == _selected) return;
            _selected = v;
            Invalidate();
            SelectedCategoryChanged?.Invoke(_items[_selected].Id);
        }
    }

    int IndexAt(int x)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            float right = (i < _items.Count - 1) ? _edges[i + 1] - _gap : _edges[i + 1];
            if (x >= _edges[i] && x <= right) return i;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = IndexAt(e.X);
        if (i != _hover) { _hover = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
        {
            int idx = IndexAt(e.X);
            if (idx >= 0) SelectedIndex = idx;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        for (int i = 0; i < _items.Count; i++)
        {
            float w = (i < _items.Count - 1) ? (_edges[i + 1] - _edges[i] - _gap) : (_edges[i + 1] - _edges[i]);
            var r = new RectangleF(_edges[i], 1, w, Height - 2);
            bool isSel = (i == _selected);
            bool isHov = (i == _hover);

            using var path = Theme.RoundRect(r, r.Height / 2f);

            if (isSel)
            {
                using var b = new LinearGradientBrush(r, Color.FromArgb(48, 64, 92), Color.FromArgb(30, 42, 62), 20f);
                g.FillPath(b, path);

                using var pen = new Pen(Theme.Accent, 1.3f);
                g.DrawPath(pen, path);
            }
            else
            {
                Color bg = isHov ? Color.FromArgb(28, 38, 54) : Color.FromArgb(18, 24, 35);
                using var b = new SolidBrush(bg);
                g.FillPath(b, path);

                using var pen = new Pen(isHov ? Theme.BorderHover : Theme.BorderSubtle, 1f);
                g.DrawPath(pen, path);
            }

            string label = GetItemLabel(i);
            Color fg = isSel ? Color.White : isHov ? Color.White : Theme.TextDim;
            Color iconFg = isSel ? Theme.Accent : isHov ? Color.White : Theme.TextDim;

            bool hasIcon = HasIcon(i);
            var labelFont = Theme.Button;
            var labelSize = TextRenderer.MeasureText(label, labelFont, Size.Empty, TextFormatFlags.NoPrefix);

            float totalW = labelSize.Width + (hasIcon ? 18 : 0);
            float startX = r.X + (r.Width - totalW) / 2f;

            if (hasIcon)
            {
                var iconRect = new Rectangle((int)startX, (int)r.Y, 16, (int)r.Height);
                TextRenderer.DrawText(g, _items[i].Icon, Theme.IconSmall, iconRect, iconFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                startX += 18;
            }

            var labelRect = new Rectangle((int)startX, (int)r.Y, labelSize.Width + 4, (int)r.Height);
            TextRenderer.DrawText(g, label, labelFont, labelRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
