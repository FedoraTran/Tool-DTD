using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>Công tắc bật/tắt dạng viên thuốc phong cách hiện đại với hiệu ứng gradient ngọc lam.</summary>
sealed class ToggleSwitch : Control
{
    bool _checked;
    bool _hover;

    public event EventHandler CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(96, 32);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        float radius = r.Height / 2f;

        using (var path = Theme.RoundRect(r, radius))
        {
            if (_checked)
            {
                // Nền gradient ngọc lam khi bật
                Color start = _hover ? Theme.AccentGlow : Theme.Accent;
                Color end = _hover ? Color.FromArgb(0, 205, 235) : Theme.AccentGradientEnd;
                using var brush = new LinearGradientBrush(r, start, end, 15f);
                g.FillPath(brush, path);

                using var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1f);
                g.DrawPath(pen, path);
            }
            else
            {
                // Nền tối thanh lịch khi tắt
                Color trackBg = _hover ? Color.FromArgb(28, 38, 56) : Color.FromArgb(20, 27, 40);
                using var brush = new SolidBrush(trackBg);
                g.FillPath(brush, path);

                using var pen = new Pen(_hover ? Theme.BorderHover : Theme.Border, 1.2f);
                g.DrawPath(pen, path);
            }
        }

        // Kích thước núm trượt hình viên thuốc
        float knobW = (r.Width - 4) / 2f;
        float knobH = r.Height - 4;
        float kx = _checked ? (r.Right - knobW - 2) : (r.X + 2);
        float ky = r.Y + 2;
        var knobRect = new RectangleF(kx, ky, knobW, knobH);

        // Vẽ núm nổi
        using (var kp = Theme.RoundRect(knobRect, knobH / 2f))
        {
            if (_checked)
            {
                using var kb = new SolidBrush(Color.White);
                g.FillPath(kb, kp);
            }
            else
            {
                Color kc = _hover ? Color.FromArgb(36, 48, 70) : Color.FromArgb(28, 38, 56);
                using var kb = new SolidBrush(kc);
                g.FillPath(kb, kp);
                using var kpen = new Pen(Theme.BorderHover, 1f);
                g.DrawPath(kpen, kp);
            }
        }

        // Vẽ chữ Off và On
        var offRect = new Rectangle(2, 0, (Width - 4) / 2, Height);
        var onRect = new Rectangle(Width / 2, 0, (Width - 4) / 2, Height);

        Color offColor = _checked ? Color.FromArgb(0, 77, 64) : (_hover ? Color.White : Color.FromArgb(200, 215, 235));
        Color onColor = _checked ? Color.FromArgb(0, 77, 64) : (_hover ? Theme.TextDim : Color.FromArgb(80, 98, 120));

        TextRenderer.DrawText(g, "Off", Theme.Button, offRect, offColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        TextRenderer.DrawText(g, "On", Theme.Button, onRect, onColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
