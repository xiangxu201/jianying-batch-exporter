using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JianyingBatchExporter;

public sealed class JianyingAutomationService
{
    private const byte VkControl = 0x11;
    private const byte VkMenu = 0x12;
    private const byte VkHome = 0x24;
    private const byte VkDown = 0x28;
    private const byte VkEscape = 0x1B;
    private const byte VkA = 0x41;
    private const byte VkE = 0x45;
    private const byte VkI = 0x49;
    private const byte VkO = 0x4F;
    private const byte VkV = 0x56;
    private const byte VkX = 0x58;
    private const uint KeyUp = 0x0002;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;

    public event Action<int, string>? SegmentStatusChanged;

    public string GetWindowStatus()
    {
        return TryFindWindow(out var window)
            ? $"已连接剪映窗口 · {window.Width}×{window.Height}"
            : "未检测到可见的剪映编辑窗口";
    }

    public RelativePoint CaptureCursorRelativeToJianying()
    {
        if (!TryFindWindow(out var window))
            throw new InvalidOperationException("未检测到可见的剪映窗口，请先打开剪映项目。");
        if (!GetCursorPos(out var point))
            throw new InvalidOperationException("无法读取鼠标位置。");
        if (point.X < window.Left || point.X > window.Right || point.Y < window.Top || point.Y > window.Bottom)
            throw new InvalidOperationException("鼠标不在剪映窗口范围内。");
        return new RelativePoint(point.X - window.Left, point.Y - window.Top);
    }

    public async Task OpenExportDialogAsync(AutomationCalibration calibration, CancellationToken cancellationToken = default)
    {
        var window = RequireWindow();
        Activate(window);
        if (calibration.TimelinePoint is not null) Click(window, calibration.TimelinePoint);
        await Task.Delay(250, cancellationToken);
        SendChord(VkControl, VkE);
    }

    public async Task PreviewFirstSegmentAsync(
        DraftPlan plan,
        VideoTrackPlan track,
        AutomationCalibration calibration,
        CancellationToken cancellationToken = default)
    {
        if (track.Segments.Count == 0) throw new InvalidOperationException("所选主轨道没有可导出的片段。");
        if (calibration.TimelinePoint is null) throw new InvalidOperationException("请先标定时间线位置。");
        await SetRangeAsync(plan, track.Segments[0], calibration, cancellationToken);
    }

