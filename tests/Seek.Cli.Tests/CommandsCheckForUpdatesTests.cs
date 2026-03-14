using System.Text.RegularExpressions;

namespace Seek.Cli.Tests;

public sealed partial class CommandsCheckForUpdatesTests {
	[Test]
	public async Task Program_RegistersCheckForUpdatesCommand(CancellationToken cancellationToken) {
		var programContents = await ReadProgramContentsAsync(cancellationToken);

		await Assert.That(programContents.Contains(
			"""app.Add("check-for-updates", Commands.CheckForUpdatesAsync);""",
			StringComparison.Ordinal)).IsTrue();
	}

	[Test]
	public async Task ProgramConsoleAppVersion_IsParseableBySystemVersion(CancellationToken cancellationToken) {
		var programContents = await ReadProgramContentsAsync(cancellationToken);
		var consoleAppVersion = ParseConsoleAppVersion(programContents);

		await Assert.That(Version.TryParse(consoleAppVersion, out _)).IsTrue();
	}

	private static Task<string> ReadProgramContentsAsync(CancellationToken cancellationToken) =>
		File.ReadAllTextAsync(
			Path.Combine(FindRepositoryRoot(), "src", "Seek.Cli", "Program.cs"),
			cancellationToken);

	private static string FindRepositoryRoot() {
		for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
			var programPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Program.cs");
			if (File.Exists(programPath)) {
				return current.FullName;
			}
		}

		throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
	}

	private static string ParseConsoleAppVersion(string programContents) {
		var match = ConsoleAppVersionRegex().Match(programContents);
		if (!match.Success) {
			throw new InvalidOperationException("Could not find the ConsoleApp.Version assignment in Program.cs.");
		}

		return match.Groups["version"].Value;
	}

	[GeneratedRegex("""ConsoleApp\.Version\s*=\s*"(?<version>[^"]+)";""", RegexOptions.CultureInvariant)]
	private static partial Regex ConsoleAppVersionRegex();
}
