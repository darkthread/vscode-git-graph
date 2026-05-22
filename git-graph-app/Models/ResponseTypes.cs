namespace git_graph.Models;

public record RefreshResponse(string Command = "refresh");

public record ErrorResponse(string Command, string? Error);

public record RepoErrorResponse(string Command, string Repo, string? Error);

public record MultiErrorResponse(string Command, string?[] Errors);

public record ViewUrlResponse(string Command, string Url, string? Error);

public record OpenFileResponse(string Command, string ViewUrl, string? Error);

public record LoadCommitsResponse(
    string Command,
    string Repo,
    int RefreshId,
    GitCommit[] Commits,
    string? Head,
    string[] Tags,
    bool MoreCommitsAvailable,
    bool OnlyFollowFirstParent,
    string? Error);

public record LoadRepoInfoResponse(
    string Command,
    string Repo,
    int RefreshId,
    string[] Branches,
    string? Head,
    string[] Remotes,
    GitStash[] Stashes,
    bool IsRepo,
    string? Error);

public record LoadReposResponse(
    string Command,
    Dictionary<string, GitRepoState> Repos,
    string? LastActiveRepo,
    LoadGitGraphViewTo? LoadViewTo);

public record LoadConfigResponse(string Command, string Repo, GitRepoConfig? Config, string? Error);

public record CommitDetailsResponse(
    string Command,
    string Repo,
    GitCommitDetails? CommitDetails,
    string? Avatar,
    CodeReview? CodeReview,
    bool Refresh,
    string? Error);

public record CompareCommitsResponse(
    string Command,
    string Repo,
    string CommitHash,
    string CompareWithHash,
    GitFileChange[] FileChanges,
    CodeReview? CodeReview,
    bool Refresh,
    string? Error);

public record TagDetailsResponse(
    string Command,
    string Repo,
    string TagName,
    string CommitHash,
    GitTagDetails? Details,
    string? Error);

public record AddTagResponse(
    string Command,
    string Repo,
    string TagName,
    string? PushToRemote,
    string CommitHash,
    string?[] Errors);

public record PushTagResponse(
    string Command,
    string Repo,
    string TagName,
    string[] Remotes,
    string CommitHash,
    string?[] Errors);

public record CheckoutBranchResponse(
    string Command,
    string?[] Errors,
    CheckoutBranchPullAfterwards? PullAfterwards);

public record DeleteBranchResponse(
    string Command,
    string Repo,
    string BranchName,
    string[] DeleteOnRemotes,
    string?[] Errors);

public record PushBranchResponse(string Command, string?[] Errors, bool WillUpdateBranchConfig);

public record CreatePullRequestResponse(string Command, bool Push, string?[] Errors);

public record MergeResponse(string Command, string Repo, MergeActionOn ActionOn, string? Error);

public record RebaseResponse(string Command, string Repo, RebaseActionOn ActionOn, bool Interactive, string? Error);

public record StartCodeReviewResponse(
    string Command,
    CodeReview? CodeReview,
    string CommitHash,
    string? CompareWithHash,
    string? Error);

public record CodeReviewIdResponse(string Command, string Repo, string Id, string? Error);

public record EndCodeReviewResponse(string Command, string Repo, string Id);

public record SetRepoStateResponse(string Command, string Repo);