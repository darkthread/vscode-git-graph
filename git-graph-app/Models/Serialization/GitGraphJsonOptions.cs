using System.Text.Json;
using System.Text.Json.Serialization;
using git_graph.Models;

namespace git_graph.Models.Serialization;

public static class GitGraphJsonOptions
{
    public static readonly GitGraphPayloadJsonContext PayloadContext = new(CreatePayloadOptions());
    public static readonly GitGraphStateJsonContext StateContext = new(CreateStateOptions());

    public static void ConfigurePayloadOptions(JsonSerializerOptions options)
    {
        ConfigureCommonOptions(options);
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.WriteIndented = false;
        options.TypeInfoResolver = PayloadContext;
    }

    private static JsonSerializerOptions CreatePayloadOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };
        ConfigureCommonOptions(options);
        return options;
    }

    private static JsonSerializerOptions CreateStateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        ConfigureCommonOptions(options);
        return options;
    }

    private static void ConfigureCommonOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new GitFileStatusConverter());
        options.Converters.Add(new GitSignatureStatusConverter());
        options.Converters.Add(new GitConfigLocationConverter());
        options.Converters.Add(new GitPushBranchModeConverter());
        options.Converters.Add(new GitResetModeConverter());
        options.Converters.Add(new CommitOrderingConverter());
        options.Converters.Add(new RepoCommitOrderingConverter());
        options.Converters.Add(new MergeActionOnConverter());
        options.Converters.Add(new RebaseActionOnConverter());
    }
}