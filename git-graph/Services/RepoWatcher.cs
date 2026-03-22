using System.Text.RegularExpressions;

namespace git_graph.Services;

/// <summary>
/// Watches a git repository for changes and fires a refresh event.
/// C# port of SimpleRepoWatcher from backend/server.ts.
/// </summary>
public sealed class RepoWatcher : IDisposable
{
    // Debounce: fire refresh 750ms after last change
    private const int DebounceMs = 750;
    // Cooldown after Unmute before watcher fires again
    private const int UnmuteCooldownMs = 1500;

    private static readonly Regex WatchPattern = new(
        @"(^\.git/(config|index|HEAD|refs/stash|refs/heads/.*|refs/remotes/.*|refs/tags/.*)$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private FileSystemWatcher? _fsWatcher;
    private System.Threading.Timer? _debounceTimer;
    private volatile bool _muted = false;
    private DateTime _unmutedAt = DateTime.MinValue;
    private string? _currentRepo;

    public event EventHandler? OnRefresh;

    public void Start(string repo)
    {
        Stop();
        _currentRepo = repo;

        _fsWatcher = new FileSystemWatcher(repo)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _fsWatcher.Created += OnFileSystemEvent;
        _fsWatcher.Changed += OnFileSystemEvent;
        _fsWatcher.Deleted += OnFileSystemEvent;
        _fsWatcher.Renamed += OnFileSystemRenamed;
    }

    public void Stop()
    {
        _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        if (_fsWatcher != null)
        {
            _fsWatcher.EnableRaisingEvents = false;
            _fsWatcher.Created -= OnFileSystemEvent;
            _fsWatcher.Changed -= OnFileSystemEvent;
            _fsWatcher.Deleted -= OnFileSystemEvent;
            _fsWatcher.Renamed -= OnFileSystemRenamed;
            _fsWatcher.Dispose();
            _fsWatcher = null;
        }

        _currentRepo = null;
    }

    public void Mute() => _muted = true;

    public void Unmute()
    {
        _muted = false;
        _unmutedAt = DateTime.UtcNow;
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _fsWatcher?.Dispose();
    }

    // ── Private ────────────────────────────────────────────────────────────────

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) =>
        HandleChange(e.FullPath);

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        HandleChange(e.OldFullPath);
        HandleChange(e.FullPath);
    }

    private void HandleChange(string fullPath)
    {
        if (_muted) return;
        if ((DateTime.UtcNow - _unmutedAt).TotalMilliseconds < UnmuteCooldownMs) return;
        if (_currentRepo == null) return;

        // Make path relative to repo root, normalise to forward slashes
        string relative = Path.GetRelativePath(_currentRepo, fullPath).Replace('\\', '/');
        if (!WatchPattern.IsMatch(relative)) return;

        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_debounceTimer == null)
        {
            _debounceTimer = new System.Threading.Timer(
                _ => OnRefresh?.Invoke(this, EventArgs.Empty),
                null,
                DebounceMs,
                Timeout.Infinite);
        }
        else
        {
            _debounceTimer.Change(DebounceMs, Timeout.Infinite);
        }
    }
}
