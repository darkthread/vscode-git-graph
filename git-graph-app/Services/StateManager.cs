using System.Text.Json;
using System.Text.Json.Serialization;
using git_graph.Models;

namespace git_graph.Services;

/// <summary>
/// Manages persistent state for git-graph repos and UI settings.
/// C# port of backend/stateManager.ts.
/// </summary>
public class StateManager
{
    private readonly string _stateFilePath;
    private readonly ILogger<StateManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private PersistedState _state = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public StateManager(ILogger<StateManager> logger)
    {
        _logger = logger;
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".git-graph");
        Directory.CreateDirectory(dir);
        _stateFilePath = Path.Combine(dir, "state.json");
        _ = LoadStateAsync();
    }

    // ── Repo State ────────────────────────────────────────────────────────────

    public Dictionary<string, GitRepoState> GetRepos()
    {
        return new Dictionary<string, GitRepoState>(_state.Repos);
    }

    public async Task SaveReposAsync(Dictionary<string, GitRepoState> repos)
    {
        _state.Repos = repos;
        await PersistStateAsync();
    }

    public async Task SetRepoStateAsync(string repo, GitRepoState state)
    {
        _state.Repos[repo] = state;
        await PersistStateAsync();
    }

    public GitRepoState GetRepoState(string repo)
    {
        if (_state.Repos.TryGetValue(repo, out var state))
            return state;

        var defaultState = new GitRepoState();
        _state.Repos[repo] = defaultState;
        return defaultState;
    }

    // ── Last Active Repo ──────────────────────────────────────────────────────

    public string? GetLastActiveRepo() {
        var lastActiveRepo = _state.LastActiveRepo;
        if (lastActiveRepo == null || !_state.Repos.ContainsKey(lastActiveRepo))
            lastActiveRepo = _state.Repos.Keys.FirstOrDefault();
        return lastActiveRepo;
    }

    public async Task SetLastActiveRepoAsync(string repo)
    {
        _state.LastActiveRepo = repo;
        await PersistStateAsync();
    }

    // ── Global View State ─────────────────────────────────────────────────────

    public GitGraphViewGlobalState GetGlobalViewState() => _state.GlobalViewState;

    public async Task SetGlobalViewStateAsync(GitGraphViewGlobalState state)
    {
        _state.GlobalViewState = state;
        await PersistStateAsync();
    }

    // ── Workspace View State ──────────────────────────────────────────────────

    public GitGraphViewWorkspaceState GetWorkspaceViewState() => _state.WorkspaceViewState;

    public async Task SetWorkspaceViewStateAsync(GitGraphViewWorkspaceState state)
    {
        _state.WorkspaceViewState = state;
        await PersistStateAsync();
    }

    // ── Code Reviews ──────────────────────────────────────────────────────────

    public CodeReview? GetCodeReview(string repo, string id)
    {
        if (!_state.CodeReviews.TryGetValue(repo, out var repoReviews)) return null;
        if (!repoReviews.TryGetValue(id, out var review)) return null;
        return new CodeReview(id, review.LastActive, review.LastViewedFile, [.. review.RemainingFiles]);
    }

    public async Task StartCodeReviewAsync(string repo, string id, string[] files, string? lastViewedFile)
    {
        if (!_state.CodeReviews.TryGetValue(repo, out var repoReviews))
        {
            repoReviews = new Dictionary<string, CodeReviewData>();
            _state.CodeReviews[repo] = repoReviews;
        }
        repoReviews[id] = new CodeReviewData
        {
            LastActive = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastViewedFile = lastViewedFile,
            RemainingFiles = [.. files]
        };
        await PersistStateAsync();
    }

    public async Task UpdateCodeReviewAsync(string repo, string id, string lastViewedFile, string[] remainingFiles)
    {
        if (!_state.CodeReviews.TryGetValue(repo, out var repoReviews)) return;
        if (!repoReviews.TryGetValue(id, out var review)) return;

        review.LastViewedFile = lastViewedFile;
        review.RemainingFiles = [.. remainingFiles];
        review.LastActive = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await PersistStateAsync();
    }

    public async Task EndCodeReviewAsync(string repo, string id)
    {
        if (!_state.CodeReviews.TryGetValue(repo, out var repoReviews)) return;
        if (repoReviews.Remove(id) && repoReviews.Count == 0)
            _state.CodeReviews.Remove(repo);
        await PersistStateAsync();
    }

    // ── Repo Registration ─────────────────────────────────────────────────────

    public void ClearRepoRegistrations()
    {
        _state.Repos.Clear();
        _state.CodeReviews.Clear();
    }

    public void EnsureRepoRegistered(string repo)
    {
        if (!_state.Repos.ContainsKey(repo))
            _state.Repos[repo] = new GitRepoState();
    }

    public string[] GetRegisteredRepos() => [.. _state.Repos.Keys];

    // ── Avatar Storage ────────────────────────────────────────────────────────

    public string GetAvatarStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".git-graph", "avatars");

    // ── Persistence ───────────────────────────────────────────────────────────

    private async Task LoadStateAsync()
    {
        try
        {
            if (!File.Exists(_stateFilePath)) return;
            string json = await File.ReadAllTextAsync(_stateFilePath);
            _state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions) ?? new PersistedState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state from {Path}", _stateFilePath);
            _state = new PersistedState();
        }
    }

    private async Task PersistStateAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(_state, JsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state to {Path}", _stateFilePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
