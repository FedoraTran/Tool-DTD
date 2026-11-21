using System.Drawing.Drawing2D;
using System.Globalization;
using DTDTrainer.Controls;

namespace DTDTrainer;

/// <summary>Thẻ card hiển thị một mod: tên, mô tả, điều khiển kích thước to rõ, hiệu ứng gradient chỉ khi hover chuột.</summary>
sealed class CheatRow : Panel
{
    public const int RowHeight = 60;

    public CheatDef Def { get; }
    public ToggleSwitch Toggle { get; }
    public FlatSlider Slider { get; }
    public SegmentedControl Choice { get; }
    public TextBox NumberBox { get; }
    public FlatButton ApplyButton { get; }
    public FlatButton ActionButton { get; }

    public string HotkeyText { get; set; }

    readonly Label _name;
    readonly Label _info;
    readonly Label _sub;
    readonly Label _value;
    readonly FlatButton _minus;
    readonly FlatButton _plus;
    readonly Panel _numberFrame;
    readonly Label _currencyIcon;

    bool _hover;
    public bool SimulatedHover { get; set; }
    bool IsHovered => _hover || SimulatedHover;
    bool _isNumberFocused;
    readonly bool _built;

    public bool IsActive
    {
        get
        {
            if (Toggle != null) return Toggle.Checked;
            if (Slider != null) return Math.Abs(Slider.Value - 1.0f) > 0.001f;
            if (Choice != null) return Choice.SelectedIndex != 0;
            return false;
        }
    }

