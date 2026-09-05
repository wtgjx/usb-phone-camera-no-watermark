using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("无水印手机 USB 摄像头")]
[assembly: AssemblyDescription("通过 scrcpy Camera Mode 和 OBS Virtual Camera 将安卓手机摄像头接入 Windows")]
[assembly: AssemblyCompany("Local Tool")]
[assembly: AssemblyProduct("无水印手机 USB 摄像头")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace PhoneUsbCamera
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--diagnose", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length > 1
                    ? Path.GetFullPath(args[1])
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.json");
                try
                {
                    DiagnosticsWriter.Write(output);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(output + ".error.txt", ex.ToString(), Encoding.UTF8);
                    Environment.ExitCode = 1;
                }

                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--prepare-obs", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length > 1
                    ? Path.GetFullPath(args[1])
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prepare-obs.txt");
                try
                {
                    OperationResult result = new BridgeService().PrepareObsConfiguration(QualityPreset.Stable1080());
                    File.WriteAllText(output, result.Message, Encoding.UTF8);
                    Environment.ExitCode = result.Success ? 0 : 1;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(output, ex.ToString(), Encoding.UTF8);
                    Environment.ExitCode = 1;
                }

                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--start-session", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length > 1
                    ? Path.GetFullPath(args[1])
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "start-session.txt");
                string cameraId = args.Length > 2 ? args[2] : "0";
                StringBuilder runLog = new StringBuilder();
                try
                {
                    CameraInfo camera = new CameraInfo
                    {
                        Id = cameraId,
                        Facing = "back",
                        CustomName = "Camera ID " + cameraId,
                        Sizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                    SessionResult result = new BridgeService().StartSessionAsync(
                        camera,
                        QualityPreset.FourK(),
                        delegate(string line) { runLog.AppendLine(line); }).Result;
                    runLog.AppendLine("SUCCESS=" + result.Success);
                    runLog.AppendLine("ACTUAL_QUALITY=" + result.ActualQuality);
                    runLog.AppendLine("RESULT=" + result.Message);
                    File.WriteAllText(output, runLog.ToString(), new UTF8Encoding(false));
                    Environment.ExitCode = result.Success ? 0 : 1;
                }
                catch (Exception ex)
                {
                    runLog.AppendLine(ex.ToString());
                    File.WriteAllText(output, runLog.ToString(), new UTF8Encoding(false));
                    Environment.ExitCode = 1;
                }

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CameraStudioForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Background = Color.FromArgb(10, 14, 20);
        private static readonly Color Surface = Color.FromArgb(20, 27, 36);
        private static readonly Color SurfaceAlt = Color.FromArgb(31, 40, 51);
        private static readonly Color Foreground = Color.FromArgb(239, 246, 255);
        private static readonly Color Muted = Color.FromArgb(148, 163, 184);
        private static readonly Color Accent = Color.FromArgb(34, 211, 238);
        private static readonly Color Success = Color.FromArgb(74, 222, 128);
        private static readonly Color Warning = Color.FromArgb(250, 204, 21);
        private static readonly Color Error = Color.FromArgb(251, 113, 133);

        private readonly BridgeService _bridge;
        private readonly StatusCard _usbCard;
        private readonly StatusCard _androidCard;
        private readonly StatusCard _previewCard;
        private readonly StatusCard _virtualCameraCard;
        private readonly ComboBox _cameraCombo;
        private readonly ComboBox _qualityCombo;
        private readonly Button _scanButton;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Button _refreshButton;
        private readonly Button _openScreenButton;
        private readonly RichTextBox _logBox;
        private readonly Label _footer;
        private bool _busy;

        internal MainForm()
        {
            _bridge = new BridgeService();

            Text = "无水印手机 USB 摄像头";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 790);
            MinimumSize = new Size(900, 720);
            BackColor = Background;
            ForeColor = Foreground;
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(28, 22, 28, 18);
            root.BackColor = Background;
            root.ColumnCount = 1;
            root.RowCount = 8;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Background;

            Label badge = new Label();
            badge.AutoSize = true;
            badge.Text = "USB · NO WATERMARK";
            badge.Font = new Font("Consolas", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            badge.ForeColor = Accent;
            badge.Location = new Point(2, 1);

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "无水印手机 USB 摄像头";
            title.Font = new Font("Microsoft YaHei UI", 20.5F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Foreground;
            title.Location = new Point(0, 22);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = "手机 Camera2 → scrcpy USB → OBS Virtual Camera → OpenScreen";
            subtitle.ForeColor = Muted;
            subtitle.Location = new Point(3, 61);

            header.Controls.Add(badge);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 4;
            cards.RowCount = 1;
            cards.Padding = new Padding(0, 3, 0, 10);
            for (int i = 0; i < 4; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            _usbCard = new StatusCard("USB 手机");
            _androidCard = new StatusCard("Camera Mode");
            _previewCard = new StatusCard("无水印画面");
            _virtualCameraCard = new StatusCard("虚拟摄像头");
            AddCard(cards, _usbCard, 0);
            AddCard(cards, _androidCard, 1);
            AddCard(cards, _previewCard, 2);
            AddCard(cards, _virtualCameraCard, 3);
            root.Controls.Add(cards, 0, 1);

            Panel settingsPanel = new Panel();
            settingsPanel.Dock = DockStyle.Fill;
            settingsPanel.BackColor = Surface;
            settingsPanel.Padding = new Padding(18, 13, 18, 12);

            Label settingsTitle = new Label();
            settingsTitle.AutoSize = true;
            settingsTitle.Text = "镜头与画质";
            settingsTitle.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
            settingsTitle.ForeColor = Foreground;
            settingsTitle.Location = new Point(18, 12);
            settingsPanel.Controls.Add(settingsTitle);

            TableLayoutPanel settings = new TableLayoutPanel();
            settings.Location = new Point(14, 41);
            settings.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            settings.Size = new Size(settingsPanel.Width - 28, 75);
            settings.Dock = DockStyle.Bottom;
            settings.ColumnCount = 3;
            settings.RowCount = 1;
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            _cameraCombo = MakeComboBox();
            _cameraCombo.Items.Add(CameraInfo.AutoBack());
            _cameraCombo.SelectedIndex = 0;

            _qualityCombo = MakeComboBox();
            foreach (QualityPreset preset in QualityPreset.All())
            {
                _qualityCombo.Items.Add(preset);
            }
            _qualityCombo.SelectedIndex = 2;

            _scanButton = MakeButton("扫描手机镜头", SurfaceAlt, Foreground, false);

            settings.Controls.Add(MakeField("选择镜头", _cameraCombo, 0, 8), 0, 0);
            settings.Controls.Add(MakeField("输出画质", _qualityCombo, 8, 8), 1, 0);
            settings.Controls.Add(WrapButton(_scanButton, 8, 0, 22), 2, 0);
            settingsPanel.Controls.Add(settings);
            root.Controls.Add(settingsPanel, 0, 2);

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 4;
            actions.RowCount = 1;
            actions.Padding = new Padding(0, 8, 0, 9);
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));

            _startButton = MakeButton("启动无水印虚拟摄像头", Accent, Color.FromArgb(5, 25, 31), true);
            _stopButton = MakeButton("停止本次会话", SurfaceAlt, Foreground, false);
            _refreshButton = MakeButton("刷新状态", SurfaceAlt, Foreground, false);
            _openScreenButton = MakeButton("打开 OpenScreen", SurfaceAlt, Foreground, false);
            actions.Controls.Add(WrapButton(_startButton, 0, 8, 0), 0, 0);
            actions.Controls.Add(WrapButton(_stopButton, 8, 8, 0), 1, 0);
            actions.Controls.Add(WrapButton(_refreshButton, 8, 8, 0), 2, 0);
            actions.Controls.Add(WrapButton(_openScreenButton, 8, 0, 0), 3, 0);
            root.Controls.Add(actions, 0, 3);

            Panel guide = new Panel();
            guide.Dock = DockStyle.Fill;
            guide.BackColor = Color.FromArgb(14, 32, 43);
            guide.Padding = new Padding(17, 12, 17, 10);

            Label guideText = new Label();
            guideText.Dock = DockStyle.Fill;
            guideText.Text = "启动后，OpenScreen 中请选择「OBS Virtual Camera」。\r\n" +
                             "scrcpy 预览窗口可以被其他窗口遮住，但不要最小化；最小化会让 OBS 无法继续捕获。\r\n" +
                             "程序会临时停止手机系统相机和 DroidCam 后台来释放镜头，不会卸载或清除数据。";
            guideText.ForeColor = Color.FromArgb(165, 216, 232);
            guideText.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            guide.Controls.Add(guideText);
            root.Controls.Add(guide, 0, 4);

            Label logTitle = new Label();
            logTitle.Dock = DockStyle.Fill;
            logTitle.Text = "运行记录";
            logTitle.TextAlign = ContentAlignment.BottomLeft;
            logTitle.ForeColor = Muted;
            logTitle.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(logTitle, 0, 5);

            _logBox = new RichTextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.ReadOnly = true;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.BackColor = Color.FromArgb(6, 9, 14);
            _logBox.ForeColor = Color.FromArgb(203, 213, 225);
            _logBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _logBox.DetectUrls = false;
            _logBox.Margin = new Padding(0, 6, 0, 5);
            root.Controls.Add(_logBox, 0, 6);

            _footer = new Label();
            _footer.Dock = DockStyle.Fill;
            _footer.TextAlign = ContentAlignment.MiddleLeft;
            _footer.ForeColor = Muted;
            _footer.Font = new Font("Microsoft YaHei UI", 8.3F, FontStyle.Regular, GraphicsUnit.Point);
            _footer.Text = "所有视频数据只经过 USB 和本机，不上传云端。";
            root.Controls.Add(_footer, 0, 7);

            _scanButton.Click += async delegate { await ScanCamerasAsync(true); };
            _startButton.Click += async delegate { await StartSessionAsync(); };
            _stopButton.Click += async delegate { await StopSessionAsync(); };
            _refreshButton.Click += async delegate { await RefreshStatusAsync(true); };
            _openScreenButton.Click += delegate { OpenScreenOnly(); };
            FormClosing += delegate(object sender, FormClosingEventArgs eventArgs)
            {
                if (_busy)
                {
                    eventArgs.Cancel = true;
                    MessageBox.Show(
                        this,
                        "启动或停止操作尚未完成，请等待按钮恢复后再关闭。",
                        "正在处理摄像头会话",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                _bridge.StopSession();
            };
            Shown += async delegate
            {
                await RefreshStatusAsync(false);
                if (_bridge.LastState != null && _bridge.LastState.UsbDevice != null && _bridge.LastState.CameraModeCompatible)
                {
                    await ScanCamerasAsync(false);
                }
            };
        }

        private static ComboBox MakeComboBox()
        {
            ComboBox combo = new ComboBox();
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Color.FromArgb(38, 48, 61);
            combo.ForeColor = Foreground;
            combo.Font = new Font("Microsoft YaHei UI", 9.2F, FontStyle.Regular, GraphicsUnit.Point);
            return combo;
        }

        private static Control MakeField(string label, Control control, int left, int right)
        {
            TableLayoutPanel field = new TableLayoutPanel();
            field.Dock = DockStyle.Fill;
            field.Padding = new Padding(left, 0, right, 0);
            field.ColumnCount = 1;
            field.RowCount = 2;
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label caption = new Label();
            caption.Dock = DockStyle.Fill;
            caption.Text = label;
            caption.ForeColor = Muted;
            caption.Font = new Font("Microsoft YaHei UI", 8.3F, FontStyle.Regular, GraphicsUnit.Point);
            control.Dock = DockStyle.Fill;
            field.Controls.Add(caption, 0, 0);
            field.Controls.Add(control, 0, 1);
            return field;
        }

        private static void AddCard(TableLayoutPanel parent, Control card, int column)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Padding = new Padding(column == 0 ? 0 : 7, 0, column == 3 ? 0 : 7, 0);
            holder.Controls.Add(card);
            parent.Controls.Add(holder, column, 0);
        }

        private static Panel WrapButton(Button button, int left, int right, int top)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Padding = new Padding(left, top, right, 0);
            button.Dock = DockStyle.Fill;
            holder.Controls.Add(button);
            return holder;
        }

        private static Button MakeButton(string text, Color back, Color fore, bool bold)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.Font = new Font("Microsoft YaHei UI", 9.2F, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            return button;
        }

        private async Task RefreshStatusAsync(bool writeLog)
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true, "正在检查 USB、scrcpy、OBS 和 OpenScreen…");
            try
            {
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
                if (writeLog)
                {
                    AppendLog(state.Summary);
                }
            }
            catch (Exception ex)
            {
                AppendLog("检测失败：" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task ScanCamerasAsync(bool showErrors)
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true, "正在读取手机 Camera2 镜头能力…");
            try
            {
                CameraScanResult result = await _bridge.ScanCamerasAsync(AppendLog);
                if (!result.Success)
                {
                    AppendLog(result.Message);
                    if (showErrors)
                    {
                        MessageBox.Show(this, result.Message, "无法扫描镜头", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                _cameraCombo.Items.Clear();
                foreach (CameraInfo camera in result.Cameras)
                {
                    _cameraCombo.Items.Add(camera);
                }
                if (_cameraCombo.Items.Count > 0)
                {
                    _cameraCombo.SelectedIndex = 0;
                }

                AppendLog("已读取 " + result.Cameras.Count + " 个 Camera2 镜头；默认选择后置逻辑主摄。 ");
                await RefreshStatusInternalAsync();
            }
            catch (Exception ex)
            {
                AppendLog("扫描失败：" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task RefreshStatusInternalAsync()
        {
            PhoneBridgeState state = await _bridge.InspectAsync();
            ApplyState(state);
        }

        private async Task StartSessionAsync()
        {
            if (_busy)
            {
                return;
            }

            CameraInfo camera = _cameraCombo.SelectedItem as CameraInfo ?? CameraInfo.AutoBack();
            QualityPreset preset = _qualityCombo.SelectedItem as QualityPreset ?? QualityPreset.Stable1080();
            SetBusy(true, "正在启动无水印 USB 摄像头…");
            AppendLog("准备启动：" + camera.DisplayName + "，" + preset.DisplayName);

            try
            {
                SessionResult result = await _bridge.StartSessionAsync(camera, preset, AppendLog);
                AppendLog(result.Message);
                await RefreshStatusInternalAsync();

                if (!result.Success)
                {
                    MessageBox.Show(this, result.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(
                    this,
                    "无水印 USB 摄像头已经启动。\r\n\r\n" +
                    "实际输出：" + result.ActualQuality + "\r\n" +
                    "请在 OpenScreen 中选择：OBS Virtual Camera\r\n\r\n" +
                    "不要最小化 Phone USB Camera 预览窗口；它可以被其他窗口遮住。",
                    "启动成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("启动异常：" + ex.Message);
                MessageBox.Show(this, ex.Message, "启动异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task StopSessionAsync()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true, "正在停止本次摄像头会话…");
            try
            {
                OperationResult result = await Task.Run(new Func<OperationResult>(_bridge.StopSession));
                AppendLog(result.Message);
                await RefreshStatusInternalAsync();
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void OpenScreenOnly()
        {
            OperationResult result = _bridge.LaunchOpenScreen();
            AppendLog(result.Message);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "无法打开 OpenScreen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyState(PhoneBridgeState state)
        {
            if (state.UsbDevice == null)
            {
                _usbCard.SetState("未连接", "请插入并解锁手机", Error);
            }
            else if (state.UsbDevice.State == "unauthorized")
            {
                _usbCard.SetState("等待授权", "手机上点击允许调试", Warning);
            }
            else
            {
                _usbCard.SetState("已连接", state.UsbDevice.DisplayName, Success);
            }

            if (state.UsbDevice == null)
            {
                _androidCard.SetState("待检测", "Camera Mode 需 Android 12+", Muted);
            }
            else if (state.CameraModeCompatible)
            {
                _androidCard.SetState("兼容", "Android " + state.AndroidRelease, Success);
            }
            else
            {
                _androidCard.SetState("不兼容", "需要 Android 12 或更高", Error);
            }

            _previewCard.SetState(
                state.ScrcpyRunning ? "运行中" : "未运行",
                state.ScrcpyRunning ? "Phone USB Camera" : "scrcpy 4.1 已就绪",
                state.ScrcpyRunning ? Success : (state.ScrcpyAvailable ? Muted : Error));

            _virtualCameraCard.SetState(
                state.ObsVirtualCameraActive ? "输出中" : (state.ObsVirtualCameraRegistered ? "已安装" : "未安装"),
                state.ObsVirtualCameraActive ? "OBS Virtual Camera" : (state.ObsRunning ? "OBS 正在运行" : "等待启动"),
                state.ObsVirtualCameraActive ? Success : (state.ObsVirtualCameraRegistered ? Muted : Error));

            _footer.Text = "本地链路：USB/ADB · scrcpy 4.1 · OBS Virtual Camera" +
                           (state.OpenScreenRunning ? " · OpenScreen 运行中" : "") +
                           "。不上传云端，不经过 DroidCam 视频服务。";
        }

        private void SetBusy(bool busy, string text)
        {
            _busy = busy;
            _cameraCombo.Enabled = !busy;
            _qualityCombo.Enabled = !busy;
            _scanButton.Enabled = !busy;
            _startButton.Enabled = !busy;
            _stopButton.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            _openScreenButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrEmpty(text))
            {
                AppendLog(text);
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            string clean = (message ?? string.Empty).Trim();
            if (clean.Length == 0)
            {
                return;
            }
            _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + clean + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }
    }

    internal sealed class StatusCard : Panel
    {
        private readonly Panel _bar;
        private readonly Label _value;
        private readonly Label _detail;

        internal StatusCard(string title)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 27, 36);
            Padding = new Padding(15, 10, 12, 9);

            _bar = new Panel();
            _bar.Size = new Size(4, 60);
            _bar.Location = new Point(0, 13);
            _bar.BackColor = Color.FromArgb(100, 116, 139);

            Label titleLabel = new Label();
            titleLabel.AutoSize = true;
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(148, 163, 184);
            titleLabel.Font = new Font("Microsoft YaHei UI", 8.4F, FontStyle.Regular, GraphicsUnit.Point);
            titleLabel.Location = new Point(15, 9);

            _value = new Label();
            _value.AutoSize = true;
            _value.Text = "检测中";
            _value.ForeColor = Color.FromArgb(239, 246, 255);
            _value.Font = new Font("Microsoft YaHei UI", 12.3F, FontStyle.Bold, GraphicsUnit.Point);
            _value.Location = new Point(14, 34);

            _detail = new Label();
            _detail.AutoEllipsis = true;
            _detail.AutoSize = false;
            _detail.Text = "请稍候";
            _detail.ForeColor = Color.FromArgb(148, 163, 184);
            _detail.Font = new Font("Microsoft YaHei UI", 7.8F, FontStyle.Regular, GraphicsUnit.Point);
            _detail.Location = new Point(15, 66);
            _detail.Size = new Size(185, 17);
            _detail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Controls.Add(_bar);
            Controls.Add(titleLabel);
            Controls.Add(_value);
            Controls.Add(_detail);
        }

        internal void SetState(string value, string detail, Color color)
        {
            _value.Text = value;
            _detail.Text = detail;
            _bar.BackColor = color;
        }
    }

    internal sealed class BridgeService
    {
        internal const string WindowTitle = "Phone USB Camera";
        internal const string ObsCollectionName = "PhoneUsbCamera";
        internal const string ObsSceneName = "Phone USB Camera";
        internal const string ObsSourceName = "Phone USB Camera Capture";

        private readonly string _scrcpyDirectory;
        private readonly string _scrcpyPath;
        private readonly string _adbPath;
        private readonly string _obsPath;
        private readonly StringBuilder _scrcpyOutput;
        private Process _scrcpyProcess;
        private Process _obsProcess;

        internal BridgeService()
        {
            _scrcpyDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scrcpy");
            _scrcpyPath = Path.Combine(_scrcpyDirectory, "scrcpy.exe");
            _adbPath = Path.Combine(_scrcpyDirectory, "adb.exe");
            _obsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
            _scrcpyOutput = new StringBuilder();
        }

        internal PhoneBridgeState LastState { get; private set; }

        internal bool OwnsSession
        {
            get
            {
                return (_scrcpyProcess != null && !_scrcpyProcess.HasExited) ||
                       (_obsProcess != null && !_obsProcess.HasExited);
            }
        }

        internal int ScrcpyProcessId
        {
            get
            {
                return _scrcpyProcess != null && !_scrcpyProcess.HasExited ? _scrcpyProcess.Id : 0;
            }
        }

        internal Task<PhoneBridgeState> InspectAsync()
        {
            return Task.Run(new Func<PhoneBridgeState>(Inspect));
        }

        internal PhoneBridgeState Inspect()
        {
            PhoneBridgeState state = new PhoneBridgeState();
            state.GeneratedAt = DateTime.Now;
            state.ScrcpyPath = _scrcpyPath;
            state.AdbPath = _adbPath;
            state.ObsPath = _obsPath;
            state.OpenScreenPath = FindOpenScreenPath();
            state.ScrcpyAvailable = File.Exists(_scrcpyPath) && File.Exists(_adbPath);
            state.ObsInstalled = File.Exists(_obsPath);
            state.ObsVirtualCameraRegistered = IsObsVirtualCameraRegistered();
            state.ScrcpyRunning = IsProcessRunning("scrcpy");
            state.ObsRunning = IsProcessRunning("obs64");
            state.OpenScreenRunning = IsProcessRunning("OpenScreen") || IsProcessRunning("Openscreen");
            state.OpenScreenInstalled = !string.IsNullOrEmpty(state.OpenScreenPath) && File.Exists(state.OpenScreenPath);

            if (state.ScrcpyAvailable)
            {
                RunProcess(_adbPath, "start-server", 8000);
                CommandResult devices = RunProcess(_adbPath, "devices -l", 8000);
                state.Devices = ParseDevices(devices.AllText);
                state.UsbDevice = SelectUsbDevice(state.Devices);
                if (state.UsbDevice != null && state.UsbDevice.State == "device")
                {
                    CommandResult sdk = RunProcess(_adbPath, "-s " + Quote(state.UsbDevice.Serial) + " shell getprop ro.build.version.sdk", 6000);
                    int api;
                    if (int.TryParse((sdk.Output ?? string.Empty).Trim(), out api))
                    {
                        state.AndroidApi = api;
                    }
                    CommandResult release = RunProcess(_adbPath, "-s " + Quote(state.UsbDevice.Serial) + " shell getprop ro.build.version.release", 6000);
                    state.AndroidRelease = (release.Output ?? string.Empty).Trim();
                    state.CameraModeCompatible = state.AndroidApi >= 31;
                }
            }

            state.ObsVirtualCameraActive = state.ObsRunning && IsLatestObsLogActive(
                "==== Virtual Camera Start",
                "==== Virtual Camera Stop");
            state.Summary = BuildSummary(state);
            LastState = state;
            return state;
        }

        internal Task<CameraScanResult> ScanCamerasAsync(Action<string> log)
        {
            return Task.Run(delegate
            {
                PhoneBridgeState state = Inspect();
                if (!state.ScrcpyAvailable)
                {
                    return CameraScanResult.Fail("程序包内缺少 scrcpy，请重新解压完整文件夹。");
                }
                if (state.UsbDevice == null)
                {
                    return CameraScanResult.Fail("未检测到 USB 手机。请连接数据线并开启 USB 调试。");
                }
                if (state.UsbDevice.State == "unauthorized")
                {
                    return CameraScanResult.Fail("手机尚未授权 USB 调试。请解锁手机并点击“允许”。");
                }
                if (!state.CameraModeCompatible)
                {
                    return CameraScanResult.Fail("scrcpy Camera Mode 需要 Android 12 或更高版本。");
                }

                log("正在向手机查询摄像头和可用视频尺寸，这通常需要 3–6 秒…");
                string args = "--serial " + Quote(state.UsbDevice.Serial) + " --list-camera-sizes";
                CommandResult result = RunProcess(_scrcpyPath, args, 40000);
                if (result.ExitCode != 0)
                {
                    return CameraScanResult.Fail("读取镜头能力失败：" + CleanMessage(result.AllText));
                }

                List<CameraInfo> cameras = ParseCameras(result.AllText);
                if (cameras.Count == 0)
                {
                    return CameraScanResult.Fail("手机返回了结果，但没有解析到 Camera2 镜头。详细信息已写入诊断日志。");
                }

                string localLogDirectory = GetLocalLogDirectory();
                Directory.CreateDirectory(localLogDirectory);
                File.WriteAllText(Path.Combine(localLogDirectory, "camera-capabilities.txt"), result.AllText, new UTF8Encoding(false));
                return CameraScanResult.Ok(cameras, "已读取手机镜头能力。");
            });
        }

        internal Task<SessionResult> StartSessionAsync(CameraInfo camera, QualityPreset requested, Action<string> log)
        {
            return StartSessionAsync(camera, requested, log, null);
        }

        internal Task<SessionResult> StartSessionAsync(
            CameraInfo camera,
            QualityPreset requested,
            Action<string> log,
            Action<int> previewReady)
        {
            return Task.Run(delegate
            {
                PhoneBridgeState state = Inspect();
                if (!state.ScrcpyAvailable)
                {
                    return SessionResult.Fail("程序包不完整：未找到 scrcpy.exe 或 adb.exe。");
                }
                if (!state.ObsInstalled || !state.ObsVirtualCameraRegistered)
                {
                    return SessionResult.Fail("未找到可用的 OBS Virtual Camera。请先安装或修复 OBS Studio。");
                }
                if (!state.OpenScreenInstalled)
                {
                    return SessionResult.Fail("未找到 OpenScreen 安装程序。");
                }
                if (state.UsbDevice == null)
                {
                    return SessionResult.Fail("未检测到 USB 手机。请重新插入数据线并保持手机解锁。");
                }
                if (state.UsbDevice.State == "unauthorized")
                {
                    return SessionResult.Fail("电脑已发现手机，但没有调试授权。请在手机上点击“允许 USB 调试”。");
                }
                if (!state.CameraModeCompatible)
                {
                    return SessionResult.Fail("当前手机系统不支持 Camera Mode；最低要求 Android 12。");
                }
                if (state.ObsRunning)
                {
                    return SessionResult.Fail("OBS 当前已经在运行。请先确认没有录制任务并退出 OBS，然后重新点击启动；程序需要用专用场景启动它。");
                }
                if (state.ScrcpyRunning)
                {
                    return SessionResult.Fail("检测到另一个 scrcpy 会话正在运行。请先关闭它，再启动无水印摄像头。");
                }
                if (state.OpenScreenRunning)
                {
                    return SessionResult.Fail("OpenScreen 当前已经在运行。为确保新的虚拟摄像头能进入设备列表，请先彻底退出 OpenScreen，再点击启动。");
                }

                log("正在释放手机镜头（仅结束系统相机与 DroidCam 后台，不会卸载或清数据）…");
                string cameraPackage = ResolveDefaultCameraPackage(state.UsbDevice.Serial);
                if (!string.IsNullOrEmpty(cameraPackage))
                {
                    RunProcess(_adbPath,
                        "-s " + Quote(state.UsbDevice.Serial) + " shell am force-stop " + Quote(cameraPackage),
                        8000);
                }
                RunProcess(_adbPath,
                    "-s " + Quote(state.UsbDevice.Serial) + " shell am force-stop com.dev47apps.obsdroidcam",
                    8000);
                Thread.Sleep(500);

                QualityPreset actual = requested;
                if (camera != null && camera.HasKnownSizes && !camera.Supports(requested.Width, requested.Height))
                {
                    log("所选镜头未声明支持 " + requested.Width + "×" + requested.Height + "，自动回退到 1080p30。");
                    actual = QualityPreset.Stable1080();
                }

                OperationResult config = PrepareObsConfiguration(actual);
                if (!config.Success)
                {
                    return SessionResult.Fail(config.Message);
                }
                log("已准备独立 OBS 场景；只会更新本工具专用的 PhoneUsbCamera 配置。");

                log("正在启动手机 Camera2 无水印画面：" + actual.DisplayName);
                ScrcpyStartResult preview = StartScrcpy(state.UsbDevice.Serial, camera, actual, log);
                if (!preview.Success && actual.Width > 1920 && !preview.CameraInUse)
                {
                    log("高画质档启动失败，自动尝试稳定的 1080p30 H.264…");
                    actual = QualityPreset.Stable1080();
                    OperationResult fallbackConfig = PrepareObsConfiguration(actual);
                    if (!fallbackConfig.Success)
                    {
                        return SessionResult.Fail(fallbackConfig.Message);
                    }
                    preview = StartScrcpy(state.UsbDevice.Serial, camera, actual, log);
                }

                if (!preview.Success)
                {
                    if (preview.CameraInUse)
                    {
                        return SessionResult.Fail("手机摄像头仍被其他应用占用。请关闭手机相机、视频通话或直播应用后重试。\r\n\r\n" + preview.Message);
                    }
                    return SessionResult.Fail("无法启动手机摄像头：" + preview.Message);
                }

                log("手机画面已通过 USB 建立，正在启动 OBS Virtual Camera…");
                OperationResult obsResult = StartObs();
                if (!obsResult.Success)
                {
                    StopProcess(_scrcpyProcess);
                    return SessionResult.Fail(obsResult.Message);
                }

                log("正在等待 OBS 完成初始化并确认虚拟摄像头输出（首次启动可能需要约 15–30 秒）…");
                bool virtualActive = WaitForObsVirtualCamera(40000);
                if (!IsScrcpyAlive())
                {
                    StopProcess(_scrcpyProcess);
                    StopProcess(_obsProcess);
                    return SessionResult.Fail(BuildScrcpyDisconnectMessage());
                }
                if (virtualActive)
                {
                    log("OBS Virtual Camera 已确认启动。");
                    if (previewReady != null && _scrcpyProcess != null && !_scrcpyProcess.HasExited)
                    {
                        try
                        {
                            previewReady(_scrcpyProcess.Id);
                        }
                        catch (Exception ex)
                        {
                            log("主界面实时预览暂时无法合并：" + ex.Message + "。OBS 高清输出不受影响。");
                        }
                    }
                }
                else
                {
                    StopProcess(_scrcpyProcess);
                    StopProcess(_obsProcess);
                    return SessionResult.Fail("OBS 已经运行，但 40 秒内没有确认 Virtual Camera 启动。请打开 OBS 检查场景或启动提示；OpenScreen 尚未自动打开。");
                }

                log("正在确认手机画面持续稳定…");
                if (!WaitForScrcpyStable(2500))
                {
                    StopProcess(_scrcpyProcess);
                    StopProcess(_obsProcess);
                    return SessionResult.Fail(BuildScrcpyDisconnectMessage());
                }

                OperationResult openResult = LaunchOpenScreen();
                log(openResult.Message);
                return SessionResult.Ok(
                    "无水印 USB 摄像头链路已启动。OpenScreen 中请选择 OBS Virtual Camera。",
                    actual.DisplayName);
            });
        }

        internal OperationResult PrepareObsConfiguration(QualityPreset preset)
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string scenesDirectory = Path.Combine(roaming, "obs-studio", "basic", "scenes");
                string profileDirectory = Path.Combine(roaming, "obs-studio", "basic", "profiles", ObsCollectionName);
                Directory.CreateDirectory(scenesDirectory);
                Directory.CreateDirectory(profileDirectory);

                string sceneJson = BuildObsSceneJson(preset.Width, preset.Height);
                new JavaScriptSerializer().DeserializeObject(sceneJson);
                File.WriteAllText(
                    Path.Combine(scenesDirectory, ObsCollectionName + ".json"),
                    sceneJson,
                    new UTF8Encoding(false));

                string profile = BuildObsProfile(preset.Width, preset.Height, preset.Fps);
                File.WriteAllText(
                    Path.Combine(profileDirectory, "basic.ini"),
                    profile,
                    new UTF8Encoding(false));

                return OperationResult.Ok("OBS 专用场景与画质配置已生成。");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("无法准备 OBS 专用场景：" + ex.Message);
            }
        }

        internal static string BuildObsSceneJson(int width, int height)
        {
            string w = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string h = height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "{\n" +
                   "  \"name\": \"" + ObsCollectionName + "\",\n" +
                   "  \"sources\": [\n" +
                   "    {\n" +
                   "      \"prev_ver\": 537001986,\n" +
                   "      \"name\": \"" + ObsSourceName + "\",\n" +
                   "      \"uuid\": \"930f64c7-5485-4cce-bfc7-5e093c486d91\",\n" +
                   "      \"id\": \"window_capture\",\n" +
                   "      \"versioned_id\": \"window_capture\",\n" +
                   "      \"settings\": {\n" +
                   "        \"window\": \"" + WindowTitle + ":SDL_app:scrcpy.exe\",\n" +
                   "        \"method\": 2,\n" +
                   "        \"priority\": 0,\n" +
                   "        \"cursor\": false,\n" +
                   "        \"capture_audio\": false,\n" +
                   "        \"compatibility\": false,\n" +
                   "        \"client_area\": true,\n" +
                   "        \"force_sdr\": false\n" +
                   "      },\n" +
                   "      \"mixers\": 0,\n" +
                   "      \"sync\": 0,\n" +
                   "      \"flags\": 0,\n" +
                   "      \"volume\": 1.0,\n" +
                   "      \"balance\": 0.5,\n" +
                   "      \"enabled\": true,\n" +
                   "      \"muted\": false,\n" +
                   "      \"hotkeys\": {},\n" +
                   "      \"deinterlace_mode\": 0,\n" +
                   "      \"deinterlace_field_order\": 0,\n" +
                   "      \"monitoring_type\": 0,\n" +
                   "      \"private_settings\": {}\n" +
                   "    },\n" +
                   "    {\n" +
                   "      \"prev_ver\": 537001986,\n" +
                   "      \"name\": \"" + ObsSceneName + "\",\n" +
                   "      \"uuid\": \"3cd78af0-0733-456f-83e0-9f1976965145\",\n" +
                   "      \"id\": \"scene\",\n" +
                   "      \"versioned_id\": \"scene\",\n" +
                   "      \"settings\": {\n" +
                   "        \"id_counter\": 1,\n" +
                   "        \"custom_size\": false,\n" +
                   "        \"items\": [\n" +
                   "          {\n" +
                   "            \"name\": \"" + ObsSourceName + "\",\n" +
                   "            \"source_uuid\": \"930f64c7-5485-4cce-bfc7-5e093c486d91\",\n" +
                   "            \"visible\": true,\n" +
                   "            \"locked\": true,\n" +
                   "            \"rot\": 0.0,\n" +
                   "            \"scale_ref\": {\"x\": " + w + ".0, \"y\": " + h + ".0},\n" +
                   "            \"align\": 5,\n" +
                   "            \"bounds_type\": 2,\n" +
                   "            \"bounds_align\": 0,\n" +
                   "            \"bounds_crop\": false,\n" +
                   "            \"crop_left\": 0,\n" +
                   "            \"crop_top\": 0,\n" +
                   "            \"crop_right\": 0,\n" +
                   "            \"crop_bottom\": 0,\n" +
                   "            \"id\": 1,\n" +
                   "            \"group_item_backup\": false,\n" +
                   "            \"pos\": {\"x\": 0.0, \"y\": 0.0},\n" +
                   "            \"scale\": {\"x\": 1.0, \"y\": 1.0},\n" +
                   "            \"bounds\": {\"x\": " + w + ".0, \"y\": " + h + ".0},\n" +
                   "            \"scale_filter\": \"lanczos\",\n" +
                   "            \"blend_method\": \"default\",\n" +
                   "            \"blend_type\": \"normal\",\n" +
                   "            \"show_transition\": {\"duration\": 300},\n" +
                   "            \"hide_transition\": {\"duration\": 300},\n" +
                   "            \"private_settings\": {}\n" +
                   "          }\n" +
                   "        ]\n" +
                   "      },\n" +
                   "      \"mixers\": 0,\n" +
                   "      \"sync\": 0,\n" +
                   "      \"flags\": 0,\n" +
                   "      \"volume\": 1.0,\n" +
                   "      \"balance\": 0.5,\n" +
                   "      \"enabled\": true,\n" +
                   "      \"muted\": false,\n" +
                   "      \"hotkeys\": {},\n" +
                   "      \"deinterlace_mode\": 0,\n" +
                   "      \"deinterlace_field_order\": 0,\n" +
                   "      \"monitoring_type\": 0,\n" +
                   "      \"canvas_uuid\": \"6c69626f-6273-4c00-9d88-c5136d61696e\",\n" +
                   "      \"private_settings\": {}\n" +
                   "    }\n" +
                   "  ],\n" +
                   "  \"groups\": [],\n" +
                   "  \"scene_order\": [{\"name\": \"" + ObsSceneName + "\"}],\n" +
                   "  \"current_scene\": \"" + ObsSceneName + "\",\n" +
                   "  \"current_program_scene\": \"" + ObsSceneName + "\",\n" +
                   "  \"canvases\": [],\n" +
                   "  \"current_transition\": \"Fade\",\n" +
                   "  \"transition_duration\": 300,\n" +
                   "  \"transitions\": [],\n" +
                   "  \"quick_transitions\": [],\n" +
                   "  \"saved_projectors\": [],\n" +
                   "  \"preview_locked\": true,\n" +
                   "  \"scaling_enabled\": false,\n" +
                   "  \"scaling_level\": -7,\n" +
                   "  \"scaling_off_x\": 0.0,\n" +
                   "  \"scaling_off_y\": 0.0,\n" +
                   "  \"virtual-camera\": {\"type2\": 3},\n" +
                   "  \"modules\": {\"scripts-tool\": []},\n" +
                   "  \"resolution\": {\"x\": " + w + ", \"y\": " + h + "},\n" +
                   "  \"version\": 2\n" +
                   "}\n";
        }

        internal static string BuildObsProfile(int width, int height, int fps)
        {
            return "[General]\r\n" +
                   "Name=" + ObsCollectionName + "\r\n\r\n" +
                   "[Video]\r\n" +
                   "BaseCX=" + width + "\r\n" +
                   "BaseCY=" + height + "\r\n" +
                   "OutputCX=" + width + "\r\n" +
                   "OutputCY=" + height + "\r\n" +
                   "FPSType=0\r\n" +
                   "FPSCommon=" + fps + "\r\n" +
                   "ScaleType=lanczos\r\n" +
                   "ColorFormat=NV12\r\n" +
                   "ColorSpace=709\r\n" +
                   "ColorRange=Partial\r\n\r\n" +
                   "[Output]\r\n" +
                   "Mode=Simple\r\n";
        }

        internal OperationResult StopSession()
        {
            bool stoppedPreview = StopProcess(_scrcpyProcess);
            bool stoppedObs = StopProcess(_obsProcess);
            _scrcpyProcess = null;
            _obsProcess = null;

            if (!stoppedPreview && !stoppedObs)
            {
                return OperationResult.Ok("本程序启动的摄像头会话当前没有在运行。");
            }
            return OperationResult.Ok("本次 scrcpy 预览和 OBS 虚拟摄像头会话已停止；OpenScreen 保持打开。");
        }

        internal OperationResult LaunchOpenScreen()
        {
            string path = FindOpenScreenPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return OperationResult.Fail("未找到 OpenScreen，预期位置为 AppData\\Local\\Programs\\Openscreen。");
            }
            if (IsProcessRunning("OpenScreen") || IsProcessRunning("Openscreen"))
            {
                return OperationResult.Ok("OpenScreen 已在运行。若摄像头列表未刷新，请彻底退出后重新打开。");
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = path;
                info.WorkingDirectory = Path.GetDirectoryName(path);
                info.UseShellExecute = true;
                Process.Start(info);
                return OperationResult.Ok("OpenScreen 已打开；请选择 OBS Virtual Camera。");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("无法启动 OpenScreen：" + ex.Message);
            }
        }

        private ScrcpyStartResult StartScrcpy(string serial, CameraInfo camera, QualityPreset preset, Action<string> log)
        {
            lock (_scrcpyOutput)
            {
                _scrcpyOutput.Length = 0;
            }

            string cameraArgument = camera != null && !string.IsNullOrEmpty(camera.Id)
                ? " --camera-id=" + Quote(camera.Id)
                : " --camera-facing=back";
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            int parkedX = virtualScreen.Right - 2;
            int parkedY = virtualScreen.Bottom - 2;
            string arguments =
                "--serial " + Quote(serial) +
                " --video-source=camera" + cameraArgument +
                " --camera-size=" + preset.Width + "x" + preset.Height +
                " --camera-fps=" + preset.Fps +
                " --video-codec=" + preset.Codec +
                " --video-bit-rate=" + preset.BitRate +
                " --no-audio" +
                " --window-title=" + Quote(WindowTitle) +
                " --window-width=" + preset.Width +
                " --window-height=" + preset.Height +
                " --window-x=" + parkedX + " --window-y=" + parkedY +
                " --window-borderless --keep-active" +
                " --shortcut-mod=lctrl";

            try
            {
                Process process = new Process();
                process.StartInfo = new ProcessStartInfo();
                process.StartInfo.FileName = _scrcpyPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = _scrcpyDirectory;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
                {
                    CaptureScrcpyLine(eventArgs.Data, log);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
                {
                    CaptureScrcpyLine(eventArgs.Data, log);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _scrcpyProcess = process;

                bool windowReady = false;
                for (int i = 0; i < 90; i++)
                {
                    if (process.HasExited)
                    {
                        break;
                    }
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        windowReady = true;
                        break;
                    }
                    Thread.Sleep(100);
                }

                if (process.HasExited)
                {
                    string output = GetScrcpyOutput();
                    return ScrcpyStartResult.Fail(
                        CleanMessage(output),
                        output.IndexOf("CAMERA_IN_USE", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                if (!windowReady)
                {
                    log("scrcpy 进程已运行，但尚未取得预览窗口句柄；继续尝试启动 OBS。");
                }
                return ScrcpyStartResult.Ok("scrcpy 摄像头画面已启动。");
            }
            catch (Exception ex)
            {
                return ScrcpyStartResult.Fail(ex.Message, false);
            }
        }

        private void CaptureScrcpyLine(string line, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }
            lock (_scrcpyOutput)
            {
                _scrcpyOutput.AppendLine(line);
            }
            if (line.IndexOf("Using camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Texture:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("WARN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                log("scrcpy · " + line.Trim());
            }
        }

        private string GetScrcpyOutput()
        {
            lock (_scrcpyOutput)
            {
                return _scrcpyOutput.ToString();
            }
        }

        private OperationResult StartObs()
        {
            if (!File.Exists(_obsPath))
            {
                return OperationResult.Fail("未找到 OBS Studio：" + _obsPath);
            }
            try
            {
                OperationResult sentinel = ArchiveStaleObsSentinel();
                if (!sentinel.Success)
                {
                    return sentinel;
                }
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = _obsPath;
                info.Arguments = "--collection " + Quote(ObsCollectionName) +
                                 " --profile " + Quote(ObsCollectionName) +
                                 " --scene " + Quote(ObsSceneName) +
                                 " --startvirtualcam --minimize-to-tray";
                info.WorkingDirectory = Path.GetDirectoryName(_obsPath);
                info.UseShellExecute = true;
                _obsProcess = Process.Start(info);
                Thread.Sleep(1500);
                if (_obsProcess == null || _obsProcess.HasExited)
                {
                    return OperationResult.Fail("OBS 启动后立即退出。请打开 OBS 检查日志或安全模式提示。");
                }
                return OperationResult.Ok("OBS 已启动并请求开启虚拟摄像头。");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("无法启动 OBS：" + ex.Message);
            }
        }

        private OperationResult ArchiveStaleObsSentinel()
        {
            try
            {
                string sentinelDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "obs-studio",
                    ".sentinel");
                if (!Directory.Exists(sentinelDirectory))
                {
                    return OperationResult.Ok("OBS 启动状态正常。");
                }

                FileSystemInfo[] staleEntries = new DirectoryInfo(sentinelDirectory).GetFileSystemInfos();
                if (staleEntries.Length == 0)
                {
                    return OperationResult.Ok("OBS 启动状态正常。");
                }

                string backupDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PhoneUsbCamera",
                    "obs-sentinel-backups",
                    DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
                Directory.CreateDirectory(backupDirectory);
                foreach (FileSystemInfo entry in staleEntries)
                {
                    string destination = Path.Combine(backupDirectory, entry.Name);
                    if ((entry.Attributes & FileAttributes.Directory) != 0)
                    {
                        Directory.Move(entry.FullName, destination);
                    }
                    else
                    {
                        File.Move(entry.FullName, destination);
                    }
                }
                return OperationResult.Ok("OBS 上次异常关闭标记已备份，可正常自动启动。");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("OBS 上次异常关闭标记无法备份：" + ex.Message +
                                            "。请手动打开 OBS，选择正常模式后再退出。");
            }
        }

        private bool WaitForObsVirtualCamera(int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsScrcpyAlive())
                {
                    return false;
                }
                if (!IsProcessRunning("obs64"))
                {
                    return false;
                }
                if (IsLatestObsLogActive("==== Virtual Camera Start", "==== Virtual Camera Stop"))
                {
                    return true;
                }
                Thread.Sleep(500);
            }
            return false;
        }

        private bool WaitForScrcpyStable(int milliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsScrcpyAlive())
                {
                    return false;
                }
                Thread.Sleep(250);
            }
            return IsScrcpyAlive();
        }

        private bool IsScrcpyAlive()
        {
            try
            {
                if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
                {
                    return false;
                }
                string output = GetScrcpyOutput();
                return output.IndexOf("Camera disconnected", StringComparison.OrdinalIgnoreCase) < 0 &&
                       output.IndexOf("Device disconnected", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch
            {
                return false;
            }
        }

        private string BuildScrcpyDisconnectMessage()
        {
            string output = GetScrcpyOutput();
            if (output.IndexOf("Camera disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                output.IndexOf("Camera capture failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "手机摄像头在启动期间被其他应用抢占了。请保持手机解锁，不要打开系统相机、视频通话或直播应用，然后重试。\r\n\r\n" + CleanMessage(output);
            }
            if (output.IndexOf("Device disconnected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "USB/ADB 画面在启动期间中断了。请保持手机解锁，并检查数据线、USB 调试授权后重试。\r\n\r\n" + CleanMessage(output);
            }
            return "手机 USB 摄像头预览在 OBS 启动期间退出了。请保持手机解锁并关闭其他会使用摄像头的应用后重试。\r\n\r\n" + CleanMessage(output);
        }

        private string ResolveDefaultCameraPackage(string serial)
        {
            CommandResult result = RunProcess(
                _adbPath,
                "-s " + Quote(serial) + " shell cmd package resolve-activity --brief -a android.media.action.IMAGE_CAPTURE",
                8000);
            string[] lines = (result.Output ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            foreach (string raw in lines.Reverse())
            {
                string line = raw.Trim();
                int slash = line.IndexOf('/');
                if (slash > 0)
                {
                    string packageName = line.Substring(0, slash);
                    if (Regex.IsMatch(packageName, "^[A-Za-z0-9._]+$"))
                    {
                        return packageName;
                    }
                }
            }
            return string.Empty;
        }

        private static bool StopProcess(Process process)
        {
            if (process == null)
            {
                return false;
            }
            try
            {
                if (process.HasExited)
                {
                    return false;
                }
                bool requested = process.CloseMainWindow();
                if (!requested)
                {
                    requested = NativeWindowCloser.RequestClose(process.Id);
                }
                if (!requested || !process.WaitForExit(3500))
                {
                    process.Kill();
                    process.WaitForExit(2500);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsLatestObsLogActive(string startMarker, string stopMarker)
        {
            try
            {
                Process process = Process.GetProcessesByName("obs64").FirstOrDefault();
                if (process == null)
                {
                    return false;
                }
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "obs-studio",
                    "logs");
                if (!Directory.Exists(directory))
                {
                    return false;
                }
                DateTime processStartUtc = process.StartTime.ToUniversalTime();
                FileInfo latest = new DirectoryInfo(directory).GetFiles("*.txt")
                    .Where(delegate(FileInfo file) { return file.CreationTimeUtc >= processStartUtc.AddSeconds(-1); })
                    .OrderByDescending(delegate(FileInfo file) { return file.CreationTimeUtc; })
                    .FirstOrDefault();
                if (latest == null)
                {
                    return false;
                }
                string text;
                using (FileStream stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    text = reader.ReadToEnd();
                }
                int start = text.LastIndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
                int stop = text.LastIndexOf(stopMarker, StringComparison.OrdinalIgnoreCase);
                return start >= 0 && start > stop;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsObsVirtualCameraRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\{A3FCE0F5-3493-419F-958A-ABA1250EC20B}"))
                {
                    object name = key == null ? null : key.GetValue(null);
                    return name != null && name.ToString().IndexOf("OBS Virtual Camera", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindOpenScreenPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates =
            {
                Path.Combine(local, "Programs", "Openscreen", "Openscreen.exe"),
                Path.Combine(local, "Programs", "OpenScreen", "OpenScreen.exe"),
                Path.Combine(local, "Programs", "openscreen", "openscreen.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static bool IsProcessRunning(string name)
        {
            try
            {
                return Process.GetProcessesByName(name).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        internal static List<CameraInfo> ParseCameras(string text)
        {
            List<CameraInfo> cameras = new List<CameraInfo>();
            if (string.IsNullOrEmpty(text))
            {
                return cameras;
            }

            Regex header = new Regex(
                @"--camera-id=(\S+)\s+\((back|front|external),\s+(\d+)x(\d+),\s+fps=\{([^}]*)\},\s+zoom-range=\[([^]]*)\]\)",
                RegexOptions.IgnoreCase);
            Regex size = new Regex(@"^\s*-\s*(\d+)x(\d+)\s*$");
            CameraInfo current = null;
            bool highSpeed = false;
            foreach (string raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                Match headerMatch = header.Match(raw);
                if (headerMatch.Success)
                {
                    current = new CameraInfo();
                    current.Id = headerMatch.Groups[1].Value;
                    current.Facing = headerMatch.Groups[2].Value.ToLowerInvariant();
                    current.MaxWidth = int.Parse(headerMatch.Groups[3].Value);
                    current.MaxHeight = int.Parse(headerMatch.Groups[4].Value);
                    current.FpsText = headerMatch.Groups[5].Value;
                    current.ZoomRange = headerMatch.Groups[6].Value;
                    current.Sizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    cameras.Add(current);
                    highSpeed = false;
                    continue;
                }
                if (raw.IndexOf("High speed capture", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    highSpeed = true;
                    continue;
                }
                if (current != null && !highSpeed)
                {
                    Match sizeMatch = size.Match(raw);
                    if (sizeMatch.Success)
                    {
                        current.Sizes.Add(sizeMatch.Groups[1].Value + "x" + sizeMatch.Groups[2].Value);
                    }
                }
            }

            List<CameraInfo> unique = cameras
                .GroupBy(delegate(CameraInfo camera) { return camera.Id; }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(IGrouping<string, CameraInfo> group) { return group.First(); })
                .ToList();
            CameraInfo firstBack = unique.FirstOrDefault(delegate(CameraInfo camera) { return camera.Facing == "back"; });
            if (firstBack != null)
            {
                firstBack.Recommended = true;
            }
            return unique
                .OrderBy(delegate(CameraInfo camera) { return camera.Facing == "back" ? 0 : 1; })
                .ThenBy(delegate(CameraInfo camera)
                {
                    int id;
                    return int.TryParse(camera.Id, out id) ? id : int.MaxValue;
                })
                .ToList();
        }

        private static List<AdbDevice> ParseDevices(string text)
        {
            List<AdbDevice> devices = new List<AdbDevice>();
            if (string.IsNullOrEmpty(text))
            {
                return devices;
            }
            foreach (string raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) || line.StartsWith("*"))
                {
                    continue;
                }
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                if (parts[1] != "device" && parts[1] != "unauthorized" && parts[1] != "offline")
                {
                    continue;
                }
                AdbDevice device = new AdbDevice();
                device.Serial = parts[0];
                device.State = parts[1];
                device.Model = ReadToken(parts, "model:");
                device.Product = ReadToken(parts, "product:");
                devices.Add(device);
            }
            return devices;
        }

        private static AdbDevice SelectUsbDevice(IEnumerable<AdbDevice> devices)
        {
            if (devices == null)
            {
                return null;
            }
            List<AdbDevice> usb = devices.Where(delegate(AdbDevice device)
            {
                return !string.IsNullOrEmpty(device.Serial) &&
                       device.Serial.IndexOf(':') < 0 &&
                       !device.Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase);
            }).ToList();
            return usb.FirstOrDefault(delegate(AdbDevice device) { return device.State == "device"; }) ?? usb.FirstOrDefault();
        }

        private static string ReadToken(IEnumerable<string> parts, string prefix)
        {
            foreach (string part in parts)
            {
                if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return part.Substring(prefix.Length).Replace('_', ' ');
                }
            }
            return null;
        }

        private static CommandResult RunProcess(string file, string args, int timeoutMs)
        {
            CommandResult result = new CommandResult();
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                result.ExitCode = -1;
                result.Error = "文件不存在：" + file;
                return result;
            }
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo();
                    process.StartInfo.FileName = file;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.WorkingDirectory = Path.GetDirectoryName(file);
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    process.Start();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        result.ExitCode = -2;
                        result.Error = "命令执行超时";
                        return result;
                    }
                    Task.WaitAll(new Task[] { output, error }, 2500);
                    result.ExitCode = process.ExitCode;
                    result.Output = output.IsCompleted ? output.Result : string.Empty;
                    result.Error = error.IsCompleted ? error.Result : string.Empty;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
                return result;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string CleanMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "没有返回详细错误";
            }
            string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length > 420 ? clean.Substring(0, 420) + "…" : clean;
        }

        private static string GetLocalLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhoneUsbCamera",
                "logs");
        }

        private static string BuildSummary(PhoneBridgeState state)
        {
            string usb = state.UsbDevice == null ? "USB 手机未连接" : "USB 手机=" + state.UsbDevice.State;
            string camera = state.CameraModeCompatible ? "Camera Mode 兼容" : "Camera Mode 未确认";
            string preview = state.ScrcpyRunning ? "scrcpy 运行中" : "scrcpy 未运行";
            string obs = state.ObsVirtualCameraActive ? "OBS 虚拟摄像头输出中" : "OBS 虚拟摄像头未启动";
            return usb + "；" + camera + "；" + preview + "；" + obs;
        }
    }

    internal static class NativeWindowCloser
    {
        private const uint WmClose = 0x0010;

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        internal static bool RequestClose(int processId)
        {
            bool requested = false;
            try
            {
                EnumWindows(delegate(IntPtr window, IntPtr state)
                {
                    uint owner;
                    GetWindowThreadProcessId(window, out owner);
                    if (owner == (uint)processId && PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero))
                    {
                        requested = true;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
                return false;
            }
            return requested;
        }
    }

    internal static class DiagnosticsWriter
    {
        internal static void Write(string path)
        {
            BridgeService bridge = new BridgeService();
            PhoneBridgeState state = bridge.Inspect();
            bool obsTemplateValid;
            try
            {
                new JavaScriptSerializer().DeserializeObject(BridgeService.BuildObsSceneJson(1920, 1080));
                obsTemplateValid = true;
            }
            catch
            {
                obsTemplateValid = false;
            }

            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"generatedAt\": \"" + Escape(state.GeneratedAt.ToString("o")) + "\",");
            json.AppendLine("  \"scrcpyAvailable\": " + Bool(state.ScrcpyAvailable) + ",");
            json.AppendLine("  \"scrcpyPath\": \"" + Escape(state.ScrcpyPath) + "\",");
            json.AppendLine("  \"obsInstalled\": " + Bool(state.ObsInstalled) + ",");
            json.AppendLine("  \"obsVirtualCameraRegistered\": " + Bool(state.ObsVirtualCameraRegistered) + ",");
            json.AppendLine("  \"obsTemplateValid\": " + Bool(obsTemplateValid) + ",");
            json.AppendLine("  \"openScreenInstalled\": " + Bool(state.OpenScreenInstalled) + ",");
            json.AppendLine("  \"usbDevice\": " + DeviceJson(state.UsbDevice) + ",");
            json.AppendLine("  \"androidApi\": " + state.AndroidApi + ",");
            json.AppendLine("  \"androidRelease\": \"" + Escape(state.AndroidRelease) + "\",");
            json.AppendLine("  \"cameraModeCompatible\": " + Bool(state.CameraModeCompatible) + ",");
            json.AppendLine("  \"scrcpyRunning\": " + Bool(state.ScrcpyRunning) + ",");
            json.AppendLine("  \"obsRunning\": " + Bool(state.ObsRunning) + ",");
            json.AppendLine("  \"obsVirtualCameraActive\": " + Bool(state.ObsVirtualCameraActive) + ",");
            json.AppendLine("  \"summary\": \"" + Escape(state.Summary) + "\"");
            json.AppendLine("}");

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
        }

        private static string DeviceJson(AdbDevice device)
        {
            if (device == null)
            {
                return "null";
            }
            return "{\"serial\":\"" + Escape(device.Serial) + "\",\"state\":\"" + Escape(device.State) +
                   "\",\"model\":\"" + Escape(device.Model) + "\"}";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }

    internal sealed class QualityPreset
    {
        internal string DisplayName { get; set; }
        internal int Width { get; set; }
        internal int Height { get; set; }
        internal int Fps { get; set; }
        internal string Codec { get; set; }
        internal string BitRate { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }

        internal static QualityPreset Stable1080()
        {
            return new QualityPreset
            {
                DisplayName = "1080p · 30fps · 稳定低延迟",
                Width = 1920,
                Height = 1080,
                Fps = 30,
                Codec = "h264",
                BitRate = "12M"
            };
        }

        internal static QualityPreset FourK()
        {
            return new QualityPreset
            {
                DisplayName = "4K · 30fps · 最高画质",
                Width = 3840,
                Height = 2160,
                Fps = 30,
                Codec = "h265",
                BitRate = "30M"
            };
        }

        internal static IList<QualityPreset> All()
        {
            return new List<QualityPreset>
            {
                new QualityPreset { DisplayName = "720p · 30fps · 兼容档", Width = 1280, Height = 720, Fps = 30, Codec = "h264", BitRate = "8M" },
                Stable1080(),
                FourK(),
                new QualityPreset { DisplayName = "2K · 30fps · 清晰档", Width = 2560, Height = 1440, Fps = 30, Codec = "h265", BitRate = "20M" }
            };
        }
    }

    internal sealed class CameraInfo
    {
        internal string Id { get; set; }
        internal string Facing { get; set; }
        internal int MaxWidth { get; set; }
        internal int MaxHeight { get; set; }
        internal string FpsText { get; set; }
        internal string ZoomRange { get; set; }
        internal HashSet<string> Sizes { get; set; }
        internal bool Recommended { get; set; }
        internal string CustomName { get; set; }

        internal bool HasKnownSizes
        {
            get { return Sizes != null && Sizes.Count > 0; }
        }

        internal string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomName))
                {
                    return CustomName;
                }
                string facing = Facing == "front" ? "前置" : (Facing == "back" ? "后置" : "外接");
                return "ID " + Id + " · " + facing + " · 最大 " + MaxWidth + "×" + MaxHeight +
                       (Recommended ? " · 推荐主摄" : "") +
                       (string.IsNullOrEmpty(ZoomRange) ? "" : " · 变焦 " + ZoomRange + "×");
            }
        }

        internal bool Supports(int width, int height)
        {
            return !HasKnownSizes || Sizes.Contains(width + "x" + height);
        }

        public override string ToString()
        {
            return DisplayName;
        }

        internal static CameraInfo AutoBack()
        {
            return new CameraInfo
            {
                Id = null,
                Facing = "back",
                CustomName = "后置镜头（自动；建议先扫描）",
                Sizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    internal sealed class PhoneBridgeState
    {
        internal DateTime GeneratedAt { get; set; }
        internal string ScrcpyPath { get; set; }
        internal string AdbPath { get; set; }
        internal string ObsPath { get; set; }
        internal string OpenScreenPath { get; set; }
        internal bool ScrcpyAvailable { get; set; }
        internal bool ObsInstalled { get; set; }
        internal bool ObsVirtualCameraRegistered { get; set; }
        internal bool OpenScreenInstalled { get; set; }
        internal bool ScrcpyRunning { get; set; }
        internal bool ObsRunning { get; set; }
        internal bool OpenScreenRunning { get; set; }
        internal bool ObsVirtualCameraActive { get; set; }
        internal int AndroidApi { get; set; }
        internal string AndroidRelease { get; set; }
        internal bool CameraModeCompatible { get; set; }
        internal List<AdbDevice> Devices { get; set; }
        internal AdbDevice UsbDevice { get; set; }
        internal string Summary { get; set; }
    }

    internal sealed class AdbDevice
    {
        internal string Serial { get; set; }
        internal string State { get; set; }
        internal string Model { get; set; }
        internal string Product { get; set; }

        internal string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Model)) return Model;
                if (!string.IsNullOrEmpty(Product)) return Product;
                return Serial;
            }
        }
    }

    internal sealed class CameraScanResult
    {
        internal bool Success { get; private set; }
        internal string Message { get; private set; }
        internal List<CameraInfo> Cameras { get; private set; }

        internal static CameraScanResult Ok(List<CameraInfo> cameras, string message)
        {
            return new CameraScanResult { Success = true, Cameras = cameras, Message = message };
        }

        internal static CameraScanResult Fail(string message)
        {
            return new CameraScanResult { Success = false, Cameras = new List<CameraInfo>(), Message = message };
        }
    }

    internal sealed class SessionResult
    {
        internal bool Success { get; private set; }
        internal string Message { get; private set; }
        internal string ActualQuality { get; private set; }

        internal static SessionResult Ok(string message, string actualQuality)
        {
            return new SessionResult { Success = true, Message = message, ActualQuality = actualQuality };
        }

        internal static SessionResult Fail(string message)
        {
            return new SessionResult { Success = false, Message = message, ActualQuality = string.Empty };
        }
    }

    internal sealed class ScrcpyStartResult
    {
        internal bool Success { get; private set; }
        internal bool CameraInUse { get; private set; }
        internal string Message { get; private set; }

        internal static ScrcpyStartResult Ok(string message)
        {
            return new ScrcpyStartResult { Success = true, Message = message };
        }

        internal static ScrcpyStartResult Fail(string message, bool cameraInUse)
        {
            return new ScrcpyStartResult { Success = false, Message = message, CameraInUse = cameraInUse };
        }
    }

    internal sealed class OperationResult
    {
        internal bool Success { get; private set; }
        internal string Message { get; private set; }

        internal static OperationResult Ok(string message)
        {
            return new OperationResult { Success = true, Message = message };
        }

        internal static OperationResult Fail(string message)
        {
            return new OperationResult { Success = false, Message = message };
        }
    }

    internal sealed class CommandResult
    {
        internal int ExitCode { get; set; }
        internal string Output { get; set; }
        internal string Error { get; set; }

        internal string AllText
        {
            get { return (Output ?? string.Empty) + Environment.NewLine + (Error ?? string.Empty); }
        }
    }
}
