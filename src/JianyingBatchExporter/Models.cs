using System.Text.Json.Serialization;

namespace JianyingBatchExporter;

public enum VerticalMode
{
    CenterCrop,
    BlurredBackground,
    BlackBars
}

public enum WorkspaceMode
{
    FolderBatch,
    JianyingDraft
}

public sealed record BatchVideoItem(int Index, string InputPath)
{
    public string FileName => Path.GetFileName(InputPath);
    public string Extension => Path.GetExtension(InputPath).TrimStart('.').ToUpperInvariant();
}

public sealed record BatchItemProgress(
    int RowIndex,
    int CompletedCount,
    int TotalCount,
    string FileName,
    string Status,
    string? Message = null);

public sealed record BatchRunSummary(int TotalCount, int CompletedCount, int FailedCount);

public sealed record DraftInfo(
    string Id,
    string Name,
    string FolderPath,
    string ContentPath,
    DateTime ModifiedAt)
{
    public override string ToString() => $"{Name}  ·  {ModifiedAt:MM-dd HH:mm}";
}

public sealed record TimelineSegment(
    int Index,
    long StartUs,
    long DurationUs,
    string MaterialId,
    string SourceName,
    int OverlappingTrackCount)
{
    public long EndUs => StartUs + DurationUs;
    public string StartText => TimeText(StartUs);
    public string EndText => TimeText(EndUs);
    public string DurationText => TimeText(DurationUs);

    public static string TimeText(long microseconds)
    {
        var value = TimeSpan.FromTicks(Math.Max(0, microseconds) * 10);
        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss\.fff")
            : value.ToString(@"mm\:ss\.fff");
    }
}

public sealed record VideoTrackPlan(
    string TrackId,
    int TrackIndex,
    IReadOnlyList<TimelineSegment> Segments,
    long CoveredDurationUs)
{
    public string DisplayName => $"视频轨道 {TrackIndex + 1}  ·  {Segments.Count} 段  ·  {TimelineSegment.TimeText(CoveredDurationUs)}";
    public override string ToString() => DisplayName;
}

public sealed record DraftPlan(
    DraftInfo Draft,
    string PlainJsonPath,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<VideoTrackPlan> VideoTracks,
    IReadOnlyList<long> EditBoundariesUs,
    IReadOnlyDictionary<string, int> TrackTypeCounts,
    IReadOnlyDictionary<string, int> MaterialTypeCounts)
{
    public int TotalTrackCount => TrackTypeCounts.Values.Sum();
    public int CompositeTrackCount => Math.Max(0, TotalTrackCount - 1);
    public int NativeEffectMaterialCount => MaterialTypeCounts
        .Where(item => item.Key.Contains("effect", StringComparison.OrdinalIgnoreCase)
                       || item.Key.Contains("filter", StringComparison.OrdinalIgnoreCase)
                       || item.Key.Contains("text", StringComparison.OrdinalIgnoreCase)
                       || item.Key.Contains("sticker", StringComparison.OrdinalIgnoreCase)
                       || item.Key.Contains("transition", StringComparison.OrdinalIgnoreCase))
        .Sum(item => item.Value);
    public bool HasNativeCompositeContent => CompositeTrackCount > 0 || NativeEffectMaterialCount > 0;
}

public sealed class AutomationCalibration
{
    public RelativePoint? TimelinePoint { get; set; }
    public RelativePoint? FileNamePoint { get; set; }
    public RelativePoint? ExportButtonPoint { get; set; }

    [JsonIgnore]
    public bool IsComplete => TimelinePoint is not null && FileNamePoint is not null && ExportButtonPoint is not null;
}

public sealed record RelativePoint(int X, int Y);

public sealed class AppSettings
{
    public string InputFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "JianyingBatchExports");
    public VerticalMode VerticalMode { get; set; } = VerticalMode.BlurredBackground;
    public decimal Speed { get; set; } = 1.1m;
    public bool WatermarkEnabled { get; set; } = true;
    public string WatermarkImagePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "watermark.png");
    public AutomationCalibration Calibration { get; set; } = new();
}
