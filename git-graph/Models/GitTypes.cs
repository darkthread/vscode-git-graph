namespace git_graph.Models;

public record GitCommit(
    string Hash,
    string[] Parents,
    string Author,
    string Email,
    long Date,
    string Message,
    string[] Heads,
    GitCommitTag[] Tags,
    GitCommitRemote[] Remotes,
    GitCommitStash? Stash
);

public record GitCommitTag(string Name, bool Annotated);

public record GitCommitRemote(
    string Name,
    string? Remote  // null = remote not found
);

public record GitCommitStash(
    string Selector,
    string BaseHash,
    string? UntrackedFilesHash
);

public record GitCommitDetails(
    string Hash,
    string[] Parents,
    string Author,
    string AuthorEmail,
    long AuthorDate,
    string Committer,
    string CommitterEmail,
    long CommitterDate,
    GitSignature? Signature,
    string Body,
    GitFileChange[] FileChanges
);

public record GitSignature(
    string Key,
    string Signer,
    GitSignatureStatus Status
);

public record GitFileChange(
    string OldFilePath,
    string NewFilePath,
    GitFileStatus Type,
    int? Additions,
    int? Deletions
);

public record GitStash(
    string Hash,
    string BaseHash,
    string? UntrackedFilesHash,
    string Selector,
    string Author,
    string Email,
    long Date,
    string Message
);

public record GitTagDetails(
    string Hash,
    string TaggerName,
    string TaggerEmail,
    long TaggerDate,
    string Message,
    GitSignature? Signature
);

public record GitRepoConfig(
    Dictionary<string, GitRepoConfigBranch> Branches,
    string? DiffTool,
    string? GuiDiffTool,
    string? PushDefault,
    GitRepoSettingsRemote[] Remotes,
    GitRepoConfigUser User
);

public record GitRepoConfigBranch(string? PushRemote, string? Remote);

public record GitRepoSettingsRemote(string Name, string? Url, string? PushUrl);

public record GitRepoConfigUser(
    GitRepoConfigUserLocal Name,
    GitRepoConfigUserLocal Email
);

public record GitRepoConfigUserLocal(string? Local, string? Global);

// Response wrapper types
public record GitRepoInfo(
    string[] Branches,
    string? Head,
    string[] Remotes,
    GitStash[] Stashes,
    string? Error
);

public record GitCommitData(
    GitCommit[] Commits,
    string? Head,
    string[] Tags,
    bool MoreCommitsAvailable,
    string? Error
);

public record GitCommitDetailsData(
    GitCommitDetails? CommitDetails,
    string? Error
);

public record GitCommitComparisonData(
    GitFileChange[] FileChanges,
    string? Error
);

public record GitRepoConfigData(
    GitRepoConfig? Config,
    string? Error
);

public record GitTagDetailsData(
    GitTagDetails? Details,
    string? Error
);
