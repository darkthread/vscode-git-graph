using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace git_graph.Services;

public record GitExecutable(string Path, string Version);

/// <summary>
/// Discovers and validates the git executable on the host system.
/// Mirrors the logic in src/utils.ts (findGit, getGitExecutable).
/// </summary>
public class GitExecutableService
{
    private readonly ILogger<GitExecutableService> _logger;
    private GitExecutable? _cached;

    // Version requirements mirroring GitVersionRequirement in utils.ts
    public static readonly string VersionFetchAndPruneTags = "2.17.0";
    public static readonly string VersionGpgInfo = "2.4.0";
    public static readonly string VersionPushStash = "2.13.2";
    public static readonly string VersionTagDetails = "1.7.8";

    public GitExecutableService(ILogger<GitExecutableService> logger)
    {
        _logger = logger;
    }

    /// <summary>Returns the cached git executable, or discovers it on first call.</summary>
    public async Task<GitExecutable?> GetGitAsync()
    {
        if (_cached != null) return _cached;

        // 1. Explicit env override
        var envPath = Environment.GetEnvironmentVariable("GIT_GRAPH_GIT_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            _cached = await TryGetExecutableAsync(envPath);
            if (_cached != null) return _cached;
        }

        // 2. Platform-specific discovery
        _cached = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? await FindGitOnWindowsAsync()
            : await FindGitOnUnixAsync();

        if (_cached == null)
            _logger.LogWarning("Could not locate git executable – git operations will fail.");
        else
            _logger.LogInformation("Using git {Version} at {Path}", _cached.Version, _cached.Path);

        return _cached;
    }

    public bool DoesVersionMeetRequirement(string version, string required)
    {
        return CompareVersions(version, required) >= 0;
    }

    public string ConstructIncompatibleVersionMessage(GitExecutable exe, string required, string? feature = null)
    {
        var feat = feature != null ? $" for {feature}" : "";
        return $"A newer version of Git (>= {required}) is required{feat}. " +
               $"Current Git version is {exe.Version} ({exe.Path}).";
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task<GitExecutable?> FindGitOnWindowsAsync()
    {
        // Common Windows installation paths (mirroring findSystemGitWin32 in utils.ts)
        var candidates = new List<string?>();

        foreach (var envVar in new[] { "ProgramFiles", "ProgramFiles(x86)" })
        {
            var pf = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(pf))
            {
                candidates.Add(Path.Combine(pf, "Git", "cmd", "git.exe"));
                candidates.Add(Path.Combine(pf, "Git", "bin", "git.exe"));
                candidates.Add(Path.Combine(pf, "Git", "mingw64", "bin", "git.exe"));
                candidates.Add(Path.Combine(pf, "Git", "mingw32", "bin", "git.exe"));
            }
        }

        var localAppData = Environment.GetEnvironmentVariable("LocalAppData");
        if (!string.IsNullOrEmpty(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, "Programs", "Git", "cmd", "git.exe"));
            candidates.Add(Path.Combine(localAppData, "Programs", "Git", "bin", "git.exe"));
        }

        // Try each candidate
        foreach (var candidate in candidates)
        {
            if (candidate == null || !File.Exists(candidate)) continue;
            var exe = await TryGetExecutableAsync(candidate);
            if (exe != null) return exe;
        }

        // Fall back to PATH
        return await TryGetExecutableAsync("git");
    }

    private async Task<GitExecutable?> FindGitOnUnixAsync()
    {
        // Try `which git`
        try
        {
            string whichOutput = await RunCommandAsync("which", ["git"]);
            string gitPath = whichOutput.Trim();
            if (!string.IsNullOrEmpty(gitPath))
            {
                var exe = await TryGetExecutableAsync(gitPath);
                if (exe != null) return exe;
            }
        }
        catch { /* ignore */ }

        // On macOS: xcode-select --print-path
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                string xcodePath = (await RunCommandAsync("xcode-select", ["--print-path"])).Trim();
                if (!string.IsNullOrEmpty(xcodePath))
                {
                    var xcodeGit = Path.Combine(xcodePath, "usr", "bin", "git");
                    var exe = await TryGetExecutableAsync(xcodeGit);
                    if (exe != null) return exe;
                }
            }
            catch { /* ignore */ }
        }

        return await TryGetExecutableAsync("git");
    }

    /// <summary>Spawns `git --version` to validate the path and parse the version string.</summary>
    public async Task<GitExecutable?> TryGetExecutableAsync(string path)
    {
        try
        {
            var output = await RunCommandAsync(path, ["--version"]);
            // "git version 2.45.1.windows.1"  or  "git version 2.45.0"
            var match = Regex.Match(output, @"git version (\d+\.\d+\.\d+)");
            if (match.Success)
                return new GitExecutable(path, match.Groups[1].Value);
        }
        catch { /* path doesn't exist or isn't git */ }
        return null;
    }

    private static async Task<string> RunCommandAsync(string fileName, string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = ParseVersion(a);
        var pb = ParseVersion(b);
        for (int i = 0; i < 3; i++)
        {
            int diff = pa[i].CompareTo(pb[i]);
            if (diff != 0) return diff;
        }
        return 0;
    }

    private static int[] ParseVersion(string v)
    {
        var parts = v.Split('.');
        var result = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++)
            int.TryParse(parts[i], out result[i]);
        return result;
    }
}
