using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BossKey;

internal sealed class MainForm : Form
{
    private readonly GroupBox _gbTarget = new();
    private readonly GroupBox _gbHotkey = new();
    private readonly GroupBox _gbBehavior = new();

    private readonly Button _btnPick = new();
    private readonly Button _btnClearTarget = new();
    private readonly TextBox _txtTarget = new();

    private readonly Label _lblHotkey = new();
    private readonly Button _btnRecord = new();
    private readonly Button _btnClearHotkey = new();
    private readonly Label _lblRecHint = new();

    private readonly ComboBox _cmbMode = new();
    private readonly CheckBox _chkHideSelf = new();
    private readonly CheckBox _chkHideTray = new();
    private readonly CheckBox _chkPreventRestore = new();
    private readonly CheckBox _chkAutoStart = new();
    private readonly CheckBox _chkAutoFind = new();

    private readonly Button _btnHide = new();
    private readonly Button _btnShow = new();
    private readonly Button _btnQuit = new();
    private readonly Label _lblStatus = new();

    private readonly System.Windows.Forms.Timer _antiRestoreTimer = new();
    private NotifyIcon? _tray;
    private readonly EventWaitHandle _showRequest;
    private Thread? _showListener;
    private bool _stopShowListener;

    private readonly AppSettings _settings;
    private bool _applyingUi;
    private bool _recording;
    private bool _quitting;
    private bool _selfHidden;
    private bool _trayHiddenByBoss;
    private bool _bossActive;
    private bool _balloonShown;

    private IntPtr _target;
    private string _targetTitle = string.Empty;
    private string _targetClass = string.Empty;
    private string _targetProcess = string.Empty;
    private uint _targetPid;
    private NativeMethods.WindowPlacement _savedPlacement;
    private NativeMethods.RectApi _savedRect;

    private Keys _hotkeyCode;
    private bool _hotkeyCtrl;
    private bool _hotkeyShift;
    private bool _hotkeyAlt;
    private bool _hotkeyWin;
    private string _hotkeyDisplay = string.Empty;
    private NativeMethods.EnumWindowsProc? _enumCallback;
    private NativeMethods.LowLevelKeyboardProc? _keyboardHookProc;
    private IntPtr _keyboardHookHandle;
    private bool _baseKeyDown;

    internal MainForm(EventWaitHandle showRequest)
    {
        _showRequest = showRequest;
        Font = new Font("Microsoft YaHei UI", 9F);
        _settings = AppSettings.Load();

        BuildUi();
        BuildTray();
        AttachEvents();

        _antiRestoreTimer.Interval = 250;
        _antiRestoreTimer.Tick += OnAntiRestoreTick;

        ApplySettingsToUi();
        if (_settings.AutoStart)
            ApplyAutoStart(true);
        UpdateBossUi();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _enumCallback = EnumWindowsProc;
        InstallKeyboardHook();
        StartShowListener();

        if (_settings.AutoFindTarget)
            AutoFindSavedTarget();

        UpdateBossUi();
    }

