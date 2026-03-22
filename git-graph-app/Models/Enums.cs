using System.Text.Json;
using System.Text.Json.Serialization;

namespace git_graph.Models;

public enum GitFileStatus
{
    Added = 'A',
    Modified = 'M',
    Deleted = 'D',
    Renamed = 'R',
    Untracked = 'U'
}

/// <summary>
/// Serializes GitFileStatus as its single-character value ("A", "M", "D", "R", "U")
/// to match the TypeScript GitFileStatus string enum expected by the frontend.
/// </summary>
public class GitFileStatusConverter : JsonConverter<GitFileStatus>
{
    public override GitFileStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return s?.Length == 1 ? (GitFileStatus)s[0] : GitFileStatus.Modified;
    }

    public override void Write(Utf8JsonWriter writer, GitFileStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(((char)(int)value).ToString());
    }
}

public enum GitSignatureStatus
{
    GoodAndValid,           // G
    GoodWithUnknownValidity, // U
    GoodButExpired,         // X
    GoodButMadeByExpiredKey, // Y
    GoodButMadeByRevokedKey, // R
    CannotBeChecked,        // E
    Bad                     // B
}

/// <summary>Serializes GitSignatureStatus as its single-letter value to match the TypeScript GitSignatureStatus string enum.</summary>
public class GitSignatureStatusConverter : JsonConverter<GitSignatureStatus>
{
    private static readonly Dictionary<string, GitSignatureStatus> _read = new()
    {
        ["G"] = GitSignatureStatus.GoodAndValid,
        ["U"] = GitSignatureStatus.GoodWithUnknownValidity,
        ["X"] = GitSignatureStatus.GoodButExpired,
        ["Y"] = GitSignatureStatus.GoodButMadeByExpiredKey,
        ["R"] = GitSignatureStatus.GoodButMadeByRevokedKey,
        ["E"] = GitSignatureStatus.CannotBeChecked,
        ["B"] = GitSignatureStatus.Bad
    };
    private static readonly Dictionary<GitSignatureStatus, string> _write =
        _read.ToDictionary(kv => kv.Value, kv => kv.Key);

    public override GitSignatureStatus Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        var s = reader.GetString() ?? "";
        return _read.TryGetValue(s, out var v) ? v : GitSignatureStatus.CannotBeChecked;
    }
    public override void Write(Utf8JsonWriter writer, GitSignatureStatus value, JsonSerializerOptions o) =>
        writer.WriteStringValue(_write.TryGetValue(value, out var s) ? s : "E");
}

public enum GitConfigLocation
{
    Local,
    Global,
    System
}

public enum GitPushBranchMode
{
    Normal,
    Force,
    ForceWithLease
}

public enum GitResetMode
{
    Soft,
    Mixed,
    Hard
}

public enum BooleanOverride
{
    Default = 0,
    Enabled = 1,
    Disabled = 2
}

public enum CommitOrdering
{
    Date,
    AuthorDate,
    Topological
}

public enum RepoCommitOrdering
{
    Default,
    Date,
    AuthorDate,
    Topological
}

public enum DateFormatType
{
    DateAndTime,
    DateOnly,
    Relative
}

public enum DateType
{
    Author,
    Commit
}

public enum FileViewType
{
    Default = 0,
    Tree = 1,
    List = 2
}

public enum GraphStyle
{
    Rounded,
    Angular
}

public enum GraphUncommittedChangesStyle
{
    OpenCircleAtTheUncommittedChanges,
    OpenCircleAtTheCheckedOutCommit
}

public enum CommitDetailsViewLocation
{
    Inline,
    DockedToBottom
}

public enum RefLabelAlignment
{
    Normal,
    BranchesOnLeftAndTagsOnRight,
    BranchesAlignedToGraphAndTagsOnRight
}

public enum RepoDropdownOrder
{
    FullPath,
    Name,
    WorkspaceFullPath
}

public enum TagType
{
    Annotated,
    Lightweight
}

public enum PullRequestProvider
{
    Bitbucket,
    Custom,
    GitHub,
    GitLab
}

public enum TabIconColourTheme
{
    Colour,
    Grey
}

public enum SquashMessageFormat
{
    Default,
    GitSquashMsg
}

public enum MergeActionOn
{
    Branch,
    RemoteTrackingBranch,
    Commit
}

/// <summary>Serializes MergeActionOn to match TypeScript MergeActionOn string enum values.</summary>
public class MergeActionOnConverter : JsonConverter<MergeActionOn>
{
    public override MergeActionOn Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) =>
        (reader.GetString() ?? "") switch
        {
            "Branch" => MergeActionOn.Branch,
            "Remote-tracking Branch" => MergeActionOn.RemoteTrackingBranch,
            "Commit" => MergeActionOn.Commit,
            _ => MergeActionOn.Branch
        };

    public override void Write(Utf8JsonWriter writer, MergeActionOn value, JsonSerializerOptions o) =>
        writer.WriteStringValue(value switch
        {
            MergeActionOn.Branch => "Branch",
            MergeActionOn.RemoteTrackingBranch => "Remote-tracking Branch",
            MergeActionOn.Commit => "Commit",
            _ => "Branch"
        });
}

public enum RebaseActionOn
{
    Branch,
    Commit
}

/// <summary>Serializes RebaseActionOn to match TypeScript RebaseActionOn string enum values.</summary>
public class RebaseActionOnConverter : JsonConverter<RebaseActionOn>
{
    public override RebaseActionOn Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) =>
        (reader.GetString() ?? "") switch
        {
            "Branch" => RebaseActionOn.Branch,
            "Commit" => RebaseActionOn.Commit,
            _ => RebaseActionOn.Branch
        };

    public override void Write(Utf8JsonWriter writer, RebaseActionOn value, JsonSerializerOptions o) =>
        writer.WriteStringValue(value switch
        {
            RebaseActionOn.Branch => "Branch",
            RebaseActionOn.Commit => "Commit",
            _ => "Branch"
        });
}
