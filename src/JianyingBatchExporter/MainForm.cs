using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JianyingBatchExporter;

public sealed class MainForm : Form
{
    private static readonly Color AppBackground = Color.FromArgb(14, 16, 20);
    private static readonly Color SidebarBackground = Color.FromArgb(10, 12, 15);
    private static readonly Color Surface = Color.FromArgb(24, 27, 33);
    private static readonly Color SurfaceRaised = Color.FromArgb(34, 38, 45);
    private static readonly Color SurfaceHover = Color.FromArgb(45, 50, 58);
    private static readonly Color Border = Color.FromArgb(62, 69, 80);
    private static readonly Color Accent = Color.FromArgb(20, 205, 187);
    private static readonly Color AccentText = Color.FromArgb(4, 31, 28);
    private static readonly Color PrimaryText = Color.FromArgb(242, 244, 247);
    private static readonly Color SecondaryText = Color.FromArgb(160, 165, 175);
    private static readonly Color Danger = Color.FromArgb(202, 76, 83);
    private static readonly int[] VideoBitrateOptions = [500, 800, 1000, 1500, 1600, 2000, 2500, 3000, 4000, 5000, 6000, 8000];
    private static readonly int[] AudioBitrateOptions = [64, 96, 128, 160, 192, 256, 320];