    private void BuildUi()
    {
        Text = "老板键 BossKey";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(560, 478);
        KeyPreview = true;

        // ---------- 目标窗口 ----------
        _gbTarget.Text = " 目标窗口 ";
        _gbTarget.SetBounds(12, 12, 536, 146);

        _btnPick.Text = "拾取窗口…";
        _btnPick.SetBounds(14, 28, 100, 30);

        _btnClearTarget.Text = "清除";
        _btnClearTarget.SetBounds(120, 28, 64, 30);

        var hintTarget = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(196, 30, 330, 28),
            Text = "点击后出现全屏十字光标，再点一下要隐藏的窗口，Esc 取消。",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        _txtTarget.Multiline = true;
        _txtTarget.ReadOnly = true;
        _txtTarget.ScrollBars = ScrollBars.Vertical;
        _txtTarget.BackColor = Color.White;
        _txtTarget.SetBounds(14, 66, 508, 70);
        _txtTarget.Text = "尚未选择目标窗口。";

        _gbTarget.Controls.AddRange(new Control[] { _btnPick, _btnClearTarget, hintTarget, _txtTarget });

        // ---------- 快捷键 ----------
        _gbHotkey.Text = " 快捷键 ";
        _gbHotkey.SetBounds(12, 166, 536, 102);

        var lblHotkeyCaption = new Label
        {
            AutoSize = true,
            Bounds = new Rectangle(14, 32, 84, 22),
            Text = "当前快捷键：",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblHotkey.AutoSize = true;
        _lblHotkey.Bounds = new Rectangle(104, 32, 130, 22);
        _lblHotkey.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
        _lblHotkey.Text = "未设置";
        _lblHotkey.TextAlign = ContentAlignment.MiddleLeft;

        _btnRecord.Text = "录制快捷键…";
        _btnRecord.SetBounds(250, 24, 106, 30);

        _btnClearHotkey.Text = "清除";
        _btnClearHotkey.SetBounds(364, 24, 64, 30);

        _lblRecHint.AutoSize = false;
        _lblRecHint.Bounds = new Rectangle(14, 64, 508, 34);
        _lblRecHint.ForeColor = Color.FromArgb(90, 90, 90);
        _lblRecHint.Text = "录制时直接按下组合键即可（例如 Ctrl+Alt+F10）。只按一个普通键会被拒绝。";

        _gbHotkey.Controls.AddRange(new Control[]
        {
            lblHotkeyCaption, _lblHotkey, _btnRecord, _btnClearHotkey, _lblRecHint
        });

        // ---------- 隐藏行为 ----------
        _gbBehavior.Text = " 隐藏行为 ";
        _gbBehavior.SetBounds(12, 276, 536, 106);

        var lblMode = new Label
        {
            AutoSize = true,
            Bounds = new Rectangle(14, 29, 76, 26),
            Text = "隐藏方式：",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMode.Items.AddRange(new object[]
        {
            "完全隐藏（最彻底）",
            "最小化到任务栏",
            "移到屏幕外"
        });
        _cmbMode.SetBounds(90, 24, 180, 28);

        _chkHideSelf.Text = "按老板键时隐藏本程序";
        _chkHideSelf.SetBounds(292, 26, 230, 24);

        _chkPreventRestore.Text = "隐藏后防止窗口自己弹出";
        _chkPreventRestore.SetBounds(14, 58, 240, 24);

        _chkAutoFind.Text = "启动时自动找回上次目标";
        _chkAutoFind.SetBounds(268, 58, 240, 24);

        _chkAutoStart.Text = "开机自动启动";
        _chkAutoStart.SetBounds(14, 82, 200, 24);

        _chkHideTray.Text = "老板键时同时隐藏托盘图标";
        _chkHideTray.SetBounds(268, 82, 250, 24);

        _gbBehavior.Controls.AddRange(new Control[]
        {
            lblMode, _cmbMode, _chkHideSelf, _chkHideTray,
            _chkPreventRestore, _chkAutoFind, _chkAutoStart
        });

        // ---------- 底部按钮和状态 ----------
        _btnHide.Text = "隐藏目标窗口";
        _btnHide.SetBounds(12, 396, 140, 34);

        _btnShow.Text = "恢复目标窗口";
        _btnShow.SetBounds(158, 396, 140, 34);

        _btnQuit.Text = "退出";
        _btnQuit.SetBounds(482, 396, 66, 34);

        _lblStatus.AutoSize = false;
        _lblStatus.Bounds = new Rectangle(12, 444, 536, 24);
        _lblStatus.ForeColor = Color.FromArgb(70, 70, 70);
        _lblStatus.Text = "就绪。";
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        Controls.AddRange(new Control[]
        {
            _gbTarget, _gbHotkey, _gbBehavior,
            _btnHide, _btnShow, _btnQuit, _lblStatus
        });
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主界面", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("隐藏目标窗口", null, (_, _) => HideTarget());
        menu.Items.Add("恢复目标窗口", null, (_, _) => RestoreTarget());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Quit());

        _tray = new NotifyIcon
        {
            Icon = AppIcon.Create(),
            Text = "老板键 BossKey",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void AttachEvents()
    {
        _btnPick.Click += (_, _) => PickTargetWindow();
        _btnClearTarget.Click += (_, _) => ClearTarget();
        _btnRecord.Click += (_, _) => ToggleRecording();
        _btnClearHotkey.Click += (_, _) => ClearHotkey();
        _btnHide.Click += (_, _) => HideTarget();
        _btnShow.Click += (_, _) => RestoreTarget();
        _btnQuit.Click += (_, _) => Quit();

        _chkHideSelf.CheckedChanged += (_, _) => OnHideSelfChanged();
        _chkHideTray.CheckedChanged += (_, _) => SettingsChanged();
        _chkPreventRestore.CheckedChanged += (_, _) => SettingsChanged();
        _chkAutoFind.CheckedChanged += (_, _) => SettingsChanged();
        _chkAutoStart.CheckedChanged += (_, _) =>
        {
            SettingsChanged();
            ApplyAutoStart(_chkAutoStart.Checked);
        };
        _cmbMode.SelectedIndexChanged += (_, _) => SettingsChanged();

        Resize += OnMainResize;
        FormClosing += OnMainClosing;
        Disposed += (_, _) =>
        {
            _stopShowListener = true;
            try
            {
                _showRequest.Set();
            }
            catch
            {
                // 事件句柄已释放时忽略。
            }

            _tray?.Dispose();
            _antiRestoreTimer.Dispose();
            CleanupKeyboardHook();
        };
    }

    private void ApplySettingsToUi()
    {
        _applyingUi = true;
        _cmbMode.SelectedIndex = (int)_settings.HideMode;
        _chkHideSelf.Checked = _settings.HideSelf;
        _chkHideTray.Checked = _settings.HideTray && _settings.HideSelf;
        _chkHideTray.Enabled = _settings.HideSelf;
        _chkPreventRestore.Checked = _settings.PreventRestore;
        _chkAutoFind.Checked = _settings.AutoFindTarget;
        _chkAutoStart.Checked = _settings.AutoStart;

        _hotkeyCode = (Keys)_settings.HotkeyCode;
        _hotkeyCtrl = _settings.HotkeyCtrl;
        _hotkeyShift = _settings.HotkeyShift;
        _hotkeyAlt = _settings.HotkeyAlt;
        _hotkeyWin = _settings.HotkeyWin;
        _hotkeyDisplay = _settings.HotkeyDisplay ?? string.Empty;
        _lblHotkey.Text = string.IsNullOrEmpty(_hotkeyDisplay) ? "未设置" : _hotkeyDisplay;
        _btnClearHotkey.Enabled = !string.IsNullOrEmpty(_hotkeyDisplay);
        _applyingUi = false;
    }

    private void SettingsChanged()
    {
        if (_applyingUi)
            return;

        _settings.HideMode = (HideMode)_cmbMode.SelectedIndex;
        _settings.HideSelf = _chkHideSelf.Checked;
        _settings.HideTray = _chkHideTray.Checked;
        _settings.PreventRestore = _chkPreventRestore.Checked;
        _settings.AutoFindTarget = _chkAutoFind.Checked;
        _settings.AutoStart = _chkAutoStart.Checked;
        _settings.Save();
    }

    private void OnHideSelfChanged()
    {
        bool hideSelf = _chkHideSelf.Checked;
        _chkHideTray.Enabled = hideSelf;
        if (!hideSelf && _chkHideTray.Checked)
            _chkHideTray.Checked = false;

        SettingsChanged();
    }

    private void ApplyAutoStart(bool enabled)
    {
        try
        {
            const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.CreateSubKey(runPath);
            if (enabled)
                key.SetValue("BossKey", $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue("BossKey", false);
        }
        catch
        {
            SetStatus("无法写入开机启动设置。");
        }
    }

    // ---------- 窗口拾取 ----------

    private void PickTargetWindow()
    {
        bool wasVisible = Visible;
        Hide();

        try
        {
            using var picker = new WindowPicker();
            if (picker.ShowDialog(this) == DialogResult.OK)
                SetTarget(picker.PickedWindow);
            else
                SetStatus("已取消拾取。");
        }
        finally
        {
            if (wasVisible)
                Show();
        }
    }

    private void SetTarget(IntPtr rawWindow)
    {
        if (rawWindow == IntPtr.Zero || !NativeMethods.IsWindow(rawWindow))
        {
            SetStatus("拾取失败：没有找到有效窗口。");
            return;
        }

        string cls = NativeMethods.GetWindowClassName(rawWindow);
        if (IsShellWindowClass(cls))
        {
            SetStatus("这是桌面或任务栏，请选择一个应用窗口。");
            return;
        }

        // 旧目标如果正处于隐藏状态，先恢复再切换，避免窗口“丢”了。
        if (_bossActive)
            RestoreTarget();

        NativeMethods.GetWindowThreadProcessId(rawWindow, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
        {
            SetStatus("不能把本程序自己作为目标。");
            return;
        }

        _target = rawWindow;
        _targetTitle = NativeMethods.GetWindowTitle(rawWindow);
        _targetClass = cls;
        _targetPid = pid;
        _targetProcess = DescribeProcess(pid);

        _settings.TargetProcessName = _targetProcess;
        _settings.TargetTitle = _targetTitle;
        _settings.Save();

        RefreshTargetInfo();
        SetStatus($"已选择目标：{_targetTitle}");
        UpdateBossUi();
    }

    private static bool IsShellWindowClass(string cls) => cls switch
    {
        "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow" => true,
        _ => false
    };

    private string DescribeProcess(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string name = p.ProcessName;
            try
            {
                if (!string.IsNullOrEmpty(p.MainModule?.FileName))
                    return Path.GetFileName(p.MainModule.FileName);
            }
            catch
            {
                // 某些系统进程拿不到路径，退回进程名。
            }

            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
        }
        catch
        {
            return "未知进程";
        }
    }

    private void RefreshTargetInfo()
    {
        if (_target == IntPtr.Zero)
        {
            _txtTarget.Text = "尚未选择目标窗口。";
            return;
        }

        string geometry = "未知";
        if (NativeMethods.GetWindowRect(_target, out var rc))
        {
            geometry = $"左={rc.Left}  上={rc.Top}  右={rc.Right}  下={rc.Bottom}  ({rc.Width}×{rc.Height})";
        }

        _txtTarget.Text =
            $"标题：{_targetTitle}\r\n" +
            $"进程：{_targetProcess}  (PID {_targetPid})\r\n" +
            $"类名：{_targetClass}\r\n" +
            $"句柄：0x{_target.ToInt64():X}\r\n" +
            $"位置：{geometry}";
    }

    private void ClearTarget()
    {
        if (_bossActive)
            RestoreTarget();

        _target = IntPtr.Zero;
        _targetTitle = string.Empty;
        _targetClass = string.Empty;
        _targetProcess = string.Empty;
        _targetPid = 0;

        _settings.TargetProcessName = null;
        _settings.TargetTitle = null;
        _settings.Save();

        RefreshTargetInfo();
        SetStatus("已清除目标窗口。");
        UpdateBossUi();
    }

    private void AutoFindSavedTarget()
    {
        if (string.IsNullOrWhiteSpace(_settings.TargetProcessName) || _enumCallback is null)
            return;

        NativeMethods.EnumWindows(_enumCallback, IntPtr.Zero);
    }

    private bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)
    {
        string processName = _settings.TargetProcessName ?? string.Empty;
        string wantedName = Path.GetFileNameWithoutExtension(processName);
        string? wantedTitle = string.IsNullOrWhiteSpace(_settings.TargetTitle)
            ? null
            : _settings.TargetTitle;

        if (string.IsNullOrWhiteSpace(wantedName))
            return true;

        if (!NativeMethods.IsWindowVisible(hWnd))
            return true;

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0)
            return true;

        try
        {
            using var p = Process.GetProcessById((int)pid);
            if (!p.ProcessName.Equals(wantedName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            return true;
        }

        string title = NativeMethods.GetWindowTitle(hWnd);
        if (wantedTitle is not null && !title.Equals(wantedTitle, StringComparison.Ordinal))
            return true;

        SetTarget(hWnd);
        return false;
    }

    // ---------- 快捷键 ----------

    private void ToggleRecording()
    {
        _recording = !_recording;
        if (_recording)
        {
            _btnRecord.Text = "取消录制";
            _lblRecHint.Text = "正在录制：请直接按下想要的组合键（建议带 Ctrl 或 Alt），Esc 取消。";
            SetStatus("等待按键…");
        }
        else
        {
            _btnRecord.Text = "录制快捷键…";
            _lblRecHint.Text = "录制时直接按下组合键即可（例如 Ctrl+Alt+F10）。只按一个普通键会被拒绝。";
            SetStatus("已取消录制。");
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_recording)
            return base.ProcessCmdKey(ref msg, keyData);

        // 忽略按键重复消息，避免按住不放时反复处理。
        int repeatCount = (int)((long)msg.LParam >> 30) & 1;
        if (repeatCount == 1)
            return true;

        bool ctrl = keyData.HasFlag(Keys.Control);
        bool shift = keyData.HasFlag(Keys.Shift);
        bool alt = keyData.HasFlag(Keys.Alt);
        bool win = keyData.HasFlag(Keys.LWin) || keyData.HasFlag(Keys.RWin);
        Keys code = keyData & Keys.KeyCode;

        // 只按下修饰键时继续等主键。
        if (IsModifierKey(code))
            return true;

        if (code == Keys.Escape && !ctrl && !shift && !alt && !win)
        {
            ToggleRecording();
            return true;
        }

        if (!ctrl && !shift && !alt && !win)
        {
            SetStatus("请至少配合 Ctrl / Alt / Shift / Win 中的一个键。");
            return true;
        }

        string display = BuildHotkeyDisplay(code, ctrl, shift, alt, win);
        if (!TryApplyHotkey(code, ctrl, shift, alt, win, display))
            return true;

        ToggleRecording();
        SetStatus($"老板键已启用：{display}");
        return true;
    }

    private static bool IsModifierKey(Keys key) => key is
        Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin
        or Keys.Shift or Keys.Control or Keys.Alt;

    private bool TryApplyHotkey(Keys code, bool ctrl, bool shift, bool alt, bool win, string display)
    {
        // 组合键由全局低层键盘监听识别，不需要向系统“注册占用”，
        // 因此这里只保存配置即可。
        _hotkeyCode = code;
        _hotkeyCtrl = ctrl;
        _hotkeyShift = shift;
        _hotkeyAlt = alt;
        _hotkeyWin = win;
        _hotkeyDisplay = display;

        _lblHotkey.Text = display;
        _btnClearHotkey.Enabled = true;

        _settings.HotkeyCode = (int)code;
        _settings.HotkeyCtrl = ctrl;
        _settings.HotkeyShift = shift;
        _settings.HotkeyAlt = alt;
        _settings.HotkeyWin = win;
        _settings.HotkeyDisplay = display;
        _settings.Save();

        return true;
    }

    private void ClearHotkey()
    {
        _baseKeyDown = false;
        _hotkeyDisplay = string.Empty;
        _hotkeyCode = Keys.None;
        _hotkeyCtrl = _hotkeyShift = _hotkeyAlt = _hotkeyWin = false;
        _lblHotkey.Text = "未设置";
        _btnClearHotkey.Enabled = false;

        _settings.HotkeyDisplay = null;
        _settings.HotkeyCode = 0;
        _settings.HotkeyCtrl = _settings.HotkeyShift = _settings.HotkeyAlt = _settings.HotkeyWin = false;
        _settings.Save();

        SetStatus("已清除老板键快捷键。");
    }

    // ---------- 全局键盘监听 ----------

    private void InstallKeyboardHook()
    {
        CleanupKeyboardHook();
        _keyboardHookProc ??= KeyboardHookCallback;
        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _keyboardHookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_keyboardHookHandle == IntPtr.Zero)
            SetStatus("全局键盘监听启动失败，快捷键将不可用。");
    }

    private void CleanupKeyboardHook()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            _keyboardHookHandle != IntPtr.Zero &&
            _hotkeyCode != Keys.None &&
            _hotkeyDisplay.Length > 0)
        {
            int msg = wParam.ToInt32();
            bool isDown = msg == NativeMethods.WmKeyDown ||
                          msg == NativeMethods.WmSysKeyDown;
            bool isUp = msg == NativeMethods.WmKeyUp ||
                        msg == NativeMethods.WmSysKeyUp;

            if ((isDown || isUp) &&
                Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam).VkCode ==
                (uint)_hotkeyCode)
            {
                if (isDown && ModifiersMatch())
                {
                    if (!_baseKeyDown)
                    {
                        _baseKeyDown = true;
                        try
                        {
                            BeginInvoke(new Action(ToggleBossKey));
                        }
                        catch
                        {
                            // 主窗口正在关闭时忽略这次触发。
                        }
                    }

                    // 吞掉组合键本身，避免它再落到前台程序。
                    return (IntPtr)1;
                }

                if (isUp && _baseKeyDown)
                {
                    _baseKeyDown = false;
                    return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private bool ModifiersMatch()
    {
        bool ctrl = IsKeyDown(NativeMethods.VkControl) ||
                    IsKeyDown(NativeMethods.VkLControl) ||
                    IsKeyDown(NativeMethods.VkRControl);
        bool shift = IsKeyDown(NativeMethods.VkShift) ||
                     IsKeyDown(NativeMethods.VkLShift) ||
                     IsKeyDown(NativeMethods.VkRShift);
        bool alt = IsKeyDown(NativeMethods.VkMenu) ||
                   IsKeyDown(NativeMethods.VkLMenu) ||
                   IsKeyDown(NativeMethods.VkRMenu);
        bool win = IsKeyDown(NativeMethods.VkLWin) ||
                   IsKeyDown(NativeMethods.VkRWin);

        return ctrl == _hotkeyCtrl &&
               shift == _hotkeyShift &&
               alt == _hotkeyAlt &&
               win == _hotkeyWin;
    }

    private static bool IsKeyDown(int virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static string BuildHotkeyDisplay(Keys code, bool ctrl, bool shift, bool alt, bool win)
    {
        var parts = new List<string>();
        if (win) parts.Add("Win");
        if (ctrl) parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt) parts.Add("Alt");
        parts.Add(KeyName(code));
        return string.Join("+", parts);
    }

    private static string KeyName(Keys key)
    {
        if (key >= Keys.A && key <= Keys.Z)
            return ((char)('A' + (key - Keys.A))).ToString();
        if (key >= Keys.D0 && key <= Keys.D9)
            return ((char)('0' + (key - Keys.D0))).ToString();
        if (key >= Keys.F1 && key <= Keys.F24)
            return $"F{key - Keys.F1 + 1}";

        return key switch
        {
            Keys.OemQuestion => "?",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemPipe => "\\",
            Keys.OemQuotes => "'",
            Keys.OemSemicolon => ";",
            Keys.Oemtilde => "`",
            Keys.Space => "空格",
            Keys.Return => "Enter",
            Keys.Tab => "Tab",
            Keys.Back => "Backspace",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Left => "←",
            Keys.Right => "→",
            Keys.Up => "↑",
            Keys.Down => "↓",
            Keys.PrintScreen => "PrintScreen",
            Keys.CapsLock => "CapsLock",
            Keys.Scroll => "ScrollLock",
            Keys.Pause => "Pause",
            _ => key.ToString()
        };
    }

    // ---------- 隐藏与恢复 ----------

    private void ToggleBossKey()
    {
        if (_bossActive)
            RestoreTarget();
        else
            HideTarget();
    }

    private void HideTarget()
    {
        if (_target == IntPtr.Zero || !NativeMethods.IsWindow(_target))
        {
            _bossActive = false;
            RestoreBossUi();
            ClearDeadTarget();
            return;
        }

        if (_bossActive)
        {
            RestoreTarget();
            return;
        }

        CapturePlacement();

        var mode = _settings.HideMode;
        switch (mode)
        {
            case HideMode.Hide:
                NativeMethods.ShowWindow(_target, NativeMethods.SwHide);
                break;

            case HideMode.Minimize:
                NativeMethods.ShowWindow(_target, NativeMethods.SwMinimize);
                break;

            case HideMode.MoveOffScreen:
                NativeMethods.GetWindowRect(_target, out _savedRect);
                NativeMethods.SetWindowPos(
                    _target, IntPtr.Zero,
                    -32000, -32000, 0, 0,
                    NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
                break;
        }

        _bossActive = true;

        if (_settings.HideSelf && Visible && !_selfHidden)
        {
            _selfHidden = true;
            Hide();
        }

        if (_settings.HideTray && _settings.HideSelf && _tray is { Visible: true })
        {
            _trayHiddenByBoss = true;
            _tray.Visible = false;
        }

        if (_settings.PreventRestore)
            _antiRestoreTimer.Start();

        if (!_trayHiddenByBoss)
            ShowTrayBalloonOnce();
        SetStatus($"已隐藏目标：{_targetTitle}");
        UpdateBossUi();
    }

    private void CapturePlacement()
    {
        _savedPlacement = new NativeMethods.WindowPlacement
        {
            Length = Marshal.SizeOf<NativeMethods.WindowPlacement>()
        };
        NativeMethods.GetWindowPlacement(_target, ref _savedPlacement);
    }

    private void RestoreTarget()
    {
        _bossActive = false;
        _antiRestoreTimer.Stop();

        if (_target != IntPtr.Zero && NativeMethods.IsWindow(_target))
        {
            var mode = _settings.HideMode;
            switch (mode)
            {
                case HideMode.Hide:
                case HideMode.Minimize:
                    _savedPlacement.Length = Marshal.SizeOf<NativeMethods.WindowPlacement>();
                    NativeMethods.SetWindowPlacement(_target, ref _savedPlacement);
                    break;

                case HideMode.MoveOffScreen:
                    NativeMethods.MoveWindow(
                        _target,
                        _savedRect.Left, _savedRect.Top,
                        _savedRect.Width, _savedRect.Height,
                        true);
                    break;
            }
        }

        RestoreBossUi();

        SetStatus(_target != IntPtr.Zero ? $"已恢复目标：{_targetTitle}" : "老板键状态已解除。");
        UpdateBossUi();
    }

    private void RestoreBossUi()
    {
        if (_trayHiddenByBoss && _tray is not null)
        {
            _trayHiddenByBoss = false;
            _tray.Visible = true;
        }

        if (_selfHidden)
        {
            _selfHidden = false;
            ShowMainWindow();
        }
    }

    private void OnAntiRestoreTick(object? sender, EventArgs e)
    {
        if (!_bossActive)
        {
            _antiRestoreTimer.Stop();
            return;
        }

        if (_target == IntPtr.Zero || !NativeMethods.IsWindow(_target))
        {
            _antiRestoreTimer.Stop();
            _bossActive = false;
            ClearDeadTarget();
            return;
        }

        switch (_settings.HideMode)
        {
            case HideMode.Hide:
                if (NativeMethods.IsWindowVisible(_target))
                    NativeMethods.ShowWindow(_target, NativeMethods.SwHide);
                break;

            case HideMode.Minimize:
                if (!NativeMethods.IsIconic(_target))
                    NativeMethods.ShowWindow(_target, NativeMethods.SwMinimize);
                break;

            case HideMode.MoveOffScreen:
                if (NativeMethods.GetWindowRect(_target, out var rc) &&
                    rc.Left > -10000)
                {
                    NativeMethods.SetWindowPos(
                        _target, IntPtr.Zero,
                        -32000, -32000, 0, 0,
                        NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
                }
                break;
        }
    }

    private void ClearDeadTarget()
    {
        string oldTitle = _targetTitle;
        _target = IntPtr.Zero;
        _targetPid = 0;
        _targetProcess = string.Empty;
        _targetClass = string.Empty;
        _targetTitle = string.Empty;

        _settings.TargetProcessName = null;
        _settings.TargetTitle = null;
        _settings.Save();

        RefreshTargetInfo();
        SetStatus($"目标窗口已关闭：{oldTitle}");
        UpdateBossUi();
    }

    private void UpdateBossUi()
    {
        bool hasTarget = _target != IntPtr.Zero && NativeMethods.IsWindow(_target);
        _btnHide.Enabled = hasTarget && !_bossActive;
        _btnShow.Enabled = hasTarget && _bossActive;
        _btnPick.Enabled = !_bossActive;
        _btnClearTarget.Enabled = hasTarget && !_bossActive;
    }

    // ---------- 主窗口与托盘 ----------

    private void StartShowListener()
    {
        if (_showListener is not null)
            return;

        _showListener = new Thread(ShowRequestLoop)
        {
            IsBackground = true,
            Name = "BossKeyShowRequest"
        };
        _showListener.Start();
    }

    private void ShowRequestLoop()
    {
        while (!_stopShowListener)
        {
            try
            {
                _showRequest.WaitOne();
            }
            catch
            {
                return;
            }

            if (_stopShowListener)
                return;

            try
            {
                BeginInvoke(new Action(ShowFromExternalRequest));
            }
            catch
            {
                return;
            }
        }
    }

    private void ShowFromExternalRequest()
    {
        // 用户重新运行程序时，只把 BossKey 自身找回来；
        // 之前隐藏的 QQ 等目标窗口保持隐藏，由用户确认后再恢复。
        _selfHidden = false;
        _trayHiddenByBoss = false;

        if (_tray is not null && !_tray.Visible)
            _tray.Visible = true;

        ShowMainWindow();
        SetStatus("已通过重新运行程序唤起。");
    }

    private void ShowMainWindow()
    {
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Show();
        Activate();
        BringToFront();
    }

    private void OnMainResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && !_quitting)
            Hide();
    }

    private void OnMainClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        CleanupKeyboardHook();
    }

    private void Quit()
    {
        _quitting = true;
        _antiRestoreTimer.Stop();
        CleanupKeyboardHook();
        if (_tray is not null)
            _tray.Visible = false;
        Close();
    }

    private void ShowTrayBalloonOnce()
    {
        if (_balloonShown || _tray is null)
            return;

        _balloonShown = true;
        _tray.ShowBalloonTip(2000, "老板键已生效",
            "按快捷键即可隐藏/恢复目标窗口，本程序会驻留托盘。",
            ToolTipIcon.Info);
    }

    private void SetStatus(string text)
    {
        _lblStatus.Text = text;
    }
}
