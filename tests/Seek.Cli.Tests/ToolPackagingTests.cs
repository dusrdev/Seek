using System.Xml.Linq;

namespace Seek.Cli.Tests;

public sealed partial class ToolPackagingTests {
    [Test]
    public async Task CliProject_ToolPackageRuntimeIdentifiers_ExcludeWindowsRidPackages(CancellationToken cancellationToken) {
        var projectContents = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "src", "Seek.Cli", "Seek.Cli.csproj"),
            cancellationToken);

        var runtimeIdentifiers = ParseToolPackageRuntimeIdentifiers(projectContents);

        await Assert.That(runtimeIdentifiers).Contains("linux-x64");
        await Assert.That(runtimeIdentifiers).Contains("linux-arm64");
        await Assert.That(runtimeIdentifiers).Contains("osx-x64");
        await Assert.That(runtimeIdentifiers).Contains("osx-arm64");
        await Assert.That(runtimeIdentifiers).Contains("any");
        await Assert.That(runtimeIdentifiers).DoesNotContain("win-x64");
        await Assert.That(runtimeIdentifiers).DoesNotContain("win-arm64");
    }

    [Test]
    public async Task PublishReleaseWorkflow_NugetRidMatrix_SkipsWindowsRidPackagesButKeepsWindowsBinaryReleases(CancellationToken cancellationToken) {
        var workflowContents = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), ".github", "workflows", "publish-release.yml"),
            cancellationToken);

        var nugetRidSection = ExtractSection(workflowContents, "nuget-rid", "nuget-publish");
        var binariesSection = ExtractSection(workflowContents, "binaries", string.Empty);

        await Assert.That(nugetRidSection).Contains("linux-x64");
        await Assert.That(nugetRidSection).Contains("linux-arm64");
        await Assert.That(nugetRidSection).Contains("osx-x64");
        await Assert.That(nugetRidSection).Contains("osx-arm64");
        await Assert.That(nugetRidSection).DoesNotContain("win-x64");
        await Assert.That(nugetRidSection).DoesNotContain("win-arm64");

        await Assert.That(binariesSection).Contains("win-x86");
        await Assert.That(binariesSection).Contains("win-x64");
        await Assert.That(binariesSection).Contains("win-arm64");
    }

    private static string FindRepositoryRoot() {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) {
            var projectPath = Path.Combine(current.FullName, "src", "Seek.Cli", "Seek.Cli.csproj");
            var workflowPath = Path.Combine(current.FullName, ".github", "workflows", "publish-release.yml");
            if (File.Exists(projectPath) && File.Exists(workflowPath)) {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private static string[] ParseToolPackageRuntimeIdentifiers(string projectContents) {
        var project = XDocument.Parse(projectContents);
        var runtimeIdentifiers = project.Root?
            .Elements()
            .Where(element => element.Name.LocalName == "PropertyGroup")
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "ToolPackageRuntimeIdentifiers")
            ?.Value;

        if (string.IsNullOrWhiteSpace(runtimeIdentifiers)) {
            throw new InvalidOperationException("Could not find ToolPackageRuntimeIdentifiers in Seek.Cli.csproj.");
        }

        return runtimeIdentifiers
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ExtractSection(string contents, string sectionName, string nextSectionName) {
        var sectionHeader = $"{sectionName}:";
        var startIndex = contents.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (startIndex < 0) {
            throw new InvalidOperationException($"Could not find the {sectionName} section in publish-release.yml.");
        }

        var sectionStart = startIndex + sectionHeader.Length;
        if (nextSectionName.Length == 0) {
            return contents[sectionStart..];
        }

        var nextSectionHeader = $"{nextSectionName}:";
        var endIndex = contents.IndexOf(nextSectionHeader, sectionStart, StringComparison.Ordinal);
        if (endIndex < 0) {
            throw new InvalidOperationException($"Could not find the {nextSectionName} section in publish-release.yml.");
        }

        return contents[sectionStart..endIndex];
    }
}
