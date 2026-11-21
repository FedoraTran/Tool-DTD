namespace DTDTrainer.Controls;

/// <summary>FlowLayoutPanel có bật DoubleBuffered đầy đủ để cuộn mượt không bị nháy hình.</summary>
sealed class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int ShowScrollBar(IntPtr hWnd, int wBar, int bShow);

    public DoubleBufferedFlowLayoutPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0083 || m.Msg == 0x0085) // WM_NCCALCSIZE, WM_NCPAINT
        {
            ShowScrollBar(Handle, 3 /* SB_BOTH */, 0);
        }
    }

    /// <summary>
    /// Không tự cuộn tới control vừa nhận focus (vd. ô nhập Set Gold lúc mở tool,
    /// hoặc công tắc vừa bấm) — giữ nguyên vị trí cuộn của người dùng.
    /// </summary>
    protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;
}

/// <summary>Panel có bật DoubleBuffered.</summary>
sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
    }
}
