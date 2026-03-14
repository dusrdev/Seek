namespace Seek.Cli.Tests;

public sealed class CommandMetadataTests {
	[Test]
	public async Task SearchCommandSummary_DescribesTheDefaultCommandAndApp(CancellationToken cancellationToken) {
		var searchCommandContents = await File.ReadAllTextAsync(
			Path.Combine(FindRepositoryRoot(), "src", "Seek.Cli", "Commands.Search.cs"),
			cancellationToken);

		await Assert.That(searchCommandContents.Contains(
			"Seek is a fast filesystem search tool for files and directories by David Shnayder (@dusrdev).",
			StringComparison.Ordinal)).IsTrue();
	}

	private static string FindRepositoryRoot() {
		for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
			var searchCommandPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Commands.Search.cs");
			if (File.Exists(searchCommandPath)) {
				return current.FullName;
			}
		}

		throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
	}
}
