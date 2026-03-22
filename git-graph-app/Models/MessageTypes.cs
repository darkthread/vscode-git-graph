using System.Text.Json;
using System.Text.Json.Serialization;

namespace git_graph.Models;

// ── Base ─────────────────────────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "command")]
[JsonDerivedType(typeof(RequestLoadCommits), "loadCommits")]
[JsonDerivedType(typeof(RequestLoadRepoInfo), "loadRepoInfo")]
[JsonDerivedType(typeof(RequestLoadRepos), "loadRepos")]
[JsonDerivedType(typeof(RequestLoadConfig), "loadConfig")]
[JsonDerivedType(typeof(RequestCommitDetails), "commitDetails")]
[JsonDerivedType(typeof(RequestCompareCommits), "compareCommits")]
[JsonDerivedType(typeof(RequestTagDetails), "tagDetails")]
[JsonDerivedType(typeof(RequestViewDiff), "viewDiff")]
[JsonDerivedType(typeof(RequestViewDiffWithWorkingFile), "viewDiffWithWorkingFile")]
[JsonDerivedType(typeof(RequestViewFileAtRevision), "viewFileAtRevision")]
[JsonDerivedType(typeof(RequestOpenFile), "openFile")]
[JsonDerivedType(typeof(RequestAddRemote), "addRemote")]
[JsonDerivedType(typeof(RequestAddTag), "addTag")]
[JsonDerivedType(typeof(RequestApplyStash), "applyStash")]
[JsonDerivedType(typeof(RequestBranchFromStash), "branchFromStash")]
[JsonDerivedType(typeof(RequestCheckoutBranch), "checkoutBranch")]
[JsonDerivedType(typeof(RequestCheckoutCommit), "checkoutCommit")]
[JsonDerivedType(typeof(RequestCherrypickCommit), "cherrypickCommit")]
[JsonDerivedType(typeof(RequestCleanUntrackedFiles), "cleanUntrackedFiles")]
[JsonDerivedType(typeof(RequestCreateBranch), "createBranch")]
[JsonDerivedType(typeof(RequestCreatePullRequest), "createPullRequest")]
[JsonDerivedType(typeof(RequestDeleteBranch), "deleteBranch")]
[JsonDerivedType(typeof(RequestDeleteRemote), "deleteRemote")]
[JsonDerivedType(typeof(RequestDeleteRemoteBranch), "deleteRemoteBranch")]
[JsonDerivedType(typeof(RequestDeleteTag), "deleteTag")]
[JsonDerivedType(typeof(RequestDeleteUserDetails), "deleteUserDetails")]
[JsonDerivedType(typeof(RequestDropCommit), "dropCommit")]
[JsonDerivedType(typeof(RequestDropStash), "dropStash")]
[JsonDerivedType(typeof(RequestEditRemote), "editRemote")]
[JsonDerivedType(typeof(RequestEditUserDetails), "editUserDetails")]
[JsonDerivedType(typeof(RequestEndCodeReview), "endCodeReview")]
[JsonDerivedType(typeof(RequestFetch), "fetch")]
[JsonDerivedType(typeof(RequestFetchIntoLocalBranch), "fetchIntoLocalBranch")]
[JsonDerivedType(typeof(RequestMerge), "merge")]
[JsonDerivedType(typeof(RequestOpenExternalDirDiff), "openExternalDirDiff")]
[JsonDerivedType(typeof(RequestPopStash), "popStash")]
[JsonDerivedType(typeof(RequestPruneRemote), "pruneRemote")]
[JsonDerivedType(typeof(RequestPullBranch), "pullBranch")]
[JsonDerivedType(typeof(RequestPushBranch), "pushBranch")]
[JsonDerivedType(typeof(RequestPushStash), "pushStash")]
[JsonDerivedType(typeof(RequestPushTag), "pushTag")]
[JsonDerivedType(typeof(RequestRebase), "rebase")]
[JsonDerivedType(typeof(RequestRenameBranch), "renameBranch")]
[JsonDerivedType(typeof(RequestResetFileToRevision), "resetFileToRevision")]
[JsonDerivedType(typeof(RequestResetToCommit), "resetToCommit")]
[JsonDerivedType(typeof(RequestRevertCommit), "revertCommit")]
[JsonDerivedType(typeof(RequestSetGlobalViewState), "setGlobalViewState")]
[JsonDerivedType(typeof(RequestSetRepoState), "setRepoState")]
[JsonDerivedType(typeof(RequestSetWorkspaceViewState), "setWorkspaceViewState")]
[JsonDerivedType(typeof(RequestStartCodeReview), "startCodeReview")]
[JsonDerivedType(typeof(RequestUpdateCodeReview), "updateCodeReview")]
[JsonDerivedType(typeof(RequestCreateArchive), "createArchive")]
[JsonDerivedType(typeof(RequestNoOp), "copyToClipboard")]
[JsonDerivedType(typeof(RequestNoOp), "copyFilePath")]
[JsonDerivedType(typeof(RequestNoOp), "openExternalUrl")]
[JsonDerivedType(typeof(RequestNoOp), "showErrorMessage")]
[JsonDerivedType(typeof(RequestNoOp), "openExtensionSettings")]
[JsonDerivedType(typeof(RequestNoOp), "viewScm")]
[JsonDerivedType(typeof(RequestNoOp), "fetchAvatar")]
[JsonDerivedType(typeof(RequestNoOp), "openTerminal")]
[JsonDerivedType(typeof(RequestNoOp), "rescanForRepos")]
[JsonDerivedType(typeof(RequestNoOp), "exportRepoConfig")]
public abstract class RequestMessage
{
    public abstract string Command { get; }
}

