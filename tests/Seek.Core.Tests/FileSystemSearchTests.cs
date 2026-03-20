namespace Seek.Core.Tests;

public sealed class FileSystemSearchTests {
    [Test]
    public async Task ContainsSearch_FindsFilesAndDirectories(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "logs"));
        await File.WriteAllTextAsync(Path.Combine(sandbox.RootPath, "logs", "alpha.log"), "alpha", cancellationToken);

        var search = CreateContainsSearch(sandbox.RootPath, "log");

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Any(match => Render(match) == "logs")).IsTrue();
        await Assert.That(results.Any(match => Render(match) == Path.Combine("logs", "alpha.log"))).IsTrue();
    }

    [Test]
    public async Task SearchAsync_FilesTarget_SkipsDirectoryMatchesButFindsNestedFiles(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var nestedDirectory = Path.Combine(sandbox.RootPath, "container", "nested");
        var nestedFile = Path.Combine(nestedDirectory, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(nestedFile, "alpha", cancellationToken);

        var search = CreateContainsSearch(sandbox.RootPath, "alpha", searchType: SearchType.Files);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await AssertSamePaths(results.Select(Render), [Path.Combine("container", "nested", "alpha.log")]);
    }

    [Test]
    public async Task SearchAsync_DirectoriesTarget_SkipsFileMatches(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var matchingDirectory = Path.Combine(sandbox.RootPath, "alpha-dir");
        var filePath = Path.Combine(sandbox.RootPath, "alpha.log");
        Directory.CreateDirectory(matchingDirectory);
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var search = CreateContainsSearch(sandbox.RootPath, "alpha", searchType: SearchType.Directories);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await AssertSamePaths(results.Select(Render), ["alpha-dir"]);
    }

    [Test]
    public async Task SearchAsync_DirectoriesTarget_DoesNotEmitRootPath(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create("alpha-root");
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "child"));

        var search = CreateRegexSearch(
            sandbox.RootPath,
            @"^\.$",
            caseSensitive: true,
            searchType: SearchType.Directories);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_SkipsHiddenDirectoriesByDefault(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var hiddenDirectory = Path.Combine(sandbox.RootPath, ".git");
        Directory.CreateDirectory(hiddenDirectory);
        EnsureAttribute(hiddenDirectory, FileAttributes.Hidden);
        await File.WriteAllTextAsync(Path.Combine(hiddenDirectory, "config"), "git-config", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sandbox.RootPath, "tracked.txt"), "tracked", cancellationToken);

        var hiddenSearch = CreateContainsSearch(sandbox.RootPath, ".git");
        var trackedSearch = CreateContainsSearch(sandbox.RootPath, "tracked");

        var hiddenResults = await CollectAsync(hiddenSearch.SearchAsync(), cancellationToken);
        var trackedResults = await CollectAsync(trackedSearch.SearchAsync(), cancellationToken);

        await Assert.That(hiddenResults.Count).IsEqualTo(0);
        await Assert.That(trackedResults.Count).IsEqualTo(1);
        await Assert.That(Render(trackedResults[0])).IsEqualTo("tracked.txt");
    }

    [Test]
    public async Task SearchAsync_AllowsHiddenDirectories_WhenConfigured(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var hiddenDirectory = Path.Combine(sandbox.RootPath, ".git");
        var hiddenFile = Path.Combine(hiddenDirectory, "config");
        Directory.CreateDirectory(hiddenDirectory);
        EnsureAttribute(hiddenDirectory, FileAttributes.Hidden);
        await File.WriteAllTextAsync(hiddenFile, "git-config", cancellationToken);

        var search = CreateContainsSearch(sandbox.RootPath, ".git", attributesToSkip: FileAttributes.ReparsePoint | FileAttributes.System);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Any(match => Render(match) == ".git")).IsTrue();
        await Assert.That(results.Any(match => Render(match) == Path.Combine(".git", "config"))).IsTrue();
    }

    [Test]
    public async Task SearchAsync_SkipsHiddenFilesByDefault(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var hiddenFile = Path.Combine(sandbox.RootPath, ".env");
        await File.WriteAllTextAsync(hiddenFile, "secret", cancellationToken);
        EnsureAttribute(hiddenFile, FileAttributes.Hidden);

        var search = CreateContainsSearch(sandbox.RootPath, ".env");

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_AllowsHiddenFiles_WhenConfigured(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var hiddenFile = Path.Combine(sandbox.RootPath, ".env");
        await File.WriteAllTextAsync(hiddenFile, "secret", cancellationToken);
        EnsureAttribute(hiddenFile, FileAttributes.Hidden);

        var search = CreateContainsSearch(sandbox.RootPath, ".env", attributesToSkip: FileAttributes.ReparsePoint | FileAttributes.System);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(Render(results[0])).IsEqualTo(".env");
    }

    [Test]
    public async Task SearchAsync_SkipsSystemFilesByDefault_WhenSupported(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var systemFile = Path.Combine(sandbox.RootPath, "system.txt");
        await File.WriteAllTextAsync(systemFile, "system", cancellationToken);
        if (!TryEnsureAttribute(systemFile, FileAttributes.System)) {
            return;
        }

        var search = CreateContainsSearch(sandbox.RootPath, "system.txt");

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_AllowsSystemFiles_WhenConfiguredAndSupported(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var systemFile = Path.Combine(sandbox.RootPath, "system.txt");
        await File.WriteAllTextAsync(systemFile, "system", cancellationToken);
        if (!TryEnsureAttribute(systemFile, FileAttributes.System)) {
            return;
        }

        var search = CreateContainsSearch(sandbox.RootPath, "system.txt", attributesToSkip: FileAttributes.ReparsePoint | FileAttributes.Hidden);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(Render(results[0])).IsEqualTo("system.txt");
    }

    [Test]
    public async Task SearchAsync_CompletesForNestedDirectories(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "level1", "level2"));
        await File.WriteAllTextAsync(Path.Combine(sandbox.RootPath, "level1", "level2", "needle.txt"), "needle", cancellationToken);

        var search = CreateContainsSearch(sandbox.RootPath, "needle");

        var results = await CollectAsync(search.SearchAsync(), cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(Render(results[0])).IsEqualTo(Path.Combine("level1", "level2", "needle.txt"));
    }

    [Test]
    public async Task SearchAsync_CancellationAfterFirstResult_StopsPromptlyWithoutHanging(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();

        for (var i = 0; i < 256; i++) {
            var directoryPath = Path.Combine(sandbox.RootPath, $"dir-{i:D3}");
            Directory.CreateDirectory(directoryPath);
            await File.WriteAllTextAsync(Path.Combine(directoryPath, $"match-{i:D3}.txt"), "x", cancellationToken);
        }

        var search = CreateContainsSearch(sandbox.RootPath, "match");
        using var cts = new CancellationTokenSource();
        var sawFirstResult = false;

        var enumerateTask = Task.Run(async () => {
            await foreach (var _ in search.SearchAsync().WithCancellation(cts.Token)) {
                sawFirstResult = true;
                cts.Cancel();
            }
        }, CancellationToken.None);

        await Assert.That(async () => await enumerateTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
            .Throws<OperationCanceledException>();
        await Assert.That(sawFirstResult).IsTrue();
    }

    [Test]
    public async Task SearchAsync_ContainsSearch_UsesCaseSensitiveFlag(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "Alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var caseInsensitiveSearch = CreateContainsSearch(sandbox.RootPath, "alpha", caseSensitive: false);
        var caseSensitiveSearch = CreateContainsSearch(sandbox.RootPath, "alpha", caseSensitive: true);

        var caseInsensitiveResults = await CollectAsync(caseInsensitiveSearch.SearchAsync(), cancellationToken);
        var caseSensitiveResults = await CollectAsync(caseSensitiveSearch.SearchAsync(), cancellationToken);

        await Assert.That(caseInsensitiveResults.Any(match => GetRelativePath(match) == "Alpha.log")).IsTrue();
        await Assert.That(caseSensitiveResults.Any(match => GetRelativePath(match) == "Alpha.log")).IsFalse();
    }

    [Test]
    public async Task SearchAsync_RegexSearch_UsesCaseSensitiveFlag(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        var filePath = Path.Combine(sandbox.RootPath, "Alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);

        var caseInsensitiveSearch = CreateRegexSearch(sandbox.RootPath, @"alpha\.log$", caseSensitive: false);
        var caseSensitiveSearch = CreateRegexSearch(sandbox.RootPath, @"alpha\.log$", caseSensitive: true);

        var caseInsensitiveResults = await CollectAsync(caseInsensitiveSearch.SearchAsync(), cancellationToken);
        var caseSensitiveResults = await CollectAsync(caseSensitiveSearch.SearchAsync(), cancellationToken);

        await Assert.That(caseInsensitiveResults.Any(match => GetRelativePath(match) == "Alpha.log")).IsTrue();
        await Assert.That(caseSensitiveResults.Any(match => GetRelativePath(match) == "Alpha.log")).IsFalse();
    }

    [Test]
    public async Task ContainsMatcher_ReturnsHighlightedPrefixMatchAndTail() {
        var matcher = new ContainsMatcher(".mp4", caseSensitive: true);

        var found = matcher.TryFindMatches("alpha.mp4.bak", out var match);
        var ranges = match.ToArray();

        await Assert.That(found).IsTrue();
        await Assert.That(ranges.Length).IsEqualTo(3);
        await Assert.That((ranges[0].Start, ranges[0].Length, ranges[0].IsMatch)).IsEqualTo((0, 5, false));
        await Assert.That((ranges[1].Start, ranges[1].Length, ranges[1].IsMatch)).IsEqualTo((5, 4, true));
        await Assert.That((ranges[2].Start, ranges[2].Length, ranges[2].IsMatch)).IsEqualTo((9, 4, false));
        await Assert.That(Render("alpha.mp4.bak", match)).IsEqualTo("alpha.mp4.bak");
    }

    [Test]
    public async Task ContainsMatcher_UsesCaseSensitiveFlag() {
        var caseInsensitiveMatcher = new ContainsMatcher("alpha", caseSensitive: false);
        var caseSensitiveMatcher = new ContainsMatcher("alpha", caseSensitive: true);

        var caseInsensitiveFound = caseInsensitiveMatcher.TryFindMatches("Alpha.log", out var caseInsensitiveMatch);
        var caseSensitiveFound = caseSensitiveMatcher.TryFindMatches("Alpha.log", out var caseSensitiveMatch);

        await Assert.That(caseInsensitiveFound).IsTrue();
        await Assert.That(caseSensitiveFound).IsFalse();
        await Assert.That(Render("Alpha.log", caseInsensitiveMatch)).IsEqualTo("Alpha.log");
        await Assert.That(caseSensitiveMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RegexMatcher_ReturnsCorrectAbsoluteHighlightSlices() {
        var matcher = new RegexMatcher(@"\d+", caseSensitive: true);

        var found = matcher.TryFindMatches("ab12cd34ef", out var match);
        var ranges = match.ToArray();

        await Assert.That(found).IsTrue();
        await Assert.That(ranges.Length).IsEqualTo(5);
        await Assert.That((ranges[0].Start, ranges[0].Length, ranges[0].IsMatch)).IsEqualTo((0, 2, false));
        await Assert.That((ranges[1].Start, ranges[1].Length, ranges[1].IsMatch)).IsEqualTo((2, 2, true));
        await Assert.That((ranges[2].Start, ranges[2].Length, ranges[2].IsMatch)).IsEqualTo((4, 2, false));
        await Assert.That((ranges[3].Start, ranges[3].Length, ranges[3].IsMatch)).IsEqualTo((6, 2, true));
        await Assert.That((ranges[4].Start, ranges[4].Length, ranges[4].IsMatch)).IsEqualTo((8, 2, false));
        await Assert.That(Render("ab12cd34ef", match)).IsEqualTo("ab12cd34ef");
    }

    [Test]
    public async Task RegexMatcher_UsesCaseSensitiveFlag() {
        var caseInsensitiveMatcher = new RegexMatcher(@"alpha\.log$", caseSensitive: false);
        var caseSensitiveMatcher = new RegexMatcher(@"alpha\.log$", caseSensitive: true);

        var caseInsensitiveFound = caseInsensitiveMatcher.TryFindMatches("Alpha.log", out var caseInsensitiveMatch);
        var caseSensitiveFound = caseSensitiveMatcher.TryFindMatches("Alpha.log", out var caseSensitiveMatch);

        await Assert.That(caseInsensitiveFound).IsTrue();
        await Assert.That(caseSensitiveFound).IsFalse();
        await Assert.That(Render("Alpha.log", caseInsensitiveMatch)).IsEqualTo("Alpha.log");
        await Assert.That(caseSensitiveMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_FileHighlightRanges_AreBasedOnRelativePath(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "nested"));
        var filePath = Path.Combine(sandbox.RootPath, "nested", "alpha.log");
        await File.WriteAllTextAsync(filePath, "alpha", cancellationToken);
        var relativeFilePath = Path.Combine("nested", "alpha.log");

        var search = CreateContainsSearch(sandbox.RootPath, "alpha");

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);
        var fileMatches = results.Where(match => GetRelativePath(match) == relativeFilePath).ToList();
        await Assert.That(fileMatches.Count).IsEqualTo(1);
        var fileMatch = fileMatches[0];
        await Assert.That(fileMatch.Path).IsEqualTo(filePath);

        await Assert.That(Render(fileMatch)).IsEqualTo(relativeFilePath);

        var highlightedRanges = fileMatch.Sections.Where(range => range.IsMatch).ToList();
        await Assert.That(highlightedRanges.Count).IsEqualTo(1);
        var highlighted = highlightedRanges[0];
        await Assert.That(highlighted.Start).IsEqualTo(relativeFilePath.LastIndexOf("alpha", StringComparison.Ordinal));
        await Assert.That(relativeFilePath.AsSpan(highlighted.Start, highlighted.Length).ToString()).IsEqualTo("alpha");
    }

    [Test]
    public async Task SearchAsync_RegexHighlight_ReconstructsRelativePathForFiles(CancellationToken cancellationToken) {
        using var sandbox = Sandbox.Create();
        Directory.CreateDirectory(Path.Combine(sandbox.RootPath, "nested"));
        var filePath = Path.Combine(sandbox.RootPath, "nested", "file-42.txt");
        await File.WriteAllTextAsync(filePath, "x", cancellationToken);
        var relativeFilePath = Path.Combine("nested", "file-42.txt");

        var search = CreateRegexSearch(sandbox.RootPath, @"file-42\.txt", caseSensitive: true);

        var results = await CollectAsync(search.SearchAsync(), cancellationToken);
        var fileMatches = results.Where(match => GetRelativePath(match) == relativeFilePath).ToList();
        await Assert.That(fileMatches.Count).IsEqualTo(1);
        var fileMatch = fileMatches[0];
        await Assert.That(fileMatch.Path).IsEqualTo(filePath);

        await Assert.That(Render(fileMatch)).IsEqualTo(relativeFilePath);

        var highlightedRanges = fileMatch.Sections.Where(range => range.IsMatch).ToList();
        await Assert.That(highlightedRanges.Count).IsEqualTo(1);
        var highlighted = highlightedRanges[0];
        await Assert.That(relativeFilePath.AsSpan(highlighted.Start, highlighted.Length).ToString()).IsEqualTo("file-42.txt");
    }

    private static FileSystemSearch CreateContainsSearch(
        string rootPath,
        string query,
        bool caseSensitive = false,
        SearchType searchType = SearchType.FilesAndDirectories,
        FileAttributes attributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System) {
        return new FileSystemSearch(new SearchOptions {
            Query = query,
            Root = rootPath,
            Regex = false,
            CaseSensitive = caseSensitive,
            AttributesToSkip = attributesToSkip,
            SearchType = searchType
        });
    }

    private static FileSystemSearch CreateRegexSearch(
        string rootPath,
        string query,
        bool caseSensitive = false,
        SearchType searchType = SearchType.FilesAndDirectories,
        FileAttributes attributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System) {
        return new FileSystemSearch(new SearchOptions {
            Query = query,
            Root = rootPath,
            Regex = true,
            CaseSensitive = caseSensitive,
            AttributesToSkip = attributesToSkip,
            SearchType = searchType
        });
    }

    private static void EnsureAttribute(string path, FileAttributes attribute) {
        if (!TryEnsureAttribute(path, attribute)) {
            throw new InvalidOperationException($"Could not apply the '{attribute}' attribute to '{path}'.");
        }
    }

    private static bool TryEnsureAttribute(string path, FileAttributes attribute) {
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(attribute)) {
            return true;
        }

        try {
            File.SetAttributes(path, attributes | attribute);
        } catch (PlatformNotSupportedException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }

        return File.GetAttributes(path).HasFlag(attribute);
    }

    private static string Render(SearchMatch match) {
        return Render(GetRelativePath(match), match.Sections);
    }

    private static string Render(string source, Sections match) {
        return string.Concat(match.Select(range => source.AsSpan(range.Start, range.Length).ToString()));
    }

    private static string GetRelativePath(SearchMatch match) {
        if (match.RelativePathOffset == match.Path.Length) {
            return ".";
        }

        return match.Path[match.RelativePathOffset..];
    }

    private static async Task<List<SearchMatch>> CollectAsync(
        IAsyncEnumerable<SearchMatch> results,
        CancellationToken cancellationToken) {
        var collected = new List<SearchMatch>();
        await foreach (var result in results.WithCancellation(cancellationToken)) {
            collected.Add(result);
        }

        return collected;
    }

    private static async Task AssertSamePaths(IEnumerable<string> actual, IEnumerable<string> expected) {
        var actualPaths = actual.Order(StringComparer.Ordinal).ToArray();
        var expectedPaths = expected.Order(StringComparer.Ordinal).ToArray();

        await Assert.That(actualPaths.Length).IsEqualTo(expectedPaths.Length);
        for (var i = 0; i < actualPaths.Length; i++) {
            await Assert.That(actualPaths[i]).IsEqualTo(expectedPaths[i]);
        }
    }

    private sealed class Sandbox : IDisposable {
        private Sandbox(string rootPath) {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static Sandbox Create(string? suffix = null) {
            var suffixSegment = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"-{suffix}";
            var rootPath = Path.Combine(Path.GetTempPath(), $"seek-search-tests-{Guid.NewGuid():N}{suffixSegment}");
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
