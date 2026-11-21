using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>Dải nút chọn dạng capsule phân đoạn hiện đại (vd: Mặc định / 120 / 144 / 240 / 255 / Theo màn hình).</summary>
sealed class SegmentedControl : Control
{
    string[] _items = Array.Empty<string>();
    int _selected;
    int _hover = -1;

    public event EventHandler SelectedIndexChanged;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Height = 28;
        Cursor = Cursors.Hand;
        Font = Theme.Small;
        BackColor = Color.Transparent;
    }

    public string[] Items
    {
        get => _items;
        set
        {
            _items = value ?? Array.Empty<string>();
            _edges = new float[_items.Length + 1];
            for (int i = 0; i < _items.Length; i++)
                _edges[i + 1] = _edges[i] + Math.Max(TextRenderer.MeasureText(_items[i], Theme.Button).Width + 18, 46);
            Width = (int)Math.Ceiling(_edges[^1]);
            Invalidate();
        }
    }

    float[] _edges = { 0f };

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = Math.Clamp(value, 0, Math.Max(_items.Length - 1, 0));
            if (v == _selected) return;
            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    int IndexAt(int x)
    {
        for (int i = 0; i < _items.Length; i++)
            if (x < _edges[i + 1]) return i;
        return _items.Length - 1;
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
        if (e.Button == MouseButtons.Left) SelectedIndex = IndexAt(e.X);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Khung capsule ngoài
        using (var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 6f))
        {
            using (var b = new SolidBrush(Color.FromArgb(20, 26, 42)))
                g.FillPath(b, path);
            using (var pen = new Pen(Theme.Border, 1.2f))
                g.DrawPath(pen, path);
        }

        for (int i = 0; i < _items.Length; i++)
        {
            var r = new RectangleF(_edges[i] + 2, 2, _edges[i + 1] - _edges[i] - 4, Height - 4);
            if (i == _selected)
            {
                // Đoạn được chọn: Gradient ngọc lam rực rỡ
                using var path = Theme.RoundRect(r, 5f);
                using var grad = new LinearGradientBrush(r, Theme.Accent, Theme.AccentGradientEnd, 20f);
                g.FillPath(grad, path);

                TextRenderer.DrawText(g, _items[i], Theme.Button, Rectangle.Round(r), Theme.TextDark,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else
            {
                if (i == _hover)
                {
                    using var path = Theme.RoundRect(r, 5f);
                    using var b = new SolidBrush(Theme.ChipHover);
                    g.FillPath(b, path);
                }

                Color fg = (i == _hover) ? Theme.Text : Theme.TextDim;
                TextRenderer.DrawText(g, _items[i], Font, Rectangle.Round(r), fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }
}
