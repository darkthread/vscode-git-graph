using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using git_graph.Models;

namespace git_graph.Services;

/// <summary>
/// Executes git commands and parses the output.
/// C# port of src/dataSource.ts.
/// </summary>
public class GitService
{
    private readonly GitExecutableService _gitExeService;
    private readonly ILogger<GitService> _logger;

    private const string GIT_LOG_SEPARATOR = "XX7Nal-YARtTpjCikii9nJxER19D6diSyk-AWkPb";
    private const string UNCOMMITTED = "*";
    private static readonly Regex EolRegex = new(@"\r\n|\r|\n", RegexOptions.Compiled);
    private static readonly Regex InvalidBranchRegex = new(@"^\(.* .*\)$", RegexOptions.Compiled);
    private static readonly Regex RemoteHeadBranchRegex = new(@"^remotes\/.*\/HEAD$", RegexOptions.Compiled);
    private static readonly Regex DriveLetterPathRegex = new(@"^[a-z]:/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public GitService(GitExecutableService gitExeService, ILogger<GitService> logger)
    {
        _gitExeService = gitExeService;
        _logger = logger;
    }

    // ── Public: Repo Info ────────────────────────────────────────────────────

    public async Task<GitRepoInfo> GetRepoInfoAsync(string repo, bool showRemoteBranches, bool showStashes, string[] hideRemotes)
    {
        try
        {
            var branches = await GetBranchesAsync(repo, showRemoteBranches, hideRemotes);
            var remotes = await GetRemotesAsync(repo);
            var stashes = showStashes ? await GetStashesAsync(repo) : [];

            return new GitRepoInfo(branches.Branches, branches.Head, remotes, stashes, null);
        }
        catch (Exception ex)
        {
            return new GitRepoInfo([], null, [], [], ex.Message);
        }
    }

    public async Task<GitCommitData> GetCommitsAsync(
        string repo, string[]? branches, int maxCommits,
        bool showTags, bool showRemoteBranches, bool includeCommitsMentionedByReflogs,
        bool onlyFollowFirstParent, CommitOrdering commitOrdering,
        string[] remotes, string[] hideRemotes, GitStash[] stashes)
    {
        try
        {
            var commits = await GetLogAsync(repo, branches, maxCommits + 1, showTags,
                showRemoteBranches, includeCommitsMentionedByReflogs, onlyFollowFirstParent,
                commitOrdering, remotes, hideRemotes, stashes);
            var refs = await GetRefsAsync(repo, showRemoteBranches, hideRemotes);

            bool moreAvailable = commits.Count == maxCommits + 1;
            if (moreAvailable) commits.RemoveAt(commits.Count - 1);

            // Add uncommitted changes node if HEAD exists
            if (refs.Head != null)
            {
                int headIdx = commits.FindIndex(c => c.Hash == refs.Head);
                if (headIdx >= 0)
                {
                    int numUncommitted = await GetUncommittedChangesCountAsync(repo);
                    if (numUncommitted > 0)
                    {
                        commits.Insert(0, new RawCommit(
                            UNCOMMITTED, [refs.Head], "*", "", DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            $"Uncommitted Changes ({numUncommitted})"
                        ));
                    }
                }
            }

            // Build commit nodes
            var commitNodes = new List<GitCommit>(commits.Count);
            var lookup = new Dictionary<string, int>(commits.Count);
            for (int i = 0; i < commits.Count; i++)
            {
                lookup[commits[i].Hash] = i;
                commitNodes.Add(new GitCommit(commits[i].Hash, commits[i].Parents, commits[i].Author,
                    commits[i].Email, commits[i].Date, commits[i].Message, [], [], [], null));
            }

            // Insert stashes
            var toAdd = new List<(int Index, GitStash Stash)>();
            foreach (var stash in stashes)
            {
                if (lookup.TryGetValue(stash.Hash, out int idx))
                {
                    var c = commitNodes[idx];
                    commitNodes[idx] = c with
                    {
                        Stash = new GitCommitStash(stash.Selector, stash.BaseHash, stash.UntrackedFilesHash)
                    };
                }
                else if (lookup.TryGetValue(stash.BaseHash, out int baseIdx))
                {
                    toAdd.Add((baseIdx, stash));
                }
            }
            toAdd.Sort((a, b) => a.Index != b.Index ? a.Index - b.Index : (int)(b.Stash.Date - a.Stash.Date));
            for (int i = toAdd.Count - 1; i >= 0; i--)
            {
                var (index, stash) = toAdd[i];
                commitNodes.Insert(index, new GitCommit(stash.Hash, [stash.BaseHash],
                    stash.Author, stash.Email, stash.Date, stash.Message,
                    [], [], [],
                    new GitCommitStash(stash.Selector, stash.BaseHash, stash.UntrackedFilesHash)));
            }
            // Rebuild lookup after inserts
            lookup.Clear();
            for (int i = 0; i < commitNodes.Count; i++) lookup[commitNodes[i].Hash] = i;

            // Annotate
            foreach (var head in refs.Heads)
            {
                if (lookup.TryGetValue(head.Hash, out int i))
                {
                    var c = commitNodes[i];
                    commitNodes[i] = c with { Heads = [.. c.Heads, head.Name] };
                }
            }
            if (showTags)
            {
                foreach (var tag in refs.Tags)
                {
                    if (lookup.TryGetValue(tag.Hash, out int i))
                    {
                        var c = commitNodes[i];
                        commitNodes[i] = c with { Tags = [.. c.Tags, new GitCommitTag(tag.Name, tag.Annotated)] };
                    }
                }
            }
            foreach (var remote in refs.Remotes)
            {
                if (lookup.TryGetValue(remote.Hash, out int i))
                {
                    var remoteName = remotes.FirstOrDefault(r => remote.Name.StartsWith(r + "/"));
                    var c = commitNodes[i];
                    commitNodes[i] = c with { Remotes = [.. c.Remotes, new GitCommitRemote(remote.Name, remoteName)] };
                }
            }

            string[] uniqueTags = refs.Tags.Select(t => t.Name).Distinct().ToArray();
            return new GitCommitData([.. commitNodes], refs.Head, uniqueTags, moreAvailable, null);
        }
        catch (Exception ex)
        {
            return new GitCommitData([], null, [], false, ex.Message);
        }
    }

    public async Task<GitRepoConfigData> GetConfigAsync(string repo, string[] remotes)
    {
        try
        {
            var all = await GetConfigListAsync(repo);
            var local = await GetConfigListAsync(repo, GitConfigLocation.Local);
            var global = await GetConfigListAsync(repo, GitConfigLocation.Global);

            var branches = new Dictionary<string, GitRepoConfigBranch>();
            foreach (var (key, value) in local)
            {
                if (key.StartsWith("branch."))
                {
                    if (key.EndsWith(".remote"))
                    {
                        var bn = key[7..^7];
                        branches[bn] = branches.TryGetValue(bn, out var b)
                            ? b with { Remote = value }
                            : new GitRepoConfigBranch(null, value);
                    }
                    else if (key.EndsWith(".pushremote"))
                    {
                        var bn = key[7..^11];
                        branches[bn] = branches.TryGetValue(bn, out var b)
                            ? b with { PushRemote = value }
                            : new GitRepoConfigBranch(value, null);
                    }
                }
            }

            var config = new GitRepoConfig(
                branches,
                GetConfigValue(all, "diff.tool"),
                GetConfigValue(all, "diff.guitool"),
                GetConfigValue(all, "remote.pushdefault"),
                remotes.Select(r => new GitRepoSettingsRemote(
                    r,
                    GetConfigValue(local, $"remote.{r}.url"),
                    GetConfigValue(local, $"remote.{r}.pushurl")
                )).ToArray(),
                new GitRepoConfigUser(
                    new GitRepoConfigUserLocal(
                        GetConfigValue(local, "user.name"),
                        GetConfigValue(global, "user.name")),
                    new GitRepoConfigUserLocal(
                        GetConfigValue(local, "user.email"),
                        GetConfigValue(global, "user.email"))
                )
            );

            return new GitRepoConfigData(config, null);
        }
        catch (Exception ex)
        {
            return new GitRepoConfigData(null, ex.Message);
        }
    }

    public async Task<GitCommitDetailsData> GetCommitDetailsAsync(string repo, string commitHash, bool hasParents)
    {
        try
        {
            string fromCommit = commitHash + (hasParents ? "^" : "");
            var (details, nameStatus, numStat) = await (
                GetCommitDetailsBaseAsync(repo, commitHash),
                GetDiffNameStatusAsync(repo, fromCommit, commitHash),
                GetDiffNumStatAsync(repo, fromCommit, commitHash)
            ).WhenAllThree();

            return new GitCommitDetailsData(details with { FileChanges = GenerateFileChanges(nameStatus, numStat, null) }, null);
        }
        catch (Exception ex)
        {
            return new GitCommitDetailsData(null, ex.Message);
        }
    }

    public async Task<GitCommitDetailsData> GetStashDetailsAsync(string repo, string commitHash, GitCommitStash stash)
    {
        try
        {
            var tasks = new List<Task>();
            var detailsTask = GetCommitDetailsBaseAsync(repo, commitHash);
            var nsTask = GetDiffNameStatusAsync(repo, stash.BaseHash, commitHash);
            var numStatTask = GetDiffNumStatAsync(repo, stash.BaseHash, commitHash);
            Task<DiffNameStatusRecord[]>? untrackedNsTask = null;
            Task<DiffNumStatRecord[]>? untrackedNumStatTask = null;
            if (stash.UntrackedFilesHash != null)
            {
                untrackedNsTask = GetDiffNameStatusAsync(repo, stash.UntrackedFilesHash, stash.UntrackedFilesHash);
                untrackedNumStatTask = GetDiffNumStatAsync(repo, stash.UntrackedFilesHash, stash.UntrackedFilesHash);
            }

            await Task.WhenAll(detailsTask, nsTask, numStatTask);
            if (untrackedNsTask != null) await Task.WhenAll(untrackedNsTask, untrackedNumStatTask!);

            var baseDetails = await detailsTask;
            var fileChanges = GenerateFileChanges(await nsTask, await numStatTask, null).ToList();

            if (stash.UntrackedFilesHash != null)
            {
                var untrackedChanges = GenerateFileChanges(await untrackedNsTask!, await untrackedNumStatTask!, null);
                foreach (var fc in untrackedChanges)
                {
                    if (fc.Type == GitFileStatus.Added)
                        fileChanges.Add(fc with { Type = GitFileStatus.Untracked });
                }
            }

            return new GitCommitDetailsData(baseDetails with { FileChanges = [.. fileChanges] }, null);
        }
        catch (Exception ex)
        {
            return new GitCommitDetailsData(null, ex.Message);
        }
    }

    public async Task<GitCommitDetailsData> GetUncommittedDetailsAsync(string repo)
    {
        try
        {
            var nsTask = GetDiffNameStatusAsync(repo, "HEAD", "");
            var numStatTask = GetDiffNumStatAsync(repo, "HEAD", "");
            var statusTask = GetStatusFilesAsync(repo);
            await Task.WhenAll(nsTask, numStatTask, statusTask);

            var details = new GitCommitDetails(
                UNCOMMITTED, [], "", "", 0, "", "", 0, null, "",
                GenerateFileChanges(await nsTask, await numStatTask, await statusTask));

            return new GitCommitDetailsData(details, null);
        }
        catch (Exception ex)
        {
            return new GitCommitDetailsData(null, ex.Message);
        }
    }

    public async Task<GitCommitComparisonData> GetCommitComparisonAsync(string repo, string fromHash, string toHash)
    {
        try
        {
            string toArg = toHash == UNCOMMITTED ? "" : toHash;
            var nsTask = GetDiffNameStatusAsync(repo, fromHash, toArg);
            var numStatTask = GetDiffNumStatAsync(repo, fromHash, toArg);
            GitStatusFiles? status = toHash == UNCOMMITTED ? await GetStatusFilesAsync(repo) : null;
            await Task.WhenAll(nsTask, numStatTask);

            return new GitCommitComparisonData(
                GenerateFileChanges(await nsTask, await numStatTask, status), null);
        }
        catch (Exception ex)
        {
            return new GitCommitComparisonData([], ex.Message);
        }
    }

    public async Task<string> GetCommitFileAsync(string repo, string commitHash, string filePath)
    {
        var result = await SpawnGitAsync(["show", commitHash + ":" + filePath], repo);
        return result.Stdout;
    }

    public async Task<string> GetDiffAsync(string repo, string fromHash, string toHash, string oldPath, string newPath)
    {
        var args = new List<string>
        {
            "-c", "color.ui=false",
            "diff", fromHash == toHash ? $"{fromHash}^..{fromHash}" : $"{fromHash}..{toHash}",
            "--", oldPath
        };
        if (oldPath != newPath) args.Add(newPath);
        var result = await SpawnGitAsync([.. args], repo);
        return result.Stdout;
    }

    public async Task<string?> GetCommitSubjectAsync(string repo, string commitHash)
    {
        try
        {
            var result = await SpawnGitAsync(["-c", "log.showSignature=false", "log",
                "--format=%s", "-n", "1", commitHash, "--"], repo);
            return Regex.Replace(result.Stdout.Trim(), @"\s+", " ");
        }
        catch { return null; }
    }

    public async Task<string?> GetRemoteUrlAsync(string repo, string remote)
    {
        try
        {
            var result = await SpawnGitAsync(["config", "--get", $"remote.{remote}.url"], repo);
            return EolRegex.Split(result.Stdout)[0];
        }
        catch { return null; }
    }

    public async Task<GitTagDetailsData> GetTagDetailsAsync(string repo, string tagName)
    {
        var exe = await _gitExeService.GetGitAsync();
        if (exe != null && !_gitExeService.DoesVersionMeetRequirement(exe.Version, GitExecutableService.VersionTagDetails))
        {
            return new GitTagDetailsData(null, _gitExeService.ConstructIncompatibleVersionMessage(exe, GitExecutableService.VersionTagDetails, "retrieving Tag Details"));
        }

        try
        {
            string @ref = "refs/tags/" + tagName;
            string format = string.Join(GIT_LOG_SEPARATOR, [
                "%(objectname)", "%(taggername)", "%(taggeremail)", "%(taggerdate:unix)", "%(contents:signature)", "%(contents)"
            ]);
            var result = await SpawnGitAsync(["for-each-ref", @ref, $"--format={format}"], repo);
            var data = result.Stdout.Split(GIT_LOG_SEPARATOR);

            string taggerEmail = data[2];
            if (taggerEmail.StartsWith('<')) taggerEmail = taggerEmail[1..];
            if (taggerEmail.EndsWith('>')) taggerEmail = taggerEmail[..^1];

            string rawContents = string.Join(GIT_LOG_SEPARATOR, data[5..]);
            string message = string.Join("\n", RemoveTrailingBlankLines(EolRegex.Split(rawContents.Replace(data[4], ""))));

            bool signed = data[4] != "";
            GitSignature? signature = null;
            if (signed) signature = await GetTagSignatureAsync(repo, @ref);

            return new GitTagDetailsData(
                new GitTagDetails(data[0], data[1], taggerEmail, long.Parse(data[3]), message, signature),
                null);
        }
        catch (Exception ex)
        {
            return new GitTagDetailsData(null, ex.Message);
        }
    }

    public async Task<string[]> GetSubmodulesAsync(string repo)
    {
        var modulesPath = Path.Combine(repo, ".gitmodules");
        if (!File.Exists(modulesPath)) return [];

        try
        {
            var content = await File.ReadAllTextAsync(modulesPath);
            var lines = EolRegex.Split(content);
            var submodules = new List<string>();
            bool inSubmodule = false;
            var sectionRegex = new Regex(@"^\s*\[.*\]\s*$");
            var submoduleRegex = new Regex(@"^\s*\[submodule ""([^""]+)""\]\s*$");
            var pathRegex = new Regex(@"^\s*path\s+=\s+(.*)$");

            foreach (var line in lines)
            {
                if (sectionRegex.IsMatch(line))
                {
                    inSubmodule = submoduleRegex.IsMatch(line);
                    continue;
                }
                if (inSubmodule)
                {
                    var m = pathRegex.Match(line);
                    if (m.Success)
                    {
                        string subPath = Path.Combine(repo, m.Groups[1].Value.Trim());
                        string? root = await RepoRootAsync(subPath);
                        if (root != null && !submodules.Contains(root))
                            submodules.Add(root);
                    }
                }
            }
            return [.. submodules];
        }
        catch { return []; }
    }

    public async Task<string?> RepoRootAsync(string pathOfPotentialRepo)
    {
        try
        {
            var result = await SpawnGitAsync(["rev-parse", "--show-toplevel"], pathOfPotentialRepo);
            return NormalizePath(result.Stdout.Trim());
        }
        catch { return null; }
    }

    public async Task<string?> GetNewPathOfRenamedFileAsync(string repo, string commitHash, string oldFilePath)
    {
        try
        {
            var renamed = await GetDiffNameStatusAsync(repo, commitHash, "", "R");
            return renamed.FirstOrDefault(r => r.OldFilePath == oldFilePath)?.NewFilePath;
        }
        catch { return null; }
    }

    // ── Public: Git Write Operations – Remotes ───────────────────────────────

    public async Task<string?> AddRemoteAsync(string repo, string name, string url, string? pushUrl, bool fetch)
    {
        var status = await RunGitCommandAsync(["remote", "add", name, url], repo);
        if (status != null) return status;

        if (pushUrl != null)
        {
            status = await RunGitCommandAsync(["remote", "set-url", name, "--push", pushUrl], repo);
            if (status != null) return status;
        }

        return fetch ? await FetchAsync(repo, name, false, false) : null;
    }

    public Task<string?> DeleteRemoteAsync(string repo, string name) =>
        RunGitCommandAsync(["remote", "remove", name], repo);

    public async Task<string?> EditRemoteAsync(string repo, string nameOld, string nameNew,
        string? urlOld, string? urlNew, string? pushUrlOld, string? pushUrlNew)
    {
        if (nameOld != nameNew)
        {
            var s = await RunGitCommandAsync(["remote", "rename", nameOld, nameNew], repo);
            if (s != null) return s;
        }

        if (urlOld != urlNew)
        {
            var args = new List<string> { "remote", "set-url", nameNew };
            if (urlNew == null) args.AddRange(["--delete", urlOld!]);
            else if (urlOld == null) args.AddRange(["--add", urlNew]);
            else args.AddRange([urlNew, urlOld]);
            var s = await RunGitCommandAsync([.. args], repo);
            if (s != null) return s;
        }

        if (pushUrlOld != pushUrlNew)
        {
            var args = new List<string> { "remote", "set-url", "--push", nameNew };
            if (pushUrlNew == null) args.AddRange(["--delete", pushUrlOld!]);
            else if (pushUrlOld == null) args.AddRange(["--add", pushUrlNew]);
            else args.AddRange([pushUrlNew, pushUrlOld]);
            var s = await RunGitCommandAsync([.. args], repo);
            if (s != null) return s;
        }

        return null;
    }

    public Task<string?> PruneRemoteAsync(string repo, string name) =>
        RunGitCommandAsync(["remote", "prune", name], repo);

    // ── Public: Git Write Operations – Tags ──────────────────────────────────

    public Task<string?> AddTagAsync(string repo, string tagName, string commitHash, TagType type, string message, bool force)
    {
        var args = new List<string> { "tag" };
        if (force) args.Add("-f");
        if (type == TagType.Lightweight)
            args.Add(tagName);
        else
            args.AddRange(["-a", tagName, "-m", message]);
        args.Add(commitHash);
        return RunGitCommandAsync([.. args], repo);
    }

    public async Task<string?> DeleteTagAsync(string repo, string tagName, string? deleteOnRemote)
    {
        if (deleteOnRemote != null)
        {
            var s = await RunGitCommandAsync(["push", deleteOnRemote, "--delete", tagName], repo);
            if (s != null) return s;
        }
        return await RunGitCommandAsync(["tag", "-d", tagName], repo);
    }

    // ── Public: Git Write Operations – Remote Sync ───────────────────────────

    public async Task<string?> FetchAsync(string repo, string? remote, bool prune, bool pruneTags)
    {
        var exe = await _gitExeService.GetGitAsync();
        var args = new List<string> { "fetch", remote ?? "--all" };
        if (prune) args.Add("--prune");
        if (pruneTags)
        {
            if (!prune) return $"In order to Prune Tags, pruning must also be enabled when fetching.";
            if (exe != null && !_gitExeService.DoesVersionMeetRequirement(exe.Version, GitExecutableService.VersionFetchAndPruneTags))
                return _gitExeService.ConstructIncompatibleVersionMessage(exe, GitExecutableService.VersionFetchAndPruneTags, "pruning tags when fetching");
            args.Add("--prune-tags");
        }
        return await RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> PushBranchAsync(string repo, string branchName, string remote, bool setUpstream, GitPushBranchMode mode)
    {
        var args = new List<string> { "push", remote, branchName };
        if (setUpstream) args.Add("--set-upstream");
        if (mode == GitPushBranchMode.Force) args.Add("--force");
        else if (mode == GitPushBranchMode.ForceWithLease) args.Add("--force-with-lease");
        return RunGitCommandAsync([.. args], repo);
    }

    public async Task<string?[]> PushBranchToMultipleRemotesAsync(string repo, string branchName, string[] remotes, bool setUpstream, GitPushBranchMode mode)
    {
        if (remotes.Length == 0)
            return [$"No remote(s) were specified to push the branch {branchName} to."];

        var results = new List<string?>();
        foreach (var remote in remotes)
        {
            var result = await PushBranchAsync(repo, branchName, remote, setUpstream, mode);
            results.Add(result);
            if (result != null) break;
        }
        return [.. results];
    }

    public async Task<string?[]> PushTagAsync(string repo, string tagName, string[] remotes, string commitHash, bool skipRemoteCheck)
    {
        if (remotes.Length == 0)
            return [$"No remote(s) were specified to push the tag {tagName} to."];

        if (!skipRemoteCheck)
        {
            var remotesWithCommit = await GetRemotesContainingCommitAsync(repo, commitHash, remotes);
            var missing = remotes.Where(r => !remotesWithCommit.Contains(r)).ToArray();
            if (missing.Length > 0)
                return [$"VSCODE_GIT_GRAPH:PUSH_TAG:COMMIT_NOT_ON_REMOTE:{System.Text.Json.JsonSerializer.Serialize(missing)}"];
        }

        var results = new List<string?>();
        foreach (var remote in remotes)
        {
            var result = await RunGitCommandAsync(["push", remote, tagName], repo);
            results.Add(result);
            if (result != null) break;
        }
        return [.. results];
    }

    // ── Public: Git Write Operations – Branches ──────────────────────────────

    public Task<string?> CheckoutBranchAsync(string repo, string branchName, string? remoteBranch)
    {
        var args = new List<string> { "checkout" };
        if (remoteBranch == null) args.Add(branchName);
        else args.AddRange(["-b", branchName, remoteBranch]);
        return RunGitCommandAsync([.. args], repo);
    }

    public async Task<string?[]> CreateBranchAsync(string repo, string branchName, string commitHash, bool checkout, bool force)
    {
        var args = new List<string>();
        if (checkout && !force)
            args.AddRange(["checkout", "-b"]);
        else
        {
            args.Add("branch");
            if (force) args.Add("-f");
        }
        args.AddRange([branchName, commitHash]);

        var statuses = new List<string?> { await RunGitCommandAsync([.. args], repo) };
        if (statuses[0] == null && checkout && force)
            statuses.Add(await CheckoutBranchAsync(repo, branchName, null));
        return [.. statuses];
    }

    public Task<string?> DeleteBranchAsync(string repo, string branchName, bool force) =>
        RunGitCommandAsync(["branch", force ? "-D" : "-d", branchName], repo);

    public async Task<string?> DeleteRemoteBranchAsync(string repo, string branchName, string remote)
    {
        var status = await RunGitCommandAsync(["push", remote, "--delete", branchName], repo);
        if (status != null && Regex.IsMatch(status, "remote ref does not exist", RegexOptions.IgnoreCase))
        {
            var trackStatus = await RunGitCommandAsync(["branch", "-d", "-r", $"{remote}/{branchName}"], repo);
            return trackStatus == null ? null
                : $"Branch does not exist on the remote, deleting the remote tracking branch {remote}/{branchName}.\n{trackStatus}";
        }
        return status;
    }

    public Task<string?> FetchIntoLocalBranchAsync(string repo, string remote, string remoteBranch, string localBranch, bool force)
    {
        var args = new List<string> { "fetch" };
        if (force) args.Add("-f");
        args.AddRange([remote, $"{remoteBranch}:{localBranch}"]);
        return RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> PullBranchAsync(string repo, string branchName, string remote, bool createNewCommit, bool squash)
    {
        var args = new List<string> { "pull", remote, branchName };
        if (squash) args.Add("--squash");
        else if (createNewCommit) args.Add("--no-ff");
        return RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> RenameBranchAsync(string repo, string oldName, string newName) =>
        RunGitCommandAsync(["branch", "-m", oldName, newName], repo);

    // ── Public: Git Write Operations – Merge & Rebase ────────────────────────

    public async Task<string?> MergeAsync(string repo, string obj, MergeActionOn actionOn, bool createNewCommit, bool squash, bool noCommit)
    {
        var args = new List<string> { "merge", obj };
        if (squash) args.Add("--squash");
        else if (createNewCommit) args.Add("--no-ff");
        if (noCommit) args.Add("--no-commit");

        var status = await RunGitCommandAsync([.. args], repo);
        if (status == null && squash && !noCommit)
            status = await CommitSquashIfStagedAsync(repo, obj, actionOn);
        return status;
    }

    public Task<string?> RebaseAsync(string repo, string obj, RebaseActionOn actionOn, bool ignoreDate, bool interactive)
    {
        if (interactive)
        {
            // In standalone mode, open in system terminal is not supported
            var label = actionOn == RebaseActionOn.Branch ? obj : obj[..Math.Min(8, obj.Length)];
            _logger.LogWarning("Interactive rebase on '{Label}' is not supported in standalone mode.", label);
            return Task.FromResult<string?>("Interactive rebase requires a VS Code terminal and is not supported in standalone mode.");
        }

        var args = new List<string> { "rebase", obj };
        if (ignoreDate) args.Add("--ignore-date");
        return RunGitCommandAsync([.. args], repo);
    }

    // ── Public: Git Write Operations – Commits ───────────────────────────────

    public Task<string?> CheckoutCommitAsync(string repo, string commitHash) =>
        RunGitCommandAsync(["checkout", commitHash], repo);

    public Task<string?> CherrypickCommitAsync(string repo, string commitHash, int parentIndex, bool recordOrigin, bool noCommit)
    {
        var args = new List<string> { "cherry-pick" };
        if (noCommit) args.Add("--no-commit");
        if (recordOrigin) args.Add("-x");
        if (parentIndex > 0) args.AddRange(["-m", parentIndex.ToString()]);
        args.Add(commitHash);
        return RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> DropCommitAsync(string repo, string commitHash) =>
        RunGitCommandAsync(["rebase", "--onto", commitHash + "^", commitHash], repo);

    public Task<string?> ResetToCommitAsync(string repo, string commit, GitResetMode resetMode) =>
        RunGitCommandAsync(["reset", $"--{resetMode.ToString().ToLower()}", commit], repo);

    public Task<string?> RevertCommitAsync(string repo, string commitHash, int parentIndex)
    {
        var args = new List<string> { "revert", "--no-edit" };
        if (parentIndex > 0) args.AddRange(["-m", parentIndex.ToString()]);
        args.Add(commitHash);
        return RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> CleanUntrackedFilesAsync(string repo, bool directories) =>
        RunGitCommandAsync(["clean", directories ? "-fd" : "-f"], repo);

    public Task<string?> ResetFileToRevisionAsync(string repo, string commitHash, string filePath) =>
        RunGitCommandAsync(["checkout", commitHash, "--", filePath], repo);

    // ── Public: Git Write Operations – Stash ─────────────────────────────────

    public Task<string?> ApplyStashAsync(string repo, string selector, bool reinstateIndex)
    {
        var args = new List<string> { "stash", "apply" };
        if (reinstateIndex) args.Add("--index");
        args.Add(selector);
        return RunGitCommandAsync([.. args], repo);
    }

    public Task<string?> BranchFromStashAsync(string repo, string selector, string branchName) =>
        RunGitCommandAsync(["stash", "branch", branchName, selector], repo);

    public Task<string?> DropStashAsync(string repo, string selector) =>
        RunGitCommandAsync(["stash", "drop", selector], repo);

    public Task<string?> PopStashAsync(string repo, string selector, bool reinstateIndex)
    {
        var args = new List<string> { "stash", "pop" };
        if (reinstateIndex) args.Add("--index");
        args.Add(selector);
        return RunGitCommandAsync([.. args], repo);
    }

    public async Task<string?> PushStashAsync(string repo, string message, bool includeUntracked)
    {
        var exe = await _gitExeService.GetGitAsync();
        if (exe == null) return "Unable to find a git executable.";
        if (!_gitExeService.DoesVersionMeetRequirement(exe.Version, GitExecutableService.VersionPushStash))
            return _gitExeService.ConstructIncompatibleVersionMessage(exe, GitExecutableService.VersionPushStash);

        var args = new List<string> { "stash", "push" };
        if (includeUntracked) args.Add("--include-untracked");
        if (!string.IsNullOrEmpty(message)) args.AddRange(["--message", message]);
        return await RunGitCommandAsync([.. args], repo);
    }

    // ── Public: Config ───────────────────────────────────────────────────────

    public Task<string?> SetConfigValueAsync(string repo, string key, string value, GitConfigLocation location) =>
        RunGitCommandAsync(["config", $"--{location.ToString().ToLower()}", key, value], repo);

    public Task<string?> UnsetConfigValueAsync(string repo, string key, GitConfigLocation location) =>
        RunGitCommandAsync(["config", $"--{location.ToString().ToLower()}", "--unset-all", key], repo);

    // ── Public: External Diff ─────────────────────────────────────────────────

    public async Task<string?> OpenExternalDirDiffAsync(string repo, string fromHash, string toHash, bool isGui)
    {
        var exe = await _gitExeService.GetGitAsync();
        if (exe == null) return "Unable to find a git executable.";

        var args = new List<string> { "difftool", "--dir-diff" };
        if (isGui) args.Add("-g");

        if (fromHash == toHash)
        {
            if (toHash == UNCOMMITTED) args.Add("HEAD");
            else args.Add($"{toHash}^..{toHash}");
        }
        else
        {
            if (toHash == UNCOMMITTED) args.Add(fromHash);
            else args.Add($"{fromHash}..{toHash}");
        }

        // Fire and forget (like the TS implementation)
        _ = RunGitCommandAsync([.. args], repo);
        await Task.Delay(1500);
        return null;
    }

    // ── Repo discovery helper ─────────────────────────────────────────────────

    public async Task<bool> IsGitRepoAsync(string dirPath)
    {
        try
        {
            await SpawnGitAsync(["-C", dirPath, "rev-parse", "--is-inside-work-tree"], dirPath);
            return true;
        }
        catch { return false; }
    }

    // ── Private: Core Git Infrastructure ─────────────────────────────────────

    /// <summary>Public wrapper allowing the hub to run arbitrary git commands.</summary>
    public Task<string?> RunGitCommandPublicAsync(string[] args, string repo) =>
        RunGitCommandAsync(args, repo);

    private async Task<string?> RunGitCommandAsync(string[] args, string repo)
    {
        try
        {
            await SpawnGitAsync(args, repo);
            return null;
        }
        catch (GitException ex)
        {
            return ex.Message;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<SpawnResult> SpawnGitAsync(string[] args, string repo, bool ignoreExitCode = false)
    {
        var exe = await _gitExeService.GetGitAsync()
            ?? throw new InvalidOperationException("Unable to find a git executable.");

        var psi = new ProcessStartInfo(exe.Path)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        _logger.LogDebug("git {Args}", string.Join(" ", args));

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (proc.ExitCode != 0 && !ignoreExitCode)
            throw new GitException(BuildErrorMessage(stdout, stderr), proc.ExitCode);

        return new SpawnResult(stdout, stderr, proc.ExitCode);
    }

    // ── Private: Data Parsers ─────────────────────────────────────────────────

    private async Task<List<RawCommit>> GetLogAsync(
        string repo, string[]? branches, int maxCommits,
        bool showTags, bool showRemoteBranches, bool includeReflogs,
        bool firstParentOnly, CommitOrdering ordering,
        string[] remotes, string[] hideRemotes, GitStash[] stashes)
    {
        string format = string.Join(GIT_LOG_SEPARATOR, ["%H", "%P", "%aN", "%aE", "%at", "%s"]);
        var args = new List<string>
        {
            "-c", "log.showSignature=false",
            "log", $"--format={format}", $"--max-count={maxCommits}"
        };

        args.Add(ordering switch
        {
            CommitOrdering.AuthorDate => "--author-date-order",
            CommitOrdering.Topological => "--topo-order",
            _ => "--date-order"
        });

        if (firstParentOnly) args.Add("--first-parent");

        if (branches != null && branches.Length > 0)
        {
            foreach (var b in branches) args.Add(b);
        }
        else
        {
            args.Add("--branches");
            if (showRemoteBranches) args.Add("--remotes");
            if (showTags) args.Add("--tags");
            if (includeReflogs) args.Add("--reflog");
        }

        var result = await SpawnGitAsync([.. args], repo);
        var commits = new List<RawCommit>();

        foreach (var line in EolRegex.Split(result.Stdout))
        {
            if (string.IsNullOrEmpty(line)) continue;
            var parts = line.Split(GIT_LOG_SEPARATOR);
            if (parts.Length < 6) continue;
            string[] parents = parts[1] != "" ? parts[1].Split(' ') : [];
            if (!long.TryParse(parts[4], out long date)) date = 0;
            commits.Add(new RawCommit(parts[0], parents, parts[2], parts[3], date,
                string.Join(GIT_LOG_SEPARATOR, parts[5..])));
        }

        return commits;
    }

    private async Task<(string[] Branches, string? Head)> GetBranchesAsync(string repo, bool showRemoteBranches, string[] hideRemotes)
    {
        var args = new List<string> { "branch" };
        if (showRemoteBranches) args.Add("-a");
        args.Add("--no-color");

        var hidePatterns = hideRemotes.Select(r => "remotes/" + r + "/").ToArray();
        var result = await SpawnGitAsync([.. args], repo);

        var branches = new List<string>();
        string? head = null;

        foreach (var rawLine in EolRegex.Split(result.Stdout))
        {
            if (rawLine.Length < 2) continue;
            string name = rawLine[2..].Split(" -> ")[0];
            if (InvalidBranchRegex.IsMatch(name)) continue;
            if (hidePatterns.Any(p => name.StartsWith(p))) continue;
            if (RemoteHeadBranchRegex.IsMatch(name)) continue;

            if (rawLine[0] == '*')
            {
                head = name;
                branches.Insert(0, name);
            }
            else
            {
                branches.Add(name);
            }
        }

        return ([.. branches], head);
    }

    private async Task<string[]> GetRemotesAsync(string repo)
    {
        var result = await SpawnGitAsync(["remote"], repo);
        var lines = EolRegex.Split(result.Stdout).ToList();
        if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        return [.. lines];
    }

    private async Task<GitStash[]> GetStashesAsync(string repo)
    {
        try
        {
            string format = string.Join(GIT_LOG_SEPARATOR, ["%H", "%P", "%gD", "%aN", "%aE", "%at", "%s"]);
            var result = await SpawnGitAsync(["reflog", $"--format={format}", "refs/stash", "--"], repo);
            var stashes = new List<GitStash>();

            foreach (var line in EolRegex.Split(result.Stdout))
            {
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split(GIT_LOG_SEPARATOR);
                if (parts.Length != 7 || parts[1] == "") continue;
                var parents = parts[1].Split(' ');
                stashes.Add(new GitStash(
                    parts[0], parents[0],
                    parents.Length == 3 ? parents[2] : null,
                    parts[2], parts[3], parts[4],
                    long.Parse(parts[5]), parts[6]));
            }
            return [.. stashes];
        }
        catch { return []; }
    }

    private record RefData(
        string? Head,
        List<RefEntry> Heads,
        List<TagRefEntry> Tags,
        List<RefEntry> Remotes
    );
    private record RefEntry(string Hash, string Name);
    private record TagRefEntry(string Hash, string Name, bool Annotated);

    private async Task<RefData> GetRefsAsync(string repo, bool showRemoteBranches, string[] hideRemotes)
    {
        var args = new List<string> { "show-ref" };
        if (!showRemoteBranches) args.AddRange(["--heads", "--tags"]);
        args.AddRange(["-d", "--head"]);

        var hidePatterns = hideRemotes.Select(r => "refs/remotes/" + r + "/").ToArray();
        var result = await SpawnGitAsync([.. args], repo);

        var heads = new List<RefEntry>();
        var tags = new List<TagRefEntry>();
        var remotes = new List<RefEntry>();
        string? head = null;

        foreach (var line in EolRegex.Split(result.Stdout))
        {
            if (string.IsNullOrEmpty(line)) continue;
            int spaceIdx = line.IndexOf(' ');
            if (spaceIdx < 0) continue;
            string hash = line[..spaceIdx];
            string refStr = line[(spaceIdx + 1)..];

            if (refStr.StartsWith("refs/heads/"))
                heads.Add(new RefEntry(hash, refStr[11..]));
            else if (refStr.StartsWith("refs/tags/"))
            {
                bool annotated = refStr.EndsWith("^{}");
                string tagName = annotated ? refStr[10..^3] : refStr[10..];
                tags.Add(new TagRefEntry(hash, tagName, annotated));
            }
            else if (refStr.StartsWith("refs/remotes/"))
            {
                if (!hidePatterns.Any(p => refStr.StartsWith(p)) && !refStr.EndsWith("/HEAD"))
                    remotes.Add(new RefEntry(hash, refStr[13..]));
            }
            else if (refStr == "HEAD")
                head = hash;
        }

        return new RefData(head, heads, tags, remotes);
    }

    private async Task<GitCommitDetails> GetCommitDetailsBaseAsync(string repo, string commitHash)
    {
        string format = string.Join(GIT_LOG_SEPARATOR, [
            "%H", "%P",
            "%aN", "%aE", "%at", "%cN", "%cE", "%ct",
            "%G?", "%GS", "%GK",
            "%B"
        ]);
        var result = await SpawnGitAsync(["-c", "log.showSignature=false", "show", "--quiet", commitHash, $"--format={format}"], repo);
        var parts = result.Stdout.Split(GIT_LOG_SEPARATOR);

        GitSignature? sig = null;
        if (new[] { "G", "U", "X", "Y", "R", "E", "B" }.Contains(parts[8]))
        {
            sig = new GitSignature(
                parts[10].Trim(),
                parts[9].Trim(),
                ParseGpgStatus(parts[8])
            );
        }

        string body = string.Join("\n", RemoveTrailingBlankLines(
            EolRegex.Split(string.Join(GIT_LOG_SEPARATOR, parts[11..]))));

        return new GitCommitDetails(
            parts[0],
            parts[1] != "" ? parts[1].Split(' ') : [],
            parts[2], parts[3], long.Parse(parts[4]),
            parts[5], parts[6], long.Parse(parts[7]),
            sig, body, []);
    }

    private async Task<Dictionary<string, string>> GetConfigListAsync(string repo, GitConfigLocation? location = null)
    {
        var args = new List<string> { "--no-pager", "config", "--list", "-z", "--includes" };
        if (location.HasValue) args.Add($"--{location.Value.ToString().ToLower()}");

        try
        {
            var result = await SpawnGitAsync([.. args], repo);
            var configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pairs = result.Stdout.Split('\0');
            for (int i = 0; i < pairs.Length - 1; i++)
            {
                var eol = EolRegex.Split(pairs[i]);
                if (eol.Length > 0)
                    configs[eol[0]] = string.Join("\n", eol[1..]);
            }
            return configs;
        }
        catch (GitException ex) when (ex.Message.Contains("unable to read config file", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
    }

    private async Task<DiffNameStatusRecord[]> GetDiffNameStatusAsync(string repo, string fromHash, string toHash, string filter = "AMDR")
    {
        var output = await ExecDiffAsync(repo, fromHash, toHash, "--name-status", filter);
        var records = new List<DiffNameStatusRecord>();
        int i = 0;
        while (i < output.Length && output[i] != "")
        {
            char type = output[i][0];
            if (type == 'A' || type == 'D' || type == 'M')
            {
                string p = output[i + 1];
                records.Add(new DiffNameStatusRecord(MapFileStatus(type), p, p));
                i += 2;
            }
            else if (type == 'R')
            {
                records.Add(new DiffNameStatusRecord(GitFileStatus.Renamed, output[i + 1], output[i + 2]));
                i += 3;
            }
            else break;
        }
        return [.. records];
    }

    private async Task<DiffNumStatRecord[]> GetDiffNumStatAsync(string repo, string fromHash, string toHash, string filter = "AMDR")
    {
        var output = await ExecDiffAsync(repo, fromHash, toHash, "--numstat", filter);
        var records = new List<DiffNumStatRecord>();
        int i = 0;
        while (i < output.Length && output[i] != "")
        {
            var fields = output[i].Split('\t');
            if (fields.Length != 3) break;
            if (fields[0] == "-") fields[0] = "0";
            if (fields[1] == "-") fields[1] = "0";
            if (fields[2] != "")
            {
                records.Add(new DiffNumStatRecord(fields[2], int.Parse(fields[0]), int.Parse(fields[1])));
                i += 1;
            }
            else
            {
                records.Add(new DiffNumStatRecord(output[i + 2], int.Parse(fields[0]), int.Parse(fields[1])));
                i += 3;
            }
        }
        return [.. records];
    }

    private async Task<string[]> ExecDiffAsync(string repo, string fromHash, string toHash, string arg, string filter)
    {
        string[] args;
        if (fromHash == toHash)
            args = ["diff-tree", arg, "-r", "--root", "--find-renames", $"--diff-filter={filter}", "-z", fromHash];
        else
        {
            var argsList = new List<string> { "diff", arg, "--find-renames", $"--diff-filter={filter}", "-z", fromHash };
            if (toHash != "") argsList.Add(toHash);
            args = [.. argsList];
        }

        var result = await SpawnGitAsync(args, repo);
        var lines = result.Stdout.Split('\0').ToList();
        if (fromHash == toHash && lines.Count > 0) lines.RemoveAt(0);
        return [.. lines];
    }

    private async Task<GitStatusFiles> GetStatusFilesAsync(string repo)
    {
        var result = await SpawnGitAsync(["status", "-s", "--untracked-files=all", "--porcelain", "-z"], repo);
        var parts = result.Stdout.Split('\0');
        var deleted = new List<string>();
        var untracked = new List<string>();
        int i = 0;
        while (i < parts.Length && parts[i] != "")
        {
            if (parts[i].Length < 4) break;
            string path = parts[i][3..];
            char c1 = parts[i][0], c2 = parts[i][1];
            if (c1 == 'D' || c2 == 'D') deleted.Add(path);
            else if (c1 == '?' || c2 == '?') untracked.Add(path);
            if (c1 == 'R' || c2 == 'R' || c1 == 'C' || c2 == 'C') i += 2;
            else i += 1;
        }
        return new GitStatusFiles([.. deleted], [.. untracked]);
    }

    private async Task<int> GetUncommittedChangesCountAsync(string repo)
    {
        try
        {
            var result = await SpawnGitAsync(["status", "--untracked-files=all", "--porcelain"], repo);
            var lines = EolRegex.Split(result.Stdout);
            return lines.Length > 1 ? lines.Count(l => !string.IsNullOrEmpty(l)) : 0;
        }
        catch { return 0; }
    }

    private async Task<bool> AreStagedChangesAsync(string repo)
    {
        try
        {
            var result = await SpawnGitAsync(["diff-index", "HEAD"], repo);
            return !string.IsNullOrEmpty(result.Stdout);
        }
        catch { return false; }
    }

    private async Task<string?> CommitSquashIfStagedAsync(string repo, string obj, MergeActionOn actionOn)
    {
        if (await AreStagedChangesAsync(repo))
        {
            string commitMsg = $"Merge {actionOn.ToString().ToLower()} '{obj}'";
            return await RunGitCommandAsync(["commit", "-m", commitMsg], repo);
        }
        return null;
    }

    private async Task<GitSignature> GetTagSignatureAsync(string repo, string @ref)
    {
        try
        {
            var result = await SpawnGitAsync(["verify-tag", "--raw", @ref], repo, ignoreExitCode: true);
            string output = result.Stderr + result.Stdout;

            var records = EolRegex.Split(output)
                .Where(l => l.StartsWith("[GNUPG:] "))
                .Select(l => l.Split(' '))
                .ToArray();

            GitSignature? sig = null;
            string? trustLevel = null;

            foreach (var record in records)
            {
                if (record.Length < 2) continue;
                string code = record[1];

                var status = code switch
                {
                    "GOODSIG" => GitSignatureStatus.GoodAndValid,
                    "BADSIG" => GitSignatureStatus.Bad,
                    "ERRSIG" => GitSignatureStatus.CannotBeChecked,
                    "EXPSIG" => GitSignatureStatus.GoodButExpired,
                    "EXPKEYSIG" => GitSignatureStatus.GoodButMadeByExpiredKey,
                    "REVKEYSIG" => GitSignatureStatus.GoodButMadeByRevokedKey,
                    _ => (GitSignatureStatus?)null
                };

                if (status.HasValue)
                {
                    string key = record.Length > 2 ? record[2] : "";
                    bool hasUid = code != "ERRSIG";
                    string signer = hasUid && record.Length > 3 ? string.Join(" ", record[3..]) : "";
                    sig = new GitSignature(key, signer, status.Value);
                }
                else if (code.StartsWith("TRUST_"))
                {
                    trustLevel = code;
                }
            }

            if (sig != null)
            {
                if (sig.Status == GitSignatureStatus.GoodAndValid &&
                    (trustLevel == "TRUST_UNDEFINED" || trustLevel == "TRUST_NEVER"))
                    sig = sig with { Status = GitSignatureStatus.GoodWithUnknownValidity };
                return sig;
            }
        }
        catch { }

        return new GitSignature("", "", GitSignatureStatus.CannotBeChecked);
    }

    private async Task<string[]> GetRemotesContainingCommitAsync(string repo, string commitHash, string[] knownRemotes)
    {
        try
        {
            var result = await SpawnGitAsync(["branch", "-r", "--no-color", $"--contains={commitHash}"], repo);
            var branches = EolRegex.Split(result.Stdout)
                .Where(l => l.Length > 2)
                .Select(l => l[2..].Split(" -> ")[0])
                .Where(n => !InvalidBranchRegex.IsMatch(n))
                .ToArray();

            return knownRemotes
                .Where(r => branches.Any(b => b.StartsWith(r + "/")))
                .ToArray();
        }
        catch { return knownRemotes; }
    }

    // ── Private: Helpers ─────────────────────────────────────────────────────

    private static GitFileChange[] GenerateFileChanges(
        DiffNameStatusRecord[] nameStatus, DiffNumStatRecord[] numStat, GitStatusFiles? status)
    {
        var changes = new List<GitFileChange>();
        var lookup = new Dictionary<string, int>();

        for (int i = 0; i < nameStatus.Length; i++)
        {
            lookup[nameStatus[i].NewFilePath] = changes.Count;
            changes.Add(new GitFileChange(nameStatus[i].OldFilePath, nameStatus[i].NewFilePath,
                nameStatus[i].Type, null, null));
        }

        if (status != null)
        {
            foreach (var fp in status.Deleted)
            {
                if (lookup.TryGetValue(fp, out int idx))
                    changes[idx] = changes[idx] with { Type = GitFileStatus.Deleted };
                else
                    changes.Add(new GitFileChange(fp, fp, GitFileStatus.Deleted, null, null));
            }
            foreach (var fp in status.Untracked)
                changes.Add(new GitFileChange(fp, fp, GitFileStatus.Untracked, null, null));
        }

        foreach (var ns in numStat)
        {
            if (lookup.TryGetValue(ns.FilePath, out int idx))
                changes[idx] = changes[idx] with { Additions = ns.Additions, Deletions = ns.Deletions };
        }

        return [.. changes];
    }

    private static string? GetConfigValue(Dictionary<string, string> configs, string key) =>
        configs.TryGetValue(key, out var v) ? v : null;

    private static string[] RemoveTrailingBlankLines(string[] lines)
    {
        var list = lines.ToList();
        while (list.Count > 0 && list[^1] == "") list.RemoveAt(list.Count - 1);
        return [.. list];
    }

    private static string BuildErrorMessage(string stdout, string stderr)
    {
        string combined = stderr + stdout;
        if (!string.IsNullOrEmpty(combined))
        {
            var lines = EolRegex.Split(combined).ToList();
            if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
            return string.Join("\n", lines);
        }
        return "Git command failed.";
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static GitFileStatus MapFileStatus(char c) => c switch
    {
        'A' => GitFileStatus.Added,
        'M' => GitFileStatus.Modified,
        'D' => GitFileStatus.Deleted,
        'R' => GitFileStatus.Renamed,
        'U' => GitFileStatus.Untracked,
        _ => GitFileStatus.Modified
    };

    private static GitSignatureStatus ParseGpgStatus(string s) => s switch
    {
        "G" => GitSignatureStatus.GoodAndValid,
        "U" => GitSignatureStatus.GoodWithUnknownValidity,
        "X" => GitSignatureStatus.GoodButExpired,
        "Y" => GitSignatureStatus.GoodButMadeByExpiredKey,
        "R" => GitSignatureStatus.GoodButMadeByRevokedKey,
        "E" => GitSignatureStatus.CannotBeChecked,
        "B" => GitSignatureStatus.Bad,
        _ => GitSignatureStatus.CannotBeChecked
    };

    // ── Private: Internal types ───────────────────────────────────────────────

    private record SpawnResult(string Stdout, string Stderr, int ExitCode);
    private record RawCommit(string Hash, string[] Parents, string Author, string Email, long Date, string Message);
    private record DiffNameStatusRecord(GitFileStatus Type, string OldFilePath, string NewFilePath);
    private record DiffNumStatRecord(string FilePath, int Additions, int Deletions);
    private record GitStatusFiles(string[] Deleted, string[] Untracked);
}

// ── Tuple extension helper ────────────────────────────────────────────────────

file static class TaskExtensions
{
    public static async Task<(T1, T2, T3)> WhenAllThree<T1, T2, T3>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3);
        return (await tasks.t1, await tasks.t2, await tasks.t3);
    }
}

// ── GitException ─────────────────────────────────────────────────────────────

public class GitException : Exception
{
    public int ExitCode { get; }
    public GitException(string message, int exitCode) : base(message) => ExitCode = exitCode;
}