    public CheatRow(CheatDef def, ToolTip tip)
    {
        Def = def;
        HotkeyText = def.Hotkey ?? "";
        Height = RowHeight;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 5);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);

        _name = new Label
        {
            Text = def.Name,
            Font = Theme.BodyBold,
            ForeColor = Theme.Text,
            AutoSize = true,
            BackColor = Color.Transparent,
            Cursor = Cursors.Default
        };

        _info = new Label
        {
            Text = MatIcons.Info,
            Font = Theme.IconSmall,
            ForeColor = Theme.TextDim,
            AutoSize = true,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        _info.MouseEnter += (_, _) => { _info.ForeColor = Theme.Accent; };
        _info.MouseLeave += (_, _) => { _info.ForeColor = Theme.TextDim; };

        tip.SetToolTip(_info, def.Description);
        tip.SetToolTip(_name, def.Description);
        Controls.Add(_name);
        Controls.Add(_info);

        switch (def.Kind)
        {
            case CheatKind.Toggle:
                Toggle = new ToggleSwitch { Size = new Size(96, 32) };
                Toggle.CheckedChanged += (_, _) => Invalidate();
                Controls.Add(Toggle);
                ForwardHover(Toggle);
                break;

            case CheatKind.Slider:
                Slider = new FlatSlider { Width = 210, Minimum = 1f, Maximum = 5f, Step = 0.25f };
                _minus = new FlatButton { Text = MatIcons.Remove, Size = new Size(36, 32), Font = Theme.IconNormal };
                _plus = new FlatButton { Text = MatIcons.Add, Size = new Size(36, 32), Font = Theme.IconNormal };
                _minus.Click += (_, _) => Slider.Value -= Slider.Step;
                _plus.Click += (_, _) => Slider.Value += Slider.Step;
                tip.SetToolTip(_minus, L.T("Giảm 0.25x", "Decrease 0.25x"));
                tip.SetToolTip(_plus, L.T("Tăng 0.25x", "Increase 0.25x"));
                Controls.Add(_minus);
                Controls.Add(_plus);
                _value = new Label
                {
                    Font = Theme.Keycap,
                    ForeColor = Theme.Accent,
                    AutoSize = false,
                    Width = 66,
                    Height = 32,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Theme.Chip
                };
                Slider.ValueChanged += (_, _) => { UpdateSliderLabel(); Invalidate(); };
                Controls.Add(Slider);
                Controls.Add(_value);
                UpdateSliderLabel();
                ForwardHover(Slider);
                ForwardHover(_minus);
                ForwardHover(_plus);
                ForwardHover(_value);
                break;

            case CheatKind.Choice:
                Choice = new SegmentedControl { Items = def.OptionLabels, Height = 32 };
                Choice.SelectedIndexChanged += (_, _) => Invalidate();
                Controls.Add(Choice);
                ForwardHover(Choice);
                break;

            case CheatKind.Number:
                _numberFrame = new Panel
                {
                    BackColor = Theme.Chip,
                    Size = new Size(122, 32),
                    Padding = new Padding(6, 6, 8, 4)
                };

                bool isGold = def.Key == "SetGold";
                string currSymbol = isGold ? MatIcons.Gold : def.Key switch
                {
                    "SetBei" => MatIcons.Star,
                    "SetChefFlame" => MatIcons.Bolt,
                    "SetDoni" => MatIcons.Gold,
                    "SetJungleChefFlame" => MatIcons.Bolt,
                    _ => MatIcons.Tune
                };

                _currencyIcon = new Label
                {
                    Text = currSymbol,
                    Font = Theme.IconSmall,
                    AutoSize = true,
                    Location = new Point(6, 6),
                    ForeColor = isGold ? Theme.Gold : Theme.Accent,
                    BackColor = Color.Transparent
                };

                NumberBox = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Chip,
                    ForeColor = Theme.Text,
                    Font = Theme.BodyBold,
                    Location = new Point(26, 6),
                    Width = 82,
                    MaxLength = 9,
                    TextAlign = HorizontalAlignment.Right,
                };
                NumberBox.GotFocus += (_, _) => { _isNumberFocused = true; _numberFrame.Invalidate(); };
                NumberBox.LostFocus += (_, _) => { _isNumberFocused = false; _numberFrame.Invalidate(); };
                NumberBox.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

                _numberFrame.Paint += (_, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var fr = new RectangleF(0.5f, 0.5f, _numberFrame.Width - 1, _numberFrame.Height - 1);
                    using var path = Theme.RoundRect(fr, 6f);
                    using var pen = new Pen(_isNumberFocused ? Theme.Accent : Theme.Border, 1.2f);
                    pe.Graphics.DrawPath(pen, path);
                };

                _numberFrame.Controls.Add(_currencyIcon);
                _numberFrame.Controls.Add(NumberBox);

                ApplyButton = new FlatButton { Text = L.T("Áp dụng", "Apply"), Primary = true, Size = new Size(98, 32), Font = Theme.Button };
                _sub = new Label
                {
                    Font = Theme.Small,
                    ForeColor = Theme.TextDim,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Text = L.T("Hiện tại", "Current") + ": —"
                };

                Controls.Add(_numberFrame);
                Controls.Add(ApplyButton);
                Controls.Add(_sub);
                ForwardHover(_numberFrame);
                ForwardHover(_currencyIcon);
                ForwardHover(NumberBox);
                ForwardHover(ApplyButton);
                ForwardHover(_sub);
                break;

            case CheatKind.Button:
                ActionButton = new FlatButton { Text = def.ButtonLabel, Primary = true, Size = new Size(122, 32), Font = Theme.Button };
                _sub = new Label
                {
                    Font = Theme.Small,
                    ForeColor = Theme.TextDim,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Text = CurrentLabel + ": —"
                };
                Controls.Add(ActionButton);
                Controls.Add(_sub);
                ForwardHover(ActionButton);
                ForwardHover(_sub);
                break;
        }

        ForwardHover(_name);
        ForwardHover(_info);

        _built = true;
        PerformLayout();
    }

    void ForwardHover(Control c)
    {
        if (c == null) return;
        c.MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        c.MouseLeave += (_, _) =>
        {
            var pt = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(pt))
            {
                _hover = false;
                Invalidate();
            }
        };
    }

    public long? NumberValue
    {
        get => long.TryParse(NumberBox?.Text, out long v) ? v : null;
        set { if (NumberBox != null) NumberBox.Text = value?.ToString(CultureInfo.InvariantCulture) ?? ""; }
    }

    string CurrentLabel => Def.SnapshotKey == "jungleClock" ? L.T("Giờ Jungle", "Jungle time") : L.T("Hiện tại", "Current");

    public void SetCurrent(int value)
    {
        if (_sub == null) return;
        string shown = value < 0 ? "—"
            : Def.SnapshotKey == "jungleClock" ? $"{value / 60:00}:{value % 60:00}"
            : value.ToString("N0", L.Numbers);
        string text = CurrentLabel + ": " + shown;
        if (_sub.Text != text)
        {
            _sub.Text = text;
            _sub.ForeColor = value >= 0 ? Theme.Accent : Theme.TextDim;
        }
    }

    void UpdateSliderLabel()
    {
        if (_value == null || Slider == null) return;
        _value.Text = Slider.Value.ToString("0.00", CultureInfo.InvariantCulture) + "x";
        _minus.Enabled = Slider.Value > Slider.Minimum + 0.001f;
        _plus.Enabled = Slider.Value < Slider.Maximum - 0.001f;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        var pt = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(pt))
        {
            _hover = false;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        float radius = 7f;
        bool active = IsActive;

        if (IsHovered)
        {
            // Rê chuột vào mới hiển thị dải màu gradient biển sâu ngọc lam -> tím huyền ảo
            Color g1 = active ? Color.FromArgb(0, 105, 138) : Color.FromArgb(16, 82, 122);
            Color g2 = active ? Color.FromArgb(64, 24, 108) : Color.FromArgb(52, 22, 88);
            using (var grad = new LinearGradientBrush(r, g1, g2, 20f))
            {
                using var path = Theme.RoundRect(r, radius);
                g.FillPath(grad, path);
            }

            // Thanh chỉ báo dọc ngọc lam ở cạnh trái đặc trưng
            var barRect = new RectangleF(r.X + 2, r.Y + 8, 3.5f, r.Height - 16);
            using (var barPath = Theme.RoundRect(barRect, 1.8f))
            using (var barBrush = new SolidBrush(active ? Theme.Accent : Color.FromArgb(0, 225, 240)))
            {
                g.FillPath(barBrush, barPath);
            }

            using (var path = Theme.RoundRect(r, radius))
            using (var pen = new Pen(active ? Theme.Accent : Color.FromArgb(0, 210, 230), 1.3f))
                g.DrawPath(pen, path);
        }
        else
        {
            Color fill = active ? Theme.SurfaceActive : Theme.Surface;
            Color border = active ? Color.FromArgb(140, Theme.BorderActive) : Theme.Border;

            using (var path = Theme.RoundRect(r, radius))
            {
                using (var b = new SolidBrush(fill))
                    g.FillPath(b, path);

                using var pen = new Pen(border, active ? 1.4f : 1f);
                    g.DrawPath(pen, path);
            }
        }

        // Biểu tượng bên trái to rõ
        var iconRect = new RectangleF(16, (Height - 32) / 2f, 32, 32);
        using var circleBrush = new SolidBrush(active ? Color.FromArgb(36, Theme.Accent) : Color.FromArgb(22, 28, 40));
        g.FillEllipse(circleBrush, iconRect);

        string iconStr = Def.Icon ?? MatIcons.Bolt;
        Color iconColor = active ? Theme.Accent : IsHovered ? Color.White : Theme.TextDim;
        TextRenderer.DrawText(g, iconStr, Theme.IconNormal, Rectangle.Round(iconRect), iconColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        if (_name != null)
            _name.ForeColor = active ? Color.White : IsHovered ? Color.White : Color.FromArgb(220, 228, 240);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (!_built) return;

        int cy = Height / 2;
        int x = Width - 16;

        void Place(Control c, int gap = 8)
        {
            if (c == null || !c.Visible) return;
            x -= c.Width;
            c.Location = new Point(x, cy - c.Height / 2);
            x -= gap;
        }

        // Bố trí điều khiển chính với kích thước to rõ
        switch (Def.Kind)
        {
            case CheatKind.Toggle:
                Place(Toggle);
                break;

            case CheatKind.Slider:
                Place(_value, 8);
                Place(_plus, 5);
                Place(Slider, 5);
                Place(_minus);
                break;

            case CheatKind.Choice:
                Place(Choice);
                break;

            case CheatKind.Number:
                Place(ApplyButton, 8);
                Place(_numberFrame);
                break;

            case CheatKind.Button:
                Place(ActionButton);
                break;
        }

        int textLeft = 60;
        if (_sub != null)
        {
            _name.Location = new Point(textLeft, cy - _name.Height + 2);
            _sub.Location = new Point(textLeft, cy + 2);
        }
        else
        {
            _name.Location = new Point(textLeft, cy - _name.Height / 2);
        }

        _info.Location = new Point(_name.Right + 8, _name.Top + 2);
    }
}