// ── Read-only queries ────────────────────────────────────────────────────────

public class RequestLoadCommits : RequestMessage
{
    public override string Command => "loadCommits";
    public string Repo { get; set; } = "";
    public int RefreshId { get; set; }
    public string[]? Branches { get; set; }
    public int MaxCommits { get; set; } = 300;
    public bool ShowTags { get; set; } = true;
    public bool ShowRemoteBranches { get; set; } = true;
    public bool IncludeCommitsMentionedByReflogs { get; set; } = false;
    public bool OnlyFollowFirstParent { get; set; } = false;
    public CommitOrdering CommitOrdering { get; set; } = CommitOrdering.Date;
    public string[] Remotes { get; set; } = [];
    public string[] HideRemotes { get; set; } = [];
    public GitStash[] Stashes { get; set; } = [];
}

public class RequestLoadRepoInfo : RequestMessage
{
    public override string Command => "loadRepoInfo";
    public string Repo { get; set; } = "";
    public int RefreshId { get; set; }
    public bool ShowRemoteBranches { get; set; } = true;
    public bool ShowStashes { get; set; } = true;
    public string[] HideRemotes { get; set; } = [];
}

public class RequestLoadRepos : RequestMessage
{
    public override string Command => "loadRepos";
    public bool Check { get; set; } = false;
}

public class RequestLoadConfig : RequestMessage
{
    public override string Command => "loadConfig";
    public string Repo { get; set; } = "";
    public string[] Remotes { get; set; } = [];
}

public class RequestCommitDetails : RequestMessage
{
    public override string Command => "commitDetails";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public bool HasParents { get; set; } = true;
    public GitCommitStash? Stash { get; set; }
    public string? AvatarEmail { get; set; }
    public bool Refresh { get; set; } = false;
}

public class RequestCompareCommits : RequestMessage
{
    public override string Command => "compareCommits";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string CompareWithHash { get; set; } = "";
    public string FromHash { get; set; } = "";
    public string ToHash { get; set; } = "";
    public bool Refresh { get; set; } = false;
}

public class RequestTagDetails : RequestMessage
{
    public override string Command => "tagDetails";
    public string Repo { get; set; } = "";
    public string TagName { get; set; } = "";
    public string CommitHash { get; set; } = "";
}

