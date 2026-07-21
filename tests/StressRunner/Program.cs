using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using JianyingBatchExporter;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var runRoot = Path.Combine(Path.GetTempPath(), $"jianying-100-stress-{Guid.NewGuid():N}");
var inputRoot = Path.Combine(runRoot, "input");
var outputRoot = Path.Combine(runRoot, "output");
Directory.CreateDirectory(inputRoot);

try
{
    var ffmpeg = new FfmpegService();
    Assert(ffmpeg.ExecutablePath is not null, "FFmpeg is available");

    var seed = Path.Combine(inputRoot, "seed.mp4");
    await CreateOneFrameVideoAsync(ffmpeg.ExecutablePath!, seed);
    Assert(File.Exists(seed) && new FileInfo(seed).Length > 0, "create a one-frame stress-test input");

    var items = Enumerable.Range(1, 100)
        .Select(index => new BatchVideoItem(index, seed))
        .ToList();
    var folderService = new FolderBatchService();
    var progressEvents = 0;
    var timer = Stopwatch.StartNew();
    var summary = await folderService.ProcessAsync(
        items,
        outputRoot,
        VerticalMode.CenterCrop,
        1.1m,
        ffmpeg,
        new ImmediateProgress<BatchItemProgress>(_ => progressEvents++));
    timer.Stop();

    var outputs = Directory.EnumerateFiles(outputRoot, "*.mp4", SearchOption.TopDirectoryOnly).ToList();
    Assert(summary.TotalCount == 100, "batch summary contains 100 tasks");
    Assert(summary.CompletedCount == 100 && summary.FailedCount == 0, "all 100 videos finish without failure");
    Assert(outputs.Count == 100 && outputs.All(path => new FileInfo(path).Length > 0), "produce 100 non-empty MP4 files");
    var invalidResolution = new List<string>();
    foreach (var output in outputs)
    {
        if (!await Is1080By1920Async(ffmpeg.ExecutablePath!, output)) invalidResolution.Add(output);
    }
    Assert(invalidResolution.Count == 0, "all 100 outputs are exactly 1080x1920");
    Assert(!Directory.EnumerateFiles(outputRoot, "*.processing.mp4").Any(), "leave no unfinished processing files");
    Assert(progressEvents >= 200, "report start and completion state for every file");
    Console.WriteLine($"100-file elapsed: {timer.Elapsed}");

    var draftJson = Path.Combine(runRoot, "draft-100.json");
    await File.WriteAllTextAsync(draftJson, BuildSyntheticDraftJson());
    var draft = new DraftInfo("stress-100", "100段多轨压力草稿", runRoot, draftJson, DateTime.Now);
    var draftPlan = await new DraftService().LoadPlanAsync(draft);
    var mainTrack = draftPlan.VideoTracks[0];
    Assert(mainTrack.Segments.Count == 100, "parse 100 main-track segments from one draft");
    Assert(draftPlan.VideoTracks.Count == 2, "discover the main and overlay video tracks");
    Assert(mainTrack.Segments.All(segment => segment.OverlappingTrackCount >= 2), "count text/effect overlays on every exported segment");
    Assert(mainTrack.Segments.Count(segment => segment.OverlappingTrackCount == 3) == 50, "count the extra video overlay on 50 segments");
    Assert(draftPlan.EditBoundariesUs.Count == 101, "map the 100 segments to 101 ordered edit points");

    Console.WriteLine("100-FILE AND 100-SEGMENT STRESS TESTS PASSED");
}
finally
{
    if (Directory.Exists(runRoot)) Directory.Delete(runRoot, true);
}

static async Task CreateOneFrameVideoAsync(string ffmpegPath, string outputPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    foreach (var argument in new[]
    {
        "-y", "-hide_banner", "-loglevel", "error",
        "-f", "lavfi", "-i", "color=c=0x16b8a6:s=64x64:r=1",
        "-frames:v", "1", "-c:v", "libopenh264", "-pix_fmt", "yuv420p", outputPath
    }) startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Cannot start FFmpeg.");
    var stderrTask = process.StandardError.ReadToEndAsync();
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    var stderr = await stderrTask;
    _ = await stdoutTask;
    if (process.ExitCode != 0) throw new InvalidOperationException(stderr);
}

static async Task<bool> Is1080By1920Async(string ffmpegPath, string inputPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    foreach (var argument in new[] { "-hide_banner", "-i", inputPath }) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Cannot start FFmpeg probe.");
    var stderrTask = process.StandardError.ReadToEndAsync();
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    var stderr = await stderrTask;
    _ = await stdoutTask;
    return Regex.IsMatch(stderr, @"Video:.*\b1080x1920\b", RegexOptions.IgnoreCase);
}

static string BuildSyntheticDraftJson()
{
    var mainMaterials = Enumerable.Range(1, 100)
        .Select(index => new { id = $"main-{index:000}", material_name = $"main_{index:000}.mp4" })
        .Cast<object>();
    var overlayMaterials = Enumerable.Range(1, 50)
        .Select(index => new { id = $"overlay-{index:000}", material_name = $"overlay_{index:000}.png" })
        .Cast<object>();

    object Segment(long start, long duration, string materialId) => new
    {
        material_id = materialId,
        target_timerange = new { start, duration }
    };

    var mainSegments = Enumerable.Range(0, 100)
        .Select(index => Segment(index * 100_000L, 100_000L, $"main-{index + 1:000}"))
        .ToArray();
    var textSegments = Enumerable.Range(0, 100)
        .Select(index => Segment(index * 100_000L, 100_000L, $"text-{index + 1:000}"))
        .ToArray();
    var overlaySegments = Enumerable.Range(0, 50)
        .Select(index => Segment(index * 200_000L, 100_000L, $"overlay-{index + 1:000}"))
        .ToArray();

    var document = new
    {
        canvas_config = new { width = 1080, height = 1920 },
        materials = new { videos = mainMaterials.Concat(overlayMaterials).ToArray() },
        tracks = new object[]
        {
            new { id = "main-track", type = "video", segments = mainSegments },
            new { id = "text-track", type = "text", segments = textSegments },
            new { id = "effect-track", type = "effect", segments = new[] { Segment(0, 10_000_000L, "effect") } },
            new { id = "overlay-track", type = "video", segments = overlaySegments }
        }
    };
    return JsonSerializer.Serialize(document);
}

sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
