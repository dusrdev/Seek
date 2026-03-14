using System.Diagnostics;

namespace Seek.Core.Tests;

public sealed class HiddenDirectoryVisibilityTests {
    [Test]
    public async Task CommonDotPrefixedDirectories_AreUsuallyReportedAsHidden() {
        if (OperatingSystem.IsWindows()) {
            await Assert.That(GitDirectoryCreatedByGit_IsReportedAsHidden()).IsTrue();
            return;
        }

        using var sandbox = Sandbox.Create();

        var hiddenDirectoryNames = new[] {
            ".git",
            ".vscode",
            ".vs",
            ".vsc",
            ".idea"
        };

        var hiddenDirectories = hiddenDirectoryNames
            .Select((directoryName, index) => Path.Combine(sandbox.RootPath, $"repo-{index}", directoryName))
            .ToList();

        foreach (var hiddenDirectory in hiddenDirectories) {
            Directory.CreateDirectory(hiddenDirectory);
        }

        var hiddenResults = hiddenDirectories
            .Select(path => File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
            .ToList();

        var hiddenCount = hiddenResults.Count(isHidden => isHidden);
        var majorityAreHidden = hiddenCount > hiddenResults.Count / 2;

        await Assert.That(majorityAreHidden).IsTrue();
    }

    private static bool GitDirectoryCreatedByGit_IsReportedAsHidden() {
        using var sandbox = Sandbox.Create();
        var repositoryPath = Path.Combine(sandbox.RootPath, "repo");
        Directory.CreateDirectory(repositoryPath);

        var startInfo = new ProcessStartInfo("git", "init") {
            WorkingDirectory = repositoryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git process.");

        process.WaitForExit();
        if (process.ExitCode != 0) {
            var standardError = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git init failed with exit code {process.ExitCode}: {standardError}");
        }

        var gitDirectoryPath = Path.Combine(repositoryPath, ".git");
        return File.GetAttributes(gitDirectoryPath).HasFlag(FileAttributes.Hidden);
    }

    private sealed class Sandbox : IDisposable {
        private Sandbox(string rootPath) {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static Sandbox Create() {
            var rootPath = Path.Combine(Path.GetTempPath(), $"seek-visibility-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootPath);
            return new Sandbox(rootPath);
        }

        public void Dispose() {
            if (Directory.Exists(RootPath)) {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
