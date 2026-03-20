namespace Seek.Cli.Tests;

public sealed class CommandsDeleteMetadataTests {
    [Test]
    public async Task DeleteCommandSummary_DescribesDeleteCommand(CancellationToken cancellationToken) {
        var deleteCommandContents = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "src", "Seek.Cli", "Commands.Delete.cs"),
            cancellationToken);

        await Assert.That(deleteCommandContents.Contains(
            "Delete matching files and directories.",
            StringComparison.Ordinal)).IsTrue();
    }

    private static string FindRepositoryRoot() {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
            var deleteCommandPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Commands.Delete.cs");
            if (File.Exists(deleteCommandPath)) {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
