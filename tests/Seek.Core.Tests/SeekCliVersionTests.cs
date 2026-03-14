using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Seek.Core.Tests;

public sealed partial class SeekCliVersionTests {
    [Test]
    public async Task ProgramConsoleAppVersion_MatchesCliProjectVersion(CancellationToken cancellationToken) {
        var repositoryRoot = FindRepositoryRoot();
        var programPath = Path.Combine(repositoryRoot, "src", "Seek.Cli", "Program.cs");
        var projectPath = Path.Combine(repositoryRoot, "src", "Seek.Cli", "Seek.Cli.csproj");

        var programContents = await File.ReadAllTextAsync(programPath, cancellationToken);
        var projectContents = await File.ReadAllTextAsync(projectPath, cancellationToken);

        var consoleAppVersion = ParseConsoleAppVersion(programContents);
        var projectVersion = ParseProjectVersion(projectContents);

        await Assert.That(consoleAppVersion).IsEqualTo(projectVersion);
    }

    private static string FindRepositoryRoot() {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
            var programPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Program.cs");
            var projectPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Seek.Cli.csproj");
            if (File.Exists(programPath) && File.Exists(projectPath)) {
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

    private static string ParseProjectVersion(string projectContents) {
        var project = XDocument.Parse(projectContents);
        var version = project.Root?
            .Elements()
            .Where(element => element.Name.LocalName == "PropertyGroup")
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Version")
            ?.Value;

        if (string.IsNullOrWhiteSpace(version)) {
            throw new InvalidOperationException("Could not find the Version property in Seek.Cli.csproj.");
        }

        return version;
    }

    [GeneratedRegex("""ConsoleApp\.Version\s*=\s*"(?<version>[^"]+)";""", RegexOptions.CultureInvariant)]
    private static partial Regex ConsoleAppVersionRegex();
}
