using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using git_graph.Hubs;
using git_graph.Models;
using git_graph.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddSignalR()
    .AddJsonProtocol(opt =>
    {
        opt.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // Do NOT set WhenWritingNull — the frontend checks `msg.error === null` to detect success;
        // omitting null props causes `undefined !== null` to fall into the error branch.
        opt.PayloadSerializerOptions.Converters.Add(new GitFileStatusConverter());
        opt.PayloadSerializerOptions.Converters.Add(new GitSignatureStatusConverter());
        opt.PayloadSerializerOptions.Converters.Add(new MergeActionOnConverter());
        opt.PayloadSerializerOptions.Converters.Add(new RebaseActionOnConverter());
        opt.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddSingleton<GitExecutableService>();
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<StateManager>();
builder.Services.AddSingleton<RepoWatcher>();

// ── Port configuration ────────────────────────────────────────────────────────

string? portEnv = Environment.GetEnvironmentVariable("GIT_GRAPH_PORT");
if (int.TryParse(portEnv, out int port))
{
    builder.WebHost.UseUrls($"http://localhost:{port}");
}

var app = builder.Build();

// ── Static files (../media/) ──────────────────────────────────────────────────

string mediaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "media"));

if (Directory.Exists(mediaPath))
{
    var provider = new PhysicalFileProvider(mediaPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = provider,
        RequestPath = ""
    });
    Console.WriteLine($"Serving static files from {mediaPath}");
}

// ── Register repos from CLI args ──────────────────────────────────────────────

var stateManager = app.Services.GetRequiredService<StateManager>();
var gitService = app.Services.GetRequiredService<GitService>();
var watcher = app.Services.GetRequiredService<RepoWatcher>();

var repoPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Directory.GetCurrentDirectory();
string? root = await gitService.RepoRootAsync(repoPath);
if (root != null)
{
    stateManager.ClearRepoRegistrations();
    stateManager.EnsureRepoRegistered(root);
    // Also register submodules
    var submodules = await gitService.GetSubmodulesAsync(root);
    foreach (var sub in submodules)
        stateManager.EnsureRepoRegistered(sub);    
}
else {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"No git repository found at {repoPath} or any of its parent directories.");
    Console.ResetColor();
    return;
}


// ── SignalR hub ───────────────────────────────────────────────────────────────

app.MapHub<GitGraphHub>("/hub");

// Wire watcher refresh to all connected clients
watcher.OnRefresh += (_, _) =>
{
    var hub = app.Services.GetRequiredService<IHubContext<GitGraphHub, IGitGraphClient>>();
    _ = hub.Clients.All.ReceiveMessage(new { command = "refresh" });
};

// ── Page routes ───────────────────────────────────────────────────────────────

app.MapGet("/", async context =>
{
    string indexHtml = Path.Combine(mediaPath, "index.html");
    if (File.Exists(indexHtml))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexHtml);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});

app.MapGet("/diff", async context =>
{
    string diffHtml = Path.Combine(mediaPath, "diff.html");
    if (File.Exists(diffHtml))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(diffHtml);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});

// ── API routes ────────────────────────────────────────────────────────────────

