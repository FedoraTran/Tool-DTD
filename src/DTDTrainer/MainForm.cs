using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using DTDTrainer.Controls;

namespace DTDTrainer;

/// <summary>
/// Cửa sổ chính hiện đại: giao diện Wand (WeMod) chân thực cho Dave the Diver.
/// Gồm thanh tiêu đề tuỳ biến, sidebar điều hướng danh sách game,
/// hero banner đại dương sâu thẳm, thẻ mod phong cách WeMod với phím tắt và nút ghim.
/// </summary>
sealed class MainForm : Form
{
    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

    readonly TrainerSettings _settings = TrainerSettings.Load();
    readonly PipeClient _pipe = new();
    readonly ToolTip _tip = new() { InitialDelay = 300, AutoPopDelay = 15000 };

    readonly Dictionary<string, CheatRow> _rows = new();
    readonly Dictionary<string, CategoryHeader> _categoryHeaders = new();
    readonly ConcurrentDictionary<string, string> _desired = new();

    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 800 };

    // Layout Panels
    DoubleBufferedPanel _topBar;
    DoubleBufferedPanel _mainContainer;
    DoubleBufferedPanel _heroHeader;
    DoubleBufferedPanel _tabsBar;
    DoubleBufferedPanel _settingsPanel;
    DoubleBufferedPanel _listViewport;
    DoubleBufferedFlowLayoutPanel _list;
    DoubleBufferedPanel _toastPanel;

    // Controls
    CategoryFilterBar _filterBar;
    StatusPill _pill;
    FlatButton _playBtn;
    FlatButton _gearBtn;
    FlatButton _allOffBtn;
    FlatButton _langBtn;
    TextBox _pathBox;
    Label _pathStatus;
    Label _installInfo;
    Label _toastMsg;

    Image _coverImg;
    Image _heroImg;
    Icon _gameIcon;

    DateTime? _launchStarted;
    DateTime? _lastAutoInjectTime;
    int _lastInjectedPid;
    bool _connected => _pipe.Connected;
    bool _restoring;
    string _currentFilter = null; // null = All
    IReadOnlyDictionary<string, string> _lastState;

    readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 4000 };

    public bool RenderOnly { get; set; }

    public MainForm(bool renderOnly = false)
    {
        RenderOnly = renderOnly;
        L.English = _settings.Language == "en";
        Text = "DAVE THE DIVER TRAINER";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(840, 560);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        LoadAssets();

        if (!ModInstaller.IsGameDir(_settings.GamePath))
            _settings.GamePath = ModInstaller.DetectGameDir() ?? _settings.GamePath;
        if (ModInstaller.IsGameDir(_settings.GamePath))
        {
            _settings.GamePath = Path.GetFullPath(_settings.GamePath);
            _settings.Save();
        }

        BuildUi();
        RestoreStates();

        _pipe.SyncCommands = () => _desired.Select(kv => $"SET {kv.Key} {kv.Value}").ToList();
        _pipe.ConnectionChanged += c => BeginInvokeSafe(() =>
        {
            RefreshStatus();
            UpdateToast(c ? L.T("✓ Đã kết nối với Dave the Diver. Mods đang hoạt động!", "✓ Connected to Dave the Diver. Mods are active!")
                          : L.T("Mất kết nối với game.", "Lost connection to the game."));
        });
        _pipe.StateReceived += s => BeginInvokeSafe(() => OnState(s));
        _pipe.Replied += (cmd, reply) => BeginInvokeSafe(() => OnReply(cmd, reply));

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); if (_toastPanel != null) _toastPanel.Visible = false; };

        if (!RenderOnly)
        {
            try { EnsureModReady(announce: false); }
            catch (Exception ex) { UpdateToast(L.T("Chưa chuẩn bị được mod: ", "Could not prepare the mod: ") + ex.Message); }
            if (ModInstaller.HasLegacyInstall(GameDir))
                UpdateToast(L.T("Thư mục game còn bản cài cũ. Vào ⚙ Cài đặt → Dọn thư mục game.",
                               "The game folder still has old install. Open ⚙ Settings → Clean."));
        }
        RefreshStatus();
    }

    void LoadAssets()
    {
        try
        {
            using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("AppIcon.ico");
            if (iconStream != null)
            {
                Icon = new Icon(iconStream);
                iconStream.Position = 0;
                _gameIcon = new Icon(iconStream, new Size(48, 48));
            }
        }
        catch { }

        _coverImg = LoadResourceImage("cover.jpg");
        _heroImg = LoadResourceImage("hero.jpg");
    }

    Image LoadResourceImage(string name)
    {
        string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", name);
        if (!File.Exists(local))
            local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", name);
        if (File.Exists(local))
        {
            try { return Image.FromFile(local); } catch { }
        }
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream(name);
            if (stream != null) return Image.FromStream(stream);
        }
        catch { }
        return null;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        base.WndProc(ref m);

        if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT && WindowState == FormWindowState.Normal)
        {
            var pt = PointToClient(new Point(m.LParam.ToInt32()));
            int grip = 8;
            if (pt.X <= grip && pt.Y <= grip) m.Result = (IntPtr)HTTOPLEFT;
            else if (pt.X >= ClientSize.Width - grip && pt.Y <= grip) m.Result = (IntPtr)HTTOPRIGHT;
            else if (pt.X <= grip && pt.Y >= ClientSize.Height - grip) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (pt.X >= ClientSize.Width - grip && pt.Y >= ClientSize.Height - grip) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (pt.X <= grip) m.Result = (IntPtr)HTLEFT;
            else if (pt.X >= ClientSize.Width - grip) m.Result = (IntPtr)HTRIGHT;
            else if (pt.Y <= grip) m.Result = (IntPtr)HTTOP;
            else if (pt.Y >= ClientSize.Height - grip) m.Result = (IntPtr)HTBOTTOM;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        if (_list != null && _list.IsHandleCreated) SetWindowTheme(_list.Handle, "DarkMode_Explorer", null);
    }

    // ==================================================================
    // DỰNG GIAO DIỆN CHÍNH
    // ==================================================================

    void BuildUi()
    {
        SuspendLayout();

        // 1. Container chính Fill toàn màn hình
        _mainContainer = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background
        };

        // 2. Thanh tiêu đề trên cùng
        BuildTopBar();

        Controls.Add(_mainContainer);
        Controls.Add(_topBar);
        _topBar.SendToBack();
        _mainContainer.BringToFront();

        // Dựng các phần bên trong main container
        BuildHeroHeader();
        BuildSettingsPanel();
        BuildFilterTabsBar();
        BuildCheatsListArea();
        BuildBottomToast();

        ApplyDockOrder();
        ResumeLayout(true);
    }

    /// <summary>
    /// Thứ tự xếp từ trên xuống: Hero → Cài đặt → Thanh lọc → Danh sách (Fill), toast nổi trên cùng.
    /// Dock=Top xếp control ở cuối danh sách con trước, nên đặt chỉ số tường minh; gọi lại mỗi khi bật/tắt Cài đặt
    /// (sau khi đổi ngôn ngữ, cách cũ dùng SendToBack làm bảng Cài đặt nhảy lên trên Hero).
    /// </summary>
    void ApplyDockOrder()
    {
        var c = _mainContainer.Controls;
        c.SetChildIndex(_toastPanel, 0);
        c.SetChildIndex(_listViewport, 1);
        c.SetChildIndex(_tabsBar, 2);
        c.SetChildIndex(_settingsPanel, 3);
        c.SetChildIndex(_heroHeader, 4);
        _mainContainer.PerformLayout();
    }

    void ShowSettings(bool visible)
    {
        _settingsPanel.Visible = visible;
        if (_gearBtn != null) _gearBtn.Primary = visible;
        ApplyDockOrder();
    }

    // ------------------------------------------------------------------ Top Bar
    void BuildTopBar()
    {
        _topBar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Theme.HeaderBg,
            Padding = new Padding(16, 0, 8, 0)
        };

        _topBar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };

        _topBar.Paint += (_, pe) =>
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Icon Dave The Diver hoặc biểu tượng bơi
            int iconX = 14;
            int iconY = 12;
            if (_gameIcon != null)
            {
                g.DrawIcon(_gameIcon, new Rectangle(iconX, iconY, 24, 24));
            }
            else
            {
                var ir = new RectangleF(iconX, iconY, 24, 24);
                using var ib = new SolidBrush(Theme.Gold);
                g.FillEllipse(ib, ir);
                TextRenderer.DrawText(g, MatIcons.Swim, Theme.IconSmall, Rectangle.Round(ir), Color.Black,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // Tên ứng dụng bằng phông Google Inter Bold
            TextRenderer.DrawText(g, "DAVE THE DIVER TRAINER", Theme.Header, new Point(46, 15), Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Huy hiệu phiên bản
            int titleW = TextRenderer.MeasureText("DAVE THE DIVER TRAINER", Theme.Header, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            var verRect = new RectangleF(46 + titleW + 8, 15, 38, 18);
            using var vb = new SolidBrush(Color.FromArgb(28, 38, 54));
            using var vp = Theme.RoundRect(verRect, 4f);
            g.FillPath(vb, vp);
            TextRenderer.DrawText(g, "v1.0", Theme.Badge, Rectangle.Round(verRect), Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // Kẻ đáy phân cách
            using var pen = new Pen(Theme.BorderSubtle, 1f);
            g.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
        };

        // Khối nút góc phải
        BuildTopRightButtons();
    }

    void BuildTopRightButtons()
    {
        // Window Controls (—, □, ✕) với Material Icons to rõ
        var closeBtn = new Label { Text = MatIcons.Close, Font = Theme.IconNormal, Size = new Size(42, 36), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextDim, Cursor = Cursors.Hand };
        closeBtn.MouseEnter += (_, _) => { closeBtn.BackColor = Theme.Danger; closeBtn.ForeColor = Color.White; };
        closeBtn.MouseLeave += (_, _) => { closeBtn.BackColor = Color.Transparent; closeBtn.ForeColor = Theme.TextDim; };
        closeBtn.Click += (_, _) => Close();

        var maxBtn = new Label { Text = MatIcons.Maximize, Font = Theme.IconNormal, Size = new Size(42, 36), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextDim, Cursor = Cursors.Hand };
        maxBtn.MouseEnter += (_, _) => { maxBtn.BackColor = Theme.SurfaceHover; maxBtn.ForeColor = Color.White; };
        maxBtn.MouseLeave += (_, _) => { maxBtn.BackColor = Color.Transparent; maxBtn.ForeColor = Theme.TextDim; };
        maxBtn.Click += (_, _) => WindowState = (WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);

        var minBtn = new Label { Text = MatIcons.Minimize, Font = Theme.IconNormal, Size = new Size(42, 36), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextDim, Cursor = Cursors.Hand };
        minBtn.MouseEnter += (_, _) => { minBtn.BackColor = Theme.SurfaceHover; minBtn.ForeColor = Color.White; };
        minBtn.MouseLeave += (_, _) => { minBtn.BackColor = Color.Transparent; minBtn.ForeColor = Theme.TextDim; };
        minBtn.Click += (_, _) => WindowState = FormWindowState.Minimized;

        _langBtn = new FlatButton
        {
            Text = L.T("VI", "EN"),
            Size = new Size(66, 36),
            Font = Theme.ButtonLarge
        };
        _langBtn.Click += (_, _) => BeginInvoke(SwitchLanguage);
        _tip.SetToolTip(_langBtn, L.T("Đổi ngôn ngữ (Tiếng Việt / English)", "Switch language"));

        _topBar.Controls.AddRange(new Control[] { closeBtn, maxBtn, minBtn, _langBtn });

        _topBar.Layout += (_, _) =>
        {
            int rx = _topBar.Width - 6;
            closeBtn.Location = new Point(rx - closeBtn.Width, 6); rx -= closeBtn.Width;
            maxBtn.Location = new Point(rx - maxBtn.Width, 6); rx -= maxBtn.Width;
            minBtn.Location = new Point(rx - minBtn.Width, 6); rx -= minBtn.Width + 14;

            _langBtn.Location = new Point(rx - _langBtn.Width, 6);
        };
    }

    // ------------------------------------------------------------------ Hero Header
    void BuildHeroHeader()
    {
        _heroHeader = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            Height = 160,
            BackColor = Color.FromArgb(12, 18, 28),
            Padding = new Padding(24, 14, 24, 14)
        };

        _heroHeader.Paint += (_, pe) =>
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Vẽ nền hero banner đại dương nếu có
            if (_heroImg != null)
            {
                g.DrawImage(_heroImg, new Rectangle(0, 0, _heroHeader.Width, _heroHeader.Height));
            }

            // Lớp phủ chuyển sắc tối sang trọng
            using (var grad = new LinearGradientBrush(new Rectangle(0, 0, _heroHeader.Width, _heroHeader.Height),
                Color.FromArgb(160, 8, 14, 24), Color.FromArgb(245, Theme.Background), 90f))
            {
                g.FillRectangle(grad, 0, 0, _heroHeader.Width, _heroHeader.Height);
            }

            // Vignette cạnh phải
            using (var sideGrad = new LinearGradientBrush(new Rectangle(_heroHeader.Width - 320, 0, 320, _heroHeader.Height),
                Color.Transparent, Color.FromArgb(220, Theme.Background), 0f))
            {
                g.FillRectangle(sideGrad, _heroHeader.Width - 320, 0, 320, _heroHeader.Height);
            }

            // 2. Poster bìa game Dave the Diver
            var coverRect = new RectangleF(24, 14, 88, 128);
            if (_coverImg != null)
            {
                using var cp = Theme.RoundRect(coverRect, 8f);
                g.SetClip(cp);
                g.DrawImage(_coverImg, coverRect);
                g.ResetClip();

                using var cpen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f);
                g.DrawPath(cpen, cp);
            }
            else
            {
                using var cp = Theme.RoundRect(coverRect, 8f);
                using var cb = new SolidBrush(Theme.Surface);
                g.FillPath(cb, cp);
                TextRenderer.DrawText(g, MatIcons.Swim, Theme.IconLarge, Rectangle.Round(coverRect), Theme.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // 3. Tiêu đề game DAVE THE DIVER bằng Google Inter
            int titleX = 130;
            TextRenderer.DrawText(g, "DAVE THE DIVER", Theme.HeroTitle, new Point(titleX, 16), Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Đường kẻ đáy phân cách
            using var btmPen = new Pen(Theme.BorderSubtle, 1f);
            g.DrawLine(btmPen, 0, _heroHeader.Height - 1, _heroHeader.Width, _heroHeader.Height - 1);
        };

        // Hàng nút điều khiển & Nút Play to
        BuildHeroActions();

        // Khối thông tin bên phải (PLATFORM Steam, StatusPill)
        BuildHeroRightMeta();

        _mainContainer.Controls.Add(_heroHeader);
    }

    void BuildHeroActions()
    {
        int y = 92;
        int x = 130;

        // NÚT PLAY TO TRẮNG / NGỌC LAM PHONG CÁCH HIỆN ĐẠI
        _playBtn = new FlatButton
        {
            PlayButton = true,
            IsPlaying = _connected,
            Icon = _connected ? MatIcons.Check : MatIcons.Play,
            Text = _connected ? L.T("Đang chơi", "Playing") : L.T("Bật game", "Play"),
            Size = new Size(168, 48),
            Location = new Point(x, y),
            Font = Theme.ButtonXLarge
        };
        _playBtn.Click += (_, _) => OnPlay();
        _heroHeader.Controls.Add(_playBtn);
        x += 180;

        // Nút Tắt hết ⚡ to rõ
        _allOffBtn = new FlatButton
        {
            Icon = MatIcons.Power,
            Text = L.T("Tắt hết", "All off"),
            Size = new Size(142, 48),
            Location = new Point(x, y),
            Font = Theme.ButtonLarge
        };
        _allOffBtn.Click += (_, _) => TurnAllOff();
        _tip.SetToolTip(_allOffBtn, L.T("Tắt tất cả mod ngay lập tức", "Turn off all mods"));
        _heroHeader.Controls.Add(_allOffBtn);
        x += 154;

        // Cài đặt ⚙ (Bật tắt Settings panel) to rõ
        _gearBtn = new FlatButton
        {
            Icon = MatIcons.Settings,
            Text = L.T("Cài đặt", "Settings"),
            Size = new Size(142, 48),
            Location = new Point(x, y),
            Font = Theme.ButtonLarge
        };
        _gearBtn.Click += (_, _) => ShowSettings(!_settingsPanel.Visible);
        _tip.SetToolTip(_gearBtn, L.T("Cài đặt thư mục game & cập nhật mod", "Settings & game folder"));
        _heroHeader.Controls.Add(_gearBtn);
    }

    void BuildHeroRightMeta()
    {
        var metaPanel = new Panel
        {
            Size = new Size(240, 100),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(_heroHeader.Width - 256, 16),
            BackColor = Color.Transparent
        };

        metaPanel.Paint += (_, pe) =>
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int my = 4;
            TextRenderer.DrawText(g, "PLATFORM", Theme.Badge, new Point(0, my), Theme.TextMuted);
            my += 16;
            TextRenderer.DrawText(g, "Steam · DaveTheDiver.exe", Theme.SmallBold, new Point(0, my), Color.White);
        };

        _pill = new StatusPill { Location = new Point(0, 52) };
        metaPanel.Controls.Add(_pill);

        _heroHeader.Controls.Add(metaPanel);
        _heroHeader.Layout += (_, _) => metaPanel.Location = new Point(_heroHeader.Width - 256, 16);
    }

    // ------------------------------------------------------------------ Settings Panel
    void BuildSettingsPanel()
    {
        _settingsPanel = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            Height = 184,
            BackColor = Color.FromArgb(16, 22, 34),
            Visible = false,
            Padding = new Padding(24, 12, 24, 12)
        };

        _settingsPanel.Paint += (_, pe) =>
        {
            using var pen = new Pen(Theme.Border, 1.2f);
            pe.Graphics.DrawLine(pen, 0, _settingsPanel.Height - 1, _settingsPanel.Width, _settingsPanel.Height - 1);
        };

        var pathIcon = new Label
        {
            Text = MatIcons.Folder,
            Font = Theme.IconSmall,
            ForeColor = Theme.Accent,
            Size = new Size(20, 20),
            Location = new Point(24, 10),
            BackColor = Color.Transparent
        };

        var pathLabel = new Label
        {
            Text = L.T("Thư mục Dave the Diver (chứa DaveTheDiver.exe)", "Dave the Diver folder (contains DaveTheDiver.exe)"),
            ForeColor = Theme.Text,
            Font = Theme.SmallBold,
            AutoSize = true,
            Location = new Point(48, 10),
            BackColor = Color.Transparent
        };

        var frame = new Panel
        {
            BackColor = Theme.Chip,
            Location = new Point(24, 30),
            Height = 36,
            Padding = new Padding(8, 8, 8, 4)
        };
        frame.Paint += (_, pe) =>
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fr = new RectangleF(0.5f, 0.5f, frame.Width - 1, frame.Height - 1);
            using var path = Theme.RoundRect(fr, 6f);
            using var pen = new Pen(Theme.Border, 1f);
            pe.Graphics.DrawPath(pen, path);
        };

        _pathBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Chip,
            ForeColor = Theme.Text,
            Font = Theme.Body,
            Dock = DockStyle.Fill,
            Text = _settings.GamePath ?? ""
        };
        _pathBox.Leave += (_, _) => { _settings.GamePath = _pathBox.Text.Trim(); SaveSoon(); RefreshStatus(); };
        frame.Controls.Add(_pathBox);

        var browse = new FlatButton { Icon = MatIcons.Folder, Text = L.T("Chọn…", "Browse…"), Size = new Size(118, 38), Font = Theme.Button };
        browse.Click += (_, _) => BrowseGameDir();

        var detect = new FlatButton { Icon = MatIcons.Search, Text = L.T("Tìm Steam", "Find Steam"), Size = new Size(152, 38), Font = Theme.Button };
        detect.Click += (_, _) =>
        {
            var found = ModInstaller.DetectGameDir();
            if (found == null) { UpdateToast(L.T("Không tìm thấy game trong Steam.", "Game not found in Steam.")); return; }
            _pathBox.Text = _settings.GamePath = found;
            SaveSoon();
            RefreshStatus();
            UpdateToast(L.T("Đã tìm thấy game từ Steam!", "Found game from Steam!"));
        };

        _pathStatus = new Label { Font = Theme.SmallBold, AutoSize = true, Location = new Point(24, 70), BackColor = Color.Transparent };
        _installInfo = new Label { ForeColor = Theme.TextDim, Font = Theme.Small, AutoSize = true, Location = new Point(24, 96), BackColor = Color.Transparent };

        var install = new FlatButton { Icon = MatIcons.Science, Text = L.T("Cập nhật mod", "Update mod"), Primary = true, Size = new Size(182, 42), Font = Theme.ButtonLarge };
        install.Click += (_, _) => RunInstall();

        var uninstall = new FlatButton { Icon = MatIcons.Close, Text = L.T("Dọn bản cũ", "Clean old install"), Danger = true, Size = new Size(160, 42), Font = Theme.ButtonLarge };
        uninstall.Click += (_, _) => RunUninstall();

        var openModDir = new FlatButton { Icon = MatIcons.Folder, Text = L.T("Mở Mod", "Mod folder"), Size = new Size(142, 42), Font = Theme.ButtonLarge };
        openModDir.Click += (_, _) => OpenFolder(ModInstaller.RuntimeDir);

        var openGameDir = new FlatButton { Icon = MatIcons.Folder, Text = L.T("Mở Game", "Game folder"), Size = new Size(148, 42), Font = Theme.ButtonLarge };
        openGameDir.Click += (_, _) => OpenFolder(GameDir);

        _settingsPanel.Controls.AddRange(new Control[]
        {
            pathIcon, pathLabel, frame, browse, detect, _pathStatus, _installInfo,
            install, uninstall, openModDir, openGameDir
        });

        _settingsPanel.Layout += (_, _) =>
        {
            int right = _settingsPanel.Width - 24;
            foreach (var b in new[] { browse, detect, install, uninstall, openModDir, openGameDir })
                b.Width = Math.Max(b.Width, b.ContentWidth());
            detect.Location = new Point(right - detect.Width, 28);
            browse.Location = new Point(detect.Left - 8 - browse.Width, 28);
            frame.Width = browse.Left - 8 - frame.Left;

            install.Location = new Point(24, 126);
            uninstall.Location = new Point(install.Right + 12, 126);
            openModDir.Location = new Point(uninstall.Right + 12, 126);
            openGameDir.Location = new Point(openModDir.Right + 12, 126);
        };

        _mainContainer.Controls.Add(_settingsPanel);
    }

    // ------------------------------------------------------------------ Tabs Bar
    void BuildFilterTabsBar()
    {
        _tabsBar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Theme.Background,
            Padding = new Padding(20, 6, 20, 4)
        };

        _filterBar = new CategoryFilterBar();
        var items = new List<CategoryFilterBar.Item>
        {
            new(null, L.T("TẤT CẢ", "ALL"), MatIcons.Grid, Cheats.All.Length),
            new("Player", L.T("Nhân vật", "Player"), MatIcons.Person, Cheats.All.Count(c => c.Category == "Player")),
            new("Inventory", L.T("Kho đồ", "Inventory"), MatIcons.Bag, Cheats.All.Count(c => c.Category == "Inventory")),
            new("Stats", L.T("Sushi & Chỉ số", "Sushi & Stats"), MatIcons.Stats, Cheats.All.Count(c => c.Category == "Stats")),
            new("Weapons", L.T("Vũ khí", "Weapons"), MatIcons.Shield, Cheats.All.Count(c => c.Category == "Weapons")),
            new("Enemies", L.T("Cá & Biển", "Marine Life"), MatIcons.Heart, Cheats.All.Count(c => c.Category == "Enemies")),
            new("Game", L.T("Game", "Game"), MatIcons.Gamepad, Cheats.All.Count(c => c.Category == "Game")),
            new("Physics", L.T("Tốc độ", "Speed"), MatIcons.Walk, Cheats.All.Count(c => c.Category == "Physics")),
            new("Graphics", L.T("Hiển thị", "Display"), MatIcons.Display, Cheats.All.Count(c => c.Category == "Graphics")),
        };

        _filterBar.SetItems(items);
        _filterBar.SelectedCategoryChanged += catId =>
        {
            _currentFilter = catId;
            ApplyFilter();
        };

        _tabsBar.Controls.Add(_filterBar);
        _tabsBar.Layout += (_, _) =>
        {
            _filterBar.Location = new Point(20, 6);
            _filterBar.FitWidth(_tabsBar.Width - 40);
        };

        _mainContainer.Controls.Add(_tabsBar);
    }

    // ------------------------------------------------------------------ Cheats List Area
    void BuildCheatsListArea()
    {
        _listViewport = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
        };

        _list = new DoubleBufferedFlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background,
            Padding = new Padding(24, 6, 24, 60),
        };
        _list.HorizontalScroll.Maximum = 0;
        _list.HorizontalScroll.Visible = false;

        _listViewport.Controls.Add(_list);
        _mainContainer.Controls.Add(_listViewport);
        _listViewport.BringToFront();

        void AdjustListBounds()
        {
            if (_listViewport == null || _list == null) return;
            // Đẩy thanh cuộn dọc lệch ra khỏi khung nhìn của _listViewport để ẩn hoàn toàn thanh kéo
            int sbWidth = SystemInformation.VerticalScrollBarWidth + 18;
            _list.SetBounds(0, 0, _listViewport.Width + sbWidth, _listViewport.Height);
            ResizeRows();
        }

        _listViewport.Resize += (_, _) => AdjustListBounds();

        PopulateCheatsList();
        AdjustListBounds();
    }

    void PopulateCheatsList()
    {
        _list.SuspendLayout();
        _list.Controls.Clear();
        _rows.Clear();
        _categoryHeaders.Clear();

        // Từng nhóm theo Category
        foreach (var category in Cheats.Categories)
        {
            var catInfo = Cheats.GetCategory(category);
            int count = Cheats.All.Count(c => c.Category == category);

            var header = new CategoryHeader(catInfo, count);
            _categoryHeaders[category] = header;
            _list.Controls.Add(header);

            foreach (var def in Cheats.All.Where(c => c.Category == category))
            {
                var row = new CheatRow(def, _tip);
                _rows[def.Key] = row;
                _list.Controls.Add(row);
                WireRow(row);
            }

            header.CollapsedChanged += (_, _) =>
            {
                foreach (var def in Cheats.All.Where(c => c.Category == category))
                {
                    if (_rows.TryGetValue(def.Key, out var r)) r.Visible = !header.Collapsed;
                }
            };
        }

        _list.ResumeLayout();
        ResizeRows();
    }

    void ResizeRows()
    {
        if (_list == null || _listViewport == null) return;
        int w = _listViewport.ClientSize.Width - 48;
        w = Math.Max(w, 520);

        _list.SuspendLayout();
        foreach (var row in _rows.Values) row.Width = w;
        foreach (var header in _categoryHeaders.Values) header.Width = w;
        _list.ResumeLayout();
    }

    // ------------------------------------------------------------------ Bottom Toast Banner
    void BuildBottomToast()
    {
        _toastPanel = new DoubleBufferedPanel
        {
            Size = new Size(520, 40),
            BackColor = Theme.ToastBg,
            Anchor = AnchorStyles.Bottom,
            Visible = false
        };

        _toastPanel.Paint += (_, pe) =>
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, _toastPanel.Width - 1, _toastPanel.Height - 1);
            using var p = Theme.RoundRect(r, r.Height / 2f);
            using var b = new SolidBrush(Theme.ToastBg);
            pe.Graphics.FillPath(b, p);
            using var pen = new Pen(Theme.ToastBorder, 1.2f);
            pe.Graphics.DrawPath(pen, p);

            // Biểu tượng Material Icon CheckCircle
            var cr = new RectangleF(10, 8, 24, 24);
            using var cb = new SolidBrush(Color.FromArgb(32, Theme.Accent));
            pe.Graphics.FillEllipse(cb, cr);
            TextRenderer.DrawText(pe.Graphics, MatIcons.CheckCircle, Theme.IconSmall, Rectangle.Round(cr), Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };

        _toastMsg = new Label
        {
            Text = "",
            ForeColor = Theme.Text,
            Font = Theme.Body,
            Location = new Point(40, 10),
            Size = new Size(440, 20),
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = Color.Transparent
        };

        var closeToast = new Label
        {
            Text = MatIcons.Close,
            Font = Theme.IconSmall,
            ForeColor = Theme.TextDim,
            Size = new Size(24, 24),
            Location = new Point(488, 8),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        closeToast.Click += (_, _) => _toastPanel.Visible = false;

        _toastPanel.Controls.AddRange(new Control[] { _toastMsg, closeToast });

        _mainContainer.Controls.Add(_toastPanel);
        _mainContainer.Controls.SetChildIndex(_toastPanel, 0);

        _mainContainer.Layout += (_, _) =>
        {
            _toastPanel.Location = new Point((_mainContainer.Width - _toastPanel.Width) / 2, _mainContainer.Height - _toastPanel.Height - 16);
        };
    }

    void UpdateToast(string msg)
    {
        if (RenderOnly) return;
        if (_toastMsg != null && _toastPanel != null)
        {
            _toastMsg.Text = msg.StartsWith("✓ ") ? msg[2..] : msg; // icon ✓ đã vẽ sẵn bên trái
            _toastPanel.Visible = true;
            _toastPanel.BringToFront();
            _toastTimer.Stop();
            _toastTimer.Start();
        }
    }

    // ==================================================================
    // TƯƠNG TÁC CHEAT & LỌC DỮ LIỆU
    // ==================================================================

    void WireRow(CheatRow row)
    {
        var def = row.Def;
        switch (def.Kind)
        {
            case CheatKind.Toggle:
                row.Toggle.CheckedChanged += (_, _) =>
                {
                    if (_restoring) return;
                    SetDesired(def.Key, row.Toggle.Checked ? "1" : "0");
                    UpdateToast($"{def.Name}: {(row.Toggle.Checked ? L.T("BẬT", "ON") : L.T("TẮT", "OFF"))}");
                };
                break;

            case CheatKind.Slider:
                row.Slider.ValueChanged += (_, _) =>
                {
                    if (!_restoring) SetDesired(def.Key, row.Slider.Value.ToString(CultureInfo.InvariantCulture));
                };
                break;

            case CheatKind.Choice:
                row.Choice.SelectedIndexChanged += (_, _) =>
                {
                    if (_restoring) return;
                    SetDesired(def.Key, def.Options[row.Choice.SelectedIndex].ToString(CultureInfo.InvariantCulture));
                    UpdateToast($"{def.Name}: {def.OptionLabels[row.Choice.SelectedIndex]}");
                };
                break;

            case CheatKind.Number:
                row.ApplyButton.Click += (_, _) => ApplyNumber(row);
                row.NumberBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyNumber(row); } };
                row.NumberBox.TextChanged += (_, _) =>
                {
                    if (row.NumberValue is long v) _settings.Numbers[def.Key] = v; else _settings.Numbers.Remove(def.Key);
                    SaveSoon();
                };
                break;

            case CheatKind.Button:
                row.ActionButton.Click += (_, _) =>
                {
                    if (!_connected)
                    {
                        UpdateToast(L.T("Chưa kết nối với game. Bấm Play và vào save game trước.", "Not connected to game. Press Play and load save."));
                        return;
                    }
                    _pipe.Send($"ACTION {def.Key}");
                };
                break;
        }
    }

    void ApplyNumber(CheatRow row)
    {
        if (row.NumberValue is not long v || v > 999_999_999)
        {
            UpdateToast(row.Def.Name + ": " + L.T("nhập số từ 0 đến 999.999.999.", "enter number 0 to 999,999,999."));
            return;
        }
        if (!_connected)
        {
            UpdateToast(L.T("Chưa kết nối với game. Bấm Play và vào save game trước.", "Not connected to game. Press Play and load save."));
            return;
        }
        _pipe.Send($"ACTION {row.Def.Key} {v.ToString(CultureInfo.InvariantCulture)}");
    }

    void SetDesired(string key, string value)
    {
        _desired[key] = value;
        _pipe.Send($"SET {key} {value}");
    }

    void ApplyFilter()
    {
        _list.SuspendLayout();

        foreach (var category in Cheats.Categories)
        {
            bool visible = _currentFilter == null || _currentFilter == category;
            _categoryHeaders.TryGetValue(category, out var header);
            bool collapsed = header?.Collapsed ?? false; // nhóm đang thu gọn thì vẫn ẩn các thẻ
            foreach (var def in Cheats.All.Where(c => c.Category == category))
            {
                if (_rows.TryGetValue(def.Key, out var row)) row.Visible = visible && !collapsed;
            }
            if (header != null) header.Visible = visible;
        }

        _list.ResumeLayout();
        _list.AutoScrollPosition = Point.Empty;
        ResizeRows();
    }

    void RestoreStates()
    {
        foreach (var row in _rows.Values)
        {
            var def = row.Def;
            if (def.Kind == CheatKind.Number && _settings.Numbers.TryGetValue(def.Key, out long v))
                row.NumberValue = v;
        }
    }

    void TurnAllOff()
    {
        _desired.Clear();
        _pipe.Send("RESET");
        _restoring = true;
        foreach (var row in _rows.Values)
        {
            if (row.Toggle != null) row.Toggle.Checked = false;
            if (row.Slider != null) row.Slider.Value = 1.0f;
            if (row.Choice != null) row.Choice.SelectedIndex = 0;
        }
        _restoring = false;
        UpdateToast(L.T("⚡ Đã tắt tất cả các mod.", "⚡ All mods turned off."));
    }

    void SwitchLanguage()
    {
        L.English = !L.English;
        _settings.Language = L.Code;
        SaveSoon();

        SuspendLayout();
        _tip.RemoveAll();
        foreach (var c in Controls.Cast<Control>().ToList())
        {
            Controls.Remove(c);
            c.Dispose();
        }
        _rows.Clear();
        _categoryHeaders.Clear();

        BuildUi();
        RestoreStates();          // số đã nhập ở các ô Set
        ApplyDesiredToControls(); // trạng thái mod đang bật
        if (_lastState != null) OnState(_lastState);
        ResumeLayout(true);

        RefreshStatus();
        UpdateToast(L.T("Đã chuyển sang tiếng Việt.", "Switched to English."));
    }

    void ApplyDesiredToControls()
    {
        _restoring = true;
        foreach (var (key, val) in _desired)
        {
            if (_rows.TryGetValue(key, out var row)) SetRowValue(row, val);
        }
        _restoring = false;
    }

    void SetRowValue(CheatRow row, string val)
    {
        if (row.Toggle != null) row.Toggle.Checked = val == "1";
        else if (row.Slider != null && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) row.Slider.Value = f;
        else if (row.Choice != null && int.TryParse(val, out int opt))
        {
            int idx = Array.IndexOf(row.Def.Options, opt);
            if (idx >= 0) row.Choice.SelectedIndex = idx;
        }
    }

    // ==================================================================
    // CÁC HÀM CHO CHẾ ĐỘ --RENDER
    // ==================================================================

    public void SelectCategoryForRender(string id)
    {
        _currentFilter = id;
        ApplyFilter();
    }

    public void ShowSettingsForRender() => ShowSettings(true);

    public void SimulateActiveCheatsForRender()
    {
        if (_rows.TryGetValue("UnlimitedOxygen", out var oxy) && oxy.Toggle != null) oxy.Toggle.Checked = true;
        if (_rows.TryGetValue("UnlimitedStamina", out var sta) && sta.Toggle != null) sta.Toggle.Checked = true;
        if (_rows.TryGetValue("SwimSpeed", out var spd) && spd.Slider != null) spd.Slider.Value = 2.5f;
        if (_rows.TryGetValue("SetGold", out var gold)) { gold.NumberValue = 99999999; gold.SetCurrent(150000); }
        if (_rows.TryGetValue("FpsLimit", out var fps) && fps.Choice != null) fps.Choice.SelectedIndex = 2;
        _pill.Set(L.T("Mods đang hoạt động", "Mods active"), Theme.Accent);
        if (_playBtn != null)
        {
            _playBtn.IsPlaying = true;
            _playBtn.Icon = MatIcons.Check;
            _playBtn.Text = L.T("Đang chơi", "Playing");
            _playBtn.Invalidate();
        }
        UpdateToast(L.T("✓ Đã kết nối với game. Mods đang hoạt động!", "✓ Connected to game. Mods active!"));
    }

    public void SimulateHoverForRender(string key)
    {
        if (_rows.TryGetValue(key, out var row))
        {
            row.SimulatedHover = true;
            row.Invalidate();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        BeginInvoke(() => _list.AutoScrollPosition = Point.Empty); // luôn mở ở đầu danh sách
        if (!RenderOnly) _pipe.Start();
    }

    // ==================================================================
    // QUẢN LÝ TIẾN TRÌNH & NẠP MOD
    // ==================================================================

    string GameDir => _settings.GamePath;

    void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path)) Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            else UpdateToast(L.T("Thư mục chưa tồn tại: ", "Folder not found: ") + path);
        }
        catch (Exception ex) { UpdateToast("Lỗi mở thư mục: " + ex.Message); }
    }

    void BrowseGameDir()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = L.T("Chọn thư mục cài Dave the Diver (chứa DaveTheDiver.exe)", "Select Dave the Diver install folder"),
            UseDescriptionForTitle = true,
        };
        if (ModInstaller.IsGameDir(GameDir)) dlg.InitialDirectory = GameDir;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!ModInstaller.IsGameDir(dlg.SelectedPath))
        {
            UpdateToast(L.T("Thư mục này không có DaveTheDiver.exe.", "DaveTheDiver.exe not found here."));
            return;
        }
        _pathBox.Text = _settings.GamePath = dlg.SelectedPath;
        SaveSoon();
        RefreshStatus();
    }

    bool EnsureGameDir()
    {
        if (ModInstaller.IsGameDir(GameDir)) return true;
        UpdateToast(L.T("Chưa chọn đúng thư mục game. Bấm ⚙ Cài đặt.", "Game folder not set. Click ⚙ Settings."));
        ShowSettings(true);
        return false;
    }

    void RunInstall()
    {
        if (!EnsureGameDir()) return;
        try
        {
            string what = ModInstaller.EnsureRuntime();
            UpdateToast(L.T("Đã chuẩn bị runtime: ", "Runtime prepared: ") + what);
        }
        catch (Exception ex) { UpdateToast(L.T("Cập nhật thất bại: ", "Update failed: ") + ex.Message); }
        RefreshStatus();
    }

    void RunUninstall()
    {
        if (!EnsureGameDir()) return;
        try
        {
            ModInstaller.CleanLegacyInstall(GameDir);
            UpdateToast(L.T("Đã dọn sạch bản cài cũ khỏi thư mục game.", "Cleaned old install from game folder."));
        }
        catch (Exception ex) { UpdateToast("Dọn thất bại: " + ex.Message); }
        RefreshStatus();
    }

    void OnPlay()
    {
        if (!EnsureGameDir()) return;
        EnsureModReady(announce: false);

        if (ModInstaller.IsGameRunning())
        {
            if (_connected)
            {
                UpdateToast(L.T("Game đang chạy và mod đang hoạt động.", "Game is running and mods active."));
                return;
            }

            using var proc = Injector.GetGameProcess();
            if (proc != null)
            {
                var (injected, msg) = Injector.InjectDll(proc.Id, Injector.LoaderDllPath);
                _launchStarted = DateTime.Now;
                _lastAutoInjectTime = DateTime.Now;
                _lastInjectedPid = proc.Id;
                UpdateToast(msg);
            }
            RefreshStatus();
            return;
        }

        try
        {
            EnsureModReady(announce: true);
            ModInstaller.LaunchGame();
            _launchStarted = DateTime.Now;
            UpdateToast(L.T("Đang mở game qua Steam… Tool sẽ tự động nạp mod.", "Launching via Steam… Auto-injecting mod."));
        }
        catch (Exception ex) { UpdateToast("Không mở được game: " + ex.Message); }
        RefreshStatus();
    }

    void EnsureModReady(bool announce)
    {
        string what = ModInstaller.EnsureRuntime();
        if (announce && what.Length > 0) UpdateToast(L.T("Đã chuẩn bị mod: ", "Mod prepared: ") + what);
    }

    void RefreshStatus()
    {
        bool dirOk = ModInstaller.IsGameDir(GameDir);
        var gameStart = ModInstaller.GameStartTime();
        bool running = gameStart != null;
        bool ready = ModInstaller.IsBepInExReady && ModInstaller.IsPluginInstalled;

        if (!RenderOnly && running && !_connected && ready)
        {
            using var proc = Injector.GetGameProcess();
            if (proc != null)
            {
                bool alreadyInjected = Injector.IsModuleLoaded(proc.Id, "DTDLoader.dll");
                if (!alreadyInjected && (_lastAutoInjectTime == null || DateTime.Now - _lastAutoInjectTime.Value > TimeSpan.FromSeconds(3)))
                {
                    _lastAutoInjectTime = DateTime.Now;
                    _lastInjectedPid = proc.Id;
                    var (injected, msg) = Injector.InjectDll(proc.Id, Injector.LoaderDllPath);
                    if (injected)
                    {
                        UpdateToast(L.T("✓ Đã tự động chèn mod vào game đang chạy!", "✓ Auto-injected mod into running game!"));
                        _launchStarted = DateTime.Now;
                    }
                }
            }
        }

        if (!dirOk) _pill.Set(L.T("Chưa tìm thấy game", "Game not found"), Theme.Danger);
        else if (_connected) _pill.Set(L.T("Mods đang hoạt động", "Mods active"), Theme.Accent);
        else if (running) _pill.Set(L.T("Đang nạp mod…", "Loading mod…"), Theme.Warning);
        else if (_launchStarted != null) _pill.Set(L.T("Đang mở game…", "Launching…"), Theme.Warning);
        else if (!ready) _pill.Set(L.T("Mod chưa sẵn sàng", "Mod not ready"), Theme.TextDim);
        else _pill.Set(L.T("Sẵn sàng chơi", "Ready to play"), Theme.TextDim);

        if (_playBtn != null)
        {
            _playBtn.IsPlaying = _connected;
            _playBtn.Icon = _connected ? MatIcons.Check : MatIcons.Play;
            _playBtn.Text = _connected ? L.T("Đang chơi", "Playing") : L.T("Bật game", "Play");
            _playBtn.Invalidate();
        }

        if (dirOk && _pathStatus != null)
        {
            _pathStatus.Text = L.T("✓ Thư mục hợp lệ (có DaveTheDiver.exe)", "✓ Valid folder (DaveTheDiver.exe found)");
            _pathStatus.ForeColor = Theme.Success;
            string bepinex = ModInstaller.IsBepInExReady ? L.T("✓ Đã có", "✓ Ready") : L.T("Chưa có", "Missing");
            string plugin = ModInstaller.IsPluginInstalled ? L.T("✓ Mới nhất", "✓ Ready") : L.T("Chưa có", "Missing");
            _installInfo.Text = $"BepInEx: {bepinex}   ·   Plugin: {plugin}";
        }
    }

    void OnState(IReadOnlyDictionary<string, string> s)
    {
        _lastState = s;
        foreach (var row in _rows.Values)
        {
            if (row.Def.SnapshotKey == null) continue;
            int value = s.TryGetValue("saveLoaded", out var loaded) && loaded == "1" && s.TryGetValue(row.Def.SnapshotKey, out var v) && int.TryParse(v, out int n) ? n : -1;
            row.SetCurrent(value);
        }
    }

    void OnReply(string cmd, string reply)
    {
        if (!cmd.StartsWith("ACTION ")) return;
        var parts = cmd.Split(' ');
        string name = _rows.TryGetValue(parts[1], out var row) ? row.Def.Name : parts[1];
        if (!reply.StartsWith("OK"))
        {
            UpdateToast($"✕ {name}: {DescribeError(reply)}");
            return;
        }

        string detail = reply.Length > 2 ? reply[2..].Trim() : "";
        if (row?.Def.Kind == CheatKind.Number)
        {
            long shown = long.TryParse(detail, out long actual) ? actual : long.Parse(parts[2]);
            UpdateToast($"✓ {name} = {shown.ToString("N0", L.Numbers)}");
        }
        else
        {
            UpdateToast(detail.Length > 0 ? $"✓ {name} → {detail}" : $"✓ {name}");
        }
    }

    static string DescribeError(string reply) => reply switch
    {
        "ERR nosave" => L.T("chưa vào save game.", "no save loaded yet."),
        "ERR nojunglesave" => L.T("save chưa có dữ liệu Jungle DLC.", "this save has no Jungle DLC data yet."),
        "ERR nojungle" => L.T("chỉ dùng được khi đang ở Jungle.", "only works in the Jungle."),
        "ERR number" => L.T("số không hợp lệ.", "invalid number."),
        _ => L.T("thất bại: ", "failed: ") + reply,
    };

    void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void BeginInvokeSafe(Action a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); } catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _settings.GamePath = _pathBox?.Text?.Trim() ?? _settings.GamePath;
        _settings.Save();
        _timer.Stop();
        _pipe.Dispose();
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------------ Status Pill
    sealed class StatusPill : Control
    {
        Color _color = Theme.TextDim;

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.SmallBold;
            Height = 24;
        }

        public void Set(string text, Color color)
        {
            if (Text == text && _color == color) return;
            Text = text;
            _color = color;
            Width = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPrefix).Width + 34;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            using (var path = Theme.RoundRect(r, r.Height / 2f))
            {
                using var b = new SolidBrush(Color.FromArgb(24, _color));
                g.FillPath(b, path);
                using var pen = new Pen(Color.FromArgb(90, _color), 1f);
                g.DrawPath(pen, path);
            }

            float cy = Height / 2f;
            float dotX = 10f;
            using (var dot = new SolidBrush(_color))
                g.FillEllipse(dot, dotX, cy - 4, 8, 8);

            TextRenderer.DrawText(g, Text, Font, new Rectangle(24, 0, Width - 26, Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}
