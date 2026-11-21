using System.Drawing.Drawing2D;

namespace DTDTrainer.Controls;

/// <summary>Thanh trượt số thực bo góc với rãnh gradient ngọc lam hiện đại.</summary>
sealed class FlatSlider : Control
{
    float _value = 1f;
    bool _dragging;
    bool _hover;

    public float Minimum { get; set; } = 0.25f;
    public float Maximum { get; set; } = 5f;
    public float Step { get; set; } = 0.25f;

    public event EventHandler ValueChanged;

    public FlatSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(130, 26);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    public float Value
    {
        get => _value;
        set
        {
            float v = Math.Clamp(MathF.Round(value / Step) * Step, Minimum, Maximum);
            if (Math.Abs(v - _value) < 0.0001f) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    float TrackLeft => 10;
    float TrackRight => Width - 10;

    void SetFromMouse(int x)
    {
        float t = Math.Clamp((x - TrackLeft) / (TrackRight - TrackLeft), 0f, 1f);
        Value = Minimum + t * (Maximum - Minimum);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        Capture = true;
        SetFromMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetFromMouse(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x020A) // WM_MOUSEWHEEL
        {
            // Không cho con lăn chuột thay đổi thanh trượt tốc độ.
            // Chuyển toàn bộ sự kiện lăn chuột lên container cha để cuộn danh sách mượt mà.
            Control p = Parent;
            while (p != null && !(p is ScrollableControl))
                p = p.Parent;
            if (p != null)
            {
                SendMessage(p.Handle, m.Msg, m.WParam, m.LParam);
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Vô hiệu hóa thay đổi giá trị bằng con lăn chuột theo yêu cầu
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cy = Height / 2f;
        float trackH = 8f;
        float trackY = cy - trackH / 2f;
        float t = (Value - Minimum) / (Maximum - Minimum);
        float kx = TrackLeft + t * (TrackRight - TrackLeft);

        // Rãnh nền chưa trượt
        var fullRect = new RectangleF(TrackLeft, trackY, TrackRight - TrackLeft, trackH);
        using (var trackPath = Theme.RoundRect(fullRect, 3f))
        {
            using var b = new SolidBrush(Color.FromArgb(22, 28, 44));
            g.FillPath(b, trackPath);
            using var pen = new Pen(_hover ? Theme.BorderHover : Theme.Border, 1f);
            g.DrawPath(pen, trackPath);
        }

        // Đoạn tiến độ đã trượt (Gradient ngọc lam)
        float fillW = Math.Max(kx - TrackLeft, 6f);
        var activeRect = new RectangleF(TrackLeft, trackY, fillW, trackH);
        using (var fillPath = Theme.RoundRect(activeRect, 3f))
        {
            using var grad = new LinearGradientBrush(activeRect, Theme.Accent, Theme.AccentGradientEnd, 0f);
            g.FillPath(grad, fillPath);
        }

        // Núm kéo (Thumb) to rõ, dễ nhắm trúng
        float knobRadius = (_dragging || _hover) ? 10f : 8.5f;
        var knobRect = new RectangleF(kx - knobRadius, cy - knobRadius, knobRadius * 2, knobRadius * 2);

        // Vòng phát sáng xung quanh núm
        if (_dragging || _hover)
        {
            using var glowPen = new Pen(Color.FromArgb(70, Theme.Accent), 3f);
            g.DrawEllipse(glowPen, knobRect.X - 2, knobRect.Y - 2, knobRect.Width + 4, knobRect.Height + 4);
        }

        // Vỏ ngoài núm
        using (var kb = new SolidBrush(Color.White))
            g.FillEllipse(kb, knobRect);

        // Viền núm
        using (var pen = new Pen(Theme.AccentDark, 1.5f))
            g.DrawEllipse(pen, knobRect);

        // Tâm núm
        using (var centerBrush = new SolidBrush(Theme.Accent))
            g.FillEllipse(centerBrush, kx - 2.5f, cy - 2.5f, 5f, 5f);
    }
}
