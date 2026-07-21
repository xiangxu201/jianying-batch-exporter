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

        var watermarkPath = string.IsNullOrWhiteSpace(watermarkImagePath)
            ? null
            : Path.GetFullPath(watermarkImagePath);
        if (watermarkPath is not null && !File.Exists(watermarkPath))
            throw new FileNotFoundException("水印图片不存在。", watermarkPath);

        Directory.CreateDirectory(outputDirectory);
        var safeName = DraftService.SanitizeFileName(Path.GetFileNameWithoutExtension(baseName));
        var outputPath = UniquePath(outputDirectory, safeName, ".mp4");
        var temporaryPath = Path.Combine(outputDirectory, $".{safeName}.{Guid.NewGuid():N}.processing.mp4");
        var hasAudio = await HasAudioAsync(inputPath, cancellationToken);
        var speedText = speed.ToString("0.###", CultureInfo.InvariantCulture);

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
                    "[0:v]split=2[bg][fg];" +
                    "[bg]scale=1080:1920:force_original_aspect_ratio=increase," +
                    "crop=1080:1920,gblur=sigma=25:steps=3[bg2];" +
                    "[fg]scale=1080:1920:force_original_aspect_ratio=decrease[fg2];" +
                    "[bg2][fg2]overlay=(W-w)/2:(H-h)/2[base]",
                VerticalMode.CenterCrop =>
                    "[0:v]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920[base]",
                _ =>
                    "[0:v]scale=1080:1920:force_original_aspect_ratio=decrease," +
                    "pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black[base]"
            };
            var filter = baseFilter + ";" +
                "[1:v]scale=1080:1920:force_original_aspect_ratio=decrease," +
                "pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[wm];" +
                $"[base][wm]overlay=0:0:format=auto,setpts=PTS/{speedText}[vout]";
            Add(startInfo, "-filter_complex", filter, "-map", "[vout]");
        }
        else if (mode == VerticalMode.BlurredBackground)
        {
            var filter =
                "[0:v]split=2[bg][fg];" +
                "[bg]scale=1080:1920:force_original_aspect_ratio=increase," +
                "crop=1080:1920,gblur=sigma=25:steps=3[bg2];" +
                "[fg]scale=1080:1920:force_original_aspect_ratio=decrease[fg2];" +
                $"[bg2][fg2]overlay=(W-w)/2:(H-h)/2,setpts=PTS/{speedText}[vout]";
            Add(startInfo, "-filter_complex", filter, "-map", "[vout]");
        }
        else
        {
            var filter = mode == VerticalMode.CenterCrop
                ? $"scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,setpts=PTS/{speedText}"
                : $"scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black,setpts=PTS/{speedText}";
            Add(startInfo, "-vf", filter, "-map", "0:v:0");
        }

        if (hasAudio)
        {
            Add(startInfo, "-map", "0:a:0?", "-af", BuildAtempoFilter(speed), "-c:a", "aac", "-b:a", "192k");
        }
        else
        {
            Add(startInfo, "-an");
        }

        Add(startInfo,
            "-c:v", "libopenh264",
            "-b:v", "8M",
            "-maxrate", "12M",
            "-bufsize", "16M",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
            temporaryPath);

        var watermarkNote = watermarkPath is null ? string.Empty : " · 图片水印";
        progress?.Report($"FFmpeg{watermarkNote}：{Path.GetFileName(inputPath)} → {Path.GetFileName(outputPath)}");
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
        var candidates = new List<string>();
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim('"'), "ffmpeg.exe")));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Common Files", "Adobe", "CEP", "extensions", "GifGun2", ".bin", "ffmpeg.exe"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"));
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
