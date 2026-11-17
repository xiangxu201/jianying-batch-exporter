using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace JianyingBatchExporter;

public sealed class DraftService
{
    private readonly string _rootMetaPath;
    private readonly string _cacheDirectory;
    private readonly string _decryptToolPath;
    private readonly string _runtimeToolDirectory;

    public DraftService()
    {
        _rootMetaPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingPro", "User Data", "Projects", "com.lveditor.draft", "root_meta_info.json");
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingBatchExporter",
            "cache");
        _decryptToolPath = Path.Combine(AppContext.BaseDirectory, "tools", "jy-draftc.exe");
        _runtimeToolDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingBatchExporter",
            "tools",
            "jy-draftc");
    }

    public string? DetectJianyingInstallDirectory()
    {
        var apps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JianyingPro", "Apps");
        if (!Directory.Exists(apps)) return null;

        return Directory.EnumerateDirectories(apps)
            .Where(path => File.Exists(Path.Combine(path, "videoeditor.dll")))
            .Select(path => new
            {
                Path = path,
                Version = Version.TryParse(Path.GetFileName(path), out var version)
                    ? version
                    : new Version(0, 0)
            })
            .OrderByDescending(item => item.Version)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    public IReadOnlyList<DraftInfo> DiscoverDrafts()
    {
        if (!File.Exists(_rootMetaPath)) return Array.Empty<DraftInfo>();

        using var document = JsonDocument.Parse(File.ReadAllText(_rootMetaPath));
        if (!document.RootElement.TryGetProperty("all_draft_store", out var stores) ||
            stores.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DraftInfo>();
        }

        var drafts = new List<DraftInfo>();
        foreach (var store in stores.EnumerateArray())
        {
            var folder = GetString(store, "draft_fold_path");
            if (string.IsNullOrWhiteSpace(folder)) continue;

            folder = NormalizePath(folder);
            var content = GetString(store, "draft_json_file");
            content = string.IsNullOrWhiteSpace(content)
                ? Path.Combine(folder, "draft_content.json")
                : NormalizePath(content);
            if (!File.Exists(content)) continue;

            var modified = File.GetLastWriteTime(content);
            var rawModified = GetLong(store, "tm_draft_modified");
            if (rawModified > 0)
            {
                try { modified = DateTimeOffset.FromUnixTimeMilliseconds(rawModified / 1000).LocalDateTime; }
                catch (ArgumentOutOfRangeException) { }
            }

            drafts.Add(new DraftInfo(
                GetString(store, "draft_id") ?? Path.GetFileName(folder),
                GetString(store, "draft_name") ?? Path.GetFileName(folder),
                folder,
                content,
                modified));
        }

        return drafts.OrderByDescending(draft => draft.ModifiedAt).ToList();
    }

    public async Task<DraftPlan> LoadPlanAsync(DraftInfo draft, CancellationToken cancellationToken = default)
    {
        var plainJson = await GetPlainJsonAsync(draft, cancellationToken);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(plainJson, cancellationToken));
        var root = document.RootElement;

        var canvasWidth = 0;
        var canvasHeight = 0;
        if (root.TryGetProperty("canvas_config", out var canvas))
        {
            canvasWidth = (int)GetLong(canvas, "width");
            canvasHeight = (int)GetLong(canvas, "height");
        }

        var materialNames = ReadMaterialNames(root);
        var rawTracks = ReadRawTracks(root);
        var trackTypeCounts = rawTracks
            .Where(track => track.Segments.Count > 0)
            .GroupBy(track => track.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var materialTypeCounts = ReadMaterialTypeCounts(root);
        var boundaries = rawTracks
            .SelectMany(track => track.Segments)
            .SelectMany(segment => new[] { segment.StartUs, segment.EndUs })
            .Append(0)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        var videoTracks = new List<VideoTrackPlan>();
        foreach (var track in rawTracks.Where(track => track.Type == "video"))
        {
            var segments = track.Segments
                .OrderBy(segment => segment.StartUs)
                .Select((segment, index) => new TimelineSegment(
                    index + 1,
                    segment.StartUs,
                    segment.DurationUs,
                    segment.MaterialId,
                    materialNames.GetValueOrDefault(segment.MaterialId, $"片段_{index + 1:000}"),
                    Math.Max(0, rawTracks.Count(candidate => candidate.Segments.Any(other => Overlaps(segment, other))) - 1)))
                .ToList();

            videoTracks.Add(new VideoTrackPlan(
                track.Id,
                track.Index,
                segments,
                segments.Sum(segment => segment.DurationUs)));
        }

        videoTracks = videoTracks
            .OrderByDescending(track => track.Segments.Count)
            .ThenByDescending(track => track.CoveredDurationUs)
            .ToList();

        return new DraftPlan(
            draft,
            plainJson,
            canvasWidth,
            canvasHeight,
            videoTracks,
            boundaries,
            trackTypeCounts,
            materialTypeCounts);
    }

    private async Task<string> GetPlainJsonAsync(DraftInfo draft, CancellationToken cancellationToken)
    {
        await using (var stream = File.OpenRead(draft.ContentPath))
        {
            int value;
            do { value = stream.ReadByte(); } while (value >= 0 && char.IsWhiteSpace((char)value));
            if (value == '{') return draft.ContentPath;
        }

        if (!File.Exists(_decryptToolPath))
            throw new FileNotFoundException("缺少草稿只读解析组件 jy-draftc.exe。", _decryptToolPath);

        var installDirectory = DetectJianyingInstallDirectory()
            ?? throw new InvalidOperationException("未找到包含 videoeditor.dll 的剪映安装目录。");

        Directory.CreateDirectory(_cacheDirectory);
        var stamp = File.GetLastWriteTimeUtc(draft.ContentPath).Ticks;
        var safeId = SanitizeFileName(draft.Id);
        var output = Path.Combine(_cacheDirectory, $"{safeId}-{stamp}.plain.json");
        if (File.Exists(output)) return output;
        var runtimeToolPath = PrepareDecryptTool(installDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = runtimeToolPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _runtimeToolDirectory
        };
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(draft.ContentPath);
        startInfo.ArgumentList.Add(output);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动草稿只读解析组件。");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0 || !File.Exists(output))
        {
            throw new InvalidOperationException(
                $"剪映草稿解析失败（退出码 {process.ExitCode}）。{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}".Trim());
        }

        return output;
    }

    private string PrepareDecryptTool(string installDirectory)
    {
        Directory.CreateDirectory(_runtimeToolDirectory);
        var runtimeToolPath = Path.Combine(_runtimeToolDirectory, "jy-draftc.exe");
        var source = new FileInfo(_decryptToolPath);
        var target = new FileInfo(runtimeToolPath);
        if (!target.Exists || target.Length != source.Length || target.LastWriteTimeUtc != source.LastWriteTimeUtc)
        {
            File.Copy(_decryptToolPath, runtimeToolPath, true);
            File.SetLastWriteTimeUtc(runtimeToolPath, source.LastWriteTimeUtc);
        }

        var environmentPath = Path.Combine(_runtimeToolDirectory, ".env");
        var environmentText = $"JY_INSTALL_DIR=\"{installDirectory}\"{Environment.NewLine}";
        if (!File.Exists(environmentPath) || File.ReadAllText(environmentPath) != environmentText)
            File.WriteAllText(environmentPath, environmentText, new UTF8Encoding(false));
        return runtimeToolPath;
    }

    private static Dictionary<string, string> ReadMaterialNames(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("materials", out var materials) ||
            !materials.TryGetProperty("videos", out var videos) ||
            videos.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var material in videos.EnumerateArray())
        {
            var id = GetString(material, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var name = GetString(material, "material_name");
            if (string.IsNullOrWhiteSpace(name))
            {
                var path = NormalizePath(GetString(material, "path") ?? string.Empty);
                name = string.IsNullOrWhiteSpace(path) ? id : Path.GetFileName(path);
            }
            result[id] = name!;
        }
        return result;
    }

    private static Dictionary<string, int> ReadMaterialTypeCounts(JsonElement root)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in materials.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Array => property.Value.GetArrayLength(),
                JsonValueKind.Null or JsonValueKind.Undefined => 0,
                _ => 1
            };
        }
        return result;
    }

    private static List<RawTrack> ReadRawTracks(JsonElement root)
    {
        var result = new List<RawTrack>();
        if (!root.TryGetProperty("tracks", out var tracks) || tracks.ValueKind != JsonValueKind.Array)
            return result;

        var trackIndex = 0;
        foreach (var track in tracks.EnumerateArray())
        {
            var segments = new List<RawSegment>();
            if (track.TryGetProperty("segments", out var jsonSegments) && jsonSegments.ValueKind == JsonValueKind.Array)
            {
                foreach (var segment in jsonSegments.EnumerateArray())
                {
                    if (!segment.TryGetProperty("target_timerange", out var range)) continue;
                    var duration = GetLong(range, "duration");
                    if (duration <= 0) continue;
                    var start = GetLong(range, "start");
                    segments.Add(new RawSegment(start, duration, GetString(segment, "material_id") ?? string.Empty));
                }
            }

            result.Add(new RawTrack(
                GetString(track, "id") ?? $"track-{trackIndex}",
                GetString(track, "type") ?? "unknown",
                trackIndex,
                segments));
            trackIndex++;
        }
        return result;
    }

    private static bool Overlaps(RawSegment first, RawSegment second) =>
        first.StartUs < second.EndUs && second.StartUs < first.EndUs;

    private static string NormalizePath(string value) => value.Replace('/', Path.DirectorySeparatorChar);

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "clip" : cleaned;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number) ? number : 0;
    }

    private sealed record RawTrack(string Id, string Type, int Index, List<RawSegment> Segments);
    private sealed record RawSegment(long StartUs, long DurationUs, string MaterialId)
    {
        public long EndUs => StartUs + DurationUs;
    }
}
