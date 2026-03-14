using System.Text.RegularExpressions;

using PrettyConsole;

namespace Seek.Cli.Tests;

public sealed class CommandsSearchTests {
    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_DefaultType_WritesMatchingFilesAndDirectories(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var directoryPath = Path.Combine(sandbox.RootPath, "logs");
        var filePath = Path.Combine(directoryPath, "alpha.log");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "log",
            root: sandbox.RootPath,
            noHighlight: true,
            cancellationToken: cancellationToken);

        var lines = SplitNewlineRecords(stdout);

        await Assert.That(exitCode).IsEqualTo(0);
        await AssertSamePaths(lines, [directoryPath, filePath]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_NoHighlight_WritesPlainPathsWithoutEscapeSequences(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            noHighlight: true,
            cancellationToken: cancellationToken);

        var lines = stdout
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains('\u001b', StringComparison.Ordinal)).IsFalse();
        await Assert.That(lines.Length).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo(filePath);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_FilesFlag_ReturnsOnlyFiles(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var nestedDirectory = Path.Combine(sandbox.RootPath, "container");
        var filePath = Path.Combine(nestedDirectory, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            files: true,
            noHighlight: true,
            cancellationToken: cancellationToken);

        var lines = SplitNewlineRecords(stdout);

        await Assert.That(exitCode).IsEqualTo(0);
        await AssertSamePaths(lines, [filePath]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_DirectoriesFlag_ReturnsOnlyDirectories(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            directories: true,
            noHighlight: true,
            cancellationToken: cancellationToken);

        var lines = SplitNewlineRecords(stdout);

        await Assert.That(exitCode).IsEqualTo(0);
        await AssertSamePaths(lines, [matchingDirectory]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_DirectoriesFlag_EmitsMatchingRootPath(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create("alpha-root");
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "child"));

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: $"^{Regex.Escape(sandbox.RootPath)}$",
            root: sandbox.RootPath,
            regex: true,
            directories: true,
            noHighlight: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await AssertSamePaths(SplitNewlineRecords(stdout), [sandbox.RootPath]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_Null_WritesPlainPathsWithNullTerminators(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            @null: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains('\u001b', StringComparison.Ordinal)).IsFalse();
        await Assert.That(stdout).IsEqualTo($"{filePath}\0");
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_DirectoriesFlag_ComposesWithNoHighlight(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            directories: true,
            noHighlight: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains('\u001b', StringComparison.Ordinal)).IsFalse();
        await AssertSamePaths(SplitNewlineRecords(stdout), [matchingDirectory]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_FilesFlag_ComposesWithNull(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            files: true,
            @null: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains('\u001b', StringComparison.Ordinal)).IsFalse();
        await AssertSamePaths(SplitNullRecords(stdout), [filePath]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_FilesAndDirectoriesFlagsTogether_ReturnCombinedResults(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var directoryPath = Path.Combine(sandbox.RootPath, "logs");
        var filePath = Path.Combine(directoryPath, "alpha.log");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "log",
            root: sandbox.RootPath,
            files: true,
            directories: true,
            noHighlight: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await AssertSamePaths(SplitNewlineRecords(stdout), [directoryPath, filePath]);
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_Null_PreservesSpacesInPaths(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "alpha report.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha report",
            root: sandbox.RootPath,
            @null: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout).IsEqualTo($"{filePath}\0");
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_Null_IgnoresHighlightRendering(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            @null: true,
            highlightColor: ConsoleColor.Red,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains('\u001b', StringComparison.Ordinal)).IsFalse();
        await Assert.That(stdout).IsEqualTo($"{filePath}\0");
    }

    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task SearchAsync_Null_UsesNullTerminatorsForUnixNewlinePaths(CancellationToken cancellationToken) {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "line\nbreak-alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var (exitCode, stdout) = await InvokeSearchAsync(
            query: "alpha",
            root: sandbox.RootPath,
            @null: true,
            cancellationToken: cancellationToken);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout).IsEqualTo($"{filePath}\0");
    }

    private sealed class Sandbox : IDisposable {
        private Sandbox(string rootPath) {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static Sandbox Create(string? suffix = null) {
            var suffixSegment = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"-{suffix}";
            var rootPath = Path.Combine(Path.GetTempPath(), $"seek-cli-tests-{Guid.NewGuid():N}{suffixSegment}");
            Directory.CreateDirectory(rootPath);
            return new Sandbox(rootPath);
        }

        public void Dispose() {
            if (Directory.Exists(RootPath)) {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private static async Task<(int ExitCode, string Stdout)> InvokeSearchAsync(
        string query,
        string root,
        bool regex = false,
        bool noHighlight = false,
        bool @null = false,
        bool files = false,
        bool directories = false,
        ConsoleColor highlightColor = ConsoleColor.Green,
        CancellationToken cancellationToken = default) {
        var output = new StringWriter();
        var originalOut = ConsoleContext.Out;

        try {
            ConsoleContext.Out = output;

            var exitCode = await Commands.SearchAsync(
                query: query,
                regex: regex,
                caseSensitive: false,
                plain: noHighlight,
                @null: @null,
                hidden: false,
                system: false,
                files: files,
                directories: directories,
                root: root,
                highlightColor: highlightColor,
                cancellationToken: cancellationToken);

            return (exitCode, output.ToString());
        } finally {
            ConsoleContext.Out = originalOut;
        }
    }

    private static string[] SplitNewlineRecords(string stdout) {
        return stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] SplitNullRecords(string stdout) {
        return stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static async Task AssertSamePaths(IEnumerable<string> actual, IEnumerable<string> expected) {
        var actualPaths = actual.Order(StringComparer.Ordinal).ToArray();
        var expectedPaths = expected.Order(StringComparer.Ordinal).ToArray();

        await Assert.That(actualPaths.Length).IsEqualTo(expectedPaths.Length);
        for (var i = 0; i < actualPaths.Length; i++) {
            await Assert.That(actualPaths[i]).IsEqualTo(expectedPaths[i]);
        }
    }
}
