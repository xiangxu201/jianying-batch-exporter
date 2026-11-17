using System.Diagnostics;
using System.Globalization;

namespace JianyingBatchExporter;

public sealed class FfmpegService
{
    public string? ExecutablePath { get; }

    public FfmpegService()
    {
        ExecutablePath = DetectFfmpeg();
    }

    public async Task<string> ProcessAsync(
        string inputPath,
        string outputDirectory,
        string baseName,
        VerticalMode mode,
        RotationDirection rotationDirection,
        int rotationDegrees,
        int zoomPercent,
        VideoFilterPreset videoFilterPreset,
        CompressionPreset compressionPreset,
        int videoBitrateKbps,
        int audioBitrateKbps,
        decimal speed,
        string? watermarkImagePath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (ExecutablePath is null)
            throw new InvalidOperationException("未找到可用的 FFmpeg，无法执行竖版与倍速后处理。");
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("剪映原始导出文件不存在。", inputPath);
        if (speed is < 0.1m or > 5.0m)
            throw new ArgumentOutOfRangeException(nameof(speed), "倍速必须在 0.1x 到 5.0x 之间。");
        if (mode == VerticalMode.None)
            throw new ArgumentException("必须选择一种画面适配方式。", nameof(mode));
        if (rotationDirection != RotationDirection.None && rotationDegrees is < 1 or > 359)
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees), "旋转角度必须是 1 到 359 之间的整数。");
        if (zoomPercent is not (90 or 100 or 110 or 120))
            throw new ArgumentOutOfRangeException(nameof(zoomPercent), "缩放比例必须是 90%、100%、110% 或 120%。");
        if (videoBitrateKbps is not (500 or 800 or 1000 or 1500 or 1600 or 2000 or 2500 or 3000 or 4000 or 5000 or 6000 or 8000))
            throw new ArgumentOutOfRangeException(nameof(videoBitrateKbps), "视频码率必须使用界面提供的选项。");
        if (audioBitrateKbps is not (64 or 96 or 128 or 160 or 192 or 256 or 320))
            throw new ArgumentOutOfRangeException(nameof(audioBitrateKbps), "音频码率必须使用界面提供的选项。");

        var watermarkPath = string.IsNullOrWhiteSpace(watermarkImagePath)
            ? null
            : Path.GetFullPath(watermarkImagePath);
        if (watermarkPath is not null && !File.Exists(watermarkPath))
            throw new FileNotFoundException("水印图片不存在。", watermarkPath);

        var (outputWidth, outputHeight) = OutputDimensions(compressionPreset);
        var blurWidth = outputWidth / 4;
        var blurHeight = outputHeight / 4;
        var audioBitrate = $"{audioBitrateKbps}k";

        var processingTimer = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDirectory);
        var safeName = DraftService.SanitizeFileName(Path.GetFileNameWithoutExtension(baseName));
        var outputPath = UniquePath(outputDirectory, safeName, ".mp4");
        var temporaryPath = Path.Combine(outputDirectory, $".{safeName}.{Guid.NewGuid():N}.processing.mp4");
        var hasAudio = await HasAudioAsync(inputPath, cancellationToken);
        var speedText = speed.ToString("0.###", CultureInfo.InvariantCulture);

        var rotationFilter = BuildRotationFilter(rotationDirection, rotationDegrees);
        var inputPrefix = string.IsNullOrEmpty(rotationFilter) ? string.Empty : rotationFilter + ",";
        var visualFilter = BuildVisualAdjustmentFilter(zoomPercent, videoFilterPreset, outputWidth, outputHeight);
        var speedFilter = speed == 1.0m ? string.Empty : $"setpts=PTS/{speedText}";
        var outputFilter = string.Join(",", new[] { visualFilter, speedFilter }.Where(filter => !string.IsNullOrEmpty(filter)));
        var outputFilterSuffix = string.IsNullOrEmpty(outputFilter) ? string.Empty : "," + outputFilter;
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        Add(startInfo, "-y", "-hide_banner", "-loglevel", "warning", "-i", inputPath);
        if (watermarkPath is not null) Add(startInfo, "-i", watermarkPath);

        if (watermarkPath is not null)
        {
            var baseFilter = mode switch
            {
                VerticalMode.BlurredBackground =>
                    $"[0:v]{inputPrefix}split=2[bg][fg];" +
                    $"[bg]scale={blurWidth}:{blurHeight}:force_original_aspect_ratio=increase:flags=fast_bilinear," +
                    $"crop={blurWidth}:{blurHeight},gblur=sigma=8:steps=1,scale={outputWidth}:{outputHeight}:flags=fast_bilinear[bg2];" +
                    $"[fg]scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=decrease:flags=fast_bilinear[fg2];" +
                    "[bg2][fg2]overlay=(W-w)/2:(H-h)/2[base]",
                VerticalMode.CenterCrop =>
                    $"[0:v]{inputPrefix}scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=increase:flags=fast_bilinear,crop={outputWidth}:{outputHeight}[base]",
                _ =>
                    $"[0:v]{inputPrefix}scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=decrease:flags=fast_bilinear," +
                    $"pad={outputWidth}:{outputHeight}:(ow-iw)/2:(oh-ih)/2:black[base]"
            };
            var adjustedBase = "[base]";
            if (!string.IsNullOrEmpty(visualFilter))
            {
                baseFilter += $";[base]{visualFilter}[adjusted]";
                adjustedBase = "[adjusted]";
            }
            var filter = baseFilter + ";" +
                $"[1:v]scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=decrease:flags=fast_bilinear," +
                $"pad={outputWidth}:{outputHeight}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[wm];" +
                $"{adjustedBase}[wm]overlay=0:0:format=auto{(string.IsNullOrEmpty(speedFilter) ? string.Empty : "," + speedFilter)}[vout]";
            Add(startInfo, "-filter_complex", filter, "-map", "[vout]");
        }
        else if (mode == VerticalMode.BlurredBackground)
        {
            var filter =
                $"[0:v]{inputPrefix}split=2[bg][fg];" +
                $"[bg]scale={blurWidth}:{blurHeight}:force_original_aspect_ratio=increase:flags=fast_bilinear," +
                $"crop={blurWidth}:{blurHeight},gblur=sigma=8:steps=1,scale={outputWidth}:{outputHeight}:flags=fast_bilinear[bg2];" +
                $"[fg]scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=decrease:flags=fast_bilinear[fg2];" +
                $"[bg2][fg2]overlay=(W-w)/2:(H-h)/2{outputFilterSuffix}[vout]";
            Add(startInfo, "-filter_complex", filter, "-map", "[vout]");
        }
        else
        {
            var verticalFilter = mode == VerticalMode.CenterCrop
                ? $"scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=increase:flags=fast_bilinear,crop={outputWidth}:{outputHeight}"
                : $"scale={outputWidth}:{outputHeight}:force_original_aspect_ratio=decrease:flags=fast_bilinear,pad={outputWidth}:{outputHeight}:(ow-iw)/2:(oh-ih)/2:black";
            var filter = $"{inputPrefix}{verticalFilter}{outputFilterSuffix}";
            Add(startInfo, "-vf", filter, "-map", "0:v:0");
        }

        if (hasAudio)
        {
            Add(startInfo, "-map", "0:a:0?");
            if (speed != 1.0m)
                Add(startInfo, "-af", BuildAtempoFilter(speed));
            Add(startInfo, "-c:a", "aac", "-b:a", audioBitrate);
            if (IsEfficientPreset(compressionPreset))
                Add(startInfo, "-ar", "48000");
        }
        else
        {
            Add(startInfo, "-an");
        }

        AddEncodingArguments(startInfo, compressionPreset, videoBitrateKbps);
        Add(startInfo,
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
            temporaryPath);

        var watermarkNote = watermarkPath is null ? string.Empty : " · 图片水印";
        var compressionNote = compressionPreset switch
        {
            CompressionPreset.Efficient576p => "\u6807\u51c6\u8f93\u51fa 576P",
            CompressionPreset.Efficient720p => "\u9ad8\u6548\u538b\u7f29 720P",
            _ => "\u9ad8\u6e05\u8f93\u51fa 1080P"
        };
        progress?.Report($"FFmpeg \u00b7 {compressionNote}{watermarkNote}: {Path.GetFileName(inputPath)} \u2192 {Path.GetFileName(outputPath)}");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 FFmpeg。");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            try { await process.WaitForExitAsync(); }
            catch (InvalidOperationException) { }
            TryDeleteTemporary(temporaryPath);
            throw;
        }
        var stderr = await stderrTask;
        _ = await stdoutTask;

        if (process.ExitCode != 0 || !File.Exists(temporaryPath))
        {
            TryDeleteTemporary(temporaryPath);
            throw new InvalidOperationException($"FFmpeg 处理失败（退出码 {process.ExitCode}）：{Environment.NewLine}{stderr}".Trim());
        }

        File.Move(temporaryPath, outputPath, false);
        processingTimer.Stop();
        progress?.Report($"FFmpeg 完成：{Path.GetFileName(outputPath)} · {processingTimer.Elapsed.TotalSeconds:0.0} 秒");
        return outputPath;
    }
    public static string UniquePath(string directory, string baseName, string extension)
    {
        var candidate = Path.Combine(directory, baseName + extension);
        if (!File.Exists(candidate)) return candidate;
        for (var index = 2; index < 10000; index++)
        {
            candidate = Path.Combine(directory, $"{baseName}_{index:000}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("无法为输出文件生成不冲突的名称。");
    }

    private static string BuildRotationFilter(RotationDirection direction, int degrees)
    {
        if (direction == RotationDirection.None || degrees == 0) return string.Empty;

        var clockwiseDegrees = direction == RotationDirection.Clockwise
            ? degrees
            : 360 - degrees;
        var fastFilter = clockwiseDegrees switch
        {
            90 => "transpose=clock",
            180 => "hflip,vflip",
            270 => "transpose=cclock",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(fastFilter)) return fastFilter;

        var signedDegrees = direction == RotationDirection.Clockwise ? degrees : -degrees;
        var angle = $"{signedDegrees}*PI/180";
        return $"rotate={angle}:ow=rotw({angle}):oh=roth({angle}):c=black";
    }

    private static string BuildVisualAdjustmentFilter(int zoomPercent, VideoFilterPreset preset, int outputWidth, int outputHeight)
    {
        var filters = new List<string>();
        var presetFilter = preset switch
        {
            VideoFilterPreset.Bright => "eq=brightness=0.06",
            VideoFilterPreset.Vivid => "eq=contrast=1.05:saturation=1.20",
            VideoFilterPreset.Warm => "colorbalance=rs=0.08:bs=-0.05",
            VideoFilterPreset.Cool => "colorbalance=rs=-0.04:bs=0.08",
            VideoFilterPreset.BlackAndWhite => "hue=s=0",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(presetFilter)) filters.Add(presetFilter);

        if (zoomPercent != 100)
        {
            var width = outputWidth * zoomPercent / 100;
            var height = outputHeight * zoomPercent / 100;
            filters.Add($"scale={width}:{height}:flags=fast_bilinear");
            filters.Add(zoomPercent > 100
                ? $"crop={outputWidth}:{outputHeight}"
                : $"pad={outputWidth}:{outputHeight}:(ow-iw)/2:(oh-ih)/2:black");
        }

        return string.Join(",", filters);
    }

    private static (int Width, int Height) OutputDimensions(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Efficient576p => (576, 1024),
        CompressionPreset.Efficient720p => (720, 1280),
        _ => (1080, 1920)
    };

    private static bool IsEfficientPreset(CompressionPreset preset) =>
        preset is CompressionPreset.Efficient576p or CompressionPreset.Efficient720p;

    private static void AddEncodingArguments(ProcessStartInfo startInfo, CompressionPreset preset, int videoBitrateKbps)
    {
        Add(startInfo,
            "-c:v", "libx264",
            "-preset", IsEfficientPreset(preset) ? "faster" : "veryfast",
            "-profile:v", "high");

        var bitrate = $"{videoBitrateKbps}k";
        Add(startInfo,
            "-b:v", bitrate,
            "-maxrate", $"{videoBitrateKbps * 3 / 2}k",
            "-bufsize", $"{videoBitrateKbps * 2}k");

        if (IsEfficientPreset(preset))
        {
            Add(startInfo,
                "-g", "192",
                "-keyint_min", "19",
                "-bf", "2",
                "-b_strategy", "1",
                "-b-pyramid", "none");
        }
        else
        {
            Add(startInfo, "-level:v", "4.2");
        }
    }

    private async Task<bool> HasAudioAsync(string inputPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        Add(startInfo, "-hide_banner", "-i", inputPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 FFmpeg 媒体检测。");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        var stderr = await stderrTask;
        _ = await stdoutTask;
        return stderr.Contains(" Audio:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DetectFfmpeg()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe")
        };
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim('"'), "ffmpeg.exe")));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Common Files", "Adobe", "CEP", "extensions", "GifGun2", ".bin", "ffmpeg.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string BuildAtempoFilter(decimal speed)
    {
        var remaining = speed;
        var stages = new List<string>();
        while (remaining > 2.0m)
        {
            stages.Add("atempo=2");
            remaining /= 2.0m;
        }
        while (remaining < 0.5m)
        {
            stages.Add("atempo=0.5");
            remaining /= 0.5m;
        }
        stages.Add($"atempo={remaining.ToString("0.###", CultureInfo.InvariantCulture)}");
        return string.Join(",", stages);
    }

    private static void Add(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    }

    private static void TryDeleteTemporary(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
