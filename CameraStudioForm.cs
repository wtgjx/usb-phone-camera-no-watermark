using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhoneUsbCamera
{
    internal sealed class CameraStudioForm : Form
    {
        private static readonly Color Canvas = Color.FromArgb(248, 249, 251);
        private static readonly Color Surface = Color.White;
        private static readonly Color TextPrimary = UCamBrand.Ink;
        private static readonly Color TextSecondary = UCamBrand.Muted;
        private static readonly Color Border = Color.FromArgb(230, 233, 238);
        private static readonly Color Primary = UCamBrand.Accent;
        private static readonly Color Blue = UCamBrand.Accent;
        private static readonly Color Success = Color.FromArgb(36, 138, 61);
        private static readonly Color SuccessBackground = Color.FromArgb(237, 247, 239);
        private static readonly Color Warning = Color.FromArgb(154, 77, 0);
        private static readonly Color WarningBackground = Color.FromArgb(255, 244, 229);
        private static readonly Color Error = Color.FromArgb(215, 0, 21);
        private static readonly Color ErrorBackground = Color.FromArgb(255, 240, 241);
        private static readonly Color PreviewBackground = Color.FromArgb(18, 18, 20);

        private readonly NativeBridgeService _bridge;
        private readonly NativePreviewHost _previewHost;
        private readonly TableLayoutPanel _root;
        private TableLayoutPanel _previewLayout;
        private Image _brandImage;
        private readonly ToolTip _toolTips = new ToolTip();
        private bool _uiDisposed;
        private readonly PreviewCanvas _previewCanvas;
        private PillLabel _topStatus;
        private Label _previewStatus;
        private Label _previewMeta;
        private RoundedPanel _devicePanel;
        private StatusDot _deviceDot;
        private Label _deviceTitle;
        private Label _deviceDetail;
        private ComboBox _cameraCombo;
        private ComboBox _qualityCombo;
        private ModernButton _scanButton;
        private ModernButton _startButton;
        private ModernButton _stopButton;
        private ModernButton _openScreenButton;
        private ModernButton _rotateLeftButton;
        private ModernButton _rotateRightButton;
        private ModernButton _mirrorButton;
        private ModernButton _fitButton;
        private ModernButton _detailsButton;
        private Label _notice;
        private RoundedPanel _detailsPanel;
        private RichTextBox _logBox;
        private bool _busy;
        private bool _detailsVisible;
        private bool _sessionRunning;
        private bool _fillPreview;
        private bool _mirrored;
        private int _rotation;
        private readonly Timer _statusTimer;
        private bool _polling;
        private bool _closing;
        private bool _closeApproved;

        internal CameraStudioForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            _bridge = new NativeBridgeService();
            _previewHost = new NativePreviewHost(_bridge);
            _statusTimer = new Timer();
            _statusTimer.Interval = 4000;
            _statusTimer.Tick += async delegate { await PollStatusAsync(); };

            Text = "U镜 · UCam";
            Icon = UCamBrand.LoadIcon();
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            MinimumSize = new Size(1000, 680);
            BackColor = Canvas;
            ForeColor = TextPrimary;
            Font = UiFont(9F, FontStyle.Regular);
            DoubleBuffered = true;

            _root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Canvas,
                Padding = new Padding(0), Margin = new Padding(0), ColumnCount = 1, RowCount = 4 };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            Controls.Add(_root);
            _root.Controls.Add(BuildHeader(), 0, 0);

            TableLayoutPanel body = new TableLayoutPanel { Name = "Workspace", Dock = DockStyle.Fill,
                BackColor = Canvas, Margin = new Padding(0), Padding = new Padding(24, 18, 24, 12),
                ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 312F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.Controls.Add(body, 0, 1);

            _previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Canvas,
                Margin = new Padding(0, 0, 22, 0), ColumnCount = 1, RowCount = 4 };
            _previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            _previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            _previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            body.Controls.Add(_previewLayout, 0, 0);

            TableLayoutPanel previewHeader = new TableLayoutPanel { Dock = DockStyle.Fill,
                Margin = new Padding(0), BackColor = Canvas, ColumnCount = 2, RowCount = 1 };
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
            previewHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _previewStatus = MakeLabel("摄像头预览", 9.3F, true);
            _previewMeta = MakeLabel("等待连接", 8.5F, false);
            _previewMeta.TextAlign = ContentAlignment.MiddleRight;
            previewHeader.Controls.Add(_previewStatus, 0, 0);
            previewHeader.Controls.Add(_previewMeta, 1, 0);
            _previewLayout.Controls.Add(previewHeader, 0, 0);

            _previewCanvas = new PreviewCanvas { Name = "CameraPreview", Dock = DockStyle.Fill,
                Margin = new Padding(0), BackColor = Canvas };
            _previewLayout.Controls.Add(_previewCanvas, 0, 1);

            FlowLayoutPanel toolbar = new FlowLayoutPanel { Name = "PreviewToolbar", Dock = DockStyle.Fill,
                BackColor = Canvas, Margin = new Padding(0), Padding = new Padding(0, 12, 0, 0),
                WrapContents = false };
            _rotateLeftButton = MakeCompactButton("左转 90°", 90);
            _rotateRightButton = MakeCompactButton("右转 90°", 90);
            _mirrorButton = MakeCompactButton("水平镜像", 90);
            _fitButton = MakeCompactButton("适应", 68);
            toolbar.Controls.AddRange(new Control[] { _rotateLeftButton, _rotateRightButton, _mirrorButton, _fitButton });
            _previewLayout.Controls.Add(toolbar, 0, 2);

            RoundedPanel sidebar = BuildSidebar();
            sidebar.Name = "DeviceInspector";
            sidebar.Margin = new Padding(0);
            body.Controls.Add(sidebar, 1, 0);
            _detailsPanel = BuildDetailsPanel();
            _detailsPanel.Margin = new Padding(0);
            _previewLayout.Controls.Add(_detailsPanel, 0, 3);

            TableLayoutPanel footer = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface,
                Margin = new Padding(0), Padding = new Padding(24, 0, 20, 0), ColumnCount = 2, RowCount = 1 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label footerText = MakeLabel("USB 直连  /  无水印  /  仅在本机处理", 8F, false);
            _detailsButton = MakeLinkButton("运行日志");
            _detailsButton.Dock = DockStyle.Fill;
            _detailsButton.Margin = new Padding(0);
            footer.Controls.Add(footerText, 0, 0);
            footer.Controls.Add(_detailsButton, 1, 0);
            _root.Controls.Add(footer, 0, 3);

            _cameraCombo.Items.Add(CameraInfo.AutoBack());
            _cameraCombo.SelectedIndex = 0;
            foreach (QualityPreset preset in QualityPreset.All())
            {
                if(preset.Width>1920) preset.DisplayName = (preset.Width==3840?"4K":"2K") + " · 实验档，帧率未保证";
                _qualityCombo.Items.Add(preset);
            }
            _qualityCombo.SelectedIndex = 1;
            _cameraCombo.SelectedIndexChanged += delegate {
                CameraInfo camera = _cameraCombo.SelectedItem as CameraInfo;
                _toolTips.SetToolTip(_cameraCombo, camera == null ? "" : camera.DisplayName);
            };
            _toolTips.SetToolTip(_qualityCombo, "1080p 为默认档。2K / 4K 帧率尚未保证。");

            _scanButton.Click += async delegate { await ScanCamerasAsync(true); };
            _startButton.Click += async delegate { await StartSessionAsync(); };
            _stopButton.Click += async delegate { await StopSessionAsync(); };
            _openScreenButton.Click += delegate { OpenScreenOnly(); };
            _rotateLeftButton.Click += delegate { ApplyTransform(PreviewCommand.RotateLeft); };
            _rotateRightButton.Click += delegate { ApplyTransform(PreviewCommand.RotateRight); };
            _mirrorButton.Click += delegate { ApplyTransform(PreviewCommand.FlipHorizontal); };
            _fitButton.Click += delegate { TogglePreviewFit(); };
            _detailsButton.Click += delegate { ToggleDetails(); };
            Move += delegate { _previewHost.UpdateLayout(); };
            Resize += delegate { _previewHost.UpdateLayout(); };
            Activated += delegate { _previewHost.UpdateLayout(); };
            FormClosing += OnFormClosing;
            Shown += async delegate
            {
                await RefreshStatusAsync(false);
                if (_bridge.LastState != null &&
                    _bridge.LastState.UsbDevice != null &&
                    _bridge.LastState.CameraModeCompatible)
                {
                    await ScanCamerasAsync(false);
                }
                _statusTimer.Start();
            };

            SetTransformButtons(false);
            ResumeLayout(true);
        }

        private static Label MakeLabel(string text, float size, bool bold)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Margin = new Padding(0),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, BackColor = Color.Transparent,
                Font = UiFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = bold ? TextPrimary : TextSecondary };
        }

        private Panel BuildHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface,
                Margin = new Padding(0), Padding = new Padding(24, 10, 24, 10), ColumnCount = 4, RowCount = 1 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156F));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Panel brand = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Surface };
            _brandImage = UCamBrand.LoadImage();
            PictureBox logo = new PictureBox { Image = _brandImage, SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(0, 4), Size = new Size(40, 40), AccessibleName = "U镜 UCam 图标" };
            Label title = MakeLabel("U镜", 16F, true);
            title.Dock = DockStyle.None; title.Location = new Point(50, 0); title.Size = new Size(110, 29);
            Label subtitle = MakeLabel("UCam", 8.5F, false);
            subtitle.Dock = DockStyle.None; subtitle.Location = new Point(51, 29); subtitle.Size = new Size(108, 18);
            brand.Controls.AddRange(new Control[] { logo, title, subtitle });
            Label workspace = MakeLabel("摄像头工作台", 9F, false);
            workspace.Padding = new Padding(16, 0, 0, 0);
            _topStatus = new PillLabel { Text = "正在检测", Anchor = AnchorStyles.None, Size = new Size(94, 28) };
            _openScreenButton = MakeSecondaryButton("打开 OpenScreen");
            _openScreenButton.Dock = DockStyle.Fill;
            _openScreenButton.Margin = new Padding(12, 5, 0, 5);
            header.Controls.Add(brand, 0, 0); header.Controls.Add(workspace, 1, 0);
            header.Controls.Add(_topStatus, 2, 0); header.Controls.Add(_openScreenButton, 3, 0);
            return header;
        }

        private RoundedPanel BuildSidebar()
        {
            RoundedPanel sidebar = new RoundedPanel { Dock = DockStyle.Fill, FillColor = Surface,
                BorderColor = Border, CornerRadius = 16, Padding = new Padding(18, 12, 18, 12),
                AutoScroll = true };
            TableLayoutPanel layout = new TableLayoutPanel { Name = "InspectorFields",
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0), BackColor = Surface, ColumnCount = 1, RowCount = 10 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            foreach (float height in new float[] { 30, 64, 66, 66, 32, 14, 48, 36, 62, 40 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            sidebar.Controls.Add(layout);
            layout.Controls.Add(MakeLabel("设备与输出", 11F, true), 0, 0);

            _devicePanel = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 8),
                FillColor = Color.FromArgb(245, 246, 248), BorderColor = Color.FromArgb(245, 246, 248),
                CornerRadius = 10, Padding = new Padding(12, 7, 12, 7) };
            TableLayoutPanel device = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent,
                ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            device.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            device.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            device.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            device.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _deviceDot = new StatusDot { Size = new Size(8, 8), Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            _deviceTitle = MakeLabel("正在检查手机", 9F, true);
            _deviceDetail = MakeLabel("请稍候", 8F, false);
            device.Controls.Add(_deviceDot, 0, 0);
            device.Controls.Add(_deviceTitle, 1, 0); device.Controls.Add(_deviceDetail, 1, 1);
            _devicePanel.Controls.Add(device);
            layout.Controls.Add(_devicePanel, 0, 1);

            _cameraCombo = MakeComboBox(); _qualityCombo = MakeComboBox();
            _cameraCombo.AccessibleName = "手机镜头"; _qualityCombo.AccessibleName = "输出画质";
            layout.Controls.Add(MakeField("手机镜头", _cameraCombo), 0, 2);
            layout.Controls.Add(MakeField("输出画质", _qualityCombo), 0, 3);
            _scanButton = MakeLinkButton("重新扫描镜头");
            _scanButton.ButtonBackColor = Surface; _scanButton.BorderColor = Surface;
            _scanButton.Dock = DockStyle.Fill; _scanButton.Margin = new Padding(0);
            layout.Controls.Add(_scanButton, 0, 4);
            Panel divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Border,
                Margin = new Padding(0, 10, 0, 9) };
            layout.Controls.Add(divider, 0, 5);
            _startButton = MakePrimaryButton("启动摄像头");
            _startButton.Dock = DockStyle.Fill; _startButton.Margin = new Padding(0, 0, 0, 6);
            layout.Controls.Add(_startButton, 0, 6);
            _stopButton = MakeSecondaryButton("停止输出");
            _stopButton.Dock = DockStyle.Fill; _stopButton.Margin = new Padding(0, 0, 0, 4);
            layout.Controls.Add(_stopButton, 0, 7);
            _notice = MakeLabel("连接手机，选择镜头，然后启动摄像头。", 8.3F, false);
            _notice.TextAlign = ContentAlignment.TopLeft; _notice.Padding = new Padding(0, 12, 0, 0);
            layout.Controls.Add(_notice, 0, 8);
            Label output = MakeLabel("Windows 设备名\nPhone USB Camera", 8F, false);
            output.BackColor = Color.FromArgb(247, 248, 250);
            output.Padding = new Padding(10, 0, 0, 0);
            layout.Controls.Add(output, 0, 9);
            return sidebar;
        }

        private RoundedPanel BuildDetailsPanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.FillColor = Surface;
            panel.BorderColor = Border;
            panel.CornerRadius = 12;
            panel.Padding = new Padding(16, 11, 16, 11);
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label title = new Label();
            title.Dock = DockStyle.Fill;
            title.Text = "运行详情";
            title.Font = UiFont(9.5F, FontStyle.Bold);
            title.ForeColor = TextPrimary;
            _logBox = new RichTextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.ReadOnly = true;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.BackColor = Surface;
            _logBox.ForeColor = Color.FromArgb(74, 74, 78);
            _logBox.Font = new Font("Consolas", 8.7F, FontStyle.Regular, GraphicsUnit.Point);
            _logBox.DetectUrls = false;
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(_logBox, 0, 1);
            panel.Controls.Add(layout);
            return panel;
        }

        private static ComboBox MakeComboBox()
        {
            return new UCamComboBox { Dock = DockStyle.Fill };
        }

        private static Control MakeField(string caption, Control control)
        {
            TableLayoutPanel field = new TableLayoutPanel();
            field.Dock = DockStyle.Fill;
            field.BackColor = Surface;
            field.ColumnCount = 1;
            field.RowCount = 2;
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            field.Margin = new Padding(0);
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.Text = caption;
            label.TextAlign = ContentAlignment.BottomLeft;
            label.Font = UiFont(8.8F, FontStyle.Regular);
            label.ForeColor = TextSecondary;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 0, 0);
            field.Controls.Add(label, 0, 0);
            field.Controls.Add(control, 0, 1);
            return field;
        }

        private static ModernButton MakePrimaryButton(string text)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.ButtonBackColor = Primary;
            button.HoverBackColor = Color.FromArgb(174, 57, 23);
            button.PressedBackColor = Color.FromArgb(148, 48, 20);
            button.ButtonForeColor = Color.White;
            button.BorderColor = Primary;
            button.CornerRadius = 9;
            button.Font = UiFont(9.7F, FontStyle.Bold);
            return button;
        }

        private static ModernButton MakeSecondaryButton(string text)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.ButtonBackColor = Surface;
            button.HoverBackColor = Color.FromArgb(247, 247, 248);
            button.PressedBackColor = Color.FromArgb(237, 237, 239);
            button.ButtonForeColor = TextPrimary;
            button.BorderColor = Border;
            button.CornerRadius = 9;
            button.Font = UiFont(9.3F, FontStyle.Regular);
            return button;
        }

        private static ModernButton MakeCompactButton(string text, int width)
        {
            ModernButton button = MakeSecondaryButton(text);
            button.Size = new Size(width, 34);
            button.Margin = new Padding(0, 0, 8, 0);
            button.CornerRadius = 8;
            button.Font = UiFont(8.8F, FontStyle.Regular);
            return button;
        }

        private static ModernButton MakeLinkButton(string text)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.ButtonBackColor = Canvas;
            button.HoverBackColor = Color.FromArgb(239, 239, 240);
            button.PressedBackColor = Color.FromArgb(232, 232, 234);
            button.ButtonForeColor = TextSecondary;
            button.BorderColor = Canvas;
            button.CornerRadius = 7;
            button.Font = UiFont(8.8F, FontStyle.Regular);
            return button;
        }

        private async Task RefreshStatusAsync(bool writeLog)
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true, "正在检查 USB、摄像头和虚拟输出…");
            try
            {
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
                ShowNotice(state.UsbDevice == null ? "插入 USB 数据线后，连接状态会自动更新。" :
                    "选择镜头和画质，然后启动摄像头。", TextSecondary);
                if (writeLog)
                {
                    AppendLog(state.Summary);
                }
            }
            catch (Exception ex)
            {
                ShowNotice("检测失败：" + ex.Message, Error);
                AppendLog("检测失败：" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task PollStatusAsync()
        {
            if (_busy || _polling || _closing || _uiDisposed) return;
            _polling = true;
            try
            {
                PhoneBridgeState state = await _bridge.InspectAsync();
                if (_busy || _closing) return;
                if (_sessionRunning && !state.NativeOutputActive)
                {
                    DetachPreview();
                    _sessionRunning = false;
                    ShowNotice("画面连接已中断，请停止本次会话后重新启动。", Warning);
                }
                ApplyState(state);
            }
            catch (Exception)
            {
                // A transient device probe must not interrupt the UI or session.
            }
            finally { _polling = false; }
        }

        private async Task ScanCamerasAsync(bool showErrors)
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true, "正在读取手机镜头…");
            try
            {
                CameraScanResult result = await _bridge.ScanCamerasAsync(AppendLog);
                if (!result.Success)
                {
                    ShowNotice(result.Message, Error);
                    AppendLog(result.Message);
                    if (showErrors)
                    {
                        ToggleDetails(true);
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
                ShowNotice("已发现 " + result.Cameras.Count + " 个可用镜头。", Success);
                AppendLog("已读取 " + result.Cameras.Count + " 个 Camera2 镜头；默认选择后置逻辑主摄。");
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
            }
            catch (Exception ex)
            {
                ShowNotice("扫描失败：" + ex.Message, Error);
                AppendLog("扫描失败：" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task StartSessionAsync()
        {
            if (_busy)
            {
                return;
            }
            CameraInfo camera = _cameraCombo.SelectedItem as CameraInfo ?? CameraInfo.AutoBack();
            QualityPreset preset = _qualityCombo.SelectedItem as QualityPreset ?? QualityPreset.Stable1080();
            SetBusy(true, "正在建立 USB 高清画面，这可能需要 20–40 秒…");
            _previewCanvas.StateText = "正在连接手机画面";
            _previewCanvas.Subtitle = "请保持手机解锁，不要拔出数据线";
            _previewCanvas.Invalidate();
            AppendLog("准备启动：" + camera.DisplayName + "，" + preset.DisplayName);
            bool cleanupAfterError = false;
            try
            {
                SessionResult result = await _bridge.StartSessionAsync(camera, preset, AppendLog, AttachNativePreview);
                AppendLog(result.Message);
                if (!result.Success)
                {
                    DetachPreview();
                    ShowNotice(result.Message, Error);
                    ToggleDetails(true);
                    return;
                }
                _sessionRunning = true;
                _previewStatus.Text = "实时画面";
                _previewMeta.Text = result.ActualQuality + " · USB";
                ShowNotice("摄像头已启动。在 OpenScreen 中选择 Phone USB Camera，无需 OBS。", Success);
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
            }
            catch (Exception ex)
            {
                DetachPreview();
                cleanupAfterError = true;
                _sessionRunning = false;
                ShowNotice("启动异常：" + ex.Message, Error);
                AppendLog("启动异常：" + ex.Message);
                ToggleDetails(true);
            }
            finally
            {
                SetBusy(false, null);
            }
            if (cleanupAfterError) await StopSessionAsync();
        }

        private void AttachNativePreview()
        {
            _previewHost.Attach(this, _previewCanvas);
            _previewCanvas.StateText = "";
            _previewCanvas.Subtitle = "";
            SetTransformButtons(true);
            AppendLog("预览和虚拟摄像头现在直接使用视频帧；最小化窗口不会停止输出。");
        }

        private async Task StopSessionAsync()
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true, "正在停止摄像头…");
            DetachPreview();
            try
            {
                OperationResult result = await Task.Run(new Func<OperationResult>(_bridge.StopSession));
                _sessionRunning = _bridge.OutputActive;
                AppendLog(result.Message);
                ShowNotice(result.Message, result.Success ? TextSecondary : Warning);
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
            }
            catch (Exception ex)
            {
                ShowNotice("停止失败：" + ex.Message, Error);
                AppendLog("停止失败：" + ex.Message);
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
            ShowNotice(result.Message, result.Success ? Success : Error);
            if (!result.Success)
            {
                ToggleDetails(true);
            }
        }

        private void ApplyState(PhoneBridgeState state)
        {
            if (state.UsbDevice == null)
            {
                SetConnectionState("等待连接手机", "插入 USB 数据线并解锁", TextSecondary, Color.FromArgb(245, 246, 248));
                _topStatus.SetColors(Color.FromArgb(242, 244, 247), TextSecondary);
                _topStatus.Text = "未连接";
            }
            else if (state.UsbDevice.State == "unauthorized")
            {
                SetConnectionState("等待手机授权", "请在手机上允许 USB 调试", Warning, WarningBackground);
                _topStatus.SetColors(WarningBackground, Warning);
                _topStatus.Text = "等待授权";
            }
            else if (!state.CameraModeCompatible)
            {
                SetConnectionState("系统版本不兼容", "Camera Mode 需要 Android 12+", Error, ErrorBackground);
                _topStatus.SetColors(ErrorBackground, Error);
                _topStatus.Text = "不兼容";
            }
            else
            {
                string detail = state.UsbDevice.DisplayName;
                if (!string.IsNullOrEmpty(state.AndroidRelease))
                {
                    detail += " · Android " + state.AndroidRelease;
                }
                SetConnectionState("手机已连接", detail, Success, SuccessBackground);
                _topStatus.SetColors(SuccessBackground, Success);
                _topStatus.Text = state.NativeOutputActive ? "正在输出" : "已连接";
            }

            if (state.NativeOutputActive)
            {
                _sessionRunning = true;
                _previewStatus.Text = "摄像头预览";
            }
            else if (!_busy)
            {
                _sessionRunning = false;
                if (!_previewHost.IsAttached)
                {
                    _previewMeta.Text = "等待连接";
                }
            }
            UpdateControlStates(state);
        }

        private void SetConnectionState(string title, string detail, Color accent, Color background)
        {
            _deviceTitle.Text = title;
            _deviceDetail.Text = detail;
            _deviceDot.DotColor = accent;
            _devicePanel.FillColor = background;
            _devicePanel.BorderColor = background;
            _devicePanel.Invalidate(true);
        }

        private void UpdateControlStates(PhoneBridgeState state)
        {
            bool connected = state != null && state.UsbDevice != null &&
                             state.UsbDevice.State == "device" && state.CameraModeCompatible;
            bool outputRunning = state != null && state.NativeReceiverRunning;
            _cameraCombo.Enabled = !_busy && !outputRunning;
            _qualityCombo.Enabled = !_busy && !outputRunning;
            _scanButton.Enabled = !_busy && !outputRunning;
            _startButton.Enabled = !_busy && connected && !outputRunning;
            _stopButton.Enabled = !_busy && (_sessionRunning || _bridge.OwnsSession);
            _openScreenButton.Enabled = !_busy;
            SetTransformButtons(!_busy && _previewHost.IsAttached);
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrEmpty(message))
            {
                ShowNotice(message, TextSecondary);
                AppendLog(message);
            }
            PhoneBridgeState state = _bridge.LastState;
            if (state != null)
            {
                UpdateControlStates(state);
            }
            else
            {
                _cameraCombo.Enabled = !busy;
                _qualityCombo.Enabled = !busy;
                _scanButton.Enabled = !busy;
                _startButton.Enabled = false;
                _stopButton.Enabled = false;
                _openScreenButton.Enabled = !busy;
                SetTransformButtons(false);
            }
        }

        private void ShowNotice(string message, Color color)
        {
            _notice.Text = message;
            _notice.ForeColor = color;
        }

        private void ApplyTransform(PreviewCommand command)
        {
            if (!_bridge.OutputActive)
            {
                ShowNotice("画面控制暂时不可用，请停止后重新启动摄像头。", Error);
                return;
            }
            if (command == PreviewCommand.RotateLeft)
            {
                _rotation = (_rotation + 270) % 360;
            }
            else if (command == PreviewCommand.RotateRight)
            {
                _rotation = (_rotation + 90) % 360;
            }
            else if (command == PreviewCommand.FlipHorizontal)
            {
                _mirrored = !_mirrored;
            }
            _bridge.SetTransform(_rotation,_mirrored);
            _mirrorButton.ButtonBackColor = _mirrored ? UCamBrand.AccentSoft : Surface;
            _mirrorButton.BorderColor = _mirrored ? Primary : Border;
            _mirrorButton.Invalidate();
            _previewMeta.Text = (_rotation == 0 ? "原始方向" : "旋转 " + _rotation + "°") +
                                (_mirrored ? " · 镜像" : "") + " · USB";
            _previewHost.UpdateLayout();
        }

        private void TogglePreviewFit()
        {
            _fillPreview = !_fillPreview;
            _previewHost.SetFillMode(_fillPreview);
            _fitButton.Text = _fillPreview ? "填满" : "适应";
            _fitButton.ButtonBackColor = _fillPreview ? UCamBrand.AccentSoft : Surface;
            _fitButton.BorderColor = _fillPreview ? Primary : Border;
            _fitButton.Invalidate();
        }

        private void SetTransformButtons(bool enabled)
        {
            _rotateLeftButton.Enabled = enabled;
            _rotateRightButton.Enabled = enabled;
            _mirrorButton.Enabled = enabled;
            _fitButton.Enabled = enabled;
        }

        private void ToggleDetails()
        {
            ToggleDetails(!_detailsVisible);
        }

        private void ToggleDetails(bool visible)
        {
            _detailsVisible = visible;
            _previewLayout.RowStyles[3].Height = visible ? 150F * DeviceDpi / 96F : 0F;
            _detailsButton.Text = visible ? "收起日志" : "运行日志";
            _detailsPanel.Visible = visible;
            _previewHost.UpdateLayout();
        }

        private void DetachPreview()
        {
            _previewHost.Detach();
            _previewHost.SetFillMode(false);
            _previewCanvas.FillFrame = false;
            _rotation = 0;
            _mirrored = false;
            _fillPreview = false;
            _previewCanvas.StateText = "让手机成为你的摄像头";
            _previewCanvas.Subtitle = "用 USB 连接安卓手机，即可开始高清拍摄";
            _previewCanvas.Invalidate();
            _previewMeta.Text = "等待连接";
            _mirrorButton.ButtonBackColor = Surface;
            _mirrorButton.BorderColor = Border;
            _fitButton.ButtonBackColor = Surface;
            _fitButton.BorderColor = Border;
            _fitButton.Text = "适应";
            SetTransformButtons(false);
        }

        private async void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (_closeApproved) return;
            if (_closing) { eventArgs.Cancel = true; return; }
            if (_busy)
            {
                eventArgs.Cancel = true;
                ShowNotice("正在处理摄像头会话，请稍候再关闭。", Warning);
                return;
            }
            _closing = true;
            eventArgs.Cancel = true;
            _statusTimer.Stop();
            _previewHost.Detach();
            SetBusy(true, "正在释放手机镜头…");
            try
            {
                OperationResult result = await Task.Run(new Func<OperationResult>(_bridge.StopSession));
                if (result.Success)
                {
                    _closeApproved = true;
                    Close();
                    return;
                }
                ShowNotice(result.Message, Warning);
            }
            catch (Exception ex) { ShowNotice("关闭前清理失败：" + ex.Message, Error); }
            _closing = false;
            SetBusy(false, null);
            _statusTimer.Start();
        }

        private void AppendLog(string message)
        {
            if (_closing || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(AppendLog), message); }
                catch (InvalidOperationException) { /* Window is being disposed. */ }
                return;
            }
            string clean = (message ?? string.Empty).Trim();
            if (clean.Length == 0)
            {
                return;
            }
            if (_logBox.TextLength > 60000) _logBox.Clear();
            _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + clean + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private static Font UiFont(float size, FontStyle style)
        {
            return UCamBrand.Font(size, style);
        }

        private static Font DisplayFont(float size, FontStyle style)
        {
            return UCamBrand.Font(size, style);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_uiDisposed)
            {
                _uiDisposed = true;
                _statusTimer.Dispose(); _previewHost.Dispose(); _toolTips.Dispose();
                if (_brandImage != null) _brandImage.Dispose();
                if (Icon != null) Icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        internal Color FillColor { get; set; }
        internal Color BorderColor { get; set; }
        internal int CornerRadius { get; set; }

        internal RoundedPanel()
        {
            FillColor = Color.White;
            BorderColor = Color.FromArgb(229, 229, 231);
            CornerRadius = 12;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            base.OnPaintBackground(eventArgs);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = UiDrawing.RoundRect(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class ModernButton : Button
    {
        private bool _hover;
        private bool _pressed;

        internal Color ButtonBackColor { get; set; }
        internal Color HoverBackColor { get; set; }
        internal Color PressedBackColor { get; set; }
        internal Color ButtonForeColor { get; set; }
        internal Color BorderColor { get; set; }
        internal int CornerRadius { get; set; }

        internal ModernButton()
        {
            ButtonBackColor = Color.White;
            HoverBackColor = Color.FromArgb(247, 247, 248);
            PressedBackColor = Color.FromArgb(237, 237, 239);
            ButtonForeColor = Color.FromArgb(29, 29, 31);
            BorderColor = Color.FromArgb(229, 229, 231);
            CornerRadius = 8;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(eventArgs);
        }

        protected override void OnEnabledChanged(EventArgs eventArgs)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color fill = _pressed ? PressedBackColor : (_hover ? HoverBackColor : ButtonBackColor);
            Color fore = Enabled ? ButtonForeColor : Color.FromArgb(168, 168, 172);
            Color border = Enabled ? BorderColor : Color.FromArgb(237, 237, 239);
            using (GraphicsPath path = UiDrawing.RoundRect(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(Enabled ? fill : Color.FromArgb(247, 247, 248)))
            using (Pen pen = new Pen(border))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Text,
                Font,
                bounds,
                fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, fore, Color.Transparent);
            }
        }
    }

    internal sealed class PreviewCanvas : Panel
    {
        private Bitmap _frame;
        private readonly Image _logo;
        internal bool FillFrame { get; set; }
        internal string StateText { get; set; }
        internal string Subtitle { get; set; }
        internal void SetFrame(Bitmap frame) { Bitmap old=_frame; _frame=frame; if(old!=null) old.Dispose(); Invalidate(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { SetFrame(null); _logo.Dispose(); }
            base.Dispose(disposing);
        }
        internal PreviewCanvas()
        {
            _logo = UCamBrand.LoadImage();
            StateText = "让手机成为你的摄像头";
            Subtitle = "用 USB 连接安卓手机，即可开始高清拍摄";
            AccessibleName = "摄像头实时预览";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            float dpi = DeviceDpi / 96F;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
            using (GraphicsPath path = UiDrawing.RoundRect(bounds,(int)(14*dpi)))
            {
                using (Brush fill = new SolidBrush(_frame == null ? Color.White : Color.FromArgb(22,25,31)))
                    g.FillPath(fill,path);
                if (_frame != null)
                {
                    GraphicsState state = g.Save();
                    g.SetClip(path);
                    double scale = FillFrame ? Math.Max((double)Width/_frame.Width,(double)Height/_frame.Height)
                        : Math.Min((double)Width/_frame.Width,(double)Height/_frame.Height);
                    int w=(int)(_frame.Width*scale),h=(int)(_frame.Height*scale);
                    g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                    g.DrawImage(_frame,new Rectangle((Width-w)/2,(Height-h)/2,w,h));
                    g.Restore(state);
                }
                else if (!string.IsNullOrEmpty(StateText))
                {
                    bool roomy = Height >= 290*dpi;
                    int centerY=Height/2;
                    if (roomy)
                    {
                        int icon=(int)(72*dpi);
                        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                        g.DrawImage(_logo,new Rectangle((Width-icon)/2,centerY-(int)(102*dpi),icon,icon));
                    }
                    int titleY = centerY - (int)(roomy ? 8*dpi : 36*dpi);
                    Rectangle title = new Rectangle((int)(20*dpi),titleY,Width-(int)(40*dpi),(int)(34*dpi));
                    Rectangle description = new Rectangle(title.X,title.Bottom+(int)(4*dpi),title.Width,(int)(42*dpi));
                    using (Font font = UCamBrand.Font(13F,FontStyle.Bold))
                        TextRenderer.DrawText(g,StateText,font,title,UCamBrand.Ink,
                            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                    using (Font font = UCamBrand.Font(9F,FontStyle.Regular))
                        TextRenderer.DrawText(g,Subtitle,font,description,UCamBrand.Muted,
                            TextFormatFlags.HorizontalCenter|TextFormatFlags.Top|TextFormatFlags.WordBreak);
                    if (roomy)
                    {
                        Rectangle steps = new Rectangle(title.X,Height-(int)(46*dpi),title.Width,(int)(24*dpi));
                        using (Font font = UCamBrand.Font(8F,FontStyle.Regular))
                            TextRenderer.DrawText(g,"连接数据线   ·   允许 USB 调试   ·   启动摄像头",font,steps,
                                UCamBrand.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                    }
                }
                using (Pen pen = new Pen(Color.FromArgb(232,235,240))) g.DrawPath(pen,path);
            }
        }
    }

    internal sealed class PillLabel : Label
    {
        internal Color PillBackColor { get; set; }
        internal Color PillForeColor { get; set; }

        internal PillLabel()
        {
            PillBackColor = Color.FromArgb(241, 241, 243);
            PillForeColor = Color.FromArgb(110, 110, 115);
            Font = UCamBrand.Font(8F, FontStyle.Regular);
            TextAlign = ContentAlignment.MiddleCenter;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        internal void SetColors(Color back, Color fore)
        {
            PillBackColor = back;
            PillForeColor = fore;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = UiDrawing.RoundRect(bounds, Height / 2))
            using (SolidBrush brush = new SolidBrush(PillBackColor))
            {
                eventArgs.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font, bounds, PillForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class StatusDot : Control
    {
        internal Color DotColor { get; set; }

        internal StatusDot()
        {
            DotColor = Color.FromArgb(110, 110, 115);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(DotColor))
            {
                eventArgs.Graphics.FillEllipse(brush, 1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            }
        }
    }

    internal enum PreviewCommand
    {
        RotateLeft,
        RotateRight,
        FlipHorizontal
    }

    internal static class PreviewShortcutSender
    {
        private const uint WmKeyDown = 0x0100;
        private const uint WmKeyUp = 0x0101;
        private const int VkLControl = 0xA2;
        private const int VkShift = 0x10;
        private const int VkLeft = 0x25;
        private const int VkRight = 0x27;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        internal static bool Send(IntPtr window, PreviewCommand command)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }
            int arrow = command == PreviewCommand.RotateLeft ? VkLeft : VkRight;
            bool shift = command == PreviewCommand.FlipHorizontal;
            bool ok = PostMessage(window, WmKeyDown, new IntPtr(VkLControl), IntPtr.Zero);
            if (shift)
            {
                ok = PostMessage(window, WmKeyDown, new IntPtr(VkShift), IntPtr.Zero) && ok;
            }
            ok = PostMessage(window, WmKeyDown, new IntPtr(arrow), IntPtr.Zero) && ok;
            ok = PostMessage(window, WmKeyUp, new IntPtr(arrow), new IntPtr(0xC0000000)) && ok;
            if (shift)
            {
                ok = PostMessage(window, WmKeyUp, new IntPtr(VkShift), new IntPtr(0xC0000000)) && ok;
            }
            ok = PostMessage(window, WmKeyUp, new IntPtr(VkLControl), new IntPtr(0xC0000000)) && ok;
            return ok;
        }
    }

    internal static class UiDrawing
    {
        internal static GraphicsPath RoundRect(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            if (rectangle.Width <= diameter || rectangle.Height <= diameter)
            {
                path.AddRectangle(rectangle);
                path.CloseFigure();
                return path;
            }
            Rectangle arc = new Rectangle(rectangle.X, rectangle.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rectangle, int radius)
        {
            using (GraphicsPath path = RoundRect(rectangle, radius))
            {
                graphics.DrawPath(pen, path);
            }
        }
    }
}
