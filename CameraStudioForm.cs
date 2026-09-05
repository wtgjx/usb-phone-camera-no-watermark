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
        private static readonly Color Canvas = Color.FromArgb(247, 247, 245);
        private static readonly Color Surface = Color.White;
        private static readonly Color TextPrimary = Color.FromArgb(29, 29, 31);
        private static readonly Color TextSecondary = Color.FromArgb(110, 110, 115);
        private static readonly Color Border = Color.FromArgb(229, 229, 231);
        private static readonly Color Primary = Color.FromArgb(29, 29, 31);
        private static readonly Color Blue = Color.FromArgb(0, 113, 227);
        private static readonly Color Success = Color.FromArgb(36, 138, 61);
        private static readonly Color SuccessBackground = Color.FromArgb(237, 247, 239);
        private static readonly Color Warning = Color.FromArgb(154, 77, 0);
        private static readonly Color WarningBackground = Color.FromArgb(255, 244, 229);
        private static readonly Color Error = Color.FromArgb(215, 0, 21);
        private static readonly Color ErrorBackground = Color.FromArgb(255, 240, 241);
        private static readonly Color PreviewBackground = Color.FromArgb(18, 18, 20);

        private readonly BridgeService _bridge;
        private readonly IntegratedPreviewHost _previewHost;
        private readonly TableLayoutPanel _root;
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

        internal CameraStudioForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            _bridge = new BridgeService();
            _previewHost = new IntegratedPreviewHost();
            _statusTimer = new Timer();
            _statusTimer.Interval = 4000;
            _statusTimer.Tick += async delegate { await PollStatusAsync(); };

            Text = "USB 手机摄像头";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 790);
            MinimumSize = new Size(1040, 680);
            BackColor = Canvas;
            ForeColor = TextPrimary;
            Font = UiFont(9.5F, FontStyle.Regular);
            DoubleBuffered = true;

            _root = new TableLayoutPanel();
            _root.Dock = DockStyle.Fill;
            _root.BackColor = Canvas;
            _root.Padding = new Padding(28, 20, 28, 14);
            _root.ColumnCount = 1;
            _root.RowCount = 4;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            Controls.Add(_root);

            Panel header = BuildHeader();
            _root.Controls.Add(header, 0, 0);

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Canvas;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.Controls.Add(body, 0, 1);

            RoundedPanel previewCard = new RoundedPanel();
            previewCard.Dock = DockStyle.Fill;
            previewCard.Margin = new Padding(0, 0, 10, 10);
            previewCard.FillColor = Surface;
            previewCard.BorderColor = Border;
            previewCard.CornerRadius = 14;
            previewCard.Padding = new Padding(18);
            body.Controls.Add(previewCard, 0, 0);

            TableLayoutPanel previewLayout = new TableLayoutPanel();
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.BackColor = Surface;
            previewLayout.ColumnCount = 1;
            previewLayout.RowCount = 3;
            previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            previewCard.Controls.Add(previewLayout);

            TableLayoutPanel previewHeader = new TableLayoutPanel();
            previewHeader.Dock = DockStyle.Fill;
            previewHeader.BackColor = Surface;
            previewHeader.ColumnCount = 2;
            previewHeader.RowCount = 1;
            previewHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            _previewStatus = new Label();
            _previewStatus.Dock = DockStyle.Fill;
            _previewStatus.Text = "实时画面";
            _previewStatus.TextAlign = ContentAlignment.MiddleLeft;
            _previewStatus.Font = UiFont(11.5F, FontStyle.Bold);
            _previewStatus.ForeColor = TextPrimary;
            _previewMeta = new Label();
            _previewMeta.Dock = DockStyle.Fill;
            _previewMeta.Text = "等待连接";
            _previewMeta.TextAlign = ContentAlignment.MiddleRight;
            _previewMeta.Font = UiFont(9F, FontStyle.Regular);
            _previewMeta.ForeColor = TextSecondary;
            previewHeader.Controls.Add(_previewStatus, 0, 0);
            previewHeader.Controls.Add(_previewMeta, 1, 0);
            previewLayout.Controls.Add(previewHeader, 0, 0);

            _previewCanvas = new PreviewCanvas();
            _previewCanvas.Dock = DockStyle.Fill;
            _previewCanvas.Margin = new Padding(0);
            _previewCanvas.BackColor = PreviewBackground;
            previewLayout.Controls.Add(_previewCanvas, 0, 1);

            TableLayoutPanel toolbar = new TableLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = Surface;
            toolbar.ColumnCount = 2;
            toolbar.RowCount = 1;
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            FlowLayoutPanel transformButtons = new FlowLayoutPanel();
            transformButtons.Dock = DockStyle.Fill;
            transformButtons.FlowDirection = FlowDirection.LeftToRight;
            transformButtons.WrapContents = false;
            transformButtons.Padding = new Padding(0, 13, 0, 0);
            transformButtons.BackColor = Surface;
            _rotateLeftButton = MakeCompactButton("左转 90°", 92);
            _rotateRightButton = MakeCompactButton("右转 90°", 92);
            _mirrorButton = MakeCompactButton("水平镜像", 96);
            _fitButton = MakeCompactButton("适应", 70);
            transformButtons.Controls.Add(_rotateLeftButton);
            transformButtons.Controls.Add(_rotateRightButton);
            transformButtons.Controls.Add(_mirrorButton);
            transformButtons.Controls.Add(_fitButton);
            Label privacy = new Label();
            privacy.Dock = DockStyle.Fill;
            privacy.Text = "仅在本机处理";
            privacy.TextAlign = ContentAlignment.MiddleRight;
            privacy.Font = UiFont(8.8F, FontStyle.Regular);
            privacy.ForeColor = TextSecondary;
            toolbar.Controls.Add(transformButtons, 0, 0);
            toolbar.Controls.Add(privacy, 1, 0);
            previewLayout.Controls.Add(toolbar, 0, 2);

            RoundedPanel sidebar = BuildSidebar();
            sidebar.Margin = new Padding(10, 0, 0, 10);
            body.Controls.Add(sidebar, 1, 0);

            _detailsPanel = BuildDetailsPanel();
            _detailsPanel.Margin = new Padding(0, 2, 0, 10);
            _root.Controls.Add(_detailsPanel, 0, 2);

            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = Canvas;
            footer.ColumnCount = 2;
            footer.RowCount = 1;
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            Label footerText = new Label();
            footerText.Dock = DockStyle.Fill;
            footerText.Text = "无水印 · USB 直连 · 画面仅在本机处理";
            footerText.TextAlign = ContentAlignment.MiddleLeft;
            footerText.Font = UiFont(8.7F, FontStyle.Regular);
            footerText.ForeColor = TextSecondary;
            _detailsButton = MakeLinkButton("查看运行详情");
            _detailsButton.Dock = DockStyle.Right;
            _detailsButton.Width = 132;
            footer.Controls.Add(footerText, 0, 0);
            footer.Controls.Add(_detailsButton, 1, 0);
            _root.Controls.Add(footer, 0, 3);

            _cameraCombo.Items.Add(CameraInfo.AutoBack());
            _cameraCombo.SelectedIndex = 0;
            foreach (QualityPreset preset in QualityPreset.All())
            {
                _qualityCombo.Items.Add(preset);
            }
            _qualityCombo.SelectedIndex = 2;

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

        private Panel BuildHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Canvas;
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));

            Panel brand = new Panel();
            brand.Dock = DockStyle.Fill;
            brand.BackColor = Canvas;
            Label title = new Label();
            title.AutoSize = true;
            title.Text = "USB 手机摄像头";
            title.Location = new Point(0, 0);
            title.Font = DisplayFont(21F, FontStyle.Bold);
            title.ForeColor = TextPrimary;
            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Text = "把安卓手机的高清镜头变成无水印电脑摄像头";
            subtitle.Location = new Point(2, 45);
            subtitle.Font = UiFont(9.3F, FontStyle.Regular);
            subtitle.ForeColor = TextSecondary;
            brand.Controls.Add(title);
            brand.Controls.Add(subtitle);

            Panel statusHolder = new Panel();
            statusHolder.Dock = DockStyle.Fill;
            statusHolder.BackColor = Canvas;
            _topStatus = new PillLabel();
            _topStatus.Text = "正在检测";
            _topStatus.Size = new Size(104, 30);
            _topStatus.Location = new Point(statusHolder.Width - 104, 8);
            _topStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _topStatus.PillBackColor = Color.FromArgb(241, 241, 243);
            _topStatus.PillForeColor = TextSecondary;
            statusHolder.Controls.Add(_topStatus);

            header.Controls.Add(brand, 0, 0);
            header.Controls.Add(statusHolder, 1, 0);
            return header;
        }

        private RoundedPanel BuildSidebar()
        {
            RoundedPanel sidebar = new RoundedPanel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.FillColor = Surface;
            sidebar.BorderColor = Border;
            sidebar.CornerRadius = 14;
            sidebar.Padding = new Padding(20, 16, 20, 16);
            sidebar.AutoScroll = true;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.BackColor = Surface;
            layout.ColumnCount = 1;
            layout.RowCount = 11;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            sidebar.Controls.Add(layout);

            Label heading = new Label();
            heading.Dock = DockStyle.Fill;
            heading.Text = "连接与输出";
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Font = UiFont(12F, FontStyle.Bold);
            heading.ForeColor = TextPrimary;
            layout.Controls.Add(heading, 0, 0);

            _devicePanel = new RoundedPanel();
            _devicePanel.Dock = DockStyle.Fill;
            _devicePanel.Margin = new Padding(0, 0, 0, 10);
            _devicePanel.FillColor = Color.FromArgb(246, 246, 248);
            _devicePanel.BorderColor = Color.FromArgb(246, 246, 248);
            _devicePanel.CornerRadius = 10;
            _deviceDot = new StatusDot();
            _deviceDot.Location = new Point(14, 18);
            _deviceDot.Size = new Size(10, 10);
            _deviceDot.DotColor = TextSecondary;
            _deviceTitle = new Label();
            _deviceTitle.AutoSize = true;
            _deviceTitle.Location = new Point(34, 9);
            _deviceTitle.Text = "正在检查手机";
            _deviceTitle.Font = UiFont(9.5F, FontStyle.Bold);
            _deviceTitle.ForeColor = TextPrimary;
            _deviceDetail = new Label();
            _deviceDetail.AutoEllipsis = true;
            _deviceDetail.Location = new Point(34, 31);
            _deviceDetail.Size = new Size(245, 18);
            _deviceDetail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _deviceDetail.Text = "请稍候";
            _deviceDetail.Font = UiFont(8.6F, FontStyle.Regular);
            _deviceDetail.ForeColor = TextSecondary;
            _devicePanel.Controls.Add(_deviceDot);
            _devicePanel.Controls.Add(_deviceTitle);
            _devicePanel.Controls.Add(_deviceDetail);
            layout.Controls.Add(_devicePanel, 0, 1);

            _cameraCombo = MakeComboBox();
            _qualityCombo = MakeComboBox();
            layout.Controls.Add(MakeField("手机镜头", _cameraCombo), 0, 2);
            layout.Controls.Add(MakeField("输出画质", _qualityCombo), 0, 3);

            _scanButton = MakeSecondaryButton("重新扫描手机镜头");
            _scanButton.Dock = DockStyle.Fill;
            _scanButton.Margin = new Padding(0, 2, 0, 2);
            layout.Controls.Add(_scanButton, 0, 4);

            Panel divider = new Panel();
            divider.Dock = DockStyle.Top;
            divider.Height = 1;
            divider.Margin = new Padding(0, 8, 0, 9);
            divider.BackColor = Border;
            layout.Controls.Add(divider, 0, 5);

            _startButton = MakePrimaryButton("启动摄像头");
            _startButton.Dock = DockStyle.Fill;
            _startButton.Margin = new Padding(0, 1, 0, 5);
            layout.Controls.Add(_startButton, 0, 6);

            _stopButton = MakeSecondaryButton("停止本次会话");
            _stopButton.Dock = DockStyle.Fill;
            _stopButton.Margin = new Padding(0, 3, 0, 3);
            layout.Controls.Add(_stopButton, 0, 7);

            _openScreenButton = MakeSecondaryButton("打开 OpenScreen");
            _openScreenButton.Dock = DockStyle.Fill;
            _openScreenButton.Margin = new Padding(0, 3, 0, 3);
            _openScreenButton.ForeColor = Blue;
            layout.Controls.Add(_openScreenButton, 0, 8);

            _notice = new Label();
            _notice.Dock = DockStyle.Fill;
            _notice.Padding = new Padding(2, 12, 2, 0);
            _notice.Text = "连接数据线并在手机上允许 USB 调试。启动后，在 OpenScreen 中选择 OBS Virtual Camera。";
            _notice.Font = UiFont(8.8F, FontStyle.Regular);
            _notice.ForeColor = TextSecondary;
            layout.Controls.Add(_notice, 0, 9);

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
            ComboBox combo = new ComboBox();
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Color.FromArgb(248, 248, 250);
            combo.ForeColor = TextPrimary;
            combo.Font = UiFont(9.4F, FontStyle.Regular);
            combo.IntegralHeight = false;
            combo.DropDownHeight = 220;
            combo.DropDownWidth = 560;
            return combo;
        }

        private static Control MakeField(string caption, Control control)
        {
            TableLayoutPanel field = new TableLayoutPanel();
            field.Dock = DockStyle.Fill;
            field.BackColor = Surface;
            field.ColumnCount = 1;
            field.RowCount = 2;
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
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
            button.HoverBackColor = Color.FromArgb(52, 52, 55);
            button.PressedBackColor = Color.FromArgb(8, 8, 9);
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
            button.ButtonForeColor = Blue;
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
            if (_busy || _polling || _closing) return;
            _polling = true;
            try
            {
                PhoneBridgeState state = await _bridge.InspectAsync();
                if (_busy || _closing) return;
                if (_sessionRunning && (!state.ScrcpyRunning || !state.ObsVirtualCameraActive))
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
            try
            {
                SessionResult result = await _bridge.StartSessionAsync(camera, preset, AppendLog, AttachPreviewFromWorker);
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
                ShowNotice("摄像头已启动。请在 OpenScreen 中选择 OBS Virtual Camera。", Success);
                PhoneBridgeState state = await _bridge.InspectAsync();
                ApplyState(state);
            }
            catch (Exception ex)
            {
                DetachPreview();
                _bridge.StopSession();
                _sessionRunning = false;
                ShowNotice("启动异常：" + ex.Message, Error);
                AppendLog("启动异常：" + ex.Message);
                ToggleDetails(true);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void AttachPreviewFromWorker(int processId)
        {
            Action attach = delegate
            {
                bool attached = _previewHost.Attach(this, _previewCanvas, processId);
                if (attached)
                {
                    _previewCanvas.StateText = "";
                    _previewCanvas.Subtitle = "";
                    _previewCanvas.Invalidate();
                    SetTransformButtons(true);
                    AppendLog("手机画面已合并到主界面；后台高清源窗口已从任务栏隐藏。");
                }
                else
                {
                    AppendLog("未能立即合并实时预览；OBS 高清输出仍会继续启动。");
                }
            };
            if (InvokeRequired)
            {
                Invoke(attach);
            }
            else
            {
                attach();
            }
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
                _sessionRunning = false;
                AppendLog(result.Message);
                ShowNotice("摄像头会话已停止，OpenScreen 保持打开。", TextSecondary);
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
                SetConnectionState("未连接手机", "插入 USB 数据线并解锁手机", Error, ErrorBackground);
                _topStatus.SetColors(ErrorBackground, Error);
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
                _topStatus.Text = state.ObsVirtualCameraActive ? "正在输出" : "已连接";
            }

            if (state.ObsVirtualCameraActive)
            {
                _sessionRunning = true;
                _previewStatus.Text = "实时画面";
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
            _devicePanel.Invalidate();
        }

        private void UpdateControlStates(PhoneBridgeState state)
        {
            bool connected = state != null && state.UsbDevice != null &&
                             state.UsbDevice.State == "device" && state.CameraModeCompatible;
            bool outputRunning = state != null && (state.ScrcpyRunning || state.ObsVirtualCameraActive);
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
            IntPtr source = _previewHost.SourceHandle;
            if (source == IntPtr.Zero || !PreviewShortcutSender.Send(source, command))
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
            _mirrorButton.ButtonBackColor = _mirrored ? Color.FromArgb(231, 241, 252) : Surface;
            _mirrorButton.BorderColor = _mirrored ? Color.FromArgb(163, 204, 244) : Border;
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
            _fitButton.ButtonBackColor = _fillPreview ? Color.FromArgb(231, 241, 252) : Surface;
            _fitButton.BorderColor = _fillPreview ? Color.FromArgb(163, 204, 244) : Border;
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
            _root.RowStyles[2].Height = visible ? 150F : 0F;
            _detailsButton.Text = visible ? "收起运行详情" : "查看运行详情";
            _detailsPanel.Visible = visible;
            _previewHost.UpdateLayout();
        }

        private void DetachPreview()
        {
            _previewHost.Detach();
            _previewHost.SetFillMode(false);
            _rotation = 0;
            _mirrored = false;
            _fillPreview = false;
            _previewCanvas.StateText = "连接手机后，实时画面会显示在这里";
            _previewCanvas.Subtitle = "无需单独打开第二个预览窗口";
            _previewCanvas.Invalidate();
            _previewMeta.Text = "等待连接";
            _mirrorButton.ButtonBackColor = Surface;
            _mirrorButton.BorderColor = Border;
            _fitButton.ButtonBackColor = Surface;
            _fitButton.BorderColor = Border;
            _fitButton.Text = "适应";
            SetTransformButtons(false);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (_busy)
            {
                eventArgs.Cancel = true;
                ShowNotice("正在处理摄像头会话，请稍候再关闭。", Warning);
                return;
            }
            _closing = true;
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _previewHost.Dispose();
            _bridge.StopSession();
        }

        private void AppendLog(string message)
        {
            if (_closing || IsDisposed || !IsHandleCreated) return;
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

        private static Font UiFont(float size, FontStyle style)
        {
            return new Font("Segoe UI Variable Text", size, style, GraphicsUnit.Point);
        }

        private static Font DisplayFont(float size, FontStyle style)
        {
            return new Font("Segoe UI Variable Display", size, style, GraphicsUnit.Point);
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
        internal string StateText { get; set; }
        internal string Subtitle { get; set; }

        internal PreviewCanvas()
        {
            StateText = "连接手机后，实时画面会显示在这里";
            Subtitle = "无需单独打开第二个预览窗口";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = UiDrawing.RoundRect(bounds, 12))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(18, 18, 20)))
            {
                eventArgs.Graphics.FillPath(background, path);
            }
            if (string.IsNullOrEmpty(StateText))
            {
                return;
            }
            int centerX = Width / 2;
            int centerY = Height / 2 - 22;
            using (Pen pen = new Pen(Color.FromArgb(132, 132, 138), 2F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                Rectangle body = new Rectangle(centerX - 26, centerY - 15, 43, 30);
                eventArgs.Graphics.DrawRoundedRectangle(pen, body, 7);
                Point[] lens = new Point[]
                {
                    new Point(centerX + 17, centerY - 8),
                    new Point(centerX + 31, centerY - 15),
                    new Point(centerX + 31, centerY + 15),
                    new Point(centerX + 17, centerY + 8)
                };
                eventArgs.Graphics.DrawPolygon(pen, lens);
            }
            Rectangle titleBounds = new Rectangle(20, centerY + 34, Math.Max(1, Width - 40), 26);
            using (Font titleFont = CameraStudioFormFont(10F, FontStyle.Bold))
            {
                TextRenderer.DrawText(eventArgs.Graphics, StateText, titleFont, titleBounds,
                    Color.FromArgb(242, 242, 247), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            Rectangle subtitleBounds = new Rectangle(20, centerY + 60, Math.Max(1, Width - 40), 23);
            using (Font subtitleFont = CameraStudioFormFont(8.8F, FontStyle.Regular))
            {
                TextRenderer.DrawText(eventArgs.Graphics, Subtitle, subtitleFont, subtitleBounds,
                    Color.FromArgb(152, 152, 157), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static Font CameraStudioFormFont(float size, FontStyle style)
        {
            return new Font("Segoe UI Variable Text", size, style, GraphicsUnit.Point);
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
            Font = new Font("Segoe UI Variable Text", 8.8F, FontStyle.Bold, GraphicsUnit.Point);
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
