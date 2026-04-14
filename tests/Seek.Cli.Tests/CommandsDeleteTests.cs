using System.Runtime.Versioning;

using PrettyConsole;

namespace Seek.Cli.Tests;

public sealed class CommandsDeleteTests {
	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_PreviewMode_DoesNotDeleteMatchingFiles(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
		await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: "alpha",
			root: sandbox.RootPath,
			files: true,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);

		await Assert.That(exitCode).IsEqualTo(0);
		await Assert.That(File.Exists(filePath)).IsTrue();
		await Assert.That(lines.Length).IsEqualTo(2);
		await Assert.That(lines[0]).IsEqualTo(filePath);
		await Assert.That(lines[1]).IsEqualTo("No changes were made. Re-run with --apply to delete these entries.");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_PreviewMode_DoesNotDeleteMatchingDirectories(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var directoryPath = Path.Combine(sandbox.RootPath, "alpha-dir");
		Directory.CreateDirectory(Path.Combine(directoryPath, "nested"));
		await File.WriteAllTextAsync(Path.Combine(directoryPath, "nested", "child.txt"), "child", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: "alpha",
			root: sandbox.RootPath,
			directories: true,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);

		await Assert.That(exitCode).IsEqualTo(0);
		await Assert.That(Directory.Exists(directoryPath)).IsTrue();
		await Assert.That(lines.Length).IsEqualTo(2);
		await Assert.That(lines[0]).IsEqualTo(directoryPath);
		await Assert.That(lines[1]).IsEqualTo("No changes were made. Re-run with --apply to delete these entries.");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_PreviewMode_EmptyQuery_FilesFlagMatchesAllFiles(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var nestedDirectoryPath = Path.Combine(sandbox.RootPath, "logs");
		var nestedFilePath = Path.Combine(nestedDirectoryPath, "alpha.log");
		var rootFilePath = Path.Combine(sandbox.RootPath, "beta.txt");
		Directory.CreateDirectory(nestedDirectoryPath);
		await File.WriteAllTextAsync(nestedFilePath, "alpha", cancellationToken);
		await File.WriteAllTextAsync(rootFilePath, "beta", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: string.Empty,
			root: sandbox.RootPath,
			files: true,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);
		var candidateLines = lines.Take(lines.Length - 1).ToArray();

		await Assert.That(exitCode).IsEqualTo(0);
		await Assert.That(File.Exists(rootFilePath)).IsTrue();
		await Assert.That(File.Exists(nestedFilePath)).IsTrue();
		await AssertSamePaths(candidateLines, [rootFilePath, nestedFilePath]);
		await Assert.That(lines[^1]).IsEqualTo("No changes were made. Re-run with --apply to delete these entries.");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_ApplyMode_DeletesMatchedFiles(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
		await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: "alpha",
			root: sandbox.RootPath,
			files: true,
			apply: true,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);

		await Assert.That(exitCode).IsEqualTo(0);
		await Assert.That(File.Exists(filePath)).IsFalse();
		await Assert.That(lines.Length).IsEqualTo(1);
		await Assert.That(lines[0]).IsEqualTo($"OK   {filePath}");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_ApplyMode_DeletesMatchedDirectoriesRecursively(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var directoryPath = Path.Combine(sandbox.RootPath, "alpha-dir");
		Directory.CreateDirectory(Path.Combine(directoryPath, "nested"));
		await File.WriteAllTextAsync(Path.Combine(directoryPath, "nested", "child.txt"), "child", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: "alpha",
			root: sandbox.RootPath,
			directories: true,
			apply: true,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);

		await Assert.That(exitCode).IsEqualTo(0);
		await Assert.That(Directory.Exists(directoryPath)).IsFalse();
		await Assert.That(lines.Length).IsEqualTo(1);
		await Assert.That(lines[0]).IsEqualTo($"OK   {directoryPath}");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_PreviewMode_CollapsesDescendantsUnderMatchedDirectories(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var directoryPath = Path.Combine(sandbox.RootPath, "logs");
		var filePath = Path.Combine(directoryPath, "alpha.log");
		Directory.CreateDirectory(directoryPath);
		await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

		var (exitCode, stdout) = await InvokeDeleteAsync(
			query: "log",
			root: sandbox.RootPath,
			cancellationToken: cancellationToken);
		var lines = SplitNewlineRecords(stdout);
		var candidateLines = lines.Take(lines.Length - 1).ToArray();

		await Assert.That(exitCode).IsEqualTo(0);
		await AssertSamePaths(candidateLines, [directoryPath]);
		await Assert.That(lines[^1]).IsEqualTo("No changes were made. Re-run with --apply to delete these entries.");
	}

	[Test]
	[NotInParallel("ConsoleContext")]
	public async Task DeleteAsync_ApplyMode_MixedSuccessAndFailure_ReturnsExitCodeOne(CancellationToken cancellationToken) {
		if (OperatingSystem.IsWindows()) {
			await AssertWindowsMixedDeleteFailureAsync(cancellationToken);
			return;
		}

		if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
			await AssertUnixMixedDeleteFailureAsync(cancellationToken);
		}
	}

	private static async Task AssertWindowsMixedDeleteFailureAsync(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var okFilePath = Path.Combine(sandbox.RootPath, "ok-match.txt");
		var failFilePath = Path.Combine(sandbox.RootPath, "fail-match.txt");
		await File.WriteAllTextAsync(okFilePath, "ok", cancellationToken);
		await File.WriteAllTextAsync(failFilePath, "fail", cancellationToken);
		File.SetAttributes(failFilePath, File.GetAttributes(failFilePath) | FileAttributes.ReadOnly);

		try {
			var (exitCode, stdout) = await InvokeDeleteAsync(
				query: "match",
				root: sandbox.RootPath,
				files: true,
				apply: true,
				cancellationToken: cancellationToken);
			var lines = SplitNewlineRecords(stdout);

			await Assert.That(exitCode).IsEqualTo(1);
			await Assert.That(File.Exists(okFilePath)).IsFalse();
			await Assert.That(File.Exists(failFilePath)).IsTrue();
			await Assert.That(lines.Any(line => line == $"OK   {okFilePath}")).IsTrue();
			await Assert.That(lines.Any(line => line.StartsWith($"FAIL {failFilePath} - ", StringComparison.Ordinal))).IsTrue();
		} finally {
			if (File.Exists(failFilePath)) {
				File.SetAttributes(failFilePath, FileAttributes.Normal);
			}
		}
	}

	[SupportedOSPlatform("linux")]
	[SupportedOSPlatform("macos")]
	private static async Task AssertUnixMixedDeleteFailureAsync(CancellationToken cancellationToken) {
		using var sandbox = Sandbox.Create();
		var okFilePath = Path.Combine(sandbox.RootPath, "ok-match.txt");
		var lockedParentPath = Path.Combine(sandbox.RootPath, "locked-parent");
		var failDirectoryPath = Path.Combine(lockedParentPath, "fail-match");
		Directory.CreateDirectory(failDirectoryPath);
		await File.WriteAllTextAsync(okFilePath, "ok", cancellationToken);
		await File.WriteAllTextAsync(Path.Combine(failDirectoryPath, "nested.txt"), "nested", cancellationToken);

		var originalMode = File.GetUnixFileMode(lockedParentPath);
		try {
			var lockedMode = originalMode
				& ~UnixFileMode.UserWrite
				& ~UnixFileMode.GroupWrite
				& ~UnixFileMode.OtherWrite;
			File.SetUnixFileMode(lockedParentPath, lockedMode);

			var (exitCode, stdout) = await InvokeDeleteAsync(
				query: "match",
				root: sandbox.RootPath,
				apply: true,
				cancellationToken: cancellationToken);
			var lines = SplitNewlineRecords(stdout);

			await Assert.That(exitCode).IsEqualTo(1);
			await Assert.That(File.Exists(okFilePath)).IsFalse();
			await Assert.That(Directory.Exists(failDirectoryPath)).IsTrue();
			await Assert.That(lines.Any(line => line == $"OK   {okFilePath}")).IsTrue();
			await Assert.That(lines.Any(line => line.StartsWith($"FAIL {failDirectoryPath} - ", StringComparison.Ordinal))).IsTrue();
		} finally {
			File.SetUnixFileMode(lockedParentPath, originalMode);
		}
	}

	private sealed class Sandbox : IDisposable {
		private Sandbox(string rootPath) {
			RootPath = rootPath;
		}

		public string RootPath { get; }

		public static Sandbox Create(string? suffix = null) {
			var suffixSegment = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"-{suffix}";
			var rootPath = Path.Combine(Path.GetTempPath(), $"seek-delete-cli-tests-{Guid.NewGuid():N}{suffixSegment}");
			Directory.CreateDirectory(rootPath);
			return new Sandbox(rootPath);
		}

		public void Dispose() {
			if (Directory.Exists(RootPath)) {
				Directory.Delete(RootPath, recursive: true);
			}
		}
	}

	private static async Task<(int ExitCode, string Stdout)> InvokeDeleteAsync(
		string query,
		string root,
		bool regex = false,
		bool files = false,
		bool directories = false,
		bool apply = false,
		CancellationToken cancellationToken = default) {
		var output = new StringWriter();
		var originalOut = ConsoleContext.Out;

		try {
			ConsoleContext.Out = output;

			var exitCode = await Commands.DeleteAsync(
				new SearchParameters(
					Query: query,
					Regex: regex,
					CaseSensitive: false,
					Hidden: false,
					System: false,
					Files: files,
					Directories: directories,
					Root: root),
				noProgress: true,
				apply: apply,
				cancellationToken: cancellationToken);

			return (exitCode, output.ToString());
		} finally {
			ConsoleContext.Out = originalOut;
		}
	}

	private static string[] SplitNewlineRecords(string stdout) {
		return stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
