using System.Reflection;
using JianyingBatchExporter;

namespace JianyingBatchExporterTests;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AssertDefaultSettings();
        AssertCompressionPresetContract();
        AssertFixedBitrateEncodingArguments();
        AssertAutomaticBitratesRejected();
        AssertRuntimeStorage();
        AssertDraftParserRuntimeConfiguration();
        using var form = new MainForm();
        AssertSettingsStorage(form);

        var centerCrop = GetField<RadioButton>(form, "_centerCropRadio");
        var blurredBackground = GetField<RadioButton>(form, "_blurRadio");
        var blackBars = GetField<RadioButton>(form, "_blackBarsRadio");
        var blackBarsOption = GetField<Control>(form, "_blackBarsOption");
        var centerCropOption = GetField<Control>(form, "_centerCropOption");
        var blurredBackgroundOption = GetField<Control>(form, "_blurOption");
        var settings = GetField<AppSettings>(form, "_settings");
        var rotationDirection = GetField<ComboBox>(form, "_rotationDirectionCombo");
        var rotationDegrees = GetField<Control>(form, "_rotationDegrees");
        var zoom = GetField<ComboBox>(form, "_zoomCombo");
        var filter = GetField<ComboBox>(form, "_filterCombo");
        var processingCard = GetField<Control>(form, "_visualEffectsCard");
        var speedPreset = GetField<Control>(form, "_speedPreset");
        var compressionPreset = GetField<ComboBox>(form, "_compressionPresetCombo");
        var videoBitrate = GetField<ComboBox>(form, "_videoBitrateCombo");
        var audioBitrate = GetField<ComboBox>(form, "_audioBitrateCombo");
        var folderNav = GetField<Control>(form, "_folderNav");
        var pageTitle = GetField<Control>(form, "_pageTitle");

        Assert(form.Text == "批量处理助手", "main window uses the renamed product title");
        Assert(folderNav.Text.Contains("视频批量") && pageTitle.Text == "视频批量处理",
            "folder batch workspace is renamed to video batch");
        Assert(blackBarsOption.Top < centerCropOption.Top && centerCropOption.Top < blurredBackgroundOption.Top,
            "black bars is the first visual adaptation option");
        Assert(ReferenceEquals(speedPreset.Parent, processingCard),
            "playback speed is merged into the processing parameters card");
        Assert(ReferenceEquals(compressionPreset.Parent, processingCard),
            "compression selection is merged into the processing parameters card");
        Assert(ReferenceEquals(videoBitrate.Parent, processingCard) && ReferenceEquals(audioBitrate.Parent, processingCard),
            "video and audio bitrate selections are merged into the processing parameters card");
        Assert(videoBitrate.Items.Count == 12 && videoBitrate.Items[0]!.ToString()!.Contains("500")
            && videoBitrate.Items[11]!.ToString()!.Contains("8000"),
            "video bitrate lists the complete 500 to 8000 kbps range without automatic");
        Assert(audioBitrate.Items.Count == 7 && audioBitrate.Items[0]!.ToString()!.Contains("64")
            && audioBitrate.Items[6]!.ToString()!.Contains("320"),
            "audio bitrate lists the complete 64 to 320 kbps range without automatic");
        Assert(processingCard.Controls.OfType<Label>().Any(label => label.Text == "处理参数"),
            "the merged card is named processing parameters");
        Assert(form.GetType().GetMethod("BuildSpeedCard", BindingFlags.Instance | BindingFlags.NonPublic) is null,
            "the separate playback speed card no longer exists");
        Invoke(form, "SwitchWorkspace", WorkspaceMode.JianyingDraft);
        Assert(!rotationDirection.Enabled && !zoom.Enabled && !filter.Enabled
            && speedPreset.Enabled && compressionPreset.Enabled && videoBitrate.Enabled && audioBitrate.Enabled,
            "draft mode keeps playback speed and compression available and disables folder-only visual adjustments");
        Invoke(form, "SwitchWorkspace", WorkspaceMode.FolderBatch);
        Assert(rotationDirection.Enabled && zoom.Enabled && filter.Enabled
            && speedPreset.Enabled && compressionPreset.Enabled && videoBitrate.Enabled && audioBitrate.Enabled,
            "folder mode enables visual adjustments, playback speed, and compression together");

        settings.VerticalMode = VerticalMode.None;
        settings.RotationDirection = RotationDirection.None;
        settings.RotationDegrees = 0;
        settings.ZoomPercent = 100;
        settings.VideoFilterPreset = VideoFilterPreset.None;
        settings.CompressionPreset = CompressionPreset.Efficient576p;
        settings.Speed = 1.0m;
        settings.VideoBitrateKbps = 0;
        settings.AudioBitrateKbps = 0;
        Invoke(form, "ApplySettingsToUi");
        Assert(rotationDirection.SelectedIndex == 0, "rotation defaults to none");
        Assert(rotationDegrees.Text == "1" && !rotationDegrees.Enabled,
            "rotation angle defaults to 1 degree and is disabled when rotation is off");
        Assert(zoom.SelectedItem?.ToString() == "不缩放", "zoom defaults to no scaling");
        Assert(filter.SelectedIndex == 0, "video filter defaults to none");
        Assert(speedPreset.GetType().GetProperty("SelectedItem")!.GetValue(speedPreset)?.ToString() == "1.0x",
            "playback speed defaults to 1.0x");
        Assert(compressionPreset.Items.Count == 3 && compressionPreset.SelectedIndex == 0,
            "576p is selected by default while 720p and 1080p remain available");
        Assert(compressionPreset.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(["576P", "720P", "1080P"]),
            "output compression labels are the three plain resolution names");
        var rebuildDropDown = videoBitrate.GetType().GetMethod(
            "RebuildDropDownMenu",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing custom drop-down menu builder");
        rebuildDropDown.Invoke(videoBitrate, null);
        var bitrateMenu = GetField<ContextMenuStrip>(videoBitrate, "_menu");
        Assert(!bitrateMenu.AutoSize && bitrateMenu.Items.Count == videoBitrate.Items.Count,
            "custom dark menu contains every bitrate option without a native scroll bar");
        Assert(videoBitrate.SelectedIndex == 6 && audioBitrate.SelectedIndex == 2,
            "old automatic settings migrate to the 576p recommendation of 2500/128 kbps");
        bitrateMenu.Items[1].PerformClick();
        Assert(videoBitrate.SelectedIndex == 1, "custom dark menu changes the selected bitrate");
        videoBitrate.SelectedIndex = 6;
        var comboWndProc = videoBitrate.GetType().GetMethod(
            "WndProc",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing combo box window procedure");
        var wheelMessage = Message.Create(videoBitrate.Handle, 0x020A, (IntPtr)(-120 << 16), IntPtr.Zero);
        object[] wheelArguments = [wheelMessage];
        comboWndProc.Invoke(videoBitrate, wheelArguments);
        Assert(videoBitrate.SelectedIndex == 6,
            "mouse wheel does not change a closed combo box selection");
        compressionPreset.SelectedIndex = 1;
        Assert(videoBitrate.SelectedItem?.ToString() == "4000 kbps"
            && audioBitrate.SelectedItem?.ToString() == "128 kbps",
            "selecting 720p recommends 4000/128 kbps");
        videoBitrate.SelectedIndex = 3;
        audioBitrate.SelectedIndex = 3;
        Assert(videoBitrate.SelectedItem?.ToString() == "1500 kbps"
            && audioBitrate.SelectedItem?.ToString() == "160 kbps",
            "users can override both recommended bitrates after selecting a resolution");
        compressionPreset.SelectedIndex = 2;
        Assert(videoBitrate.SelectedItem?.ToString() == "6000 kbps"
            && audioBitrate.SelectedItem?.ToString() == "128 kbps",
            "selecting 1080p recommends 6000/128 kbps");
        compressionPreset.SelectedIndex = 0;
        Assert(videoBitrate.SelectedItem?.ToString() == "2500 kbps"
            && audioBitrate.SelectedItem?.ToString() == "128 kbps",
            "selecting 576p recommends 2500/128 kbps");

        Click(centerCrop);
        AssertSelection(centerCrop, blurredBackground, blackBars);

        Click(blurredBackground);
        AssertSelection(blurredBackground, centerCrop, blackBars);

        Click(blurredBackground);
        AssertNoneSelected(centerCrop, blurredBackground, blackBars);

        Click(blackBarsOption);
        AssertSelection(blackBars, centerCrop, blurredBackground);

        Click(blackBarsOption);
        AssertNoneSelected(centerCrop, blurredBackground, blackBars);

        Invoke(form, "SyncSettingsFromUi");
        Assert(settings.VerticalMode == VerticalMode.None, "cleared selection is stored as None");

        settings.VerticalMode = VerticalMode.CenterCrop;
        settings.RotationDirection = RotationDirection.CounterClockwise;
        settings.RotationDegrees = 17;
        settings.ZoomPercent = 120;
        settings.VideoFilterPreset = VideoFilterPreset.Warm;
        settings.CompressionPreset = CompressionPreset.HighQuality1080p;
        settings.VideoBitrateKbps = 8000;
        settings.AudioBitrateKbps = 320;
        Invoke(form, "ApplySettingsToUi");
        AssertSelection(centerCrop, blurredBackground, blackBars);
        Assert(rotationDirection.SelectedIndex == 2 && rotationDegrees.Text == "17",
            "saved counter-clockwise angle is restored in the UI");
        Assert(zoom.SelectedItem?.ToString() == "120%", "saved zoom is restored in the UI");
        Assert(filter.SelectedIndex == 3, "saved filter is restored in the UI");
        Assert(compressionPreset.SelectedIndex == 2, "saved high-quality compression is restored in the UI");
        Assert(videoBitrate.SelectedIndex == 11 && audioBitrate.SelectedIndex == 6,
            "saved maximum video and audio bitrate selections are restored in the UI");

        rotationDirection.SelectedIndex = 1;
        rotationDegrees.Text = "3";
        zoom.SelectedIndex = 0;
        filter.SelectedIndex = 5;
        compressionPreset.SelectedIndex = 1;
        videoBitrate.SelectedIndex = 3;
        audioBitrate.SelectedIndex = 3;
        Invoke(form, "SyncSettingsFromUi");
        Assert(settings.RotationDirection == RotationDirection.Clockwise && settings.RotationDegrees == 3,
            "clockwise 3-degree rotation is persisted");
        Assert(settings.ZoomPercent == 90, "zoom selection is persisted");
        Assert(settings.VideoFilterPreset == VideoFilterPreset.BlackAndWhite, "filter selection is persisted");
        Assert(settings.CompressionPreset == CompressionPreset.Efficient720p,
            "720p compression selection is persisted");
        Assert(settings.VideoBitrateKbps == 1500 && settings.AudioBitrateKbps == 160,
            "manual 1500 video and 160 audio bitrate selections are persisted");

        settings.VerticalMode = VerticalMode.None;
        Invoke(form, "ApplySettingsToUi");
        AssertNoneSelected(centerCrop, blurredBackground, blackBars);

        var inputFolder = GetField<Control>(form, "_inputFolder");
        var outputFolder = GetField<Control>(form, "_outputFolder");
        inputFolder.Text = string.Empty;
        outputFolder.Text = string.Empty;
        Invoke(form, "SyncSettingsFromUi");
        Assert(string.IsNullOrEmpty(settings.InputFolder), "blank input folder remains blank");
        Assert(string.IsNullOrEmpty(settings.OutputFolder), "blank output folder remains blank");

        settings.WatermarkEnabled = true;
        settings.WatermarkImagePath = "watermark.png";
        var processingWatermark = Invoke<string>(form, "WatermarkPathForProcessing");
        Assert(processingWatermark == Path.Combine(AppContext.BaseDirectory, "watermark.png"),
            "relative watermark path resolves from the application directory");

        AssertHighQualityEncodingArguments();
        AssertVisualFilterArguments();
        AssertInvalidRotationRejected();
        AssertUnsetModeRejected();
        AssertElapsedFormatting();
        Console.WriteLine("ALL TESTS PASSED");
    }

    private static T GetField<T>(object instance, string name) where T : class
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field: {name}");
        return (T)(field.GetValue(instance)
            ?? throw new InvalidOperationException($"Null field: {name}"));
    }

    private static void Click(Control control) =>
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [EventArgs.Empty]);

    private static void Invoke(object instance, string methodName, params object?[] arguments) =>
        instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);

    private static T Invoke<T>(object instance, string methodName) =>
        (T)(instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, null)
            ?? throw new InvalidOperationException($"Null result: {methodName}"));

    private static void AssertCompressionPresetContract()
    {
        var settings = new AppSettings();
        Assert(settings.CompressionPreset == CompressionPreset.Efficient576p,
            "576p compression is the first-use default");

        var method = typeof(FfmpegService).GetMethod(
            "AddEncodingArguments",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing encoding argument builder");
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        method.Invoke(null, [startInfo, CompressionPreset.Efficient576p, 2500]);
        var arguments = startInfo.ArgumentList.ToArray();
        Assert(arguments.Contains("libx264") && arguments.Contains("faster") && arguments.Contains("2500k")
            && !arguments.Contains("-crf"),
            "576p uses the existing x264 profile with its recommended fixed bitrate");
        Assert(arguments.Contains("192") && arguments.Contains("19") && arguments.Contains("none"),
            "efficient 576p compression preserves the measured GOP and B-pyramid settings");

        var dimensionsMethod = typeof(FfmpegService).GetMethod(
            "OutputDimensions",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing output dimensions resolver");
        Assert(((ValueTuple<int, int>)dimensionsMethod.Invoke(null, [CompressionPreset.Efficient576p])!) == (576, 1024),
            "576p resolves to exactly 576x1024");
        Assert(((ValueTuple<int, int>)dimensionsMethod.Invoke(null, [CompressionPreset.Efficient720p])!) == (720, 1280),
            "720p remains exactly 720x1280");
        Assert(((ValueTuple<int, int>)dimensionsMethod.Invoke(null, [CompressionPreset.HighQuality1080p])!) == (1080, 1920),
            "1080p remains exactly 1080x1920");
    }

    private static void AssertFixedBitrateEncodingArguments()
    {
        var method = typeof(FfmpegService).GetMethod(
            "AddEncodingArguments",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing encoding argument builder");
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        method.Invoke(null, [startInfo, CompressionPreset.Efficient720p, 1500]);
        var arguments = startInfo.ArgumentList.ToArray();
        Assert(arguments.Contains("1500k") && arguments.Contains("2250k") && arguments.Contains("3000k"),
            "fixed 1500 kbps video bitrate sets average, 1.5x maximum, and 2x buffer rates");
        Assert(!arguments.Contains("-crf"),
            "fixed video bitrate does not use CRF mode");
    }

    private static void AssertAutomaticBitratesRejected()
    {
        try
        {
            _ = new FfmpegService().ProcessAsync(Application.ExecutablePath, Path.GetTempPath(),
                "unused", VerticalMode.BlackBars, RotationDirection.None, 0, 100,
                VideoFilterPreset.None, CompressionPreset.Efficient576p, 0, 128, 1.0m, null).GetAwaiter().GetResult();
            throw new InvalidOperationException("FAILED: FFmpeg accepted automatic video bitrate");
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "videoBitrateKbps")
        {
            Console.WriteLine("PASS: FFmpeg rejects the removed automatic video bitrate");
        }

        try
        {
            _ = new FfmpegService().ProcessAsync(Application.ExecutablePath, Path.GetTempPath(),
                "unused", VerticalMode.BlackBars, RotationDirection.None, 0, 100,
                VideoFilterPreset.None, CompressionPreset.Efficient576p, 2500, 0, 1.0m, null).GetAwaiter().GetResult();
            throw new InvalidOperationException("FAILED: FFmpeg accepted automatic audio bitrate");
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "audioBitrateKbps")
        {
            Console.WriteLine("PASS: FFmpeg rejects the removed automatic audio bitrate");
        }
    }

    private static void AssertDefaultSettings()
    {
        var settings = new AppSettings();
        Assert(string.IsNullOrEmpty(settings.InputFolder), "first-use input folder is blank");
        Assert(string.IsNullOrEmpty(settings.OutputFolder), "first-use output folder is blank");
        Assert(settings.VerticalMode == VerticalMode.BlackBars, "black bars is the default visual adaptation");
        Assert(settings.WatermarkImagePath == "watermark.png", "first-use watermark path is relative");
        Assert(settings.RotationDirection == RotationDirection.None && settings.RotationDegrees == 0,
            "rotation is disabled by default");
        Assert(settings.ZoomPercent == 100, "zoom is 100 percent by default");
        Assert(settings.VideoFilterPreset == VideoFilterPreset.None, "video filter is disabled by default");
        Assert(settings.VideoBitrateKbps == 2500 && settings.AudioBitrateKbps == 128,
            "video and audio bitrate default to the 576p recommendation");
        Assert(settings.Speed == 1.0m, "playback speed is 1.0x by default");
        Assert(settings.CompressionPreset == CompressionPreset.Efficient576p,
            "output resolution is 576x1024 by default");
    }

    private static void AssertSettingsStorage(MainForm form)
    {
        var settingsPath = GetField<string>(form, "_settingsPath");
        var expectedDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingBatchExporter");
        Assert(Path.GetDirectoryName(settingsPath) == expectedDirectory,
            "settings are stored per Windows user outside the portable package");
    }

    private static void AssertRuntimeStorage()
    {
        var draftService = new DraftService();
        var cacheDirectory = GetField<string>(draftService, "_cacheDirectory");
        var expectedDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingBatchExporter",
            "cache");
        Assert(cacheDirectory == expectedDirectory,
            "draft cache is stored per Windows user outside the portable package");
        var runtimeToolDirectory = GetField<string>(draftService, "_runtimeToolDirectory");
        Assert(runtimeToolDirectory == Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JianyingBatchExporter",
                "tools",
                "jy-draftc"),
            "draft parser configuration is stored per Windows user outside the portable package");
    }

    private static void AssertDraftParserRuntimeConfiguration()
    {
        var draftService = new DraftService();
        var installDirectory = draftService.DetectJianyingInstallDirectory();
        if (installDirectory is null)
        {
            Console.WriteLine("SKIP: Jianying is not installed, runtime parser configuration was not generated");
            return;
        }
        var method = typeof(DraftService).GetMethod(
            "PrepareDecryptTool",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing draft parser preparation method");
        var runtimeToolPath = (string)method.Invoke(draftService, [installDirectory])!;
        var environmentPath = Path.Combine(Path.GetDirectoryName(runtimeToolPath)!, ".env");
        Assert(File.Exists(runtimeToolPath), "draft parser is copied to the per-user runtime directory");
        Assert(File.Exists(environmentPath), "draft parser environment is generated per Windows user");
        Assert(File.ReadAllText(environmentPath) == $"JY_INSTALL_DIR=\"{installDirectory}\"{Environment.NewLine}",
            "draft parser environment uses the detected local Jianying installation");
        Assert(!runtimeToolPath.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase),
            "draft parser environment is not written into the portable package");
    }

    private static void AssertSelection(RadioButton selected, params RadioButton[] unselected)
    {
        Assert(selected.Checked, $"{selected.Text} is selected");
        Assert(unselected.All(item => !item.Checked), "mode options remain mutually exclusive");
        Assert(unselected.Append(selected).Count(item => item.Checked) == 1, "at most one mode is selected");
    }

    private static void AssertNoneSelected(params RadioButton[] options) =>
        Assert(options.All(item => !item.Checked), "clicking the selected mode clears it");

    private static void AssertHighQualityEncodingArguments()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        var method = typeof(FfmpegService).GetMethod(
            "AddEncodingArguments",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing encoding argument builder");
        method.Invoke(null, [startInfo, CompressionPreset.HighQuality1080p, 6000]);
        var arguments = startInfo.ArgumentList.ToArray();
        Assert(arguments.Contains("libx264") && arguments.Contains("veryfast")
            && arguments.Contains("6000k") && !arguments.Contains("-crf"),
            "1080p uses x264 veryfast with its recommended fixed bitrate");
    }

    private static void AssertVisualFilterArguments()
    {
        var rotationMethod = typeof(FfmpegService).GetMethod(
            "BuildRotationFilter",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing rotation filter builder");
        var visualMethod = typeof(FfmpegService).GetMethod(
            "BuildVisualAdjustmentFilter",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing visual adjustment filter builder");
        string Rotation(RotationDirection direction, int degrees) =>
            (string)rotationMethod.Invoke(null, [direction, degrees])!;
        string Visual(int zoom, VideoFilterPreset preset, int width = 1080, int height = 1920) =>
            (string)visualMethod.Invoke(null, [zoom, preset, width, height])!;

        Assert(Rotation(RotationDirection.None, 0) == string.Empty, "no rotation adds no filter work");
        Assert(Rotation(RotationDirection.Clockwise, 90) == "transpose=clock",
            "clockwise 90-degree rotation uses one transpose");
        Assert(Rotation(RotationDirection.CounterClockwise, 90) == "transpose=cclock",
            "counter-clockwise 90-degree rotation uses one transpose");
        Assert(Rotation(RotationDirection.Clockwise, 180) == "hflip,vflip",
            "180-degree rotation uses two lightweight flips");
        Assert(Rotation(RotationDirection.Clockwise, 270) == "transpose=cclock",
            "clockwise 270-degree rotation reuses the counter-clockwise transpose");
        Assert(Rotation(RotationDirection.Clockwise, 1).Contains("rotate=1*PI/180"),
            "clockwise 1-degree rotation uses the arbitrary-angle filter");
        Assert(Rotation(RotationDirection.CounterClockwise, 3).Contains("rotate=-3*PI/180"),
            "counter-clockwise 3-degree rotation uses a negative FFmpeg angle");

        var shrink = Visual(90, VideoFilterPreset.None);
        Assert(shrink.Contains("scale=972:1728:flags=fast_bilinear") && shrink.Contains("pad=1080:1920"),
            "90-percent zoom uses fast scaling and centered padding");
        var shrink720 = Visual(90, VideoFilterPreset.None, 720, 1280);
        Assert(shrink720.Contains("scale=648:1152:flags=fast_bilinear") && shrink720.Contains("pad=720:1280"),
            "efficient 720p zoom uses its own output dimensions");
        var vividZoom = Visual(120, VideoFilterPreset.Vivid);
        Assert(vividZoom.Contains("eq=contrast=1.05:saturation=1.20") && vividZoom.Contains("scale=1296:2304:flags=fast_bilinear") && vividZoom.EndsWith("crop=1080:1920"),
            "vivid 120-percent zoom combines color, fast scaling, and center crop");
        Assert(vividZoom.IndexOf("eq=", StringComparison.Ordinal) < vividZoom.IndexOf("scale=", StringComparison.Ordinal),
            "the filter affects video content before zoom padding or cropping");
        Assert(Visual(100, VideoFilterPreset.Warm).StartsWith("colorbalance=rs=0.08", StringComparison.Ordinal), "warm filter is lightweight color balance");
        Assert(Visual(100, VideoFilterPreset.Cool).StartsWith("colorbalance=rs=-0.04", StringComparison.Ordinal), "cool filter is lightweight color balance");
        Assert(Visual(100, VideoFilterPreset.BlackAndWhite) == "hue=s=0", "black-and-white filter removes saturation only");
    }

    private static void AssertInvalidRotationRejected()
    {
        try
        {
            _ = new FfmpegService().ProcessAsync(Application.ExecutablePath, Path.GetTempPath(),
                "unused", VerticalMode.CenterCrop, RotationDirection.Clockwise, 0, 100,
                VideoFilterPreset.None, CompressionPreset.Efficient720p, 4000, 128, 1.0m, null).GetAwaiter().GetResult();
            throw new InvalidOperationException("FAILED: FFmpeg accepted an invalid rotation angle");
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "rotationDegrees")
        {
            Console.WriteLine("PASS: FFmpeg rejects an invalid rotation angle");
        }
    }

    private static void AssertUnsetModeRejected()
    {
        try
        {
            _ = new FfmpegService().ProcessAsync(Application.ExecutablePath, Path.GetTempPath(),
                "unused", VerticalMode.None, RotationDirection.None, 0, 100, VideoFilterPreset.None,
                CompressionPreset.Efficient720p, 4000, 128, 1.0m, null).GetAwaiter().GetResult();
            throw new InvalidOperationException("FAILED: FFmpeg accepted an unset mode");
        }
        catch (ArgumentException exception) when (exception.ParamName == "mode")
        {
            Console.WriteLine("PASS: FFmpeg rejects an unset mode");
        }
    }


    private static void AssertElapsedFormatting()
    {
        var method = typeof(MainForm).GetMethod(
            "FormatElapsedTime",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing elapsed-time formatter");
        string Format(TimeSpan elapsed) => (string)method.Invoke(null, [elapsed])!;

        Assert(Format(TimeSpan.FromMilliseconds(3240)) == "3.2 秒",
            "elapsed time formats sub-minute runs");
        Assert(Format(TimeSpan.FromMilliseconds(65430)) == "1 分 05.4 秒",
            "elapsed time formats minute runs");
        Assert(Format(TimeSpan.FromMilliseconds(3723800)) == "1 小时 02 分 03.8 秒",
            "elapsed time formats hour runs");
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
