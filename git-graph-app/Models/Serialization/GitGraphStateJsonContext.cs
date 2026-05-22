using System.Text.Json.Serialization;
using git_graph.Models;

namespace git_graph.Models.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(PersistedState))]
[JsonSerializable(typeof(Dictionary<string, GitRepoState>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, CodeReviewData>>))]
[JsonSerializable(typeof(Dictionary<string, CodeReviewData>))]
[JsonSerializable(typeof(GitRepoState))]
[JsonSerializable(typeof(CodeReviewData))]
[JsonSerializable(typeof(GitGraphViewGlobalState))]
[JsonSerializable(typeof(GitGraphViewWorkspaceState))]
[JsonSerializable(typeof(IssueLinkingConfig))]
[JsonSerializable(typeof(PullRequestConfig))]
[JsonSerializable(typeof(PullRequestCustomConfig))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(int[]))]
public partial class GitGraphStateJsonContext : JsonSerializerContext
{
}