    private readonly DraftService _draftService = new();
    private readonly FolderBatchService _folderBatchService = new();
    private readonly FfmpegService _ffmpegService = new();
    private readonly JianyingAutomationService _automation = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JianyingBatchExporter",
        "settings.json");

    private AppSettings _settings = new();
    private WorkspaceMode _workspaceMode = WorkspaceMode.FolderBatch;
    private DraftPlan? _draftPlan;
    private IReadOnlyList<BatchVideoItem> _batchItems = Array.Empty<BatchVideoItem>();
    private CancellationTokenSource? _runCancellation;
    private bool _refreshingDrafts;

    private readonly NavigationButton _folderNav = NewNavButton("▣   视频批量");
    private readonly NavigationButton _draftNav = NewNavButton("◫   剪映草稿");
    private readonly NavigationButton _helpButton = NewNavButton("？   使用说明");
    private readonly Label _pageTitle = NewLabel("视频批量处理", 18F, FontStyle.Bold, PrimaryText);
    private readonly Label _pageSubtitle = NewLabel("把输入目录中的全部视频统一转换为竖版成品", 9.5F, FontStyle.Regular, SecondaryText);
    private readonly DarkScrollPanel _settingsScroll = new();
    private readonly Image? _appIconImage = LoadAppIcon();

    private readonly RoundedPanel _folderSourceCard = NewCard(382, 174);
    private readonly RoundedPanel _draftSourceCard = NewCard(382, 258);
    private readonly RoundedPanel _calibrationCard = NewCard(382, 338);
    private readonly RoundedPanel _visualEffectsCard = NewCard(382, 536);
    private readonly RoundedTextBox _inputFolder = NewTextBox();
    private readonly RoundedTextBox _outputFolder = NewTextBox();
    private readonly DarkComboBox _draftCombo = NewCombo();
    private readonly DarkComboBox _trackCombo = NewCombo();
    private readonly Label _canvasLabel = NewSmallLabel("尚未读取草稿");
    private readonly Label _draftCompositionLabel = NewSmallLabel("原生合成内容：尚未检测");
    private readonly RoundButton _scanFolderButton = NewButton("扫描视频", Accent, AccentText);
    private readonly RoundButton _scanDraftButton = NewButton("刷新草稿", SurfaceHover, PrimaryText);

    private readonly AccentRadioButton _centerCropRadio = NewRadio("居中裁切");
    private readonly AccentRadioButton _blurRadio = NewRadio("模糊背景填充");
    private readonly AccentRadioButton _blackBarsRadio = NewRadio("完整画面加黑边");
    private readonly DarkComboBox _rotationDirectionCombo = NewCombo();
    private readonly RoundedTextBox _rotationDegrees = NewTextBox();
    private readonly DarkComboBox _zoomCombo = NewCombo();
    private readonly DarkComboBox _filterCombo = NewCombo();
    private readonly SpeedDropDown _speedPreset = NewSpeedDropDown();
    private readonly DarkComboBox _compressionPresetCombo = NewCombo();
    private readonly DarkComboBox _videoBitrateCombo = NewCombo();
    private readonly DarkComboBox _audioBitrateCombo = NewCombo();
    private readonly RoundedTextBox _customSpeed = NewTextBox();
    private readonly AccentCheckBox _watermarkEnabled = NewCheckBox("为所有成品视频叠加图片水印");
    private readonly RoundedTextBox _watermarkPath = NewTextBox();
    private readonly RoundButton _browseWatermarkButton = NewButton("浏览", SurfaceHover, PrimaryText);
    private RoundedPanel? _centerCropOption;
    private RoundedPanel? _blurOption;
    private RoundedPanel? _blackBarsOption;

    private readonly Label _windowStatus = NewSmallLabel("正在检测剪映窗口…");
    private readonly Label _draftStatus = NewSmallLabel("正在检测剪映草稿…");
    private readonly Label _ffmpegStatus = NewSmallLabel("正在检测 FFmpeg…");
    private readonly Label _timelinePointLabel = NewSmallLabel("未标定");
    private readonly Label _fileNamePointLabel = NewSmallLabel("未标定");
    private readonly Label _exportPointLabel = NewSmallLabel("未标定");

    private readonly Label _taskTitle = NewLabel("待处理视频", 11F, FontStyle.Bold, PrimaryText);
    private readonly Label _taskSummary = NewSmallLabel("请选择输入目录并扫描");
    private readonly DataGridView _grid = new();
    private readonly DarkRichTextBox _logBox = new();
    private readonly SlimProgressBar _progress = new();
    private readonly Label _progressText = NewSmallLabel("准备就绪");
    private readonly RoundButton _clearRecordsButton = NewButton("清理记录", SurfaceHover, SecondaryText);
    private readonly RoundButton _previewButton = NewButton("预览首段", SurfaceHover, PrimaryText);
    private readonly RoundButton _startButton = NewButton("开始批量处理", Accent, AccentText);
    private readonly RoundButton _stopButton = NewButton("停止", Danger, Color.White);
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };

    public MainForm()
    {
        Text = "批量处理助手";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 760);
        Size = new Size(1440, 900);
        WindowState = FormWindowState.Maximized;
        BackColor = AppBackground;
        ForeColor = PrimaryText;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        BuildFolderSourceCard();
        BuildDraftSourceCard();
        BuildCalibrationCard();
        Controls.Add(BuildLayout());
        ConfigureGrid();
        ConfigureLog();
        WireEvents();
        LoadSettings();
        ApplySettingsToUi();
        UpdatePreflightStatus();
        SwitchWorkspace(WorkspaceMode.FolderBatch);
        _stopButton.Enabled = false;

        Shown += async (_, _) =>
        {
            await RefreshDraftsAsync(false);
            if (Directory.Exists(_settings.InputFolder)) ScanInputFolder(false);
        };
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 272));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 1, 0);
        return root;
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SidebarBackground,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18, 0, 18, 22)
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));

        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SidebarBackground,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 22, 0, 18),
            Margin = Padding.Empty
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 44));

        var mark = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _appIconImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = SidebarBackground,
            Margin = new Padding(0, 0, 12, 0)
        };
        brand.Controls.Add(mark, 0, 0);
        brand.SetRowSpan(mark, 2);

        var brandTitle = NewLabel("批量处理助手", 11.5F, FontStyle.Bold, PrimaryText);
        brandTitle.AutoSize = false;
        brandTitle.Dock = DockStyle.Fill;
        brandTitle.TextAlign = ContentAlignment.BottomLeft;
        brandTitle.Margin = Padding.Empty;
        brand.Controls.Add(brandTitle, 1, 0);

        var brandVersion = NewLabel("DESKTOP  2.9", 7.5F, FontStyle.Bold, SecondaryText);
        brandVersion.AutoSize = false;
        brandVersion.Dock = DockStyle.Fill;
        brandVersion.TextAlign = ContentAlignment.TopLeft;
        brandVersion.Margin = Padding.Empty;
        brand.Controls.Add(brandVersion, 1, 1);
        sidebar.Controls.Add(brand, 0, 0);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = SidebarBackground,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        _folderNav.Size = new Size(236, 48);
        _draftNav.Size = new Size(236, 48);
        nav.Controls.Add(_folderNav);
        nav.Controls.Add(_draftNav);
        sidebar.Controls.Add(nav, 0, 1);

        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SidebarBackground,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var note = NewLabel("两种工作方式\n\n视频批量  ·  直接处理\n剪映草稿  ·  保留滤镜与叠加", 8F, FontStyle.Regular, SecondaryText);
        note.Dock = DockStyle.Fill;
        note.Padding = new Padding(10, 0, 4, 18);
        note.AutoSize = false;
        note.TextAlign = ContentAlignment.BottomLeft;
        info.Controls.Add(note, 0, 0);

        _helpButton.Dock = DockStyle.Fill;
        _helpButton.Margin = Padding.Empty;
        info.Controls.Add(_helpButton, 0, 1);
        sidebar.Controls.Add(info, 0, 2);

        var environment = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 23, 28),
            BorderColor = Border,
            CornerRadius = 10,
            Padding = new Padding(14)
        };
        var envTitle = NewLabel("运行环境", 9F, FontStyle.Bold, PrimaryText);
        envTitle.Location = new Point(14, 14);
        environment.Controls.Add(envTitle);
        _ffmpegStatus.Location = new Point(14, 46);
        _ffmpegStatus.MaximumSize = new Size(200, 0);
        environment.Controls.Add(_ffmpegStatus);
        _draftStatus.Location = new Point(14, 76);
        _draftStatus.MaximumSize = new Size(200, 0);
        environment.Controls.Add(_draftStatus);
        _windowStatus.Location = new Point(14, 106);
        _windowStatus.MaximumSize = new Size(200, 0);
        environment.Controls.Add(_windowStatus);
        sidebar.Controls.Add(environment, 0, 3);
        return sidebar;
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(28, 0, 28, 28)
        };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        workspace.Controls.Add(BuildTopBar(), 0, 0);
        workspace.Controls.Add(BuildContent(), 0, 1);
        workspace.Controls.Add(BuildActionBar(), 0, 2);
        return workspace;
    }

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 14),
            Margin = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _pageTitle.AutoSize = false;
        _pageTitle.Dock = DockStyle.Fill;
        _pageTitle.TextAlign = ContentAlignment.BottomLeft;
        _pageTitle.Margin = new Padding(0, 0, 0, 3);
        top.Controls.Add(_pageTitle, 0, 0);

        _pageSubtitle.AutoSize = false;
        _pageSubtitle.AutoEllipsis = true;
        _pageSubtitle.Dock = DockStyle.Fill;
        _pageSubtitle.TextAlign = ContentAlignment.TopLeft;
        _pageSubtitle.Margin = new Padding(0, 4, 0, 0);
        top.Controls.Add(_pageSubtitle, 0, 1);
        return top;
    }

    private Control BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildSettingsColumn(), 0, 0);
        content.Controls.Add(BuildTaskArea(), 1, 0);
        return content;
    }

    private Control BuildSettingsColumn()
    {
        _settingsScroll.Dock = DockStyle.Fill;
        _settingsScroll.BackColor = AppBackground;
        _settingsScroll.TrackColor = Color.FromArgb(20, 22, 25);
        _settingsScroll.ThumbColor = Color.FromArgb(66, 71, 79);
        _settingsScroll.ThumbHoverColor = Color.FromArgb(91, 98, 108);
        _settingsScroll.Margin = new Padding(0, 0, 16, 0);

        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppBackground,
            Padding = new Padding(0, 0, 10, 12),
            Margin = Padding.Empty,
            Width = 392
        };
        _folderSourceCard.Margin = new Padding(0, 0, 0, 18);
        _draftSourceCard.Margin = new Padding(0, 0, 0, 18);
        stack.Controls.Add(_folderSourceCard);
        stack.Controls.Add(_draftSourceCard);
        stack.Controls.Add(BuildTransformCard());
        stack.Controls.Add(BuildVisualEffectsCard());
        stack.Controls.Add(BuildWatermarkCard());
        stack.Controls.Add(BuildOutputCard());
        _calibrationCard.Margin = new Padding(0, 0, 0, 18);
        stack.Controls.Add(_calibrationCard);
        _settingsScroll.SetContent(stack);
        return _settingsScroll;
    }

    private Control BuildTransformCard()
    {
        var card = NewCard(382, 350);
        card.Margin = new Padding(0, 0, 0, 18);
        AddCardHeader(card, "画面适配", "输出为竖版 9:16 · 分辨率由压缩档位决定");
        _blackBarsOption = BuildModeOption(_blackBarsRadio, "完整画面，空白区域填黑", 86);
        _centerCropOption = BuildModeOption(_centerCropRadio, "铺满画面，可能裁掉左右内容", 172);
        _blurOption = BuildModeOption(_blurRadio, "保留主体，模糊画面铺底", 258);
        card.Controls.Add(_blackBarsOption);
        card.Controls.Add(_centerCropOption);
        card.Controls.Add(_blurOption);
        return card;
    }


    private Control BuildVisualEffectsCard()
    {
        _visualEffectsCard.Margin = new Padding(0, 0, 0, 18);
        AddCardHeader(_visualEffectsCard, "处理参数", "画面调整仅视频批量 · 倍速、压缩与码率两种模式通用");

        var directionLabel = NewLabel("旋转方向", 8F, FontStyle.Regular, SecondaryText);
        directionLabel.Location = new Point(17, 80);
        _visualEffectsCard.Controls.Add(directionLabel);
        _rotationDirectionCombo.Items.AddRange(new object[] { "不旋转", "顺时针", "逆时针" });
        _rotationDirectionCombo.Location = new Point(16, 106);
        _rotationDirectionCombo.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_rotationDirectionCombo);

        var degreesLabel = NewLabel("旋转角度（1 - 359°）", 8F, FontStyle.Regular, SecondaryText);
        degreesLabel.Location = new Point(199, 80);
        _visualEffectsCard.Controls.Add(degreesLabel);
        ConfigureRotationInput();
        _rotationDegrees.Location = new Point(198, 106);
        _rotationDegrees.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_rotationDegrees);

        var zoomLabel = NewLabel("中心缩放", 8F, FontStyle.Regular, SecondaryText);
        zoomLabel.Location = new Point(17, 166);
        _visualEffectsCard.Controls.Add(zoomLabel);
        _zoomCombo.Items.AddRange(new object[] { "90%", "不缩放", "110%", "120%" });
        _zoomCombo.Location = new Point(16, 192);
        _zoomCombo.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_zoomCombo);

        var filterLabel = NewLabel("滤镜效果", 8F, FontStyle.Regular, SecondaryText);
        filterLabel.Location = new Point(199, 166);
        _visualEffectsCard.Controls.Add(filterLabel);
        _filterCombo.Items.AddRange(new object[] { "无滤镜", "明亮", "鲜艳", "暖色", "冷色", "黑白" });
        _filterCombo.Location = new Point(198, 192);
        _filterCombo.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_filterCombo);

        var speedLabel = NewLabel("播放速度", 8F, FontStyle.Regular, SecondaryText);
        speedLabel.Location = new Point(17, 252);
        _visualEffectsCard.Controls.Add(speedLabel);
        _speedPreset.Items.AddRange(new object[] { "1.0x", "1.1x", "1.25x", "自定义" });
        _speedPreset.Location = new Point(16, 278);
        _speedPreset.Size = new Size(350, 40);
        _visualEffectsCard.Controls.Add(_speedPreset);
        ConfigureSpeedInput();
        _customSpeed.Location = new Point(246, 278);
        _customSpeed.Size = new Size(118, 40);
        _customSpeed.Visible = false;
        _visualEffectsCard.Controls.Add(_customSpeed);

        var compressionLabel = NewLabel("输出压缩", 8F, FontStyle.Regular, SecondaryText);
        compressionLabel.Location = new Point(17, 330);
        _visualEffectsCard.Controls.Add(compressionLabel);
        _compressionPresetCombo.Items.AddRange(new object[]
        {
            "576P",
            "720P",
            "1080P"
        });
        _compressionPresetCombo.Location = new Point(16, 356);
        _compressionPresetCombo.Size = new Size(350, 40);
        _visualEffectsCard.Controls.Add(_compressionPresetCombo);

        var videoBitrateLabel = NewLabel("视频码率", 8F, FontStyle.Regular, SecondaryText);
        videoBitrateLabel.Location = new Point(17, 416);
        _visualEffectsCard.Controls.Add(videoBitrateLabel);
        _videoBitrateCombo.Items.AddRange(VideoBitrateOptions.Select(value => (object)BitrateText(value)).ToArray());
        _videoBitrateCombo.Location = new Point(16, 442);
        _videoBitrateCombo.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_videoBitrateCombo);

        var audioBitrateLabel = NewLabel("音频码率", 8F, FontStyle.Regular, SecondaryText);
        audioBitrateLabel.Location = new Point(199, 416);
        _visualEffectsCard.Controls.Add(audioBitrateLabel);
        _audioBitrateCombo.Items.AddRange(AudioBitrateOptions.Select(value => (object)BitrateText(value)).ToArray());
        _audioBitrateCombo.Location = new Point(198, 442);
        _audioBitrateCombo.Size = new Size(168, 40);
        _visualEffectsCard.Controls.Add(_audioBitrateCombo);

        var hint = NewLabel("切换分辨率会推荐码率；之后仍可手动调整", 7.8F, FontStyle.Regular, SecondaryText);
        hint.Location = new Point(17, 500);
        hint.AutoSize = false;
        hint.Size = new Size(348, 22);
        _visualEffectsCard.Controls.Add(hint);
        return _visualEffectsCard;
    }

    private Control BuildWatermarkCard()
    {
        var card = NewCard(382, 202);
        card.Margin = new Padding(0, 0, 0, 18);
        AddCardHeader(card, "图片水印", "所有处理完成的视频都会统一叠加");

        _watermarkEnabled.Location = new Point(16, 80);
        _watermarkEnabled.Size = new Size(350, 28);
        card.Controls.Add(_watermarkEnabled);

        _watermarkPath.Location = new Point(16, 122);
        _watermarkPath.Size = new Size(270, 40);
        _watermarkPath.PlaceholderText = "请选择水印图片";
        card.Controls.Add(_watermarkPath);

        _browseWatermarkButton.Location = new Point(296, 122);
        _browseWatermarkButton.Size = new Size(68, 40);
        card.Controls.Add(_browseWatermarkButton);

        var hint = NewLabel("建议使用与压缩档位一致的透明 PNG", 7.8F, FontStyle.Regular, SecondaryText);
        hint.Location = new Point(17, 176);
        card.Controls.Add(hint);
        return card;
    }

    private Control BuildOutputCard()
    {
        var card = NewCard(382, 158);
        card.Margin = new Padding(0, 0, 0, 18);
        AddCardHeader(card, "输出目录", "不会覆盖同名文件");
        _outputFolder.Location = new Point(16, 88);
        _outputFolder.Size = new Size(270, 40);
        card.Controls.Add(_outputFolder);
        var browse = NewButton("浏览", SurfaceHover, PrimaryText);
        browse.Location = new Point(296, 88);
        browse.Size = new Size(68, 40);
        browse.Click += (_, _) => BrowseOutputFolder();
        card.Controls.Add(browse);
        return card;
    }

    private Control BuildTaskArea()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderColor = Border,
            CornerRadius = 12,
            Padding = new Padding(20),
            Margin = Padding.Empty
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = Padding.Empty };
        _taskTitle.Location = new Point(0, 4);
        header.Controls.Add(_taskTitle);
        _taskSummary.Location = new Point(1, 38);
        header.Controls.Add(_taskSummary);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_grid, 0, 1);

        var logHeader = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = Padding.Empty };
        var logTitle = NewLabel("运行记录", 9F, FontStyle.Bold, PrimaryText);
        logTitle.AutoSize = false;
        logTitle.Dock = DockStyle.Left;
        logTitle.Width = 120;
        logTitle.TextAlign = ContentAlignment.MiddleLeft;
        logTitle.Margin = Padding.Empty;
        logHeader.Controls.Add(logTitle);

        _clearRecordsButton.Size = new Size(88, 32);
        _clearRecordsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clearRecordsButton.Location = new Point(Math.Max(0, logHeader.ClientSize.Width - 88), 8);
        logHeader.Resize += (_, _) =>
            _clearRecordsButton.Location = new Point(Math.Max(0, logHeader.ClientSize.Width - 88), 8);
        logHeader.Controls.Add(_clearRecordsButton);
        layout.Controls.Add(logHeader, 0, 2);

        _logBox.Margin = Padding.Empty;
        layout.Controls.Add(_logBox, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildActionBar()
    {
        var action = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 4,
            RowCount = 3,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        action.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        action.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        action.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        action.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var progressPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            Padding = new Padding(0, 0, 30, 0),
            Margin = Padding.Empty
        };
        _progressText.Location = new Point(0, 33);
        progressPanel.Controls.Add(_progressText);
        _progress.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _progress.Location = new Point(0, 76);
        _progress.Width = Math.Max(100, progressPanel.ClientSize.Width - 30);
        progressPanel.Resize += (_, _) => _progress.Width = Math.Max(100, progressPanel.ClientSize.Width - 30);
        progressPanel.Controls.Add(_progress);
        action.Controls.Add(progressPanel, 0, 0);
        action.SetRowSpan(progressPanel, 3);

        _previewButton.Dock = DockStyle.Fill;
        _previewButton.Margin = new Padding(0, 0, 14, 0);
        action.Controls.Add(_previewButton, 1, 1);
        _startButton.Dock = DockStyle.Fill;
        _startButton.Margin = new Padding(0, 0, 14, 0);
        action.Controls.Add(_startButton, 2, 1);
        _stopButton.Dock = DockStyle.Fill;
        _stopButton.Margin = Padding.Empty;
        action.Controls.Add(_stopButton, 3, 1);
        return action;
    }

    private void BuildFolderSourceCard()
    {
        AddCardHeader(_folderSourceCard, "输入目录", "扫描目录第一层中的全部视频文件");
        _inputFolder.Location = new Point(16, 88);
        _inputFolder.Size = new Size(214, 40);
        _folderSourceCard.Controls.Add(_inputFolder);
        var browse = NewButton("浏览", SurfaceHover, PrimaryText);
        browse.Location = new Point(240, 88);
        browse.Size = new Size(60, 40);
        browse.Click += (_, _) => BrowseInputFolder();
        _folderSourceCard.Controls.Add(browse);
        _scanFolderButton.Location = new Point(306, 88);
        _scanFolderButton.Size = new Size(60, 40);
        _scanFolderButton.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);
        _scanFolderButton.Text = "扫描";
        _folderSourceCard.Controls.Add(_scanFolderButton);
        var types = NewLabel("MP4 / MOV / MKV / AVI / WEBM / TS 等", 7.8F, FontStyle.Regular, SecondaryText);
        types.Location = new Point(17, 142);
        _folderSourceCard.Controls.Add(types);
    }

    private void BuildDraftSourceCard()
    {
        AddCardHeader(_draftSourceCard, "剪映草稿", "选择草稿及作为分段依据的主视频轨道");
        _draftCombo.Location = new Point(16, 88);
        _draftCombo.Size = new Size(256, 40);
        _draftSourceCard.Controls.Add(_draftCombo);
        _scanDraftButton.Location = new Point(282, 88);
        _scanDraftButton.Size = new Size(84, 40);
        _draftSourceCard.Controls.Add(_scanDraftButton);
        _trackCombo.Location = new Point(16, 144);
        _trackCombo.Size = new Size(350, 40);
        _draftSourceCard.Controls.Add(_trackCombo);
        _canvasLabel.Location = new Point(17, 201);
        _draftSourceCard.Controls.Add(_canvasLabel);
        _draftCompositionLabel.Location = new Point(17, 229);
        _draftSourceCard.Controls.Add(_draftCompositionLabel);
    }

    private void BuildCalibrationCard()
    {
        AddCardHeader(_calibrationCard, "剪映界面标定", "剪映布局变化后需要重新标定一次");
        AddCalibrationRow(_calibrationCard, 88, "时间线位置", _timelinePointLabel, async () =>
            await CapturePointAsync("把鼠标移到剪映时间线的空白或标尺区域", point =>
            {
                _settings.Calibration.TimelinePoint = point;
                UpdateCalibrationLabels();
            }));

        var openExport = NewButton("打开剪映导出设置", Color.FromArgb(48, 67, 74), PrimaryText);
        openExport.Location = new Point(16, 144);
        openExport.Size = new Size(350, 40);
        openExport.Click += async (_, _) => await OpenExportSettingsAsync();
        _calibrationCard.Controls.Add(openExport);

        AddCalibrationRow(_calibrationCard, 200, "文件名输入框", _fileNamePointLabel, async () =>
            await CapturePointAsync("把鼠标移到剪映导出窗口的文件名输入框", point =>
            {
                _settings.Calibration.FileNamePoint = point;
                UpdateCalibrationLabels();
            }));
        AddCalibrationRow(_calibrationCard, 258, "导出按钮", _exportPointLabel, async () =>
            await CapturePointAsync("把鼠标移到剪映导出窗口最下面的导出按钮", point =>
            {
                _settings.Calibration.ExportButtonPoint = point;
                UpdateCalibrationLabels();
            }));
    }

    private static void AddCalibrationRow(Control parent, int top, string title, Label value, Func<Task> capture)
    {
        var label = NewLabel(title, 8.5F, FontStyle.Regular, PrimaryText);
        label.Location = new Point(17, top + 11);
        parent.Controls.Add(label);
        value.Location = new Point(126, top + 12);
        parent.Controls.Add(value);
        var button = NewButton("3 秒后记录", SurfaceHover, PrimaryText);
        button.Location = new Point(254, top);
        button.Size = new Size(112, 40);
        button.Click += async (_, _) => await capture();
        parent.Controls.Add(button);
    }

    private RoundedPanel BuildModeOption(AccentRadioButton radio, string description, int top)
    {
        var option = new RoundedPanel
        {
            Size = new Size(350, 74),
            Location = new Point(16, top),
            BackColor = SurfaceRaised,
            BorderColor = Border,
            CornerRadius = 10,
            Cursor = Cursors.Hand
        };
        radio.Location = new Point(12, 8);
        radio.Size = new Size(326, 30);
        option.Controls.Add(radio);
        var hint = NewLabel(description, 7.8F, FontStyle.Regular, SecondaryText);
        hint.Location = new Point(38, 44);
        hint.AutoEllipsis = false;
        hint.AutoSize = false;
        hint.Size = new Size(296, 22);
        hint.TextAlign = ContentAlignment.TopLeft;
        option.Controls.Add(hint);
        radio.Click += (_, _) => ToggleModeOption(radio);
        option.Click += (_, _) => ToggleModeOption(radio);
        hint.Click += (_, _) => ToggleModeOption(radio);
        return option;
    }

    private static void AddCardHeader(Control card, string title, string subtitle)
    {
        var titleLabel = NewLabel(title, 10.5F, FontStyle.Bold, PrimaryText);
        titleLabel.Location = new Point(16, 18);
        card.Controls.Add(titleLabel);
        var subtitleLabel = NewLabel(subtitle, 8F, FontStyle.Regular, SecondaryText);
        subtitleLabel.Location = new Point(17, 50);
        card.Controls.Add(subtitleLabel);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersHeight = 38;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 35, 41);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = SecondaryText;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 35, 41);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        _grid.DefaultCellStyle.BackColor = Surface;
        _grid.DefaultCellStyle.ForeColor = PrimaryText;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 57, 54);
        _grid.DefaultCellStyle.SelectionForeColor = PrimaryText;
        _grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8.5F);
        _grid.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(27, 30, 36);
        _grid.GridColor = Color.FromArgb(39, 43, 50);
        _grid.RowTemplate.Height = 38;
        ConfigureFolderColumns();
    }

    private void ConfigureFolderColumns()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", Width = 48 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "视频文件", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "格式", Width = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 116 });
    }

    private void ConfigureDraftColumns()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", Width = 48 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "主轨片段", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Start", HeaderText = "开始", Width = 96 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "End", HeaderText = "结束", Width = 96 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tracks", HeaderText = "叠加", Width = 68 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 110 });
    }

    private void ConfigureLog()
    {
        _logBox.Dock = DockStyle.Fill;
        _logBox.ReadOnly = true;
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.BackColor = Color.FromArgb(18, 21, 26);
        _logBox.ForeColor = Color.FromArgb(180, 187, 198);
        _logBox.Font = new Font("Cascadia Mono", 8.5F);
        _logBox.DetectUrls = false;
        _logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
    }

    private void ConfigureRotationInput()
    {
        _rotationDegrees.Text = "1";
        _rotationDegrees.MaxLength = 3;
        _rotationDegrees.PlaceholderText = "1 - 359";
        _rotationDegrees.BackColor = SurfaceRaised;
        _rotationDegrees.ForeColor = PrimaryText;
        _rotationDegrees.Font = new Font("Microsoft YaHei UI", 9F);
        _rotationDegrees.TextAlign = HorizontalAlignment.Center;
        _rotationDegrees.KeyPress += (_, args) =>
        {
            if (!char.IsControl(args.KeyChar) && !char.IsDigit(args.KeyChar))
                args.Handled = true;
        };
    }

    private void UpdateVisualAdjustmentControlState()
    {
        var enabled = _workspaceMode == WorkspaceMode.FolderBatch && _runCancellation is null;
        _rotationDirectionCombo.Enabled = enabled;
        _zoomCombo.Enabled = enabled;
        _filterCombo.Enabled = enabled;
        UpdateRotationInputState();
    }

    private void UpdateRotationInputState()
    {
        _rotationDegrees.Enabled = _workspaceMode == WorkspaceMode.FolderBatch
            && _rotationDirectionCombo.SelectedIndex > 0
            && _runCancellation is null;
    }

    private void ConfigureSpeedInput()
    {
        _customSpeed.Text = "1.10";
        _customSpeed.MaxLength = 5;
        _customSpeed.PlaceholderText = "0.1 - 5.0";
        _customSpeed.BackColor = SurfaceRaised;
        _customSpeed.ForeColor = PrimaryText;
        _customSpeed.Font = new Font("Microsoft YaHei UI", 9F);
        _customSpeed.TextAlign = HorizontalAlignment.Center;
        _customSpeed.KeyPress += (_, args) =>
        {
            if (!char.IsControl(args.KeyChar) && !char.IsDigit(args.KeyChar) && args.KeyChar is not '.' and not ',')
                args.Handled = true;
        };
    }

    private void UpdateSpeedInputLayout()
    {
        var custom = _speedPreset.SelectedItem?.ToString() == "自定义";
        _speedPreset.Width = 218;
        _customSpeed.Visible = custom;
        _customSpeed.ReadOnly = !custom;
    }

    private void UpdateWatermarkControls()
    {
        var editable = _watermarkEnabled.Checked && _runCancellation is null;
        _watermarkPath.Enabled = editable;
        _browseWatermarkButton.Enabled = editable;
    }

    private void WireEvents()
    {
        _folderNav.Click += (_, _) => SwitchWorkspace(WorkspaceMode.FolderBatch);
        _draftNav.Click += (_, _) => SwitchWorkspace(WorkspaceMode.JianyingDraft);
        _scanFolderButton.Click += (_, _) => ScanInputFolder(true);
        _scanDraftButton.Click += async (_, _) => await RefreshDraftsAsync(true);
        _draftCombo.SelectedIndexChanged += async (_, _) =>
        {
            if (!_refreshingDrafts) await ScanSelectedDraftAsync();
        };
        _trackCombo.SelectedIndexChanged += (_, _) => BindSelectedTrack();
        _rotationDirectionCombo.SelectedIndexChanged += (_, _) => UpdateRotationInputState();
        _compressionPresetCombo.SelectedIndexChanged += (_, _) => ApplyRecommendedBitrates();
        _speedPreset.SelectedIndexChanged += (_, _) =>
        {
            var custom = _speedPreset.SelectedItem?.ToString() == "自定义";
            if (!custom) _customSpeed.Text = SelectedSpeedFromPreset().ToString("0.##");
            UpdateSpeedInputLayout();
        };
        _watermarkEnabled.CheckedChanged += (_, _) => UpdateWatermarkControls();
        _browseWatermarkButton.Click += (_, _) => BrowseWatermarkImage();
        _clearRecordsButton.Click += (_, _) => ClearProcessingRecords();
        _helpButton.Click += (_, _) => OpenHelp();
        _centerCropRadio.CheckedChanged += (_, _) => UpdateModeOptionStyles();
        _blurRadio.CheckedChanged += (_, _) => UpdateModeOptionStyles();
        _blackBarsRadio.CheckedChanged += (_, _) => UpdateModeOptionStyles();
        _previewButton.Click += async (_, _) => await PreviewFirstSegmentAsync();
        _startButton.Click += async (_, _) =>
        {
            if (_workspaceMode == WorkspaceMode.FolderBatch) await StartFolderBatchAsync();
            else await StartDraftExportAsync();
        };
        _stopButton.Click += (_, _) => _runCancellation?.Cancel();
        _automation.SegmentStatusChanged += UpdateDraftSegmentStatus;
        _statusTimer.Tick += (_, _) => UpdateWindowStatus();
        _statusTimer.Start();
        FormClosing += (_, _) => SaveSettings();
    }

    private void SwitchWorkspace(WorkspaceMode mode)
    {
        if (_runCancellation is not null) return;
        _workspaceMode = mode;
        var isFolder = mode == WorkspaceMode.FolderBatch;
        _folderNav.Active = isFolder;
        _draftNav.Active = !isFolder;
        _folderSourceCard.Visible = isFolder;
        _draftSourceCard.Visible = !isFolder;
        _visualEffectsCard.Visible = true;
        _calibrationCard.Visible = !isFolder;
        _previewButton.Visible = !isFolder;
        _pageTitle.Text = isFolder ? "视频批量处理" : "剪映草稿分段导出";
        _pageSubtitle.Text = isFolder
            ? "输入一个目录，自动将其中全部视频输出为统一规格的竖版成品"
            : "由剪映原生渲染滤镜与多轨画面，再按主轨片段逐条输出";
        _taskTitle.Text = isFolder ? "待处理视频" : "分段导出计划";
        _startButton.Text = isFolder ? "开始批量处理" : "开始自动导出";
        if (isFolder)
        {
            ConfigureFolderColumns();
            BindBatchItems();
        }
        else
        {
            ConfigureDraftColumns();
            BindSelectedTrack();
        }
        UpdateVisualAdjustmentControlState();
        _settingsScroll.ScrollToTop();
    }

    private void ClearProcessingRecords()
    {
        if (_runCancellation is not null) return;
        _logBox.Clear();
        _progress.Value = 0;
        if (_workspaceMode == WorkspaceMode.FolderBatch) BindBatchItems();
        else BindSelectedTrack();
        _grid.ClearSelection();
    }

    private void OpenHelp()
    {
        var helpPath = Path.Combine(AppContext.BaseDirectory, "使用说明.html");
        if (!File.Exists(helpPath))
        {
            MessageBox.Show(this, "使用说明文件不存在，请重新发布或安装完整版本。", "无法打开使用说明", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "无法打开使用说明", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenOutputFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            AppendLog($"无法自动打开输出目录：{exception.Message}");
        }
    }

    private void BrowseInputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含待处理视频的输入目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_inputFolder.Text) ? _inputFolder.Text : string.Empty,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _inputFolder.Text = dialog.SelectedPath;
        ScanInputFolder(true);
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择成品输出目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_outputFolder.Text) ? _outputFolder.Text : string.Empty,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _outputFolder.Text = dialog.SelectedPath;
    }

    private void BrowseWatermarkImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择图片水印",
            Filter = "图片文件|*.png;*.webp;*.jpg;*.jpeg;*.bmp|PNG 图片|*.png|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (File.Exists(_watermarkPath.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_watermarkPath.Text);
            dialog.FileName = Path.GetFileName(_watermarkPath.Text);
        }
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _watermarkPath.Text = dialog.FileName;
        _watermarkEnabled.Checked = true;
    }

    private void ScanInputFolder(bool showEmptyMessage)
    {
        try
        {
            _batchItems = _folderBatchService.Discover(_inputFolder.Text.Trim(), _outputFolder.Text.Trim());
            BindBatchItems();
            AppendLog($"已扫描输入目录，发现 {_batchItems.Count} 个视频。 ");
            if (showEmptyMessage && _batchItems.Count == 0)
                MessageBox.Show(this, "该目录第一层没有发现支持的视频文件。", "没有视频", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            ShowError("扫描输入目录失败", ex);
        }
    }

    private void BindBatchItems()
    {
        if (_workspaceMode != WorkspaceMode.FolderBatch) return;
        _grid.Rows.Clear();
        foreach (var item in _batchItems)
            _grid.Rows.Add(item.Index, item.FileName, item.Extension, "等待");
        _progress.Maximum = Math.Max(1, _batchItems.Count);
        _progress.Value = 0;
        _taskSummary.Text = _batchItems.Count == 0
            ? "请选择输入目录并扫描"
            : $"共 {_batchItems.Count} 个视频 · 将按文件名顺序处理";
        _progressText.Text = _batchItems.Count == 0 ? "准备就绪" : $"已就绪 · 0 / {_batchItems.Count}";
    }

    private async Task StartFolderBatchAsync()
    {
        if (!ValidateRotationSelection()) return;
        if (!ValidateSpeedSelection()) return;
        SyncSettingsFromUi();
        if (!ValidateVerticalModeSelection()) return;
        if (!ValidateWatermarkSelection()) return;
        if (_batchItems.Count == 0)
        {
            ScanInputFolder(false);
            if (_batchItems.Count == 0)
            {
                MessageBox.Show(this, "请先选择包含视频的输入目录。", "没有待处理文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        if (_ffmpegService.ExecutablePath is null)
        {
            MessageBox.Show(this, "未找到 FFmpeg，暂时无法处理视频。", "运行环境不完整", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            MessageBox.Show(this, "请选择输出目录。", "输出目录为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var output = Path.GetFullPath(_settings.OutputFolder);
        var confirmation = MessageBox.Show(this,
            $"将处理 {_batchItems.Count} 个视频\n\n" +
            $"输出目录：{output}\n" +
            $"画面：{VerticalModeText(_settings.VerticalMode)}\n" +
            $"旋转：{RotationText(_settings.RotationDirection, _settings.RotationDegrees)}\n" +
            $"缩放：{_settings.ZoomPercent}%\n" +
            $"滤镜：{VideoFilterPresetText(_settings.VideoFilterPreset)}\n" +
            $"压缩：{CompressionPresetText(_settings.CompressionPreset)}\n" +
            $"视频码率：{BitrateText(_settings.VideoBitrateKbps)}\n" +
            $"音频码率：{BitrateText(_settings.AudioBitrateKbps)}\n" +
            $"速度：{_settings.Speed:0.##}x\n" +
            $"水印：{WatermarkSummary()}\n" +
            $"规格：{OutputResolutionText(_settings.CompressionPreset)} MP4",
            "开始批量处理",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (confirmation != DialogResult.OK) return;

        Directory.CreateDirectory(output);
        SaveSettings();
        _runCancellation = new CancellationTokenSource();
        SetRunning(true);
        ResetStatuses();
        var stopwatch = Stopwatch.StartNew();
        AppendLog("文件夹批处理开始，计时已启动。 ");
        try
        {
            var log = new Progress<string>(AppendLog);
            var itemProgress = new Progress<BatchItemProgress>(state =>
            {
                UpdateFolderStatus(state.RowIndex, state.Status);
                if (state.Status == "处理中")
                {
                    _progressText.Text = $"正在处理 {state.CompletedCount + 1} / {state.TotalCount} · {state.FileName}";
                }
                else
                {
                    _progress.Value = Math.Min(_progress.Maximum, state.CompletedCount);
                    if (state.Status == "完成") AppendLog($"已完成：{Path.GetFileName(state.Message)}");
                    else AppendLog($"处理失败：{state.FileName}：{state.Message}");
                }
            });

            var summary = await _folderBatchService.ProcessAsync(
                _batchItems,
                output,
                _settings.VerticalMode,
                _settings.RotationDirection,
                _settings.RotationDegrees,
                _settings.ZoomPercent,
                _settings.VideoFilterPreset,
                _settings.CompressionPreset,
                _settings.VideoBitrateKbps,
                _settings.AudioBitrateKbps,
                _settings.Speed,
                WatermarkPathForProcessing(),
                _ffmpegService,
                itemProgress,
                log,
                _runCancellation.Token);

            stopwatch.Stop();
            var elapsed = FormatElapsedTime(stopwatch.Elapsed);
            _progressText.Text = summary.FailedCount == 0
                ? $"处理完成 · {summary.TotalCount} / {summary.TotalCount} · 耗时 {elapsed}"
                : $"处理完成 · {summary.CompletedCount} 成功，{summary.FailedCount} 失败 · 耗时 {elapsed}";
            var completionMessage = summary.FailedCount == 0
                ? $"全部 {summary.TotalCount} 个视频处理完成。\n\n总耗时：{elapsed}\n\n{output}"
                : $"处理结束：{summary.CompletedCount} 个成功，{summary.FailedCount} 个失败。\n总耗时：{elapsed}\n\n详情请查看运行记录。";
            MessageBox.Show(this, completionMessage, "批量处理完成", MessageBoxButtons.OK,
                summary.FailedCount == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            OpenOutputFolder(output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _progressText.Text = $"任务已停止 · 已完成文件会保留 · 已耗时 {FormatElapsedTime(stopwatch.Elapsed)}";
            AppendLog("文件夹批处理已停止；已经完成的成品会保留。 ");
        }
        finally
        {
            stopwatch.Stop();
            AppendLog($"本次文件夹批处理总耗时：{FormatElapsedTime(stopwatch.Elapsed)}。 ");
            _runCancellation.Dispose();
            _runCancellation = null;
            SetRunning(false);
        }
    }

    private async Task RefreshDraftsAsync(bool interactive)
    {
        try
        {
            var selectedId = (_draftCombo.SelectedItem as DraftInfo)?.Id;
            var drafts = _draftService.DiscoverDrafts();
            _refreshingDrafts = true;
            try
            {
                _draftCombo.DataSource = new BindingList<DraftInfo>(drafts.ToList());
                if (selectedId is not null)
                {
                    var selected = drafts.FirstOrDefault(item => item.Id == selectedId);
                    if (selected is not null) _draftCombo.SelectedItem = selected;
                }
            }
            finally { _refreshingDrafts = false; }
            if (interactive) AppendLog($"发现 {drafts.Count} 个本地剪映草稿。 ");
            if (drafts.Count > 0) await ScanSelectedDraftAsync(interactive);
        }
        catch (Exception ex)
        {
            if (interactive) ShowError("读取剪映草稿列表失败", ex);
            else AppendLog($"启动时读取剪映草稿列表失败：{ex.Message}");
        }
    }

    private async Task ScanSelectedDraftAsync(bool showError = true)
    {
        if (_draftCombo.SelectedItem is not DraftInfo draft) return;
        SetBusy(true, "正在只读解析剪映草稿…");
        try
        {
            _draftPlan = await _draftService.LoadPlanAsync(draft);
            _trackCombo.DataSource = new BindingList<VideoTrackPlan>(_draftPlan.VideoTracks.ToList());
            _canvasLabel.Text = $"画布 {_draftPlan.CanvasWidth} × {_draftPlan.CanvasHeight} · {_draftPlan.VideoTracks.Count} 条视频轨";
            if (_draftPlan.HasNativeCompositeContent)
            {
                _draftCompositionLabel.Text = $"●  将由剪映原生合成：{_draftPlan.CompositeTrackCount} 条叠加轨 · {_draftPlan.NativeEffectMaterialCount} 个效果素材";
                _draftCompositionLabel.ForeColor = Accent;
            }
            else
            {
                _draftCompositionLabel.Text = "●  当前草稿仅检测到主视频，没有滤镜或叠加轨";
                _draftCompositionLabel.ForeColor = Color.FromArgb(230, 174, 99);
            }
            AppendLog($"草稿“{draft.Name}”读取成功：{_draftPlan.VideoTracks.Count} 条视频轨。 ");
            BindSelectedTrack();
        }
        catch (Exception ex)
        {
            _draftPlan = null;
            if (showError) ShowError("扫描剪映草稿失败", ex);
            else AppendLog($"启动时扫描剪映草稿失败：{ex.Message}");
        }
        finally { SetBusy(false); }
    }

    private void BindSelectedTrack()
    {
        if (_workspaceMode != WorkspaceMode.JianyingDraft) return;
        _grid.Rows.Clear();
        if (_trackCombo.SelectedItem is not VideoTrackPlan track)
        {
            _taskSummary.Text = "请选择剪映草稿与主轨道";
            return;
        }
        foreach (var segment in track.Segments)
            _grid.Rows.Add(segment.Index, segment.SourceName, segment.StartText, segment.EndText, segment.OverlappingTrackCount, "等待");
        _progress.Maximum = Math.Max(1, track.Segments.Count);
        _progress.Value = 0;
        _taskSummary.Text = $"主轨共 {track.Segments.Count} 段 · 每段合成所有可见叠加轨道";
        _progressText.Text = $"已就绪 · 0 / {track.Segments.Count}";
    }

    private async Task PreviewFirstSegmentAsync()
    {
        if (_draftPlan is null || _trackCombo.SelectedItem is not VideoTrackPlan track)
        {
            MessageBox.Show(this, "请先选择并读取剪映草稿。", "尚未准备", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            SaveSettings();
            await _automation.PreviewFirstSegmentAsync(_draftPlan, track, _settings.Calibration);
            AppendLog("已在剪映中标出第一段范围；没有修改草稿或开始导出。 ");
        }
        catch (Exception ex) { ShowError("预览首段失败", ex); }
    }

    private async Task StartDraftExportAsync()
    {
        if (_draftPlan is null || _trackCombo.SelectedItem is not VideoTrackPlan track)
        {
            MessageBox.Show(this, "请先选择并读取剪映草稿。", "尚未准备", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!ValidateSpeedSelection()) return;
        SyncSettingsFromUi();
        if (!ValidateOutputFolderSelection()) return;
        if (!ValidateVerticalModeSelection()) return;
        if (!ValidateWatermarkSelection()) return;
        if (!_settings.Calibration.IsComplete)
        {
            MessageBox.Show(this, "请先完成时间线、文件名输入框和导出按钮三项标定。", "标定未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var rawFolder = Path.Combine(_settings.OutputFolder, "_jianying_raw");
        var confirmation = MessageBox.Show(this,
            $"开始前请确认：\n\n" +
            $"1. 剪映当前打开的是“{_draftPlan.Draft.Name}”\n" +
            $"2. 剪映导出目录已经设置为：\n{rawFolder}\n" +
            $"3. 导出格式为 MP4，期间不要操作鼠标键盘\n\n" +
            $"将导出 {track.Segments.Count} 条，最终为 {OutputResolutionText(_settings.CompressionPreset)}、{_settings.Speed:0.##}x。\n" +
            $"码率：视频 {BitrateText(_settings.VideoBitrateKbps)} / 音频 {BitrateText(_settings.AudioBitrateKbps)}\n" +
            $"水印：{WatermarkSummary()}",
            "确认开始",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.OK) return;

        _runCancellation = new CancellationTokenSource();
        SetRunning(true);
        var stopwatch = Stopwatch.StartNew();
        AppendLog("剪映草稿分段导出开始，计时已启动。 ");
        try
        {
            ResetStatuses();
            var progress = new Progress<string>(message =>
            {
                AppendLog(message);
                _progressText.Text = message;
            });
            await _automation.ExportAsync(
                _draftPlan,
                track,
                _settings.Calibration,
                _settings.OutputFolder,
                _settings.VerticalMode,
                _settings.CompressionPreset,
                _settings.VideoBitrateKbps,
                _settings.AudioBitrateKbps,
                _settings.Speed,
                WatermarkPathForProcessing(),
                _ffmpegService,
                progress,
                _runCancellation.Token);
            stopwatch.Stop();
            var elapsed = FormatElapsedTime(stopwatch.Elapsed);
            _progressText.Text = $"导出完成 · {track.Segments.Count} / {track.Segments.Count} · 耗时 {elapsed}";
            AppendLog("剪映草稿分段导出全部完成。 ");
            var finalFolder = Path.Combine(_settings.OutputFolder, "成品");
            MessageBox.Show(this, $"成品目录：{finalFolder}\n\n总耗时：{elapsed}", "分段导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenOutputFolder(finalFolder);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _progressText.Text = $"任务已停止 · 已完成文件会保留 · 已耗时 {FormatElapsedTime(stopwatch.Elapsed)}";
            AppendLog("剪映导出已停止；已经完成的文件会保留。 ");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ShowError("剪映批量导出中止", ex);
        }
        finally
        {
            stopwatch.Stop();
            AppendLog($"本次剪映草稿分段导出总耗时：{FormatElapsedTime(stopwatch.Elapsed)}。 ");
            _runCancellation.Dispose();
            _runCancellation = null;
            SetRunning(false);
        }
    }

    private async Task OpenExportSettingsAsync()
    {
        try
        {
            SyncSettingsFromUi();
            if (!ValidateOutputFolderSelection()) return;
            var rawFolder = Path.Combine(_settings.OutputFolder, "_jianying_raw");
            Directory.CreateDirectory(rawFolder);
            Clipboard.SetText(rawFolder);
            MessageBox.Show(this,
                $"请把剪映的“导出至”设置为下面目录（路径已复制）：\n\n{rawFolder}\n\n设置 MP4 / 1080P 后保持窗口打开，再回来记录文件名框和导出按钮。 ",
                "设置剪映导出目录",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await _automation.OpenExportDialogAsync(_settings.Calibration);
        }
        catch (ExternalException ex) { ShowError("复制输出路径失败", ex); }
        catch (Exception ex) { ShowError("无法打开剪映导出设置", ex); }
    }

    private async Task CapturePointAsync(string instruction, Action<RelativePoint> assign)
    {
        AppendLog($"标定：{instruction}，3 秒后读取鼠标位置…");
        for (var second = 3; second > 0; second--)
        {
            Text = $"{second} 秒后记录鼠标位置 · 批量处理助手";
            await Task.Delay(1000);
        }
        Text = "批量处理助手";
        try
        {
            var point = _automation.CaptureCursorRelativeToJianying();
            assign(point);
            SaveSettings();
            AppendLog($"已记录相对剪映窗口坐标：({point.X}, {point.Y})");
        }
        catch (Exception ex) { ShowError("标定失败", ex); }
    }

    private void UpdateFolderStatus(int rowIndex, string status)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
        _grid.Rows[rowIndex].Cells["Status"].Value = status;
        ApplyStatusColor(_grid.Rows[rowIndex].Cells["Status"], status);
    }

    private void UpdateDraftSegmentStatus(int index, string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateDraftSegmentStatus(index, status));
            return;
        }
        if (_workspaceMode != WorkspaceMode.JianyingDraft || index < 0 || index >= _grid.Rows.Count) return;
        _grid.Rows[index].Cells["Status"].Value = status;
        ApplyStatusColor(_grid.Rows[index].Cells["Status"], status);
        if (status == "完成")
        {
            _progress.Value = Math.Min(_progress.Maximum, index + 1);
            _progressText.Text = $"正在导出 · {index + 1} / {_grid.Rows.Count}";
        }
    }

    private static void ApplyStatusColor(DataGridViewCell cell, string status)
    {
        cell.Style.ForeColor = status switch
        {
            "完成" => Accent,
            "失败" => Color.FromArgb(240, 115, 120),
            "处理中" or "剪映导出中" or "竖版处理中" => Color.FromArgb(255, 205, 110),
            _ => PrimaryText
        };
    }

    private void ResetStatuses()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Cells["Status"].Value = "等待";
            row.Cells["Status"].Style.ForeColor = PrimaryText;
        }
        _progress.Value = 0;
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
                _settings = DeserializeSettings(File.ReadAllText(_settingsPath));
        }
        catch (JsonException) { _settings = new AppSettings(); }
        catch (IOException) { _settings = new AppSettings(); }
    }

    private static AppSettings DeserializeSettings(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

    private void SaveSettings()
    {
        try
        {
            SyncSettingsFromUi();
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    private void ApplySettingsToUi()
    {
        _inputFolder.Text = _settings.InputFolder;
        _outputFolder.Text = _settings.OutputFolder;
        _centerCropRadio.Checked = _settings.VerticalMode == VerticalMode.CenterCrop;
        _blurRadio.Checked = _settings.VerticalMode == VerticalMode.BlurredBackground;
        _blackBarsRadio.Checked = _settings.VerticalMode == VerticalMode.BlackBars;
        _rotationDirectionCombo.SelectedIndex = _settings.RotationDirection switch
        {
            RotationDirection.Clockwise => 1,
            RotationDirection.CounterClockwise => 2,
            _ => 0
        };
        if (_rotationDirectionCombo.SelectedIndex == 0)
            _settings.RotationDegrees = 0;
        else if (_settings.RotationDegrees is < 1 or > 359)
            _settings.RotationDegrees = 1;
        _rotationDegrees.Text = (_settings.RotationDegrees == 0 ? 1 : _settings.RotationDegrees).ToString();
        if (_settings.ZoomPercent is not (90 or 100 or 110 or 120))
            _settings.ZoomPercent = 100;
        _zoomCombo.SelectedIndex = _settings.ZoomPercent switch
        {
            90 => 0,
            110 => 2,
            120 => 3,
            _ => 1
        };
        _filterCombo.SelectedIndex = _settings.VideoFilterPreset switch
        {
            VideoFilterPreset.Bright => 1,
            VideoFilterPreset.Vivid => 2,
            VideoFilterPreset.Warm => 3,
            VideoFilterPreset.Cool => 4,
            VideoFilterPreset.BlackAndWhite => 5,
            _ => 0
        };
        _compressionPresetCombo.SelectedIndex = _settings.CompressionPreset switch
        {
            CompressionPreset.Efficient720p => 1,
            CompressionPreset.HighQuality1080p => 2,
            _ => 0
        };
        var recommendedBitrates = RecommendedBitrates(_settings.CompressionPreset);
        if (!VideoBitrateOptions.Contains(_settings.VideoBitrateKbps)) _settings.VideoBitrateKbps = recommendedBitrates.Video;
        if (!AudioBitrateOptions.Contains(_settings.AudioBitrateKbps)) _settings.AudioBitrateKbps = recommendedBitrates.Audio;
        _videoBitrateCombo.SelectedIndex = Array.IndexOf(VideoBitrateOptions, _settings.VideoBitrateKbps);
        _audioBitrateCombo.SelectedIndex = Array.IndexOf(AudioBitrateOptions, _settings.AudioBitrateKbps);

        var preset = _settings.Speed switch
        {
            1.0m => "1.0x",
            1.1m => "1.1x",
            1.25m => "1.25x",
            _ => "自定义"
        };
        _speedPreset.SelectedItem = preset;
        _customSpeed.Text = Math.Clamp(_settings.Speed, 0.1m, 5.0m).ToString("0.##");
        if (string.IsNullOrWhiteSpace(_settings.WatermarkImagePath))
            _settings.WatermarkImagePath = new AppSettings().WatermarkImagePath;
        _watermarkEnabled.Checked = _settings.WatermarkEnabled;
        _watermarkPath.Text = _settings.WatermarkImagePath;
        UpdateSpeedInputLayout();
        UpdateWatermarkControls();
        UpdateModeOptionStyles();
        UpdateRotationInputState();
        UpdateCalibrationLabels();
    }

    private void UpdateModeOptionStyles()
    {
        SetModeOptionStyle(_centerCropOption, _centerCropRadio.Checked);
        SetModeOptionStyle(_blurOption, _blurRadio.Checked);
        SetModeOptionStyle(_blackBarsOption, _blackBarsRadio.Checked);
    }

    private void ToggleModeOption(AccentRadioButton selected)
    {
        if (!selected.Enabled) return;
        var shouldSelect = !selected.Checked;
        _centerCropRadio.Checked = shouldSelect && ReferenceEquals(selected, _centerCropRadio);
        _blurRadio.Checked = shouldSelect && ReferenceEquals(selected, _blurRadio);
        _blackBarsRadio.Checked = shouldSelect && ReferenceEquals(selected, _blackBarsRadio);
    }

    private static void SetModeOptionStyle(RoundedPanel? panel, bool selected)
    {
        if (panel is null) return;
        panel.BackColor = selected ? Color.FromArgb(27, 52, 49) : SurfaceRaised;
        panel.BorderColor = selected ? Color.FromArgb(20, 163, 150) : Border;
        panel.Invalidate();
    }

    private void SyncSettingsFromUi()
    {
        _settings.InputFolder = _inputFolder.Text.Trim();
        _settings.OutputFolder = _outputFolder.Text.Trim();
        _settings.VerticalMode = _centerCropRadio.Checked
            ? VerticalMode.CenterCrop
            : _blurRadio.Checked
                ? VerticalMode.BlurredBackground
                : _blackBarsRadio.Checked ? VerticalMode.BlackBars : VerticalMode.None;
        _settings.RotationDirection = _rotationDirectionCombo.SelectedIndex switch
        {
            1 => RotationDirection.Clockwise,
            2 => RotationDirection.CounterClockwise,
            _ => RotationDirection.None
        };
        if (_settings.RotationDirection == RotationDirection.None)
            _settings.RotationDegrees = 0;
        else if (TryGetRotationDegrees(out var rotationDegrees))
            _settings.RotationDegrees = rotationDegrees;
        _settings.ZoomPercent = _zoomCombo.SelectedIndex switch
        {
            0 => 90,
            2 => 110,
            3 => 120,
            _ => 100
        };
        _settings.VideoFilterPreset = _filterCombo.SelectedIndex switch
        {
            1 => VideoFilterPreset.Bright,
            2 => VideoFilterPreset.Vivid,
            3 => VideoFilterPreset.Warm,
            4 => VideoFilterPreset.Cool,
            5 => VideoFilterPreset.BlackAndWhite,
            _ => VideoFilterPreset.None
        };
        _settings.CompressionPreset = SelectedCompressionPreset();
        _settings.VideoBitrateKbps = VideoBitrateOptions[Math.Max(0, _videoBitrateCombo.SelectedIndex)];
        _settings.AudioBitrateKbps = AudioBitrateOptions[Math.Max(0, _audioBitrateCombo.SelectedIndex)];
        if (TryGetSelectedSpeed(out var speed)) _settings.Speed = speed;
        _settings.WatermarkEnabled = _watermarkEnabled.Checked;
        _settings.WatermarkImagePath = _watermarkPath.Text.Trim();
    }

    private bool ValidateVerticalModeSelection()
    {
        if (_settings.VerticalMode != VerticalMode.None) return true;
        MessageBox.Show(this, "请先选择一种画面适配方式。", "未选择画面适配", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private bool ValidateOutputFolderSelection()
    {
        if (!string.IsNullOrWhiteSpace(_settings.OutputFolder)) return true;
        MessageBox.Show(this, "请先选择输出目录。", "输出目录为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _outputFolder.Focus();
        return false;
    }

    private decimal SelectedSpeedFromPreset() => _speedPreset.SelectedItem?.ToString() switch
    {
        "1.0x" => 1.0m,
        "1.25x" => 1.25m,
        _ => 1.1m
    };

    private bool TryGetRotationDegrees(out int degrees)
    {
        if (_rotationDirectionCombo.SelectedIndex <= 0)
        {
            degrees = 0;
            return true;
        }

        return int.TryParse(_rotationDegrees.Text.Trim(), out degrees)
            && degrees is >= 1 and <= 359;
    }

    private bool ValidateRotationSelection()
    {
        if (TryGetRotationDegrees(out _)) return true;
        MessageBox.Show(this, "旋转角度请输入 1 到 359 之间的整数。", "旋转角度不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _rotationDegrees.Focus();
        _rotationDegrees.SelectAll();
        return false;
    }

    private bool TryGetSelectedSpeed(out decimal speed)
    {
        if (_speedPreset.SelectedItem?.ToString() != "自定义")
        {
            speed = SelectedSpeedFromPreset();
            return true;
        }

        var normalized = _customSpeed.Text.Trim().TrimEnd('x', 'X').Replace(',', '.');
        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out speed)
            && speed is >= 0.1m and <= 5.0m)
            return true;

        speed = _settings.Speed;
        return false;
    }

    private bool ValidateSpeedSelection()
    {
        if (TryGetSelectedSpeed(out _)) return true;
        MessageBox.Show(this, "自定义倍速请输入 0.1 到 5.0 之间的数字，最高不能超过 5 倍。", "倍速格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _customSpeed.Focus();
        _customSpeed.SelectAll();
        return false;
    }

    private bool ValidateWatermarkSelection()
    {
        if (!_settings.WatermarkEnabled) return true;
        var resolvedPath = WatermarkPathForProcessing();
        if (!string.IsNullOrWhiteSpace(resolvedPath)
            && File.Exists(resolvedPath))
            return true;

        MessageBox.Show(this, "已启用图片水印，但水印文件不存在。请重新选择水印图片。", "水印图片无效", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _watermarkPath.Focus();
        _watermarkPath.SelectAll();
        return false;
    }

    private string? WatermarkPathForProcessing()
    {
        if (!_settings.WatermarkEnabled || string.IsNullOrWhiteSpace(_settings.WatermarkImagePath))
            return null;
        return Path.IsPathRooted(_settings.WatermarkImagePath)
            ? _settings.WatermarkImagePath
            : Path.Combine(AppContext.BaseDirectory, _settings.WatermarkImagePath);
    }

    private string WatermarkSummary() =>
        _settings.WatermarkEnabled
            ? Path.GetFileName(_settings.WatermarkImagePath)
            : "未启用";

    private void UpdateCalibrationLabels()
    {
        _timelinePointLabel.Text = PointText(_settings.Calibration.TimelinePoint);
        _fileNamePointLabel.Text = PointText(_settings.Calibration.FileNamePoint);
        _exportPointLabel.Text = PointText(_settings.Calibration.ExportButtonPoint);
    }

    private static string PointText(RelativePoint? point) => point is null ? "未标定" : $"{point.X}, {point.Y}";

    private void UpdatePreflightStatus()
    {
        var install = _draftService.DetectJianyingInstallDirectory();
        _draftStatus.Text = install is null ? "●  剪映未找到" : "●  剪映已安装";
        _draftStatus.ForeColor = install is null ? Color.FromArgb(230, 153, 100) : Accent;
        _ffmpegStatus.Text = _ffmpegService.ExecutablePath is null ? "●  FFmpeg 未找到" : "●  FFmpeg 可用";
        _ffmpegStatus.ForeColor = _ffmpegService.ExecutablePath is null ? Color.FromArgb(240, 115, 120) : Accent;
        UpdateWindowStatus();
    }

    private void UpdateWindowStatus()
    {
        var status = _automation.GetWindowStatus();
        var connected = status.StartsWith("已连接", StringComparison.Ordinal);
        _windowStatus.Text = connected ? "●  剪映窗口已连接" : "●  剪映窗口未打开";
        _windowStatus.ForeColor = connected ? Accent : SecondaryText;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _scanDraftButton.Enabled = !busy;
        _startButton.Enabled = !busy;
        _previewButton.Enabled = !busy;
        UseWaitCursor = busy;
        if (message is not null) AppendLog(message);
    }

    private void SetRunning(bool running)
    {
        _startButton.Enabled = !running;
        _previewButton.Enabled = !running;
        _scanFolderButton.Enabled = !running;
        _scanDraftButton.Enabled = !running;
        _folderNav.Enabled = !running;
        _draftNav.Enabled = !running;
        _inputFolder.Enabled = !running;
        _outputFolder.Enabled = !running;
        _speedPreset.Enabled = !running;
        _compressionPresetCombo.Enabled = !running;
        _videoBitrateCombo.Enabled = !running;
        _audioBitrateCombo.Enabled = !running;
        _customSpeed.ReadOnly = running || _speedPreset.SelectedItem?.ToString() != "自定义";
        _clearRecordsButton.Enabled = !running;
        _helpButton.Enabled = !running;
        _watermarkEnabled.Enabled = !running;
        _watermarkPath.Enabled = !running && _watermarkEnabled.Checked;
        _browseWatermarkButton.Enabled = !running && _watermarkEnabled.Checked;
        _centerCropRadio.Enabled = !running;
        _blurRadio.Enabled = !running;
        _blackBarsRadio.Enabled = !running;
        UpdateVisualAdjustmentControlState();
        _stopButton.Enabled = running;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}]  {message}{Environment.NewLine}");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void ShowError(string title, Exception exception)
    {
        AppendLog($"错误：{title}：{exception.Message}");
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string VerticalModeText(VerticalMode mode) =>
        mode == VerticalMode.None ? "未选择" : mode switch
    {
        VerticalMode.CenterCrop => "居中裁切",
        VerticalMode.BlackBars => "完整画面加黑边",
        _ => "模糊背景填充"
    };

    private static string RotationText(RotationDirection direction, int degrees) => direction switch
    {
        RotationDirection.Clockwise => $"顺时针 {degrees}°",
        RotationDirection.CounterClockwise => $"逆时针 {degrees}°",
        _ => "不旋转"
    };
    private static string VideoFilterPresetText(VideoFilterPreset preset) => preset switch
    {
        VideoFilterPreset.Bright => "明亮",
        VideoFilterPreset.Vivid => "鲜艳",
        VideoFilterPreset.Warm => "暖色",
        VideoFilterPreset.Cool => "冷色",
        VideoFilterPreset.BlackAndWhite => "黑白",
        _ => "无滤镜"
    };
    private static string CompressionPresetText(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Efficient576p => "576P",
        CompressionPreset.Efficient720p => "720P",
        CompressionPreset.HighQuality1080p => "1080P",
        _ => "576P"
    };

    private CompressionPreset SelectedCompressionPreset() => _compressionPresetCombo.SelectedIndex switch
    {
        1 => CompressionPreset.Efficient720p,
        2 => CompressionPreset.HighQuality1080p,
        _ => CompressionPreset.Efficient576p
    };

    private static (int Video, int Audio) RecommendedBitrates(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Efficient720p => (4000, 128),
        CompressionPreset.HighQuality1080p => (6000, 128),
        _ => (2500, 128)
    };

    private void ApplyRecommendedBitrates()
    {
        var recommended = RecommendedBitrates(SelectedCompressionPreset());
        _videoBitrateCombo.SelectedIndex = Array.IndexOf(VideoBitrateOptions, recommended.Video);
        _audioBitrateCombo.SelectedIndex = Array.IndexOf(AudioBitrateOptions, recommended.Audio);
    }

    private static string BitrateText(int bitrateKbps) => $"{bitrateKbps} kbps";

    private static string OutputResolutionText(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Efficient576p => "576 × 1024",
        CompressionPreset.Efficient720p => "720 × 1280",
        _ => "1080 × 1920"
    };


    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        var seconds = elapsed.Seconds + elapsed.Milliseconds / 1000d;
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours} 小时 {elapsed.Minutes:00} 分 {seconds:00.0} 秒";
        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes} 分 {seconds:00.0} 秒";
        return $"{elapsed.TotalSeconds:0.0} 秒";
    }

    private static Image? LoadAppIcon()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("JianyingBatchExporter.Assets.AppIcon.png");
        if (stream is null) return null;
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }

    private static RoundedPanel NewCard(int width, int height) => new()
    {
        Size = new Size(width, height),
        BackColor = Surface,
        BorderColor = Border,
        CornerRadius = 12
    };

    private static Label NewLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = new Font("Microsoft YaHei UI", size, style),
        AutoSize = true
    };

    private static Label NewSmallLabel(string text) => NewLabel(text, 8F, FontStyle.Regular, SecondaryText);

    private static RoundedTextBox NewTextBox() => new()
    {
        BackColor = SurfaceRaised,
        ForeColor = PrimaryText,
        BorderColor = Border,
        FocusBorderColor = Accent,
        CornerRadius = 8,
        Font = new Font("Microsoft YaHei UI", 9F),
        PlaceholderText = "请选择目录"
    };

    private static SpeedDropDown NewSpeedDropDown() => new()
    {
        BackColor = SurfaceRaised,
        ForeColor = PrimaryText,
        AccessibleName = "播放速度",
        FieldColor = SurfaceRaised,
        HoverColor = SurfaceHover,
        OutlineColor = Border,
        FocusOutlineColor = Accent,
        AccentColor = Accent,
        Font = new Font("Microsoft YaHei UI", 9F),
        CornerRadius = 8
    };

    private static DarkComboBox NewCombo() => new()
    {
        BackColor = SurfaceRaised,
        ForeColor = PrimaryText,
        FieldColor = SurfaceRaised,
        HoverColor = SurfaceHover,
        OutlineColor = Border,
        AccentColor = Accent,
        Font = new Font("Microsoft YaHei UI", 9F),
        ItemHeight = 32,
        CornerRadius = 8
    };

    private static RoundButton NewButton(string text, Color background, Color foreground) => new()
    {
        Text = text,
        BackColor = background,
        ForeColor = foreground,
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
        CornerRadius = 9
    };

    private static AccentCheckBox NewCheckBox(string text) => new()
    {
        Text = text,
        ForeColor = PrimaryText,
        BackColor = Color.Transparent,
        Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        AutoSize = false
    };

    private static NavigationButton NewNavButton(string text) => new()
    {
        Text = text,
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 4)
    };

    private static AccentRadioButton NewRadio(string text) => new()
    {
        Text = text,
        ForeColor = PrimaryText,
        BackColor = Color.Transparent,
        Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        AutoCheck = false
    };
}
