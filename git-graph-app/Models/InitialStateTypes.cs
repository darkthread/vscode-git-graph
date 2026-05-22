namespace git_graph.Models;

public class InitialStateResponse
{
    public GitGraphViewInitialState InitialState { get; set; } = new();
    public GitGraphViewGlobalState GlobalState { get; set; } = new();
    public GitGraphViewWorkspaceState WorkspaceState { get; set; } = new();
    public string ColorVars { get; set; } = "";
    public string ColorParams { get; set; } = "";
}

public class GitGraphViewConfig
{
    public CommitDetailsViewConfig CommitDetailsView { get; set; } = new();
    public CommitOrdering CommitOrdering { get; set; } = CommitOrdering.Date;
    public ContextMenuActionsVisibility ContextMenuActionsVisibility { get; set; } = new();
    public CustomBranchGlobPattern[] CustomBranchGlobPatterns { get; set; } = [];
    public CustomEmojiShortcodeMapping[] CustomEmojiShortcodeMappings { get; set; } = [];
    public CustomPullRequestProvider[] CustomPullRequestProviders { get; set; } = [];
    public DateFormat DateFormat { get; set; } = new();
    public DefaultColumnVisibility DefaultColumnVisibility { get; set; } = new();
    public DialogDefaults DialogDefaults { get; set; } = new();
    public bool EnhancedAccessibility { get; set; } = false;
    public bool FetchAndPrune { get; set; } = false;
    public bool FetchAndPruneTags { get; set; } = false;
    public bool FetchAvatars { get; set; } = false;
    public GraphConfig Graph { get; set; } = new();
    public bool IncludeCommitsMentionedByReflogs { get; set; } = false;
    public int InitialLoadCommits { get; set; } = 300;
    public KeybindingConfig Keybindings { get; set; } = new();
    public int LoadMoreCommits { get; set; } = 75;
    public bool LoadMoreCommitsAutomatically { get; set; } = true;
    public bool Markdown { get; set; } = true;
    public MuteCommitsConfig Mute { get; set; } = new();
    public bool OnlyFollowFirstParent { get; set; } = false;
    public OnRepoLoadConfig OnRepoLoad { get; set; } = new();
    public ReferenceLabelsConfig ReferenceLabels { get; set; } = new();
    public RepoDropdownOrder RepoDropdownOrder { get; set; } = RepoDropdownOrder.FullPath;
    public bool ShowRemoteBranches { get; set; } = true;
    public bool ShowStashes { get; set; } = true;
    public bool ShowTags { get; set; } = true;
}

public class CommitDetailsViewConfig
{
    public bool AutoCenter { get; set; } = true;
    public bool FileTreeCompactFolders { get; set; } = true;
    public FileViewType FileViewType { get; set; } = FileViewType.Tree;
    public CommitDetailsViewLocation Location { get; set; } = CommitDetailsViewLocation.Inline;
}

public class ContextMenuActionsVisibility
{
    public BranchContextMenuActionsVisibility Branch { get; set; } = new();
    public CommitContextMenuActionsVisibility Commit { get; set; } = new();
    public CommitDetailsViewFileContextMenuActionsVisibility CommitDetailsViewFile { get; set; } = new();
    public RemoteBranchContextMenuActionsVisibility RemoteBranch { get; set; } = new();
    public StashContextMenuActionsVisibility Stash { get; set; } = new();
    public TagContextMenuActionsVisibility Tag { get; set; } = new();
    public UncommittedChangesContextMenuActionsVisibility UncommittedChanges { get; set; } = new();
}

