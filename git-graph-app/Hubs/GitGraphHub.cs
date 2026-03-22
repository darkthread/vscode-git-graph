using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Text.Json.Serialization;
using git_graph.Models;
using git_graph.Services;

namespace git_graph.Hubs;

// ── Client interface ──────────────────────────────────────────────────────────

public interface IGitGraphClient
{
    Task ReceiveMessage(object response);
}

// ── Hub ───────────────────────────────────────────────────────────────────────

public class GitGraphHub : Hub<IGitGraphClient>
{
    private readonly GitService _git;
    private readonly StateManager _state;
    private readonly RepoWatcher _watcher;
    private readonly ILogger<GitGraphHub> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Do NOT use WhenWritingNull — frontend distinguishes null from undefined for error checks.
        Converters = {
            new GitFileStatusConverter(),
            new GitSignatureStatusConverter(),
            new MergeActionOnConverter(),
            new RebaseActionOnConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public GitGraphHub(
        GitService git, StateManager state, RepoWatcher watcher, ILogger<GitGraphHub> logger)
    {
        _git = git;
        _state = state;
        _watcher = watcher;
        _logger = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    public async Task HandleMessage(JsonElement message)
    {
        if (!message.TryGetProperty("command", out var cmdProp)) return;
        string command = cmdProp.GetString() ?? "";

        _logger.LogDebug("Hub received command: {Command}", command);

        switch (command)
        {
            // ── Read queries ──────────────────────────────────────────────────

            case "loadCommits":
            {
                var req = Deserialize<RequestLoadCommits>(message);
                var data = await _git.GetCommitsAsync(
                    req.Repo, req.Branches, req.MaxCommits, req.ShowTags, req.ShowRemoteBranches,
                    req.IncludeCommitsMentionedByReflogs, req.OnlyFollowFirstParent,
                    req.CommitOrdering, req.Remotes, req.HideRemotes, req.Stashes);

                _state.EnsureRepoRegistered(req.Repo);
                _watcher.Start(req.Repo);

                await Send(new
                {
                    command = "loadCommits",
                    repo = req.Repo,
                    refreshId = req.RefreshId,
                    commits = data.Commits,
                    head = data.Head,
                    tags = data.Tags,
                    moreCommitsAvailable = data.MoreCommitsAvailable,
                    error = data.Error
                });
                break;
            }

            case "loadRepoInfo":
            {
                var req = Deserialize<RequestLoadRepoInfo>(message);
                var data = await _git.GetRepoInfoAsync(req.Repo, req.ShowRemoteBranches, req.ShowStashes, req.HideRemotes);

                var responsePayload = new
                {
                    command = "loadRepoInfo",
                    repo = req.Repo,
                    refreshId = req.RefreshId,
                    branches = data.Branches,
                    head = data.Head,
                    remotes = data.Remotes,
                    stashes = data.Stashes,
                    isRepo = data.Error == null,
                    error = data.Error
                };
                _logger.LogInformation("loadRepoInfo response: error={Error} isRepo={IsRepo} branches={Count}",
                    data.Error ?? "(null)", data.Error == null, data.Branches.Length);
                await Send(responsePayload);
                break;
            }

            case "loadRepos":
            {
                var repos = _state.GetRepos();
                var lastActiveRepo = _state.GetLastActiveRepo();

                await Send(new
                {
                    command = "loadRepos",
                    repos,
                    lastActiveRepo,
                    loadViewTo = (object?)null
                });
                break;
            }

            case "loadConfig":
            {
                var req = Deserialize<RequestLoadConfig>(message);
                var data = await _git.GetConfigAsync(req.Repo, req.Remotes);

                await Send(new
                {
                    command = "loadConfig",
                    repo = req.Repo,
                    config = data.Config,
                    error = data.Error
                });
                break;
            }

            case "commitDetails":
            {
                var req = Deserialize<RequestCommitDetails>(message);
                GitCommitDetailsData data;

                if (req.Stash != null)
                    data = await _git.GetStashDetailsAsync(req.Repo, req.CommitHash, req.Stash);
                else if (req.CommitHash == "*")
                    data = await _git.GetUncommittedDetailsAsync(req.Repo);
                else
                    data = await _git.GetCommitDetailsAsync(req.Repo, req.CommitHash, req.HasParents);

                await Send(new
                {
                    command = "commitDetails",
                    repo = req.Repo,
                    commitDetails = data.CommitDetails,
                    avatar = (string?)null,
                    codeReview = (object?)null,
                    refresh = req.Refresh,
                    error = data.Error
                });
                break;
            }

            case "compareCommits":
            {
                var req = Deserialize<RequestCompareCommits>(message);
                var data = await _git.GetCommitComparisonAsync(req.Repo, req.FromHash, req.ToHash);

                await Send(new
                {
                    command = "compareCommits",
                    repo = req.Repo,
                    commitHash = req.CommitHash,
                    compareWithHash = req.CompareWithHash,
                    fileChanges = data.FileChanges,
                    codeReview = (object?)null,
                    refresh = req.Refresh,
                    error = data.Error
                });
                break;
            }

            case "tagDetails":
            {
                var req = Deserialize<RequestTagDetails>(message);
                var data = await _git.GetTagDetailsAsync(req.Repo, req.TagName);

                await Send(new
                {
                    command = "tagDetails",
                    repo = req.Repo,
                    tagName = req.TagName,
                    commitHash = req.CommitHash,
                    details = data.Details,
                    error = data.Error
                });
                break;
            }

            // ── File viewing ──────────────────────────────────────────────────

            case "viewDiff":
            {
                var req = Deserialize<RequestViewDiff>(message);
                var url = BuildDiffUrl(req.Repo, req.FromHash, req.ToHash, req.OldFilePath, req.NewFilePath);
                await Send(new { command = "viewDiff", url, error = (string?)null });
                break;
            }

            case "viewDiffWithWorkingFile":
            {
                var req = Deserialize<RequestViewDiffWithWorkingFile>(message);
                string? newPath = await _git.GetNewPathOfRenamedFileAsync(req.Repo, req.Hash, req.FilePath);
                string filePath = newPath ?? req.FilePath;
                var url = BuildDiffUrl(req.Repo, req.Hash, "*", req.FilePath, filePath);
                await Send(new { command = "viewDiffWithWorkingFile", url, error = (string?)null });
                break;
            }

            case "viewFileAtRevision":
            {
                var req = Deserialize<RequestViewFileAtRevision>(message);
                var url = $"/api/file?repo={Uri.EscapeDataString(req.Repo)}" +
                          $"&hash={Uri.EscapeDataString(req.Hash)}" +
                          $"&file={Uri.EscapeDataString(req.FilePath)}";
                await Send(new { command = "viewFileAtRevision", url, error = (string?)null });
                break;
            }

            case "openFile":
            {
                var req = Deserialize<RequestOpenFile>(message);
                // In standalone mode, open the file content in a new browser tab via /api/file
                var viewUrl = $"/api/file?repo={Uri.EscapeDataString(req.Repo)}" +
                              $"&hash={Uri.EscapeDataString(req.Hash ?? "HEAD")}" +
                              $"&file={Uri.EscapeDataString(req.FilePath)}";
                await Send(new { command = "openFile", viewUrl, error = (string?)null });
                break;
            }

            // ── Remote operations ─────────────────────────────────────────────

            case "addRemote":
            {
                var req = Deserialize<RequestAddRemote>(message);
                _watcher.Mute();
                var error = await _git.AddRemoteAsync(req.Repo, req.Name, req.Url, req.PushUrl, req.Fetch);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "deleteRemote":
            {
                var req = Deserialize<RequestDeleteRemote>(message);
                _watcher.Mute();
                var error = await _git.DeleteRemoteAsync(req.Repo, req.Name);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "editRemote":
            {
                var req = Deserialize<RequestEditRemote>(message);
                _watcher.Mute();
                var error = await _git.EditRemoteAsync(req.Repo,
                    req.NameOld, req.NameNew, req.UrlOld, req.UrlNew, req.PushUrlOld, req.PushUrlNew);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "pruneRemote":
            {
                var req = Deserialize<RequestPruneRemote>(message);
                _watcher.Mute();
                var error = await _git.PruneRemoteAsync(req.Repo, req.Name);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "fetch":
            {
                var req = Deserialize<RequestFetch>(message);
                _watcher.Mute();
                var error = await _git.FetchAsync(req.Repo, req.Name, req.Prune, req.PruneTags);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "deleteRemoteBranch":
            {
                var req = Deserialize<RequestDeleteRemoteBranch>(message);
                _watcher.Mute();
                var error = await _git.DeleteRemoteBranchAsync(req.Repo, req.BranchName, req.Remote);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "fetchIntoLocalBranch":
            {
                var req = Deserialize<RequestFetchIntoLocalBranch>(message);
                _watcher.Mute();
                var error = await _git.FetchIntoLocalBranchAsync(req.Repo, req.Remote, req.RemoteBranch, req.LocalBranch, req.Force);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            // ── Tag operations ────────────────────────────────────────────────

            case "addTag":
            {
                var req = Deserialize<RequestAddTag>(message);
                _watcher.Mute();
                var addError = await _git.AddTagAsync(req.Repo, req.TagName, req.CommitHash, req.Type, req.Message, req.Force);
                string? pushError = null;
                if (addError == null && req.PushToRemote != null)
                {
                    var pushErrors = await _git.PushTagAsync(req.Repo, req.TagName,
                        [req.PushToRemote], req.CommitHash, req.PushSkipRemoteCheck);
                    pushError = pushErrors.FirstOrDefault(e => e != null);
                }
                _watcher.Unmute();
                string?[] tagErrors = req.PushToRemote != null
                    ? [addError, pushError]
                    : [addError];
                await Send(new { command, repo = req.Repo, tagName = req.TagName, pushToRemote = req.PushToRemote, commitHash = req.CommitHash, errors = tagErrors });
                break;
            }

            case "deleteTag":
            {
                var req = Deserialize<RequestDeleteTag>(message);
                _watcher.Mute();
                var error = await _git.DeleteTagAsync(req.Repo, req.TagName, req.DeleteOnRemote);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "pushTag":
            {
                var req = Deserialize<RequestPushTag>(message);
                _watcher.Mute();
                var errors = await _git.PushTagAsync(req.Repo, req.TagName, req.Remotes, req.CommitHash, req.SkipRemoteCheck);
                _watcher.Unmute();
                await Send(new { command, repo = req.Repo, tagName = req.TagName, remotes = req.Remotes, commitHash = req.CommitHash, errors });
                break;
            }

            // ── Branch operations ─────────────────────────────────────────────

            case "checkoutBranch":
            {
                var req = Deserialize<RequestCheckoutBranch>(message);
                _watcher.Mute();
                var error = await _git.CheckoutBranchAsync(req.Repo, req.BranchName, req.RemoteBranch);
                if (error == null && req.PullAfterwards != null)
                {
                    error = await _git.PullBranchAsync(req.Repo, req.PullAfterwards.BranchName,
                        req.PullAfterwards.Remote, req.PullAfterwards.CreateNewCommit, req.PullAfterwards.Squash);
                }
                _watcher.Unmute();
                await Send(new { command, errors = new string?[] { error }, pullAfterwards = (object?)req.PullAfterwards });
                break;
            }

            case "checkoutCommit":
            {
                var req = Deserialize<RequestCheckoutCommit>(message);
                _watcher.Mute();
                var error = await _git.CheckoutCommitAsync(req.Repo, req.CommitHash);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "createBranch":
            {
                var req = Deserialize<RequestCreateBranch>(message);
                _watcher.Mute();
                var errors = await _git.CreateBranchAsync(req.Repo, req.BranchName, req.CommitHash, req.Checkout, req.Force);
                _watcher.Unmute();
                await Send(new { command, errors });
                break;
            }

            case "deleteBranch":
            {
                var req = Deserialize<RequestDeleteBranch>(message);
                _watcher.Mute();
                var deleteError = await _git.DeleteBranchAsync(req.Repo, req.BranchName, req.ForceDelete);
                var deleteErrors = new System.Collections.Generic.List<string?> { deleteError };
                if (deleteError == null)
                {
                    foreach (var remote in req.DeleteOnRemotes)
                    {
                        var remoteError = await _git.DeleteRemoteBranchAsync(req.Repo, req.BranchName, remote);
                        deleteErrors.Add(remoteError);
                        if (remoteError != null) break;
                    }
                }
                _watcher.Unmute();
                await Send(new { command, repo = req.Repo, branchName = req.BranchName, deleteOnRemotes = req.DeleteOnRemotes, errors = deleteErrors.ToArray() });
                break;
            }

            case "renameBranch":
            {
                var req = Deserialize<RequestRenameBranch>(message);
                _watcher.Mute();
                var error = await _git.RenameBranchAsync(req.Repo, req.OldName, req.NewName);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "pullBranch":
            {
                var req = Deserialize<RequestPullBranch>(message);
                _watcher.Mute();
                var error = await _git.PullBranchAsync(req.Repo, req.BranchName, req.Remote, req.CreateNewCommit, req.Squash);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "pushBranch":
            {
                var req = Deserialize<RequestPushBranch>(message);
                _watcher.Mute();
                var errors = await _git.PushBranchToMultipleRemotesAsync(
                    req.Repo, req.BranchName, req.Remotes, req.SetUpstream, req.Mode);
                _watcher.Unmute();
                await Send(new { command, errors, willUpdateBranchConfig = req.WillUpdateBranchConfig });
                break;
            }

            case "createPullRequest":
            {
                var req = Deserialize<RequestCreatePullRequest>(message);
                string? pushError = null;

                if (req.Push)
                {
                    _watcher.Mute();
                    var pushErrors = await _git.PushBranchToMultipleRemotesAsync(
                        req.Repo, req.SourceBranch, [req.SourceRemote], true, GitPushBranchMode.Normal);
                    pushError = pushErrors.FirstOrDefault(e => e != null);
                    _watcher.Unmute();
                }

                await Send(new { command, push = req.Push, errors = new string?[] { pushError } });
                break;
            }

            // ── Commit operations ─────────────────────────────────────────────

            case "cherrypickCommit":
            {
                var req = Deserialize<RequestCherrypickCommit>(message);
                _watcher.Mute();
                var error = await _git.CherrypickCommitAsync(req.Repo, req.CommitHash, req.ParentIndex, req.RecordOrigin, req.NoCommit);
                _watcher.Unmute();
                await Send(new { command, errors = new string?[] { error } });
                break;
            }

            case "dropCommit":
            {
                var req = Deserialize<RequestDropCommit>(message);
                _watcher.Mute();
                var error = await _git.DropCommitAsync(req.Repo, req.CommitHash);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "resetToCommit":
            {
                var req = Deserialize<RequestResetToCommit>(message);
                _watcher.Mute();
                var error = await _git.ResetToCommitAsync(req.Repo, req.Commit, req.ResetMode);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "revertCommit":
            {
                var req = Deserialize<RequestRevertCommit>(message);
                _watcher.Mute();
                var error = await _git.RevertCommitAsync(req.Repo, req.CommitHash, req.ParentIndex);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "cleanUntrackedFiles":
            {
                var req = Deserialize<RequestCleanUntrackedFiles>(message);
                _watcher.Mute();
                var error = await _git.CleanUntrackedFilesAsync(req.Repo, req.Directories);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "resetFileToRevision":
            {
                var req = Deserialize<RequestResetFileToRevision>(message);
                _watcher.Mute();
                var error = await _git.ResetFileToRevisionAsync(req.Repo, req.CommitHash, req.FilePath);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            // ── Merge & Rebase ────────────────────────────────────────────────

            case "merge":
            {
                var req = Deserialize<RequestMerge>(message);
                _watcher.Mute();
                var error = await _git.MergeAsync(req.Repo, req.Obj, req.ActionOn, req.CreateNewCommit, req.Squash, req.NoCommit);
                _watcher.Unmute();
                await Send(new { command, repo = req.Repo, actionOn = req.ActionOn, error });
                break;
            }

            case "rebase":
            {
                var req = Deserialize<RequestRebase>(message);
                _watcher.Mute();
                var error = await _git.RebaseAsync(req.Repo, req.Obj, req.ActionOn, req.IgnoreDate, req.Interactive);
                _watcher.Unmute();
                await Send(new { command, repo = req.Repo, actionOn = req.ActionOn, interactive = req.Interactive, error });
                break;
            }

            // ── Stash operations ──────────────────────────────────────────────

            case "applyStash":
            {
                var req = Deserialize<RequestApplyStash>(message);
                _watcher.Mute();
                var error = await _git.ApplyStashAsync(req.Repo, req.Selector, req.ReinstateIndex);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "branchFromStash":
            {
                var req = Deserialize<RequestBranchFromStash>(message);
                _watcher.Mute();
                var error = await _git.BranchFromStashAsync(req.Repo, req.Selector, req.BranchName);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "dropStash":
            {
                var req = Deserialize<RequestDropStash>(message);
                _watcher.Mute();
                var error = await _git.DropStashAsync(req.Repo, req.Selector);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "popStash":
            {
                var req = Deserialize<RequestPopStash>(message);
                _watcher.Mute();
                var error = await _git.PopStashAsync(req.Repo, req.Selector, req.ReinstateIndex);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            case "pushStash":
            {
                var req = Deserialize<RequestPushStash>(message);
                _watcher.Mute();
                var error = await _git.PushStashAsync(req.Repo, req.Message, req.IncludeUntracked);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            // ── External diff / archive ───────────────────────────────────────

            case "openExternalDirDiff":
            {
                var req = Deserialize<RequestOpenExternalDirDiff>(message);
                var error = await _git.OpenExternalDirDiffAsync(req.Repo, req.FromHash, req.ToHash, req.IsGui);
                await SendResult(command, req.Repo, error);
                break;
            }

            case "createArchive":
            {
                var req = Deserialize<RequestCreateArchive>(message);
                _watcher.Mute();
                var error = await _git.RunGitCommandPublicAsync(
                    ["archive", "--format=zip", $"--output={req.Ref}.zip", req.Ref], req.Repo);
                _watcher.Unmute();
                await SendResult(command, req.Repo, error);
                break;
            }

            // ── User config ───────────────────────────────────────────────────

            case "editUserDetails":
            {
                var req = Deserialize<RequestEditUserDetails>(message);
                _watcher.Mute();
                string? error = null;
                if (!string.IsNullOrEmpty(req.Name))
                    error = await _git.SetConfigValueAsync(req.Repo, "user.name", req.Name, req.Location);
                if (error == null && !string.IsNullOrEmpty(req.Email))
                    error = await _git.SetConfigValueAsync(req.Repo, "user.email", req.Email, req.Location);
                _watcher.Unmute();
                await Send(new { command, errors = new string?[] { error } });
                break;
            }

            case "deleteUserDetails":
            {
                var req = Deserialize<RequestDeleteUserDetails>(message);
                _watcher.Mute();
                string? error = null;
                if (req.Name)
                    error = await _git.UnsetConfigValueAsync(req.Repo, "user.name", req.Location);
                if (error == null && req.Email)
                    error = await _git.UnsetConfigValueAsync(req.Repo, "user.email", req.Location);
                _watcher.Unmute();
                await Send(new { command, errors = new string?[] { error } });
                break;
            }

            // ── Code review ───────────────────────────────────────────────────

            case "startCodeReview":
            {
                var req = Deserialize<RequestStartCodeReview>(message);
                await _state.StartCodeReviewAsync(req.Repo, req.Id, req.Files, req.LastViewedFile);
                var codeReview = _state.GetCodeReview(req.Repo, req.Id);
                await Send(new
                {
                    command,
                    codeReview,
                    commitHash = req.CommitHash,
                    compareWithHash = string.IsNullOrEmpty(req.CompareWithHash) ? (string?)null : req.CompareWithHash,
                    error = (string?)null
                });
                break;
            }

            case "updateCodeReview":
            {
                var req = Deserialize<RequestUpdateCodeReview>(message);
                await _state.UpdateCodeReviewAsync(req.Repo, req.Id,
                    req.LastViewedFile ?? "", req.RemainingFiles);
                await Send(new { command, repo = req.Repo, id = req.Id, error = (string?)null });
                break;
            }

            case "endCodeReview":
            {
                var req = Deserialize<RequestEndCodeReview>(message);
                await _state.EndCodeReviewAsync(req.Repo, req.Id);
                await Send(new { command, repo = req.Repo, id = req.Id });
                break;
            }

            // ── View state ────────────────────────────────────────────────────

            case "setGlobalViewState":
            {
                var req = Deserialize<RequestSetGlobalViewState>(message);
                await _state.SetGlobalViewStateAsync(req.State);
                await Send(new { command, error = (string?)null });
                break;
            }

            case "setWorkspaceViewState":
            {
                var req = Deserialize<RequestSetWorkspaceViewState>(message);
                await _state.SetWorkspaceViewStateAsync(req.State);
                await Send(new { command, error = (string?)null });
                break;
            }

            case "setRepoState":
            {
                var req = Deserialize<RequestSetRepoState>(message);
                await _state.SetRepoStateAsync(req.Repo, req.State);
                await Send(new { command, repo = req.Repo });
                break;
            }

            // ── No-ops ────────────────────────────────────────────────────────

            case "copyToClipboard":
            case "copyFilePath":
            case "openExternalUrl":
            case "openExtensionSettings":
            case "openTerminal":
            case "viewScm":
                // Not supported in standalone mode — send null error so the dialog closes cleanly
                await Send(new { command, error = (string?)null });
                break;

            case "showErrorMessage":
            case "fetchAvatar":
            case "rescanForRepos":
                // Fire-and-forget in standalone mode — no response needed
                break;

            case "exportRepoConfig":
                // Not supported in standalone mode — respond so the dialog closes
                await Send(new { command, error = (string?)null });
                break;

            default:
                _logger.LogWarning("Unknown command: {Command}", command);
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task Send(object response)
    {
        await Clients.Caller.ReceiveMessage(response);
    }

    private Task SendResult(string command, string repo, string? error) =>
        Send(new { command, repo, error });

    private static T Deserialize<T>(JsonElement el) where T : new()
    {
        return el.Deserialize<T>(JsonOptions) ?? new T();
    }

    private static string BuildDiffUrl(string repo, string fromHash, string toHash, string oldFilePath, string newFilePath)
    {
        return $"/diff?repo={Uri.EscapeDataString(repo)}" +
               $"&fromHash={Uri.EscapeDataString(fromHash)}" +
               $"&toHash={Uri.EscapeDataString(toHash)}" +
               $"&oldPath={Uri.EscapeDataString(oldFilePath)}" +
               $"&newPath={Uri.EscapeDataString(newFilePath)}";
    }

    private static string? BuildPullRequestUrl(PullRequestConfig config, string sourceOwner, string sourceRepo, string sourceBranch)
    {
        if (config.Custom != null && config.Provider == PullRequestProvider.Custom)
        {
            return config.Custom.TemplateUrl
                .Replace("$1", Uri.EscapeDataString(config.HostRootUrl))
                .Replace("$2", Uri.EscapeDataString(sourceOwner))
                .Replace("$3", Uri.EscapeDataString(sourceRepo))
                .Replace("$4", Uri.EscapeDataString(sourceBranch))
                .Replace("$5", Uri.EscapeDataString(config.DestOwner))
                .Replace("$6", Uri.EscapeDataString(config.DestRepo))
                .Replace("$7", Uri.EscapeDataString(config.DestBranch))
                .Replace("$8", Uri.EscapeDataString(config.DestProjectId));
        }

        return config.Provider switch
        {
            PullRequestProvider.GitHub => $"{config.HostRootUrl}/{sourceOwner}/{sourceRepo}/compare/{Uri.EscapeDataString(config.DestBranch)}...{Uri.EscapeDataString(sourceOwner)}:{Uri.EscapeDataString(sourceBranch)}?expand=1",
            PullRequestProvider.GitLab => $"{config.HostRootUrl}/{sourceOwner}/{sourceRepo}/-/merge_requests/new?merge_request[source_branch]={Uri.EscapeDataString(sourceBranch)}&merge_request[target_branch]={Uri.EscapeDataString(config.DestBranch)}",
            PullRequestProvider.Bitbucket => $"{config.HostRootUrl}/{sourceOwner}/{sourceRepo}/pull-requests/new?source={Uri.EscapeDataString(sourceBranch)}&dest={Uri.EscapeDataString(config.DestBranch)}",
            // AzureDevOps not in enum yet
            _ => null
        };
    }
}