app.MapGet("/api/initialState", (HttpContext context) =>
{
    var repos = stateManager.GetRepos();
    var lastActiveRepo = stateManager.GetLastActiveRepo();

    // Default graph colours (matches the VS Code extension defaults)
    var graphColours = new[] {
        "#0085d9", "#d9008f", "#00d90a", "#d98500", "#a300d9", "#ff0000",
        "#00d9cc", "#e138e8", "#85d900", "#dc5b23", "#6f24d6", "#ffcc00"
    };

    // Build colorVars / colorParams strings (consumed by index.html to inject CSS vars)
    var colorVars = string.Concat(graphColours.Select((c, i) => $"--git-graph-color{i}:{c}; "));
    var colorParams = string.Concat(graphColours.Select((_, i) =>
        $"[data-color=\"{i}\"]{{--git-graph-color:var(--git-graph-color{i});}} "));

    // Full config object with sensible defaults — mirrors GitGraphViewConfig in types.ts
    var config = new
    {
        commitDetailsView = new
        {
            autoCenter = true,
            fileTreeCompactFolders = true,
            fileViewType = 0,   // FileViewType.Default = 0
            location = 0        // CommitDetailsViewLocation.Inline = 0
        },
        commitOrdering = 0,     // CommitOrdering.Date = 0
        contextMenuActionsVisibility = new
        {
            branch = new { checkout = true, rename = true, delete = true, rebase = true, merge = true, revert = true, cherryPick = true, createArchive = true, copyName = true },
            commit = new { addTag = true, createBranch = true, merge = true, revert = true, cherryPick = true, checkout = true, resetCurrentBranchToHere = true, createArchive = true, copyHash = true },
            remoteBranch = new { checkout = true, delete = true, fetch = true, merge = true, rebase = true, pull = true, createArchive = true, copyName = true },
            stash = new { apply = true, createBranch = true, pop = true, drop = true, copyName = true },
            tag = new { viewDetails = true, delete = true, pushTag = true, createArchive = true, copyName = true },
            uncommittedChanges = new { stash = true, resetUncommittedChanges = true, cleanUncommittedChanges = true, openSourceControlView = true }
        },
        customBranchGlobPatterns = Array.Empty<object>(),
        customEmojiShortcodeMappings = Array.Empty<object>(),
        customPullRequestProviders = Array.Empty<object>(),
        dateFormat = new { type = 0, iso = false },  // DateFormatType.DateAndTime = 0
        defaultColumnVisibility = new { date = true, author = true, commit = true },
        dialogDefaults = new
        {
            addTag = new { pushToRemote = false, type = 1 },       // GitTagType.Annotated = 1
            applyStash = new { reinstateIndex = false },
            cherryPick = new { noCommit = false, recordOrigin = false },
            createBranch = new { checkout = true },
            deleteBranch = new { forceDelete = false },
            fetchIntoLocalBranch = new { forceFetch = false },
            fetchRemote = new { prune = false, pruneTags = false },
            merge = new { noCommit = false, noFastForward = false, squash = false },
            popStash = new { reinstateIndex = false },
            pullBranch = new { noFastForward = false, squash = false },
            rebase = new { ignoreDate = true, launchInteractiveRebase = false },
            resetCommit = new { mode = 1 },                         // GitResetMode.Mixed = 1
            resetUncommittedChanges = new { mode = 1 },
            stashChanges = new { includeUntracked = true, onlyStaged = false },
            revertCommit = new { noCommit = false }
        },
        enhancedAccessibility = false,
        fetchAndPrune = false,
        fetchAndPruneTags = false,
        fetchAvatars = false,
        graph = new
        {
            colours = graphColours,
            style = 0,              // GraphStyle.Rounded = 0
            grid = new { x = 16, y = 24, offsetX = 16, offsetY = 12, expandY = 250 },
            uncommittedChanges = 0  // GraphUncommittedChangesStyle.OpenCircleAtTheUncommittedChanges = 0
        },
        includeCommitsMentionedByReflogs = false,
        initialLoadCommits = 300,
        keybindings = new { find = "f", refresh = "r", scrollToHead = "h", scrollToStash = "s" },
        loadMoreCommits = 75,
        loadMoreCommitsAutomatically = true,
        markdown = true,
        mute = new
        {
            commitType = "head",    // MuteCommitType.Head
            head = false,
            tags = false,
            merges = false,
            whitespace = false
        },
        onlyFollowFirstParent = false,
        onRepoLoad = new { showCheckedOutBranch = false, showSpecificBranches = Array.Empty<string>() },
        referenceLabels = new
        {
            branchLabelsAlignedToGraph = false,
            combineLocalAndRemoteBranchLabels = true,
            fetchAndPrune = false,
            fetchAndPruneTags = false,
            onlyFollowFirstParent = false
        },
        repoDropdownOrder = "name",  // RepoDropdownOrder.Name
        showRemoteBranches = true,
        showStashes = true,
        showTags = true
    };

    var initialState = new
    {
        config,
        lastActiveRepo,
        loadViewTo = (object?)null,
        repos,
        loadRepoInfoRefreshId = 0,
        loadCommitsRefreshId = 0
    };

    var globalState = new
    {
        alwaysAcceptCheckoutCommit = false,
        issueLinkingConfig = (object?)null,
        pushTagSkipRemoteCheck = false
    };

    var workspaceState = new
    {
        findIsCaseSensitive = false,
        findIsRegex = false,
        findOpenCommitDetailsView = false
    };

    var response = new { initialState, globalState, workspaceState, colorVars, colorParams };

    return Results.Json(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        // Keep null values (e.g. loadViewTo, issueLinkingConfig, lastActiveRepo) so the
        // frontend can distinguish null from undefined — the JS code checks !== null.
    });
});

// Return raw file content at a given revision
app.MapGet("/api/file", async (HttpContext context, string repo, string hash, string file) =>
{
    try
    {
        string content = await gitService.GetCommitFileAsync(repo, hash, file);
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(content);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});

// Return unified diff
app.MapGet("/api/diff", async (HttpContext context, string repo, string fromHash, string toHash, string oldPath, string newPath) =>
{
    try
    {
        var gitExeService = app.Services.GetRequiredService<GitExecutableService>();
        var exe = await gitExeService.GetGitAsync();
        if (exe == null) { context.Response.StatusCode = 500; return; }

        var args = new List<string>
        {
            "-c", "color.ui=false",
            "diff", fromHash == toHash ? $"{fromHash}^..{fromHash}" : $"{fromHash}..{toHash}",
            "--", oldPath
        };
        if (oldPath != newPath) args.Add(newPath);

        var psi = new System.Diagnostics.ProcessStartInfo(exe.Path)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = System.Diagnostics.Process.Start(psi)!;
        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(output);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});

app.Run();