public class BranchContextMenuActionsVisibility
{
    public bool Checkout { get; set; } = true;
    public bool Rename { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Merge { get; set; } = true;
    public bool Rebase { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool ViewIssue { get; set; } = true;
    public bool CreatePullRequest { get; set; } = true;
    public bool CreateArchive { get; set; } = true;
    public bool SelectInBranchesDropdown { get; set; } = true;
    public bool UnselectInBranchesDropdown { get; set; } = true;
    public bool CopyName { get; set; } = true;
}

public class CommitContextMenuActionsVisibility
{
    public bool AddTag { get; set; } = true;
    public bool CreateBranch { get; set; } = true;
    public bool Checkout { get; set; } = true;
    public bool Cherrypick { get; set; } = true;
    public bool Revert { get; set; } = true;
    public bool Drop { get; set; } = true;
    public bool Merge { get; set; } = true;
    public bool Rebase { get; set; } = true;
    public bool Reset { get; set; } = true;
    public bool CopyHash { get; set; } = true;
    public bool CopySubject { get; set; } = true;
}

public class CommitDetailsViewFileContextMenuActionsVisibility
{
    public bool ViewDiff { get; set; } = true;
    public bool ViewFileAtThisRevision { get; set; } = true;
    public bool ViewDiffWithWorkingFile { get; set; } = true;
    public bool OpenFile { get; set; } = true;
    public bool MarkAsReviewed { get; set; } = true;
    public bool MarkAsNotReviewed { get; set; } = true;
    public bool ResetFileToThisRevision { get; set; } = true;
    public bool CopyAbsoluteFilePath { get; set; } = true;
    public bool CopyRelativeFilePath { get; set; } = true;
}

public class RemoteBranchContextMenuActionsVisibility
{
    public bool Checkout { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Fetch { get; set; } = true;
    public bool Merge { get; set; } = true;
    public bool Pull { get; set; } = true;
    public bool ViewIssue { get; set; } = true;
    public bool CreatePullRequest { get; set; } = true;
    public bool CreateArchive { get; set; } = true;
    public bool SelectInBranchesDropdown { get; set; } = true;
    public bool UnselectInBranchesDropdown { get; set; } = true;
    public bool CopyName { get; set; } = true;
}

public class StashContextMenuActionsVisibility
{
    public bool Apply { get; set; } = true;
    public bool CreateBranch { get; set; } = true;
    public bool Pop { get; set; } = true;
    public bool Drop { get; set; } = true;
    public bool CopyName { get; set; } = true;
    public bool CopyHash { get; set; } = true;
}

public class TagContextMenuActionsVisibility
{
    public bool ViewDetails { get; set; } = true;
    public bool Delete { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool CreateArchive { get; set; } = true;
    public bool CopyName { get; set; } = true;
}

public class UncommittedChangesContextMenuActionsVisibility
{
    public bool Stash { get; set; } = true;
    public bool Reset { get; set; } = true;
    public bool Clean { get; set; } = true;
    public bool OpenSourceControlView { get; set; } = true;
}

public record CustomBranchGlobPattern(string Name, string Glob);

public record CustomEmojiShortcodeMapping(string Shortcode, string Emoji);

public record CustomPullRequestProvider(string Name, string TemplateUrl);

public class DateFormat
{
    public DateFormatType Type { get; set; } = DateFormatType.DateAndTime;
    public bool Iso { get; set; } = false;
}

public class DefaultColumnVisibility
{
    public bool Date { get; set; } = true;
    public bool Author { get; set; } = true;
    public bool Commit { get; set; } = true;
}

public class DialogDefaults
{
    public AddTagDialogDefaults AddTag { get; set; } = new();
    public ApplyStashDialogDefaults ApplyStash { get; set; } = new();
    public CherryPickDialogDefaults CherryPick { get; set; } = new();
    public CreateBranchDialogDefaults CreateBranch { get; set; } = new();
    public DeleteBranchDialogDefaults DeleteBranch { get; set; } = new();
    public FetchIntoLocalBranchDialogDefaults FetchIntoLocalBranch { get; set; } = new();
    public FetchRemoteDialogDefaults FetchRemote { get; set; } = new();
    public GeneralDialogDefaults General { get; set; } = new();
    public MergeDialogDefaults Merge { get; set; } = new();
    public PopStashDialogDefaults PopStash { get; set; } = new();
    public PullBranchDialogDefaults PullBranch { get; set; } = new();
    public RebaseDialogDefaults Rebase { get; set; } = new();
    public ResetCommitDialogDefaults ResetCommit { get; set; } = new();
    public ResetUncommittedDialogDefaults ResetUncommitted { get; set; } = new();
    public StashUncommittedChangesDialogDefaults StashUncommittedChanges { get; set; } = new();
}

public class AddTagDialogDefaults
{
    public bool PushToRemote { get; set; } = false;
    public TagType Type { get; set; } = TagType.Annotated;
}

public class ApplyStashDialogDefaults
{
    public bool ReinstateIndex { get; set; } = false;
}

public class CherryPickDialogDefaults
{
    public bool NoCommit { get; set; } = false;
    public bool RecordOrigin { get; set; } = false;
}

public class CreateBranchDialogDefaults
{
    public bool Checkout { get; set; } = false;
}

public class DeleteBranchDialogDefaults
{
    public bool ForceDelete { get; set; } = false;
}

public class FetchIntoLocalBranchDialogDefaults
{
    public bool ForceFetch { get; set; } = false;
}

public class FetchRemoteDialogDefaults
{
    public bool Prune { get; set; } = false;
    public bool PruneTags { get; set; } = false;
}

public class GeneralDialogDefaults
{
    public string? ReferenceInputSpaceSubstitution { get; set; } = null;
}

public class MergeDialogDefaults
{
    public bool NoCommit { get; set; } = false;
    public bool NoFastForward { get; set; } = true;
    public bool Squash { get; set; } = false;
}

public class PopStashDialogDefaults
{
    public bool ReinstateIndex { get; set; } = false;
}

public class PullBranchDialogDefaults
{
    public bool NoFastForward { get; set; } = false;
    public bool Squash { get; set; } = false;
}

public class RebaseDialogDefaults
{
    public bool IgnoreDate { get; set; } = true;
    public bool Interactive { get; set; } = false;
}

public class ResetCommitDialogDefaults
{
    public GitResetMode Mode { get; set; } = GitResetMode.Mixed;
}

public class ResetUncommittedDialogDefaults
{
    public GitResetMode Mode { get; set; } = GitResetMode.Mixed;
}

public class StashUncommittedChangesDialogDefaults
{
    public bool IncludeUntracked { get; set; } = true;
}

public class GraphConfig
{
    public string[] Colours { get; set; } = [];
    public GraphStyle Style { get; set; } = GraphStyle.Rounded;
    public GraphGridConfig Grid { get; set; } = new();
    public GraphUncommittedChangesStyle UncommittedChanges { get; set; } = GraphUncommittedChangesStyle.OpenCircleAtTheUncommittedChanges;
}

public class GraphGridConfig
{
    public int X { get; set; } = 16;
    public int Y { get; set; } = 24;
    public int OffsetX { get; set; } = 16;
    public int OffsetY { get; set; } = 12;
    public int ExpandY { get; set; } = 250;
}

public class KeybindingConfig
{
    public string? Find { get; set; } = "f";
    public string? Refresh { get; set; } = "r";
    public string? ScrollToHead { get; set; } = "h";
    public string? ScrollToStash { get; set; } = "s";
}

public class MuteCommitsConfig
{
    public bool CommitsNotAncestorsOfHead { get; set; } = false;
    public bool MergeCommits { get; set; } = true;
}

public class OnRepoLoadConfig
{
    public bool ScrollToHead { get; set; } = false;
    public bool ShowCheckedOutBranch { get; set; } = false;
    public string[] ShowSpecificBranches { get; set; } = [];
}

public class ReferenceLabelsConfig
{
    public bool BranchLabelsAlignedToGraph { get; set; } = false;
    public bool CombineLocalAndRemoteBranchLabels { get; set; } = true;
    public bool TagLabelsOnRight { get; set; } = false;
}

public class LoadGitGraphViewTo
{
    public string Repo { get; set; } = "";
    public LoadGitGraphViewToCommitDetails? CommitDetails { get; set; }
    public string? RunCommandOnLoad { get; set; }
}

public class LoadGitGraphViewToCommitDetails
{
    public string CommitHash { get; set; } = "";
    public string? CompareWithHash { get; set; }
}