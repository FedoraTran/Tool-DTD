using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>Nút bấm hiện đại hỗ trợ Google Material Icons và Google Inter font.</summary>
sealed class FlatButton : Control
{
    bool _hover, _down;

    public bool Primary { get; set; }
    public bool Danger { get; set; }
    public bool Pill { get; set; }
    public bool PlayButton { get; set; }
    public bool IsPlaying { get; set; }
    public string Icon { get; set; }

    public FlatButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(96, 32);
        Cursor = Cursors.Hand;
        Font = Theme.Button;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        float radius = Pill || PlayButton ? (r.Height / 2f) : 6f;

        using (var path = Theme.RoundRect(r, radius))
        {
            if (PlayButton)
            {
                if (IsPlaying)
                {
                    Color start = _down ? Theme.AccentDark : _hover ? Theme.AccentGlow : Theme.Accent;
                    Color end = _down ? Color.FromArgb(0, 140, 170) : _hover ? Color.FromArgb(0, 205, 240) : Theme.AccentGradientEnd;
                    using var grad = new LinearGradientBrush(r, start, end, 15f);
                    g.FillPath(grad, path);
                }
                else
                {
                    Color bg = _down ? Color.FromArgb(220, 225, 235) : _hover ? Color.FromArgb(245, 248, 255) : Color.White;
                    using var b = new SolidBrush(bg);
                    g.FillPath(b, path);
                }
            }
            else if (!Enabled)
            {
                using var b = new SolidBrush(Color.FromArgb(20, 25, 38));
                g.FillPath(b, path);
                using var pen = new Pen(Theme.BorderSubtle, 1f);
                g.DrawPath(pen, path);
            }
            else if (Danger)
            {
                Color bg = _down ? Color.FromArgb(60, 20, 28) : _hover ? Color.FromArgb(75, 25, 35) : Color.FromArgb(45, 18, 26);
                using var b = new SolidBrush(bg);
                g.FillPath(b, path);
                using var pen = new Pen(_hover ? Theme.Danger : Color.FromArgb(140, Theme.Danger), 1.2f);
                g.DrawPath(pen, path);
            }
            else if (Primary)
            {
                Color start = _down ? Theme.AccentDark : _hover ? Theme.AccentGlow : Theme.Accent;
                Color end = _down ? Color.FromArgb(0, 140, 170) : _hover ? Color.FromArgb(0, 205, 240) : Theme.AccentGradientEnd;

                using var grad = new LinearGradientBrush(r, start, end, 15f);
                    g.FillPath(grad, path);

                using var pen = new Pen(_hover ? Color.White : Color.FromArgb(140, 255, 255, 255), 1.2f);
                g.DrawPath(pen, path);
            }
            else
            {
                Color fill = _down ? Color.FromArgb(18, 24, 36) : _hover ? Theme.ChipHover : Theme.Chip;
                using var b = new SolidBrush(fill);
                g.FillPath(b, path);

                using var pen = new Pen(_hover ? Theme.BorderHover : Theme.Border, 1f);
                g.DrawPath(pen, path);
            }
        }

        // Màu chữ & icon
        Color fg;
        if (PlayButton) fg = Color.Black;
        else if (!Enabled) fg = Theme.TextMuted;
        else if (Danger) fg = _hover ? Color.White : Theme.Danger;
        else if (Primary) fg = Theme.TextDark;
        else fg = _hover ? Color.White : Theme.Text;

        bool hasIcon = !string.IsNullOrEmpty(Icon);
        bool hasText = !string.IsNullOrEmpty(Text);

        if (hasIcon && hasText)
        {
            var txtSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
            int iconW = 16;
            int gap = 6;
            int totalW = iconW + gap + txtSize.Width;
            int startX = Math.Max(4, (Width - totalW) / 2);

            var iconRect = new Rectangle(startX, 0, iconW, Height);
            TextRenderer.DrawText(g, Icon, Theme.IconSmall, iconRect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            var textRect = new Rectangle(startX + iconW + gap, 0, Width - (startX + iconW + gap), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        else if (hasIcon)
        {
            TextRenderer.DrawText(g, Icon, Theme.IconSmall, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else if (hasText)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        base.OnPaint(e);
    }

    /// <summary>Chiều rộng tối thiểu để hiện đủ icon và chữ (chữ tiếng Anh/tiếng Việt dài ngắn khác nhau).</summary>
    public int ContentWidth(int padding = 32)
    {
        int w = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix).Width;
        if (!string.IsNullOrEmpty(Icon)) w += 16 + (w > 0 ? 6 : 0);
        return w + padding;
    }
}
