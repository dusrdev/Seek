namespace Seek.Cli.Tests;

public sealed class CommandsDeleteRegistrationTests {
    [Test]
    public async Task Program_RegistersDeleteCommand(CancellationToken cancellationToken) {
        var programContents = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "src", "Seek.Cli", "Program.cs"),
            cancellationToken);

        await Assert.That(programContents.Contains(
            """app.Add("delete", Commands.DeleteAsync);""",
            StringComparison.Ordinal)).IsTrue();
    }

    private static string FindRepositoryRoot() {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
            var programPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Program.cs");
            if (File.Exists(programPath)) {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