// ── File viewing ─────────────────────────────────────────────────────────────

public class RequestViewDiff : RequestMessage
{
    public override string Command => "viewDiff";
    public string Repo { get; set; } = "";
    public string FromHash { get; set; } = "";
    public string ToHash { get; set; } = "";
    public string OldFilePath { get; set; } = "";
    public string NewFilePath { get; set; } = "";
}

public class RequestViewDiffWithWorkingFile : RequestMessage
{
    public override string Command => "viewDiffWithWorkingFile";
    public string Repo { get; set; } = "";
    public string Hash { get; set; } = "";
    public string FilePath { get; set; } = "";
}

public class RequestViewFileAtRevision : RequestMessage
{
    public override string Command => "viewFileAtRevision";
    public string Repo { get; set; } = "";
    public string Hash { get; set; } = "";
    public string FilePath { get; set; } = "";
}

public class RequestOpenFile : RequestMessage
{
    public override string Command => "openFile";
    public string Repo { get; set; } = "";
    public string? Hash { get; set; }
    public string FilePath { get; set; } = "";
}

// ── Remote operations ────────────────────────────────────────────────────────

public class RequestAddRemote : RequestMessage
{
    public override string Command => "addRemote";
    public string Repo { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string? PushUrl { get; set; }
    public bool Fetch { get; set; } = false;
}

public class RequestDeleteRemote : RequestMessage
{
    public override string Command => "deleteRemote";
    public string Repo { get; set; } = "";
    public string Name { get; set; } = "";
}

public class RequestEditRemote : RequestMessage
{
    public override string Command => "editRemote";
    public string Repo { get; set; } = "";
    public string NameOld { get; set; } = "";
    public string NameNew { get; set; } = "";
    public string? UrlOld { get; set; }
    public string? UrlNew { get; set; }
    public string? PushUrlOld { get; set; }
    public string? PushUrlNew { get; set; }
}

public class RequestPruneRemote : RequestMessage
{
    public override string Command => "pruneRemote";
    public string Repo { get; set; } = "";
    public string Name { get; set; } = "";
}

public class RequestFetch : RequestMessage
{
    public override string Command => "fetch";
    public string Repo { get; set; } = "";
    public string? Name { get; set; }
    public bool Prune { get; set; } = false;
    public bool PruneTags { get; set; } = false;
}

public class RequestDeleteRemoteBranch : RequestMessage
{
    public override string Command => "deleteRemoteBranch";
    public string Repo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string Remote { get; set; } = "";
}

public class RequestFetchIntoLocalBranch : RequestMessage
{
    public override string Command => "fetchIntoLocalBranch";
    public string Repo { get; set; } = "";
    public string Remote { get; set; } = "";
    public string RemoteBranch { get; set; } = "";
    public string LocalBranch { get; set; } = "";
    public bool Force { get; set; } = false;
}

// ── Tag operations ───────────────────────────────────────────────────────────

public class RequestAddTag : RequestMessage
{
    public override string Command => "addTag";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string TagName { get; set; } = "";
    public TagType Type { get; set; } = TagType.Annotated;
    public string Message { get; set; } = "";
    public string? PushToRemote { get; set; }
    public bool PushSkipRemoteCheck { get; set; } = false;
    public bool Force { get; set; } = false;
}

public class RequestDeleteTag : RequestMessage
{
    public override string Command => "deleteTag";
    public string Repo { get; set; } = "";
    public string TagName { get; set; } = "";
    public string? DeleteOnRemote { get; set; }
}

public class RequestPushTag : RequestMessage
{
    public override string Command => "pushTag";
    public string Repo { get; set; } = "";
    public string TagName { get; set; } = "";
    public string[] Remotes { get; set; } = [];
    public string CommitHash { get; set; } = "";
    public bool SkipRemoteCheck { get; set; } = false;
}

// ── Branch operations ────────────────────────────────────────────────────────

public class CheckoutBranchPullAfterwards
{
    public string BranchName { get; set; } = "";
    public string Remote { get; set; } = "";
    public bool CreateNewCommit { get; set; } = false;
    public bool Squash { get; set; } = false;
}

public class RequestCheckoutBranch : RequestMessage
{
    public override string Command => "checkoutBranch";
    public string Repo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? RemoteBranch { get; set; }
    public CheckoutBranchPullAfterwards? PullAfterwards { get; set; }
}

public class RequestCheckoutCommit : RequestMessage
{
    public override string Command => "checkoutCommit";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
}

public class RequestCreateBranch : RequestMessage
{
    public override string Command => "createBranch";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string BranchName { get; set; } = "";
    public bool Checkout { get; set; } = false;
    public bool Force { get; set; } = false;
}

public class RequestDeleteBranch : RequestMessage
{
    public override string Command => "deleteBranch";
    public string Repo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public bool ForceDelete { get; set; } = false;
    public string[] DeleteOnRemotes { get; set; } = [];
}

public class RequestRenameBranch : RequestMessage
{
    public override string Command => "renameBranch";
    public string Repo { get; set; } = "";
    public string OldName { get; set; } = "";
    public string NewName { get; set; } = "";
}

public class RequestPullBranch : RequestMessage
{
    public override string Command => "pullBranch";
    public string Repo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string Remote { get; set; } = "";
    public bool CreateNewCommit { get; set; } = false;
    public bool Squash { get; set; } = false;
}

public class RequestPushBranch : RequestMessage
{
    public override string Command => "pushBranch";
    public string Repo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string[] Remotes { get; set; } = [];
    public bool SetUpstream { get; set; } = false;
    public GitPushBranchMode Mode { get; set; } = GitPushBranchMode.Normal;
    public bool WillUpdateBranchConfig { get; set; } = false;
}

public class RequestCreatePullRequest : RequestMessage
{
    public override string Command => "createPullRequest";
    public string Repo { get; set; } = "";
    public PullRequestConfig Config { get; set; } = new();
    public string SourceOwner { get; set; } = "";
    public string SourceRepo { get; set; } = "";
    public string SourceBranch { get; set; } = "";
    public string SourceRemote { get; set; } = "";
    public bool Push { get; set; } = false;
}

// ── Commit operations ────────────────────────────────────────────────────────

public class RequestCherrypickCommit : RequestMessage
{
    public override string Command => "cherrypickCommit";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public int ParentIndex { get; set; } = 0;
    public bool RecordOrigin { get; set; } = false;
    public bool NoCommit { get; set; } = false;
}

public class RequestDropCommit : RequestMessage
{
    public override string Command => "dropCommit";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
}

public class RequestResetToCommit : RequestMessage
{
    public override string Command => "resetToCommit";
    public string Repo { get; set; } = "";
    public string Commit { get; set; } = "";
    public GitResetMode ResetMode { get; set; } = GitResetMode.Mixed;
}

public class RequestRevertCommit : RequestMessage
{
    public override string Command => "revertCommit";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public int ParentIndex { get; set; } = 0;
}

public class RequestCleanUntrackedFiles : RequestMessage
{
    public override string Command => "cleanUntrackedFiles";
    public string Repo { get; set; } = "";
    public bool Directories { get; set; } = false;
}

public class RequestResetFileToRevision : RequestMessage
{
    public override string Command => "resetFileToRevision";
    public string Repo { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string FilePath { get; set; } = "";
}

// ── Merge & Rebase ───────────────────────────────────────────────────────────

public class RequestMerge : RequestMessage
{
    public override string Command => "merge";
    public string Repo { get; set; } = "";
    public string Obj { get; set; } = "";
    public MergeActionOn ActionOn { get; set; }
    public bool CreateNewCommit { get; set; } = false;
    public bool Squash { get; set; } = false;
    public bool NoCommit { get; set; } = false;
}

public class RequestRebase : RequestMessage
{
    public override string Command => "rebase";
    public string Repo { get; set; } = "";
    public string Obj { get; set; } = "";
    public RebaseActionOn ActionOn { get; set; }
    public bool IgnoreDate { get; set; } = false;
    public bool Interactive { get; set; } = false;
}

// ── Stash operations ─────────────────────────────────────────────────────────

public class RequestApplyStash : RequestMessage
{
    public override string Command => "applyStash";
    public string Repo { get; set; } = "";
    public string Selector { get; set; } = "";
    public bool ReinstateIndex { get; set; } = false;
}

public class RequestBranchFromStash : RequestMessage
{
    public override string Command => "branchFromStash";
    public string Repo { get; set; } = "";
    public string Selector { get; set; } = "";
    public string BranchName { get; set; } = "";
}

public class RequestDropStash : RequestMessage
{
    public override string Command => "dropStash";
    public string Repo { get; set; } = "";
    public string Selector { get; set; } = "";
}

public class RequestPopStash : RequestMessage
{
    public override string Command => "popStash";
    public string Repo { get; set; } = "";
    public string Selector { get; set; } = "";
    public bool ReinstateIndex { get; set; } = false;
}

public class RequestPushStash : RequestMessage
{
    public override string Command => "pushStash";
    public string Repo { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IncludeUntracked { get; set; } = false;
}

// ── Other diff/archive ────────────────────────────────────────────────────────

public class RequestOpenExternalDirDiff : RequestMessage
{
    public override string Command => "openExternalDirDiff";
    public string Repo { get; set; } = "";
    public string FromHash { get; set; } = "";
    public string ToHash { get; set; } = "";
    public bool IsGui { get; set; } = false;
}

public class RequestCreateArchive : RequestMessage
{
    public override string Command => "createArchive";
    public string Repo { get; set; } = "";
    public string Ref { get; set; } = "";
}

// ── User config ──────────────────────────────────────────────────────────────

public class RequestEditUserDetails : RequestMessage
{
    public override string Command => "editUserDetails";
    public string Repo { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public GitConfigLocation Location { get; set; } = GitConfigLocation.Local;
}

public class RequestDeleteUserDetails : RequestMessage
{
    public override string Command => "deleteUserDetails";
    public string Repo { get; set; } = "";
    public bool Name { get; set; } = false;
    public bool Email { get; set; } = false;
    public GitConfigLocation Location { get; set; } = GitConfigLocation.Local;
}

// ── Code review ──────────────────────────────────────────────────────────────

public class RequestStartCodeReview : RequestMessage
{
    public override string Command => "startCodeReview";
    public string Repo { get; set; } = "";
    public string Id { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string CompareWithHash { get; set; } = "";
    public string[] Files { get; set; } = [];
    public string? LastViewedFile { get; set; }
}

public class RequestUpdateCodeReview : RequestMessage
{
    public override string Command => "updateCodeReview";
    public string Repo { get; set; } = "";
    public string Id { get; set; } = "";
    public string[] RemainingFiles { get; set; } = [];
    public string? LastViewedFile { get; set; }
}

public class RequestEndCodeReview : RequestMessage
{
    public override string Command => "endCodeReview";
    public string Repo { get; set; } = "";
    public string Id { get; set; } = "";
}

// ── View state ───────────────────────────────────────────────────────────────

public class RequestSetGlobalViewState : RequestMessage
{
    public override string Command => "setGlobalViewState";
    public GitGraphViewGlobalState State { get; set; } = new();
}

public class RequestSetWorkspaceViewState : RequestMessage
{
    public override string Command => "setWorkspaceViewState";
    public GitGraphViewWorkspaceState State { get; set; } = new();
}

public class RequestSetRepoState : RequestMessage
{
    public override string Command => "setRepoState";
    public string Repo { get; set; } = "";
    public GitRepoState State { get; set; } = new();
}

// ── No-op ────────────────────────────────────────────────────────────────────

public class RequestNoOp : RequestMessage
{
    public override string Command => "noop";
}
