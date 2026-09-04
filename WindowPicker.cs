using System.Diagnostics;
using System.Drawing;

namespace BossKey;

/// <summary>
/// 全屏拾取层：光标扫过窗口时实时显示窗口信息和外框，
/// 左键点击当前高亮的窗口完成拾取，右键或 Esc 取消。
/// </summary>
internal sealed class WindowPicker : Form
{
    private readonly System.Windows.Forms.Timer _trackTimer = new() { Interval = 50 };
    private readonly HintWindow _hint;
    private readonly OutlineWindow _outline;

    private IntPtr _hoverWindow;
    private IntPtr _picked;
    private Point _lastTrackPoint = new(int.MinValue, int.MinValue);

    internal IntPtr PickedWindow => _picked;

    internal WindowPicker()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;

        // 几乎看不见的覆盖层，负责接收鼠标并显示十字光标。
        BackColor = Color.Black;
        Opacity = 0.03;
        Cursor = Cursors.Cross;
        KeyPreview = true;

        _hint = new HintWindow();
        _outline = new OutlineWindow();
        _trackTimer.Tick += (_, _) => TrackMouse();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _hint.Show();
        _outline.Show();
        // 拾取覆盖层保持在最上面：这样整个屏幕的鼠标都由它接收，十字光标一直生效。
        BringToFront();
        _trackTimer.Start();
        TrackMouse();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _trackTimer.Stop();
        _outline.Close();
        _hint.Close();
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            CancelPick();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button == MouseButtons.Right)
        {
            CancelPick();
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;

        // 点击瞬间先刷新一次当前高亮，保证选中的就是刚点到的窗口。
        TrackMouse();

        // 直接用当前高亮的窗口，点击结果和看到的轮廓保持一致。
        if (_hoverWindow == IntPtr.Zero)
        {
            _hint.ShowMessage("这里没有可隐藏的窗口，请移到应用窗口上再点击。");
            return;
        }

        _picked = _hoverWindow;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void TrackMouse()
    {
        Point cursor = Cursor.Position;
        if (cursor == _lastTrackPoint)
            return;
        _lastTrackPoint = cursor;

        IntPtr window = FindWindowAtPoint(cursor);

        if (window == _hoverWindow)
        {
            // 位置或尺寸变化时也要实时刷新外框。
            _outline.Track(window);
            return;
        }

        _hoverWindow = window;
        if (window == IntPtr.Zero)
        {
            _outline.Hide();
            _hint.ShowMessage("当前：桌面或空白区域");
            return;
        }

        bool outlineJustShown = _outline.Track(window);
        if (outlineJustShown)
            BringToFront();
        _hint.ShowWindowInfo(window);
    }

    private IntPtr FindWindowAtPoint(Point point)
    {
        // 暂时让覆盖层对鼠标“让路”，让系统报告光标下真实的窗口。
        int oldExStyle = NativeMethods.GetWindowLong(Handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(
            Handle, NativeMethods.GwlExStyle, oldExStyle | NativeMethods.WsExTransparent);

        IntPtr found;
        try
        {
            found = NativeMethods.WindowFromPoint(
                new NativeMethods.PointApi(point.X, point.Y));
        }
        finally
        {
            NativeMethods.SetWindowLong(Handle, NativeMethods.GwlExStyle, oldExStyle);
        }

        if (found == IntPtr.Zero)
            return IntPtr.Zero;

        // 如果取到的是我们自己的提示条/外框/主程序，用后备遍历找下层窗口。
        if (IsOwnWindow(found))
            found = FindTopLevelByRect(point);

        if (found == IntPtr.Zero || IsOwnWindow(found))
            return IntPtr.Zero;

        IntPtr root = NativeMethods.GetAncestor(found, NativeMethods.GaRoot);
        if (root != IntPtr.Zero)
            found = root;

        if (IsOwnWindow(found))
            return IntPtr.Zero;

        if (IsShellClass(NativeMethods.GetWindowClassName(found)))
            return IntPtr.Zero;

        return found;
    }

    private IntPtr FindTopLevelByRect(Point point)
    {
        IntPtr window = NativeMethods.GetWindow(NativeMethods.GetDesktopWindow(), NativeMethods.GwChild);
        while (window != IntPtr.Zero)
        {
            if (IsCandidate(window, point))
                return window;

            window = NativeMethods.GetWindow(window, NativeMethods.GwHwndNext);
        }

        return IntPtr.Zero;
    }

    private bool IsOwnWindow(IntPtr window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out uint pid);
        return pid == 0 || pid == (uint)Environment.ProcessId;
    }

    private bool IsCandidate(IntPtr window, Point point)
    {
        if (window == Handle || window == _hint.Handle || window == _outline.Handle)
            return false;

        NativeMethods.GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
            return false;

        if (!NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
            return false;

        string cls = NativeMethods.GetWindowClassName(window);
        if (IsShellClass(cls))
            return false;

        if (!NativeMethods.GetWindowRect(window, out var rect))
            return false;

        if (rect.Width < 30 || rect.Height < 20)
            return false;

        return point.X >= rect.Left && point.X <= rect.Right &&
               point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    private static bool IsShellClass(string cls) => cls switch
    {
        "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow" => true,
        _ => false
    };

    private void CancelPick()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>
    /// 屏幕左上角的信息条：实时显示当前扫过的窗口信息。
    /// 点击会穿透，不会挡住拾取。
    /// </summary>
    private sealed class HintWindow : Form
    {
        private readonly Label _label;

        internal HintWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(255, 250, 236);

            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 900, 600);
            Size = new Size(560, 132);
            Location = new Point(area.Left + 14, area.Top + 14);

            _label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.FromArgb(55, 45, 20),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 10, 14, 8)
            };
            Controls.Add(_label);

            ShowMessage("正在拾取窗口：光标扫过即可预览，点击目标窗口选中，右键或 Esc 取消。");
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_TRANSPARENT：鼠标点击穿透；WS_EX_NOACTIVATE：不抢焦点。
                cp.ExStyle |= 0x00000020 | 0x08000000;
                return cp;
            }
        }

        internal void ShowMessage(string message)
        {
            _label.Text = "正在拾取窗口：光标扫过即可预览，点击目标窗口选中，右键或 Esc 取消。\r\n\r\n" + message;
        }

        internal void ShowWindowInfo(IntPtr window)
        {
            string title = NativeMethods.GetWindowTitle(window);
            string cls = NativeMethods.GetWindowClassName(window);
            NativeMethods.GetWindowThreadProcessId(window, out uint pid);
            string process = DescribeProcess(pid);

            if (string.IsNullOrWhiteSpace(title))
                title = "（无标题）";

            _label.Text =
                "正在拾取窗口：光标扫过即可预览，点击目标窗口选中，右键或 Esc 取消。\r\n\r\n" +
                $"标题：{title}\r\n" +
                $"类名：{cls}\r\n" +
                $"进程：{process}（PID {pid}）    句柄：0x{window.ToInt64():X}";
        }

        private static string DescribeProcess(uint pid)
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                string name = p.ProcessName;
                return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
            }
            catch
            {
                return "未知进程";
            }
        }
    }

    /// <summary>
    /// 跟随光标的高亮外框，点击穿透、不抢焦点。
    /// </summary>
    private sealed class OutlineWindow : Form
    {
        internal OutlineWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000020 | 0x08000000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = new Rectangle(2, 2, ClientSize.Width - 4, ClientSize.Height - 4);
            using var pen = new Pen(Color.FromArgb(255, 32, 32), 3F);
            e.Graphics.DrawRectangle(pen, rect);
        }

        internal bool Track(IntPtr window)
        {
            if (!NativeMethods.GetWindowRect(window, out var rect))
            {
                Hide();
                return false;
            }

            bool justShown = !Visible;
            if (justShown)
                Show();

            var bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
            if (Bounds != bounds)
            {
                Bounds = bounds;
                Invalidate();
            }

            return justShown;
        }
    }
}
