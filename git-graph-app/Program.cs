using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using git_graph.Hubs;
using git_graph.Models;
using git_graph.Models.Serialization;
using git_graph.Services;
using Drk.AspNetCore.MinimalApiKit;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddSignalR()
    .AddJsonProtocol(opt =>
    {
        // Do NOT set WhenWritingNull — the frontend checks `msg.error === null` to detect success;
        // omitting null props causes `undefined !== null` to fall into the error branch.
        GitGraphJsonOptions.ConfigurePayloadOptions(opt.PayloadSerializerOptions);
    })
    .AddHubOptions<GitGraphHub>(opt =>
    {
        // Surface the real exception message to the client in development
        // so SignalR type-3 responses show the actual error instead of the generic message.
        opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
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

app.UseFileServer(new FileServerOptions
{
    RequestPath = "",
    FileProvider = new Microsoft.Extensions.FileProviders
                    .ManifestEmbeddedFileProvider(typeof(Program).Assembly, "media")
});

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
    watcher.Start(root);
}
else
{
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
    var hub = app.Services.GetRequiredService<IHubContext<GitGraphHub>>();
    _ = hub.Clients.All.SendAsync("ReceiveMessage", new RefreshResponse());
};

// ── Page routes ───────────────────────────────────────────────────────────────

app.MapGet("/diff", async context =>
{
    var query = context.Request.QueryString.Value ?? "";
    context.Response.Redirect($"/diff.html{query}");
});

// ── API routes ────────────────────────────────────────────────────────────────

app.MapGet("/api/initialState", () =>
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

    var initialState = new GitGraphViewInitialState
    {
        Config = new GitGraphViewConfig
        {
            Graph = new GraphConfig { Colours = graphColours }
        },
        LastActiveRepo = lastActiveRepo,
        LoadViewTo = null,
        Repos = repos,
        LoadRepoInfoRefreshId = 0,
        LoadCommitsRefreshId = 0
    };

    var response = new InitialStateResponse
    {
        InitialState = initialState,
        GlobalState = stateManager.GetGlobalViewState(),
        WorkspaceState = stateManager.GetWorkspaceViewState(),
        ColorVars = colorVars,
        ColorParams = colorParams
    };

    // Keep null values (e.g. loadViewTo, issueLinkingConfig, lastActiveRepo) so the
    // frontend can distinguish null from undefined — the JS code checks !== null.
    return Results.Json(response, GitGraphJsonOptions.PayloadContext.InitialStateResponse);
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
        string output = await gitService.GetDiffAsync(repo, fromHash, toHash, oldPath, newPath);
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(output);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});

if (System.Diagnostics.Debugger.IsAttached)
{
    app.Run();
}
else
{
    app.RunAsDesktopTool();
}

