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

public abstract class StringEnumJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private readonly Dictionary<string, TEnum> _read;
    private readonly Dictionary<TEnum, string> _write;
    private readonly TEnum _fallback;

    protected StringEnumJsonConverter(Dictionary<TEnum, string> values, TEnum fallback)
    {
        _write = values;
        _read = values.ToDictionary(pair => pair.Value, pair => pair.Key);
        _fallback = fallback;
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? "";
        return _read.TryGetValue(value, out var enumValue) ? enumValue : _fallback;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(_write.TryGetValue(value, out var text) ? text : _write[_fallback]);
    }
}

public enum GitConfigLocation
{
    Local,
    Global,
    System
}

public class GitConfigLocationConverter() : StringEnumJsonConverter<GitConfigLocation>(
    new Dictionary<GitConfigLocation, string>
    {
        [GitConfigLocation.Local] = "local",
        [GitConfigLocation.Global] = "global",
        [GitConfigLocation.System] = "system"
    },
    GitConfigLocation.Local);

public enum GitPushBranchMode
{
    Normal,
    Force,
    ForceWithLease
}

public class GitPushBranchModeConverter() : StringEnumJsonConverter<GitPushBranchMode>(
    new Dictionary<GitPushBranchMode, string>
    {
        [GitPushBranchMode.Normal] = "",
        [GitPushBranchMode.Force] = "force",
        [GitPushBranchMode.ForceWithLease] = "force-with-lease"
    },
    GitPushBranchMode.Normal);

public enum GitResetMode
{
    Soft,
    Mixed,
    Hard
}

public class GitResetModeConverter() : StringEnumJsonConverter<GitResetMode>(
    new Dictionary<GitResetMode, string>
    {
        [GitResetMode.Soft] = "soft",
        [GitResetMode.Mixed] = "mixed",
        [GitResetMode.Hard] = "hard"
    },
    GitResetMode.Mixed);

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

public class CommitOrderingConverter() : StringEnumJsonConverter<CommitOrdering>(
    new Dictionary<CommitOrdering, string>
    {
        [CommitOrdering.Date] = "date",
        [CommitOrdering.AuthorDate] = "author-date",
        [CommitOrdering.Topological] = "topo"
    },
    CommitOrdering.Date);

public enum RepoCommitOrdering
{
    Default,
    Date,
    AuthorDate,
    Topological
}

public class RepoCommitOrderingConverter() : StringEnumJsonConverter<RepoCommitOrdering>(
    new Dictionary<RepoCommitOrdering, string>
    {
        [RepoCommitOrdering.Default] = "default",
        [RepoCommitOrdering.Date] = "date",
        [RepoCommitOrdering.AuthorDate] = "author-date",
        [RepoCommitOrdering.Topological] = "topo"
    },
    RepoCommitOrdering.Default);

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
