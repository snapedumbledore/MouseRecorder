using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MouseRecorder
{
    public partial class Form1 : Form
    {
        // ============ 定时回放相关 ==============
        private bool isInitialized = false;
        private System.Windows.Forms.Timer timerScheduled;
        private bool isScheduled;
        private readonly object playbackLock = new object();
        private int originalPlaybackCount; // 新增：保存原始回放次数
        // ============ 线程安全控制 ==============
        private readonly List<InputEvent> recordedEvents = new List<InputEvent>();
        private readonly Stopwatch stopwatch = new Stopwatch();
        private bool isRecording;
        private bool isPlaying;
        private readonly object recordLock = new object();
        // ============ 钩子相关 ==============
        private HookProc mouseHookDelegate;
        private HookProc keyboardHookDelegate;
        private IntPtr mouseHookHandle = IntPtr.Zero;
        private IntPtr keyboardHookHandle = IntPtr.Zero;

        // ============ Windows API 声明 ==============
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // ============ 常量与结构体 ==============
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public static implicit operator Point(POINT p) => new Point(p.X, p.Y);
        }

        // ============ 事件数据结构 ==============
        private class InputEvent
        {
            public long TimeOffset { get; set; }
            public string EventType { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public Keys Key { get; set; }
            public int Delta { get; set; }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // 删除重复的UninstallHook调用
            if (!isInitialized)
            {
                InitializeScheduledPlayback();
                isInitialized = true;
            }
        }

        // ============ 窗体初始化 ==============
        public Form1()
        {
           
            InitializeComponent(); // ✅ 必须首先初始化控件

            // 然后检查权限
            var wi = System.Security.Principal.WindowsIdentity.GetCurrent();
            var wp = new System.Security.Principal.WindowsPrincipal(wi);
            if (!wp.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                // 提示用户需要管理员权限
                DialogResult result = MessageBox.Show(
                    "此程序需要管理员权限才能正常运行鼠标录制功能。\n是否以管理员身份重新启动？",
                    "需要管理员权限",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 创建启动管理员进程的信息
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.UseShellExecute = true;
                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                    startInfo.FileName = Application.ExecutablePath;
                    startInfo.Verb = "runas"; // 请求管理员权限

                    try
                    {
                        Process.Start(startInfo);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("无法以管理员身份启动: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // 关闭当前实例
                Environment.Exit(0);
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timerScheduled?.Stop();
            UninstallHook();
            base.OnFormClosing(e);
        }

        // ============ 定时模块初始化 ==============
        private void InitializeScheduledPlayback()
        {
            // 确保所有控件都已初始化
            if (numInterval == null || btnSchedule == null || btnClearLog == null)
            {
                MessageBox.Show("控件未正确初始化，请检查设计器配置");
                return;
            }

            try
            {
                timerScheduled = new System.Windows.Forms.Timer();
                UpdateTimerInterval(); // 初始化时立即设置间隔

                timerScheduled.Tick += TimerScheduled_Tick;

                // 数值变化时更新间隔
                numInterval.ValueChanged += (s, e) => UpdateTimerInterval();

                btnSchedule.Click += BtnSchedule_Click;
                btnClearLog.Click += BtnClearLog_Click;

                // 初始化回放次数设置
                if (numPlaybackCount != null)
                {
                    numPlaybackCount.Minimum = 1;
                    numPlaybackCount.Maximum = 100;
                    numPlaybackCount.Value = 1;
                }
                else
                {
                    txtLog.AppendText("警告：回放次数控件未初始化\r\n");
                }
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"定时模块初始化失败: {ex.Message}\r\n");
            }
        }

        // ============ 间隔更新方法 ==============
        private void UpdateTimerInterval()
        {

            // 添加边界值保护
            decimal interval = numInterval.Value < 0.1m ? 0.1m : numInterval.Value;
            numInterval.Value = interval; // 确保界面与实际值同步
        }

        // ============ 定时模块事件 ==============

        // ======== 新增立即停止事件处理 ========



        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 日志已清空\r\n");
        }

        // ============ 定时器完整优化代码 ==============
        private bool isCountdownActive; // 新增状态标识

        // ============ 定时器Tick事件 ==============
        // ============ 修改后的定时器Tick事件 ==============
        private async void TimerScheduled_Tick(object sender, EventArgs e)
        {
            var currentTimer = sender as System.Windows.Forms.Timer;
            if (currentTimer == null) return;

            // 暂停定时器防止重复触发
            currentTimer.Stop();

            try
            {
                bool hasEvents;
                lock (recordedEvents) hasEvents = recordedEvents.Count > 0;

                if (!hasEvents)
                {
                    BeginInvoke(new Action(() =>
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ⚠️ 无可回放事件\r\n")));
                    return;
                }

                // 获取当前回放次数和总次数
                int currentPlayCount = (int)numPlaybackCount.Value;
                int playIndex = originalPlaybackCount - currentPlayCount + 1;

                await Task.Run(async () =>
                {
                    try
                    {
                        lock (playbackLock) isPlaying = true;

                        // 显示当前回放信息
                        BeginInvoke(new Action(() =>
                            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ▶ 开始第 {playIndex}/{originalPlaybackCount} 次定时回放\r\n")));

                        await PlaybackEventsAsync(isScheduledMode: true);
                    }
                    finally
                    {
                        lock (playbackLock) isPlaying = false;
                    }
                });

                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ✅ 定时回放完成\r\n")));

                // 更新剩余回放次数
                if (currentPlayCount > 1)
                {
                    numPlaybackCount.BeginInvoke(new Action(() =>
                    {
                        numPlaybackCount.Value = currentPlayCount - 1;
                    }));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ⚠️ 定时回放异常: {ex.Message}\r\n")));
            }
            finally
            {
                // 检查是否还有剩余回放次数
                int remainingCount = (int)numPlaybackCount.Value;

                // 修复：正确判断是否已完成所有回放
                // 关键修复点：检查当前回放是否为最后一次
                if (remainingCount > 1 && isScheduled)
                {
                    // 重要修改：不要在这里立即启动下一次回放，而是设置一个新的定时器
                    BeginInvoke(new Action(() => {
                        // 清理当前定时器
                        if (timerScheduled != null)
                        {
                            timerScheduled.Tick -= TimerScheduled_Tick;
                            timerScheduled.Dispose();
                            timerScheduled = null;
                        }

                        // 重新设置下一次执行时间
                        var intervalMinutes = (double)numInterval.Value;
                        nextScheduledTime = DateTime.Now.AddMinutes(intervalMinutes);

                        // 创建全新的定时器 - 修复关键点：确保定时器间隔正确设置
                        timerScheduled = new System.Windows.Forms.Timer();
                        // 将间隔设置为分钟值转换为毫秒
                        timerScheduled.Interval = (int)(intervalMinutes * 60 * 1000);
                        timerScheduled.Tick += TimerScheduled_Tick;

                        // 确保定时器被启动
                        timerScheduled.Start();

                        // 启动倒计时显示
                        isCountdownActive = false; // 先确保旧倒计时停止
                        Thread.Sleep(100); // 给旧倒计时任务一些时间退出
                        isCountdownActive = true;
                        StartCountdownDisplay();

                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ⏰ 下一次回放将在 {nextScheduledTime:HH:mm:ss} 执行，剩余 {remainingCount - 1} 次\r\n");
                    }));
                }
                else
                {
                    isScheduled = false; // 重要：标记定时任务已完成
                    isCountdownActive = false; // 停止倒计时

                    // 清理定时器资源
                    if (timerScheduled != null)
                    {
                        timerScheduled.Tick -= TimerScheduled_Tick;
                        timerScheduled.Dispose();
                        timerScheduled = null;
                    }

                    BeginInvoke(new Action(() =>
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ✅ 所有定时回放已完成\r\n");
                        btnSchedule.Text = "启动定时"; // 重置按钮文本

                        // 恢复原始回放次数
                        //numPlaybackCount.Value = originalPlaybackCount;
                    }));
                }
            }
        }

        private void StartCountdownDisplay()
        {
            // 停止现有倒计时
            isCountdownActive = false;
            Thread.Sleep(100); // 确保之前的倒计时任务有时间退出

            // 启动新倒计时
            isCountdownActive = true;

            Task.Run(async () =>
            {
                try
                {
                    var endTime = nextScheduledTime;
                    int lastDisplayedSecond = -1; // 用于控制日志输出频率
                    int totalSeconds = (int)(endTime - DateTime.Now).TotalSeconds;

                    // 根据总时长确定显示间隔
                    int displayInterval = DetermineDisplayInterval(totalSeconds);

                    while (DateTime.Now < endTime && isCountdownActive)
                    {
                        // 计算剩余时间
                        var remaining = (int)(endTime - DateTime.Now).TotalSeconds;
                        if (remaining <= 0) break;

                        // 智能显示倒计时
                        bool shouldDisplay = false;

                        // 最后10秒内每秒显示
                        if (remaining <= 10)
                        {
                            shouldDisplay = (lastDisplayedSecond != remaining);
                        }
                        // 最后1分钟内每10秒显示
                        else if (remaining <= 60)
                        {
                            shouldDisplay = (remaining % 10 == 0 && lastDisplayedSecond != remaining);
                        }
                        // 根据总时长动态调整显示频率
                        else
                        {
                            shouldDisplay = (remaining % displayInterval == 0 && lastDisplayedSecond != remaining);
                        }

                        if (shouldDisplay)
                        {
                            lastDisplayedSecond = remaining;
                            try
                            {
                                string timeDisplay = FormatRemainingTime(remaining);
                                BeginInvoke(new Action(() =>
                                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 🕒 倒计时剩余：{timeDisplay}\r\n")));
                            }
                            catch (ObjectDisposedException)
                            {
                                break; // 窗体已关闭
                            }
                        }

                        // 等待适当时间
                        await Task.Delay(500);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"倒计时异常: {ex.Message}");
                }
            });
        }
        // 根据总时长确定显示间隔
        private int DetermineDisplayInterval(int totalSeconds)
        {
            if (totalSeconds <= 60) return 10;       // 1分钟内：每10秒
            if (totalSeconds <= 300) return 30;      // 5分钟内：每30秒
            if (totalSeconds <= 900) return 60;      // 15分钟内：每1分钟
            if (totalSeconds <= 1800) return 120;    // 30分钟内：每2分钟
            return 300;                              // 超过30分钟：每5分钟
        }

        // 格式化剩余时间显示
        private string FormatRemainingTime(int seconds)
        {
            if (seconds < 60) return $"{seconds}秒";

            int minutes = seconds / 60;
            int remainingSeconds = seconds % 60;

            if (minutes < 60) return $"{minutes}分{(remainingSeconds > 0 ? remainingSeconds + "秒" : "")}";

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;

            return $"{hours}小时{(remainingMinutes > 0 ? remainingMinutes + "分" : "")}";
        }
        private System.Windows.Forms.Timer CreateNewTimer(int interval)
        {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = Math.Max(interval, 100); // 强制最小间隔
            timer.Tick += TimerScheduled_Tick;
            return timer;
        }


        private void BtnSchedule_Click(object sender, EventArgs e)
        {
            try
            {
                lock (playbackLock)
                {
                    // 清理旧定时器
                    if (timerScheduled != null)
                    {
                        timerScheduled.Stop();
                        timerScheduled.Dispose();
                        timerScheduled = null;
                    }

                    // 设置定时状态为启动
                    isScheduled = true;

                    // 保存原始回放次数
                    originalPlaybackCount = (int)numPlaybackCount.Value;

                    // 计算下次执行时间
                    var intervalMinutes = (double)numInterval.Value;
                    nextScheduledTime = DateTime.Now.AddMinutes(intervalMinutes);

                    // 创建新定时器
                    timerScheduled = new System.Windows.Forms.Timer();
                    timerScheduled.Interval = Math.Max(1000, (int)(intervalMinutes * 60 * 1000));
                    timerScheduled.Tick += TimerScheduled_Tick;

                    // 启动倒计时显示
                    isCountdownActive = true;
                    StartCountdownDisplay();

                    // 更新UI和日志
                    BeginInvoke(new Action(() =>
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ⏰ 定时启动，下次执行: {nextScheduledTime:HH:mm:ss}\r\n");
                        btnSchedule.Text = "重新启动定时";
                    }));

                    // 启动定时器
                    timerScheduled.Start();
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ⚠️ 定时器操作异常: {ex.Message}\r\n")));
            }
        }


        // ============ 新增类级变量 ==============
        private DateTime nextScheduledTime;




        // █ 新增立即停止按钮的事件处理 █
        private void BtnStopSchedule_Click(object sender, EventArgs e)
        {
            lock (playbackLock)
            {
                if (isScheduled)
                {
                    isScheduled = false;
                    isCountdownActive = false; // 确保倒计时停止
                    timerScheduled?.Stop();
                    timerScheduled?.Dispose();
                    timerScheduled = null;
                    nextScheduledTime = DateTime.MinValue;

                    BeginInvoke(new Action(() =>
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 定时已取消\r\n")));
                }
            }
        }




        // ▼ 新增专用倒计时方法 ▼
        private async Task<bool> StartCountdown()
        {
            int countdownSeconds = (int)(numInterval.Value * 60);
            int remaining = countdownSeconds;

            while (remaining > 0)
            {
                // ▼ 增加线程安全锁 ▼
                bool isStillScheduled;
                lock (playbackLock)
                {
                    isStillScheduled = isScheduled;
                }

                if (!isStillScheduled) // 使用锁保护后的状态检查
                {
                    BeginInvoke(new Action(() =>
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ❌ 用户取消倒计时\r\n")));
                    return false;
                }

                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 🕒 倒计时剩余：{remaining}秒\r\n")));

                await Task.Delay(1000);
                remaining--;
            }
            return true;
        }









        // ============ 核心逻辑 ================
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (isRecording) return;

            isRecording = true;
            txtLog.AppendText("开始录制...\r\n");
            lock (recordedEvents)
                recordedEvents.Clear();

            stopwatch.Restart();

            mouseHookDelegate = MouseHookProc;
            keyboardHookDelegate = KeyboardHookProc;

            var moduleHandle = GetModuleHandle(null);
            mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, mouseHookDelegate, moduleHandle, 0);
            keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookDelegate, moduleHandle, 0);

            btnStart.Enabled = false;
            btnStop.Enabled = true;

            // 添加hook安装检查
            if (mouseHookHandle == IntPtr.Zero || keyboardHookHandle == IntPtr.Zero)
            {
                txtLog.AppendText("⚠️ 钩子安装失败，请以管理员身份运行程序！\r\n");
                UninstallHook();
                return;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!isRecording) return;

            UninstallHook();
            isRecording = false;
            stopwatch.Stop();

            txtLog.AppendText($"停止录制，已记录 {recordedEvents.Count} 个事件\r\n");
            btnStart.Enabled = true;
            btnStop.Enabled = false;
        }

        private void UninstallHook()
        {
            try
            {
                if (mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(mouseHookHandle);
                    mouseHookHandle = IntPtr.Zero;
                    // 移除Debug.WriteLine，直接使用UI日志
                    if (txtLog != null && !txtLog.IsDisposed)
                    {
                        BeginInvoke(new Action(() => {
                            if (!txtLog.IsDisposed)
                                txtLog.AppendText("✅ 鼠标钩子已卸载\r\n");
                        }));
                    }
                }
                if (keyboardHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(keyboardHookHandle);
                    keyboardHookHandle = IntPtr.Zero;
                    // 移除Debug.WriteLine，直接使用UI日志
                    if (txtLog != null && !txtLog.IsDisposed)
                    {
                        BeginInvoke(new Action(() => {
                            if (!txtLog.IsDisposed)
                                txtLog.AppendText("✅ 键盘钩子已卸载\r\n");
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                // 安全处理异常，避免崩溃
                if (txtLog != null && !txtLog.IsDisposed)
                {
                    BeginInvoke(new Action(() => {
                        if (!txtLog.IsDisposed)
                            txtLog.AppendText($"⚠️ 卸载钩子时出错: {ex.Message}\r\n");
                    }));
                }
            }
        }


        // ============ 钩子处理 ==============
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && isRecording)
                {
                    var mouseInfo = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    var point = (Point)mouseInfo.pt;

                    string eventType = null;
                    int delta = 0;

                    switch ((int)wParam)
                    {
                        case WM_LBUTTONDOWN: eventType = "LeftDown"; break;
                        case WM_LBUTTONUP: eventType = "LeftUp"; break;
                        case WM_RBUTTONDOWN: eventType = "RightDown"; break;
                        case WM_RBUTTONUP: eventType = "RightUp"; break;
                        case WM_MBUTTONDOWN: eventType = "MiddleDown"; break;
                        case WM_MBUTTONUP: eventType = "MiddleUp"; break;
                        case WM_MOUSEWHEEL:
                            eventType = "Wheel";
                            delta = (short)(mouseInfo.mouseData >> 16);
                            break;
                    }

                    if (eventType != null)
                    {
                        var evt = new InputEvent
                        {
                            TimeOffset = stopwatch.ElapsedMilliseconds,
                            EventType = eventType,
                            X = point.X,
                            Y = point.Y,
                            Delta = delta
                        };

                        lock (recordedEvents)
                            recordedEvents.Add(evt);

                        BeginInvoke(new Action(() =>
                            txtLog.AppendText($"[{evt.TimeOffset}ms] 录制 {GetEventDescription(evt)}\r\n")
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"钩子错误: {ex.Message}\r\n")
                ));
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && isRecording)
                {
                    var kbInfo = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    string eventType = null;

                    switch ((int)wParam)
                    {
                        case WM_KEYDOWN:
                        case WM_SYSKEYDOWN:
                            eventType = "KeyDown";
                            break;
                        case WM_KEYUP:
                        case WM_SYSKEYUP:
                            eventType = "KeyUp";
                            break;
                    }

                    if (eventType != null)
                    {
                        var key = SimplifyKey((Keys)kbInfo.vkCode);
                        var evt = new InputEvent
                        {
                            TimeOffset = stopwatch.ElapsedMilliseconds,
                            EventType = eventType,
                            Key = key
                        };

                        lock (recordedEvents)
                            recordedEvents.Add(evt);

                        BeginInvoke(new Action(() =>
                            txtLog.AppendText($"[{evt.TimeOffset}ms] 录制 {GetEventDescription(evt)}\r\n")
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                    txtLog.AppendText($"键盘钩子错误: {ex.Message}\r\n")
                ));
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // ============ 按键简写处理 ==============
        // ▼ 修改为静态方法 ▼
        private static Keys SimplifyKey(Keys key)
        {
            switch (key)
            {
                case Keys.LControlKey:
                case Keys.RControlKey:
                    return Keys.ControlKey;
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    return Keys.ShiftKey;
                case Keys.LMenu:
                case Keys.RMenu:
                    return Keys.Alt;
                default:
                    return key;
            }
        }
        // ============ 扩展键检测（新增）==============
        private static bool IsExtendedKey(Keys key)
        {
            return key == Keys.RControlKey
                || key == Keys.RShiftKey
                || key == Keys.RMenu
                || key == Keys.Insert
                || key == Keys.Delete
                || key == Keys.End
                || key == Keys.Home
                || key == Keys.Left
                || key == Keys.Up
                || key == Keys.Right
                || key == Keys.Down
                || key == Keys.PageUp
                || key == Keys.PageDown
                || key == Keys.NumLock;
        }


        // ============ 回放逻辑 ==============
        private async void btnPlay_Click(object sender, EventArgs e)
        {
            if (recordedEvents.Count == 0) return;

            // 修复问题2：使用临时变量保存原始状态
            bool originalState = isPlaying;
            isPlaying = !isPlaying;
            btnPlay.Text = isPlaying ? "■ 停止" : "▶ 回放";

            if (isPlaying)
            {
                int playCount = (int)numPlaybackCount.Value;
                txtLog.AppendText($"======== 开始回放 ({recordedEvents.Count} 个事件) x {playCount} 次 ========\r\n");
                try
                {
                    for (int i = 0; i < playCount && isPlaying; i++)
                    {
                        if (i > 0)
                        {
                            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ▶ 开始第 {i + 1}/{playCount} 次手动回放\r\n");
                        }
                        await PlaybackEventsAsync();

                        // 如果不是最后一次回放且回放次数大于1，添加短暂延迟
                        if (i < playCount - 1 && playCount > 1 && isPlaying)
                        {
                            await Task.Delay(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"回放异常: {ex.Message}\r\n");
                }
                finally // 新增finally块确保状态重置
                {
                    isPlaying = originalState;
                    BeginInvoke(new Action(() => btnPlay.Text = "▶ 回放"));
                }
            }
        }

        // ...（前面代码保持相同，以下是需要补全的部分）

        private async Task PlaybackEventsAsync(bool isScheduledMode = false)
        {
            InputEvent[] playbackCopy;
            lock (recordedEvents)
            {
                playbackCopy = recordedEvents.ToArray();
            }

            var timer = Stopwatch.StartNew();
            long lastEventTime = 0; // 新增时间差计算
            foreach (var evt in playbackCopy)
            {
                // 修复问题1：添加时间差等待
                if (lastEventTime > 0)
                {
                    var delay = evt.TimeOffset - lastEventTime;
                    if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay));
                }
                lastEventTime = evt.TimeOffset;

                bool shouldContinue;
                lock (playbackLock)
                {
                    shouldContinue = isScheduledMode ? isScheduled : isPlaying;
                }
                if (!shouldContinue) break;

                try
                {
                    BeginInvoke(new Action(() => ProcessSingleEvent(evt)));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                        txtLog.AppendText($"⛔ 回放异常: {ex.Message}\r\n")));
                }
            }

            // 区分日志类型
            string logMessage = isScheduledMode ?
                $"[{DateTime.Now:HH:mm:ss}] ✅ 定时回放完成\r\n" :
                $"[{DateTime.Now:HH:mm:ss}] ✅ 手动回放完成\r\n";

            BeginInvoke(new Action(() => txtLog.AppendText(logMessage)));
        }




        // ============ 事件处理器模块（新增）⬅️ ==============
        private void ProcessSingleEvent(InputEvent evt)
        {
            try
            {
                switch (evt.EventType)
                {
                    case "LeftDown":
                        MouseEvent(MouseEventFlags.LeftDown, evt.X, evt.Y, evt.Delta);
                        break;
                    case "LeftUp":
                        MouseEvent(MouseEventFlags.LeftUp, evt.X, evt.Y, evt.Delta);
                        break;
                    case "RightDown":
                        MouseEvent(MouseEventFlags.RightDown, evt.X, evt.Y, evt.Delta);
                        break;
                    case "RightUp":
                        MouseEvent(MouseEventFlags.RightUp, evt.X, evt.Y, evt.Delta);
                        break;
                    case "MiddleDown":
                        MouseEvent(MouseEventFlags.MiddleDown, evt.X, evt.Y, evt.Delta);
                        break;
                    case "MiddleUp":
                        MouseEvent(MouseEventFlags.MiddleUp, evt.X, evt.Y, evt.Delta);
                        break;
                    case "Wheel":
                        MouseEvent(MouseEventFlags.Wheel, evt.X, evt.Y, evt.Delta);
                        break;
                    case "KeyDown":
                        KeyEvent(evt.Key, isKeyDown: true);
                        break;
                    case "KeyUp":
                        KeyEvent(evt.Key, isKeyDown: false);
                        break;
                }

                txtLog.AppendText($"[{evt.TimeOffset}ms] 回放 {GetEventDescription(evt)}\r\n");
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"回放失败: {ex.Message}\r\n");
            }
        }

        // ============ 事件描述生成方法（新增）⬅️ ==============
        private string GetEventDescription(InputEvent evt)
        {
            switch (evt.EventType)
            {
                case "LeftDown":
                case "LeftUp":
                case "RightDown":
                case "RightUp":
                case "MiddleDown":
                case "MiddleUp":
                    return $"{evt.EventType} ({evt.X}, {evt.Y})";
                case "Wheel":
                    return $"滚轮 {evt.Delta} 单位 @ ({evt.X}, {evt.Y})";
                case "KeyDown":
                case "KeyUp":
                    return $"{evt.EventType} [{evt.Key}]";
                default:
                    return "未知事件";
            }
        }

        private void lblPlaybackCount_Click(object sender, EventArgs e)
        {
            // 可以为空，或者添加一些提示信息
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 回放次数设置: {numPlaybackCount.Value}\r\n");
        }

        private void numPlaybackCount_ValueChanged(object sender, EventArgs e)
        {
            // 记录回放次数变化
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 回放次数已设置为: {numPlaybackCount.Value}\r\n");
        }

        private void numInterval_ValueChanged(object sender, EventArgs e)
        {
            // 更新间隔时间
            UpdateTimerInterval();
        }


        // ============ 模拟输入API操作（新增）⬅️ ==============
        [Flags]
        private enum MouseEventFlags
        {
            LeftDown = 0x02,
            LeftUp = 0x04,
            RightDown = 0x08,
            RightUp = 0x10,
            MiddleDown = 0x20,
            MiddleUp = 0x40,
            Wheel = 0x0800,
            Absolute = 0x8000
        }




        private static void MouseEvent(MouseEventFlags flags, int x, int y, int delta)
        {
            try
            {
                // ▼ 使用虚拟屏幕范围进行坐标换算 ▼
                Rectangle virtualScreen = SystemInformation.VirtualScreen;

                int absX = (int)Math.Round((x - virtualScreen.Left) / (double)virtualScreen.Width * 65535);
                int absY = (int)Math.Round((y - virtualScreen.Top) / (double)virtualScreen.Height * 65535);

                // ▼ 确保设置硬件级光标位置 ▼
                Win32.SetCursorPos(x, y);

                // ▼ 移除Cursor.Position设置 ▼
                Win32.mouse_event(
                    (uint)(flags | MouseEventFlags.Absolute),
                    (uint)absX,
                    (uint)absY,
                    (uint)delta,
                    UIntPtr.Zero
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"坐标转换失败 ({x}, {y})", ex);
            }
        }


        private void KeyEvent(Keys key, bool isKeyDown)
        {
            try
            {
                // ▼ 完全重构键盘事件逻辑 ▼
                Keys baseKey = SimplifyKey(key);
                byte scanCode = (byte)Win32.MapVirtualKey((uint)baseKey, 0);

                uint flags = isKeyDown ? Win32.KEYEVENTF_KEYDOWN : Win32.KEYEVENTF_KEYUP;
                if (IsExtendedKey(key)) flags |= Win32.KEYEVENTF_EXTENDEDKEY;

                // ▼ 使用底层API进行精确控制 ▼
                Win32.INPUT[] inputs = new Win32.INPUT[1];
                inputs[0] = new Win32.INPUT
                {
                    type = Win32.INPUT_KEYBOARD,
                    u = new Win32.InputUnion
                    {
                        ki = new Win32.KEYBDINPUT
                        {
                            wVk = (ushort)baseKey,
                            wScan = scanCode,
                            dwFlags = flags,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                Win32.SendInput(1, inputs, Marshal.SizeOf(typeof(Win32.INPUT)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"键盘事件异常: {ex.Message}");
            }
        }

        // ▼ 补充Win32常量 ▼
        private const uint INPUT_KEYBOARD = 1;





        // 新增Win32键盘管理API
        private static class Win32
        {
            // ▼ 新增键盘映射API ▼
            [DllImport("user32.dll")]
            public static extern uint MapVirtualKey(uint uCode, uint uMapType);

            // ▼ 添加缺失的常量 ▼
            public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
            public const uint INPUT_KEYBOARD = 1;
            // === 鼠标 ===
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetCursorPos(int X, int Y);

            [DllImport("user32.dll")]
            public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

            // === 键盘 ===
            public const uint KEYEVENTF_KEYDOWN = 0x0000;
            public const uint KEYEVENTF_KEYUP = 0x0002;

            [DllImport("user32.dll")]
            public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

            // === 输入结构 ===
            [StructLayout(LayoutKind.Sequential)]
            public struct INPUT
            {
                public uint type;
                public InputUnion u;
            }

            // === 联合体定义 === 
            [StructLayout(LayoutKind.Explicit)]
            public struct InputUnion
            {
                [FieldOffset(0)] public MOUSEINPUT mi;
                [FieldOffset(0)] public KEYBDINPUT ki;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MOUSEINPUT
            {
                public int dx;
                public int dy;
                public uint mouseData;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        }


        // ============ 窗体设计器生成代码（必须存在）⬅️ ==============
        // 注意：此区域由Visual Studio自动生成，请确保控件名称一致

    }
}