    public async Task ExportAsync(
        DraftPlan plan,
        VideoTrackPlan track,
        AutomationCalibration calibration,
        string outputRoot,
        VerticalMode verticalMode,
        CompressionPreset compressionPreset,
        int videoBitrateKbps,
        int audioBitrateKbps,
        decimal speed,
        string? watermarkImagePath,
        FfmpegService ffmpeg,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (!calibration.IsComplete)
            throw new InvalidOperationException("请先完成时间线、文件名输入框和导出按钮三项标定。");
        if (ffmpeg.ExecutablePath is null)
            throw new InvalidOperationException("未找到 FFmpeg。");
        if (track.Segments.Count == 0)
            throw new InvalidOperationException("所选主轨道没有可导出的片段。");

        var initialWindow = RequireWindow();
        ValidateCalibration(initialWindow, calibration);

        var rawDirectory = Path.Combine(outputRoot, "_jianying_raw");
        var finalDirectory = Path.Combine(outputRoot, "成品");
        Directory.CreateDirectory(rawDirectory);
        Directory.CreateDirectory(finalDirectory);

        for (var index = 0; index < track.Segments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = track.Segments[index];
            var rawBaseName = DraftService.SanitizeFileName($"{index + 1:000}_{Path.GetFileNameWithoutExtension(segment.SourceName)}");
            rawBaseName = UniqueBaseName(rawDirectory, finalDirectory, rawBaseName);

            SegmentStatusChanged?.Invoke(index, "设置导出范围");
            log?.Report($"[{index + 1}/{track.Segments.Count}] 设置范围 {segment.StartText} → {segment.EndText}");
            await SetRangeAsync(plan, segment, calibration, cancellationToken);

            var before = Directory.EnumerateFiles(rawDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var exportStartedAt = DateTime.UtcNow;
            SendChord(VkControl, VkE);
            await Task.Delay(2200, cancellationToken);

            var window = RequireWindow();
            Activate(window);
            Click(window, calibration.FileNamePoint!);
            await Task.Delay(150, cancellationToken);
            PasteText(rawBaseName);
            await Task.Delay(200, cancellationToken);
            Click(window, calibration.ExportButtonPoint!);

            SegmentStatusChanged?.Invoke(index, "剪映渲染中");
            log?.Report($"[{index + 1}/{track.Segments.Count}] 剪映正在渲染 {rawBaseName}.mp4");
            var rawFile = await WaitForExportAsync(
                rawDirectory,
                rawBaseName,
                before,
                exportStartedAt,
                TimeSpan.FromHours(2),
                cancellationToken);

            await Task.Delay(700, cancellationToken);
            if (TryFindWindow(out var completedWindow)) Activate(completedWindow);
            SendKey(VkEscape);
            await Task.Delay(500, cancellationToken);
            SendKey(VkEscape);

            SegmentStatusChanged?.Invoke(index, "竖版与倍速处理");
            var ffmpegProgress = new Progress<string>(message => log?.Report($"[{index + 1}/{track.Segments.Count}] {message}"));
            var finalFile = await ffmpeg.ProcessAsync(
                rawFile,
                finalDirectory,
                rawBaseName,
                verticalMode,
                RotationDirection.None,
                0,
                100,
                VideoFilterPreset.None,
                compressionPreset,
                videoBitrateKbps,
                audioBitrateKbps,
                speed,
                watermarkImagePath,
                ffmpegProgress,
                cancellationToken);
            SegmentStatusChanged?.Invoke(index, "完成");
            log?.Report($"[{index + 1}/{track.Segments.Count}] 完成：{finalFile}");
        }

        if (TryFindWindow(out var finalWindow))
        {
            Activate(finalWindow);
            if (calibration.TimelinePoint is not null) Click(finalWindow, calibration.TimelinePoint);
            SendChord(VkMenu, VkX);
        }
    }

    private async Task SetRangeAsync(
        DraftPlan plan,
        TimelineSegment segment,
        AutomationCalibration calibration,
        CancellationToken cancellationToken)
    {
        var window = RequireWindow();
        Activate(window);
        Click(window, calibration.TimelinePoint!);
        await Task.Delay(200, cancellationToken);
        SendChord(VkMenu, VkX);
        await Task.Delay(80, cancellationToken);
        SendKey(VkHome);
        await Task.Delay(120, cancellationToken);

        var boundaries = plan.EditBoundariesUs;
        var startIndex = FindBoundaryIndex(boundaries, segment.StartUs);
        var endIndex = FindBoundaryIndex(boundaries, segment.EndUs);
        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            throw new InvalidOperationException($"无法把片段 {segment.Index} 映射到剪映编辑点。");

        for (var index = 0; index < startIndex; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendKey(VkDown);
            await Task.Delay(35, cancellationToken);
        }
        SendKey(VkI);
        await Task.Delay(80, cancellationToken);
        for (var index = startIndex; index < endIndex; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendKey(VkDown);
            await Task.Delay(35, cancellationToken);
        }
        SendKey(VkO);
        await Task.Delay(180, cancellationToken);
    }

    private static int FindBoundaryIndex(IReadOnlyList<long> boundaries, long value)
    {
        for (var index = 0; index < boundaries.Count; index++)
        {
            if (boundaries[index] == value) return index;
        }
        return -1;
    }

    private static async Task<string> WaitForExportAsync(
        string directory,
        string baseName,
        HashSet<string> filesBefore,
        DateTime startedAt,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? candidate = null;
        long lastLength = -1;
        var stableChecks = 0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidate = Directory.EnumerateFiles(directory)
                .Where(path => !filesBefore.Contains(path))
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .Where(path => File.GetLastWriteTimeUtc(path) >= startedAt.AddSeconds(-2))
                .Where(path => new[] { ".mp4", ".mov", ".mkv" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (candidate is not null)
            {
                var length = new FileInfo(candidate).Length;
                stableChecks = length > 0 && length == lastLength ? stableChecks + 1 : 0;
                lastLength = length;
                if (stableChecks >= 2 && IsFileReady(candidate)) return candidate;
            }

            await Task.Delay(1000, cancellationToken);
        }
        throw new TimeoutException($"等待剪映导出超时：{baseName}");
    }

    private static string UniqueBaseName(string rawDirectory, string finalDirectory, string baseName)
    {
        bool Exists(string name) =>
            Directory.EnumerateFiles(rawDirectory, name + ".*").Any() ||
            Directory.EnumerateFiles(finalDirectory, name + ".*").Any();
        if (!Exists(baseName)) return baseName;
        for (var index = 2; index < 10000; index++)
        {
            var candidate = $"{baseName}_{index:000}";
            if (!Exists(candidate)) return candidate;
        }
        throw new IOException("无法生成不冲突的导出名称。");
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return stream.Length > 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void ValidateCalibration(WindowRect window, AutomationCalibration calibration)
    {
        foreach (var (name, point) in new[]
        {
            ("时间线位置", calibration.TimelinePoint!),
            ("文件名输入框", calibration.FileNamePoint!),
            ("导出按钮", calibration.ExportButtonPoint!)
        })
        {
            if (point.X < 0 || point.Y < 0 || point.X >= window.Width || point.Y >= window.Height)
                throw new InvalidOperationException($"{name}的标定位置已超出当前剪映窗口，请重新标定后再开始。 ");
        }
    }

    private static void PasteText(string text)
    {
        string? previous = null;
        try { if (Clipboard.ContainsText()) previous = Clipboard.GetText(); }
        catch (ExternalException) { }

        Clipboard.SetText(text);
        SendChord(VkControl, VkA);
        Thread.Sleep(70);
        SendChord(VkControl, VkV);
        Thread.Sleep(80);

        if (previous is not null)
        {
            try { Clipboard.SetText(previous); }
            catch (ExternalException) { }
        }
    }

    private static WindowRect RequireWindow()
    {
        if (!TryFindWindow(out var window))
            throw new InvalidOperationException("未检测到可见的剪映编辑窗口。请打开目标草稿并停留在编辑页面。");
        return window;
    }

    private static bool TryFindWindow(out WindowRect result)
    {
        var candidates = new List<WindowRect>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (!process.ProcessName.Equals("JianyingPro", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception) { return true; }

            if (!GetWindowRect(handle, out var rect)) return true;
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width >= 800 && height >= 500)
                candidates.Add(new WindowRect(handle, rect.Left, rect.Top, rect.Right, rect.Bottom));
            return true;
        }, IntPtr.Zero);

        result = candidates.OrderByDescending(item => item.Width * item.Height).FirstOrDefault();
        return result.Handle != IntPtr.Zero;
    }

    private static void Activate(WindowRect window)
    {
        var currentThread = GetCurrentThreadId();
        var targetThread = GetWindowThreadProcessId(window.Handle, out _);
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var attachedTarget = targetThread != 0 && targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
        var attachedForeground = foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread
            && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            ShowWindow(window.Handle, 9);
            BringWindowToTop(window.Handle);
            SetForegroundWindow(window.Handle);
            SetFocus(window.Handle);
        }
        finally
        {
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
        }
        Thread.Sleep(160);
    }

    private static void Click(WindowRect window, RelativePoint point)
    {
        SetCursorPos(window.Left + point.X, window.Top + point.Y);
        Thread.Sleep(50);
        mouse_event(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    private static void SendChord(byte modifier, byte key)
    {
        keybd_event(modifier, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUp, UIntPtr.Zero);
        keybd_event(modifier, 0, KeyUp, UIntPtr.Zero);
        Thread.Sleep(70);
    }

    private static void SendKey(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUp, UIntPtr.Zero);
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    private readonly record struct WindowRect(IntPtr Handle, int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
}
