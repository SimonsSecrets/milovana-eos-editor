using System.IO;

namespace MilovanaEosEditor;

/// <summary>Coarse pipeline state derived cheaply from which files exist (see the functional brief §3).</summary>
public enum TeaseStatusKind
{
    Missing,     // the registered folder is gone
    Empty,       // folder exists but no script / assets yet
    Draft,       // has a script.md but nothing generated
    InProgress,  // assets mapped and/or tags present, not yet a full tease.json
    Generated,   // tease.json present
}

/// <summary>
/// A snapshot of one registered tease folder plus the <i>light</i> status the browser card shows.
/// Built by <see cref="TeaseWorkspace"/>; intentionally cheap — no manifest parsing or hashing. Rich
/// pipeline status (step N/9, problem counts) is a later (Workflow) concern.
/// </summary>
public sealed class TeaseInfo
{
    public TeaseInfo(string folderPath)
    {
        FolderPath = Path.GetFullPath(folderPath);
        Name = new DirectoryInfo(FolderPath).Name;
        Refresh();
    }

    public string FolderPath { get; }
    public string Name { get; }

    public bool Exists { get; private set; }
    public bool HasScript { get; private set; }
    public bool HasTease { get; private set; }
    public bool HasAssetMap { get; private set; }
    public bool HasContent { get; private set; }
    public int BucketCount { get; private set; }
    public int ImageCount { get; private set; }
    public string ModifiedDisplay { get; private set; } = string.Empty;

    public TeaseStatusKind StatusKind { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;

    /// <summary>One-line "what's on disk" fact for the card (e.g. "40 images · 5 buckets").</summary>
    public string AssetSummary { get; private set; } = string.Empty;

    /// <summary>Short pipeline hint for the card's secondary line.</summary>
    public string PipelineSummary { get; private set; } = string.Empty;

    /// <summary>Re-read the cheap on-disk facts. Safe to call any time.</summary>
    public void Refresh()
    {
        Exists = Directory.Exists(FolderPath);
        if (!Exists)
        {
            StatusKind = TeaseStatusKind.Missing;
            StatusLabel = "Missing";
            AssetSummary = "folder not found on disk";
            PipelineSummary = string.Empty;
            ModifiedDisplay = string.Empty;
            HasScript = HasTease = HasAssetMap = HasContent = false;
            BucketCount = ImageCount = 0;
            return;
        }

        HasScript = File.Exists(Path.Combine(FolderPath, "script.md"));
        HasTease = File.Exists(Path.Combine(FolderPath, "tease.json"));
        HasAssetMap = File.Exists(Path.Combine(FolderPath, "asset-map.json"));
        HasContent = File.Exists(Path.Combine(FolderPath, "asset-content.json"));

        string galleryDir = Path.Combine(FolderPath, "Gallery");
        if (Directory.Exists(galleryDir))
        {
            var buckets = Directory.EnumerateDirectories(galleryDir).ToList();
            BucketCount = buckets.Count;
            ImageCount = buckets.Sum(CountImages);
        }
        else
        {
            BucketCount = ImageCount = 0;
        }

        (StatusKind, StatusLabel) = DeriveStatus();
        AssetSummary = ImageCount > 0
            ? $"{ImageCount} image{(ImageCount == 1 ? "" : "s")} · {BucketCount} bucket{(BucketCount == 1 ? "" : "s")}"
            : "no assets yet";
        PipelineSummary = BuildPipelineSummary();
        ModifiedDisplay = BuildModified();
    }

    private (TeaseStatusKind, string) DeriveStatus()
    {
        if (HasTease) return (TeaseStatusKind.Generated, "Generated");
        if (HasAssetMap || HasContent) return (TeaseStatusKind.InProgress, "In progress");
        if (HasScript) return (TeaseStatusKind.Draft, "Draft");
        return (TeaseStatusKind.Empty, "Empty");
    }

    private string BuildPipelineSummary()
    {
        var parts = new List<string>();
        parts.Add(HasScript ? "script" : "no script");
        if (HasAssetMap) parts.Add("mapped");
        if (HasContent) parts.Add("tagged");
        if (HasTease) parts.Add("tease.json");
        return string.Join(" · ", parts);
    }

    private string BuildModified()
    {
        try
        {
            DateTime latest = Directory.GetLastWriteTime(FolderPath);
            foreach (string f in new[] { "script.md", "tease.json", "asset-content.json", "asset-map.json" })
            {
                string p = Path.Combine(FolderPath, f);
                if (File.Exists(p))
                {
                    DateTime t = File.GetLastWriteTime(p);
                    if (t > latest) latest = t;
                }
            }
            return RelativeTime(latest);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CountImages(string bucketDir)
    {
        try
        {
            return Directory.EnumerateFiles(bucketDir)
                .Count(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return 0;
        }
    }

    private static string RelativeTime(DateTime when)
    {
        TimeSpan ago = DateTime.Now - when;
        if (ago < TimeSpan.FromMinutes(1)) return "just now";
        if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes} min ago";
        if (ago < TimeSpan.FromHours(24)) return $"{(int)ago.TotalHours} hour{((int)ago.TotalHours == 1 ? "" : "s")} ago";
        if (ago < TimeSpan.FromDays(2)) return "yesterday";
        if (ago < TimeSpan.FromDays(30)) return $"{(int)ago.TotalDays} days ago";
        return when.ToString("d MMM yyyy");
    }
}
