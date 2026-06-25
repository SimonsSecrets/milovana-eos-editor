using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MilovanaEosEditor;

/// <summary>
/// The author's workspace: a persisted list of tease folders they've explicitly created or added.
/// There is deliberately <b>no auto-scan</b> of any <c>Teases/</c> directory — a tease appears in the
/// browser only after the author adds or creates it. The list is stored per-user under
/// <c>%APPDATA%/MilovanaEosEditor/workspace.json</c> so it survives across sessions.
/// </summary>
public sealed class TeaseWorkspace
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _registryPath;
    private List<string> _paths;

    public TeaseWorkspace(string? registryPath = null)
    {
        _registryPath = registryPath ?? DefaultRegistryPath();
        _paths = LoadPaths();
    }

    private static string DefaultRegistryPath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MilovanaEosEditor");
        return Path.Combine(dir, "workspace.json");
    }

    // ---- registry I/O ----

    private sealed class RegistryFile
    {
        [JsonPropertyName("teases")] public List<string> Teases { get; set; } = new();
    }

    private List<string> LoadPaths()
    {
        try
        {
            if (!File.Exists(_registryPath)) return new List<string>();
            RegistryFile? file = JsonSerializer.Deserialize<RegistryFile>(File.ReadAllText(_registryPath), JsonOptions);
            // De-dupe on normalized full path while preserving order.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (string p in file?.Teases ?? new())
            {
                string full = SafeFullPath(p);
                if (full.Length > 0 && seen.Add(full)) result.Add(full);
            }
            return result;
        }
        catch
        {
            return new List<string>();
        }
    }

    private void SavePaths()
    {
        string dir = Path.GetDirectoryName(_registryPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_registryPath, JsonSerializer.Serialize(new RegistryFile { Teases = _paths }, JsonOptions));
    }

    private static string SafeFullPath(string p)
    {
        try { return Path.GetFullPath(p); } catch { return string.Empty; }
    }

    // ---- queries ----

    /// <summary>Snapshot of every registered tease (including any whose folder is now missing).</summary>
    public IReadOnlyList<TeaseInfo> GetTeases() => _paths.Select(p => new TeaseInfo(p)).ToList();

    public bool Contains(string folderPath) =>
        _paths.Contains(SafeFullPath(folderPath), StringComparer.OrdinalIgnoreCase);

    /// <summary>Does this folder look like a tease we can open? (cheap structural sniff)</summary>
    public static bool LooksLikeTease(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        return File.Exists(Path.Combine(folderPath, "tease.json"))
            || File.Exists(Path.Combine(folderPath, "script.md"))
            || File.Exists(Path.Combine(folderPath, "asset-content.json"))
            || Directory.Exists(Path.Combine(folderPath, "Gallery"));
    }

    // ---- mutations ----

    /// <summary>Register an existing folder. No-op (returns the existing snapshot) if already present.</summary>
    public TeaseInfo Add(string folderPath)
    {
        string full = Path.GetFullPath(folderPath);
        if (!_paths.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            _paths.Add(full);
            SavePaths();
        }
        return new TeaseInfo(full);
    }

    public void Remove(string folderPath)
    {
        string full = SafeFullPath(folderPath);
        int removed = _paths.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) SavePaths();
    }

    /// <summary>Validate a proposed tease name (which becomes a folder name). Returns null if ok.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Enter a name for the tease.";
        name = name.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "The name contains characters that aren't allowed in a folder name.";
        if (name is "." or "..") return "That isn't a valid folder name.";
        return null;
    }

    /// <summary>
    /// Scaffold a new tease under <paramref name="parentDir"/>: create
    /// <c>&lt;name&gt;/Gallery/</c>, <c>&lt;name&gt;/Files/</c> and a starter <c>script.md</c>, then
    /// register it. Throws on an invalid name or if the target folder already exists.
    /// </summary>
    public TeaseInfo CreateTease(string parentDir, string name)
    {
        string? error = ValidateName(name);
        if (error is not null) throw new ArgumentException(error);
        name = name.Trim();

        string target = Path.Combine(parentDir, name);
        if (Directory.Exists(target))
            throw new IOException($"A folder named \"{name}\" already exists here.");

        Directory.CreateDirectory(Path.Combine(target, "Gallery"));
        Directory.CreateDirectory(Path.Combine(target, "Files"));
        File.WriteAllText(Path.Combine(target, "script.md"), StarterScript(name));

        return Add(target);
    }

    private static string StarterScript(string name) =>
        $"# intro\n\nsay \"{name} — start writing your tease here.\"\n";

    /// <summary>
    /// A sensible default location to create a new tease in: the parent of an already-registered tease,
    /// else a discovered repo <c>Teases/</c> folder, else null (caller should prompt).
    /// </summary>
    public string? SuggestParentDirectory()
    {
        foreach (string p in _paths)
        {
            string? parent = Path.GetDirectoryName(p);
            if (parent is not null && Directory.Exists(parent)) return parent;
        }
        return FindRepoTeasesDir();
    }

    /// <summary>Walk up from the running binary to a <c>Teases/</c> folder, if this is a dev checkout.</summary>
    private static string? FindRepoTeasesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "Teases");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
