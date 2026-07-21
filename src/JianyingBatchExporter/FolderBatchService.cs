namespace JianyingBatchExporter;

public sealed class FolderBatchService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".webm",
        ".wmv", ".flv", ".ts", ".mts", ".m2ts"
    };

    public IReadOnlyList<BatchVideoItem> Discover(string inputFolder, string? outputFolder = null)
    {
        if (string.IsNullOrWhiteSpace(inputFolder))
            throw new ArgumentException("请选择输入目录。", nameof(inputFolder));

        var inputRoot = Path.GetFullPath(inputFolder);
        if (!Directory.Exists(inputRoot))
            throw new DirectoryNotFoundException($"输入目录不存在：{inputRoot}");

        var outputRoot = string.IsNullOrWhiteSpace(outputFolder)
            ? null
            : Path.GetFullPath(outputFolder);
        if (outputRoot is not null && string.Equals(inputRoot, outputRoot, StringComparison.OrdinalIgnoreCase))
            outputRoot = null;

        return Directory.EnumerateFiles(inputRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => outputRoot is null || !IsInside(path, outputRoot))
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .Select((path, index) => new BatchVideoItem(index + 1, path))
            .ToList();
    }

    public async Task<BatchRunSummary> ProcessAsync(
        IReadOnlyList<BatchVideoItem> items,
        string outputFolder,
        VerticalMode verticalMode,
        decimal speed,
        string? watermarkImagePath,
        FfmpegService ffmpeg,
        IProgress<BatchItemProgress>? progress = null,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return new BatchRunSummary(0, 0, 0);
        if (string.IsNullOrWhiteSpace(outputFolder))
            throw new ArgumentException("请选择输出目录。", nameof(outputFolder));

        var outputRoot = Path.GetFullPath(outputFolder);
        Directory.CreateDirectory(outputRoot);
        var completed = 0;
        var failed = 0;

        for (var rowIndex = 0; rowIndex < items.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[rowIndex];
            progress?.Report(new BatchItemProgress(rowIndex, completed, items.Count, item.FileName, "处理中"));
            try
            {
                var result = await ffmpeg.ProcessAsync(
                    item.InputPath,
                    outputRoot,
                    Path.GetFileNameWithoutExtension(item.FileName),
                    verticalMode,
                    speed,
                    watermarkImagePath,
                    log,
                    cancellationToken);
                completed++;
                progress?.Report(new BatchItemProgress(
                    rowIndex,
                    completed + failed,
                    items.Count,
                    item.FileName,
                    "完成",
                    result));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                failed++;
                progress?.Report(new BatchItemProgress(
                    rowIndex,
                    completed + failed,
                    items.Count,
                    item.FileName,
                    "失败",
                    exception.Message));
            }
        }

        return new BatchRunSummary(items.Count, completed, failed);
    }

    public static bool IsSupportedVideo(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static bool IsInside(string filePath, string directoryPath)
    {
        var directory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(filePath).StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }
}
