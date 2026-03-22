using System.Text.Json.Serialization;

namespace git_graph.Models;

// ── Code Review ──────────────────────────────────────────────────────────────

public class CodeReviewData
{
    public long LastActive { get; set; }
    public string? LastViewedFile { get; set; }
    public List<string> RemainingFiles { get; set; } = [];
}

public record CodeReview(
    string Id,
    long LastActive,
    string? LastViewedFile,
    string[] RemainingFiles
);

// ── Repo State ───────────────────────────────────────────────────────────────

public class GitRepoState
{
    public double CdvDivider { get; set; } = 0.5;
    public int CdvHeight { get; set; } = 250;
    public int[]? ColumnWidths { get; set; } = null;
    public RepoCommitOrdering CommitOrdering { get; set; } = RepoCommitOrdering.Default;
    public FileViewType FileViewType { get; set; } = FileViewType.Default;
    public List<string> HideRemotes { get; set; } = [];
    public BooleanOverride IncludeCommitsMentionedByReflogs { get; set; } = BooleanOverride.Default;
    public IssueLinkingConfig? IssueLinkingConfig { get; set; } = null;
    public long LastImportAt { get; set; } = 0;
    public string? Name { get; set; } = null;
    public BooleanOverride OnlyFollowFirstParent { get; set; } = BooleanOverride.Default;
    public BooleanOverride OnRepoLoadShowCheckedOutBranch { get; set; } = BooleanOverride.Default;
    public List<string>? OnRepoLoadShowSpecificBranches { get; set; } = null;
    public PullRequestConfig? PullRequestConfig { get; set; } = null;
    public bool ShowRemoteBranches { get; set; } = true;
    public BooleanOverride ShowRemoteBranchesV2 { get; set; } = BooleanOverride.Default;
    public BooleanOverride ShowStashes { get; set; } = BooleanOverride.Default;
    public BooleanOverride ShowTags { get; set; } = BooleanOverride.Default;
    public int? WorkspaceFolderIndex { get; set; } = null;
}

public class IssueLinkingConfig
{
    public string Issue { get; set; } = "";
    public string Url { get; set; } = "";
}

public class PullRequestConfig
{
    public PullRequestProvider Provider { get; set; }
    public string HostRootUrl { get; set; } = "";
    public string SourceRemote { get; set; } = "";
    public string SourceOwner { get; set; } = "";
    public string SourceRepo { get; set; } = "";
    public string? DestRemote { get; set; }
    public string DestOwner { get; set; } = "";
    public string DestRepo { get; set; } = "";
    public string DestProjectId { get; set; } = "";
    public string DestBranch { get; set; } = "";
    public PullRequestCustomConfig? Custom { get; set; }
}

public class PullRequestCustomConfig
{
    public string Name { get; set; } = "";
    public string TemplateUrl { get; set; } = "";
}

// ── View State ───────────────────────────────────────────────────────────────

public class GitGraphViewGlobalState
{
    public bool AlwaysAcceptCheckoutCommit { get; set; } = false;
    public IssueLinkingConfig? IssueLinkingConfig { get; set; } = null;
    public bool PushTagSkipRemoteCheck { get; set; } = false;
}

public class GitGraphViewWorkspaceState
{
    public bool FindIsCaseSensitive { get; set; } = false;
    public bool FindIsRegex { get; set; } = false;
    public bool FindOpenCommitDetailsView { get; set; } = false;
}

// ── Persisted State ──────────────────────────────────────────────────────────

public class PersistedState
{
    public Dictionary<string, GitRepoState> Repos { get; set; } = [];
    public string? LastActiveRepo { get; set; } = null;
    public List<string> IgnoredRepos { get; set; } = [];
    public GitGraphViewGlobalState GlobalViewState { get; set; } = new();
    public GitGraphViewWorkspaceState WorkspaceViewState { get; set; } = new();
    public Dictionary<string, Dictionary<string, CodeReviewData>> CodeReviews { get; set; } = [];
}

// ── Initial State types (sent to frontend) ───────────────────────────────────

public class GitGraphViewInitialState
{
    public object Config { get; set; } = new();
    public string? LastActiveRepo { get; set; }
    public object? LoadViewTo { get; set; }
    public Dictionary<string, GitRepoState> Repos { get; set; } = [];
    public int LoadRepoInfoRefreshId { get; set; } = 0;
    public int LoadCommitsRefreshId { get; set; } = 0;
}
