using System.IO.Enumeration;
using System.Threading.Channels;

namespace Seek.Core;

internal sealed class FileSystemSearch {
    private static readonly int Workers = Math.Max(1, Environment.ProcessorCount - 1);

    private readonly EnumerationOptions _directoryEnumerationOptions;
    private readonly string _rootPath;
    private int _remainingWorkers = Workers;
    private readonly IMatcher _matcher;
    private readonly SearchType _searchType;
    private long _pending = 1;
    private readonly FileSystemEnumerable<string>.FindPredicate _handleEntryPredicate;
    private Exception? _workerFailure;

    private readonly Channel<SearchMatch> _matches = Channel.CreateUnbounded<SearchMatch>(new UnboundedChannelOptions {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<string> _processor = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
        SingleWriter = false,
        SingleReader = false
    });

    public FileSystemSearch(SearchOptions options) {
        _rootPath = NormalizePath(options.Root);
        _matcher = options.Regex
                ? new RegexMatcher(options.Query, options.CaseSensitive)
                : new ContainsMatcher(options.Query, options.CaseSensitive);
        _directoryEnumerationOptions = new() {
            AttributesToSkip = options.AttributesToSkip,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };
        _searchType = options.SearchType;
        _handleEntryPredicate = _searchType == SearchType.Directories
            ? HandleSearchEntryForDirectoriesOnly
            : HandleSearchEntryForFilesAndDirectories;
    }

    public IAsyncEnumerable<SearchMatch> SearchAsync(
        CancellationToken cancellationToken = default) {
        if (!Directory.Exists(_rootPath)) {
            throw new DirectoryNotFoundException($"Root path does not exist: {_rootPath}");
        }

        var writer = _processor.Writer;
        var reader = _processor.Reader;
        var matcher = _matcher;
        var resultsWriter = _matches.Writer;
        bool matchDirectories = _searchType.HasFlag(SearchType.Directories);

        for (int i = 0; i < Workers; i++) {
            _ = Task.Run(async () => {
                while (await reader.WaitToReadAsync(cancellationToken)) {
                    while (reader.TryRead(out var searchEntry)) {
                        if (matchDirectories && matcher.TryFindMatches(searchEntry, out var match)) {
                            resultsWriter.TryWrite(new SearchMatch(searchEntry, match));
                        }

                        var enumerable = new FileSystemEnumerable<string>(
                            searchEntry,
                            (ref fileSystemEntry) => string.Empty,
                            _directoryEnumerationOptions) {
                            ShouldIncludePredicate = _handleEntryPredicate
                        };

                        _ = enumerable.Count();

                        if (Interlocked.Decrement(ref _pending) == 0) writer.TryComplete();
                    }
                }
            }, cancellationToken).ContinueWith(static (task, state) => {
                var search = (FileSystemSearch)state!;
                var exception = task.Exception;
                var isCancellationOnly = exception is not null
                    && exception.InnerExceptions.All(static inner => inner is TaskCanceledException or OperationCanceledException);

                if (exception is not null
                    && !isCancellationOnly
                    && Interlocked.CompareExchange(ref search._workerFailure, exception, null) is null) {
                    search._processor.Writer.TryComplete(exception);
                    search._matches.Writer.TryComplete(exception);
                }

                if (Interlocked.Decrement(ref search._remainingWorkers) == 0 && search._workerFailure is null) {
                    search._matches.Writer.TryComplete();
                }
            }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        writer.TryWrite(_rootPath);

        return _matches.Reader.ReadAllAsync(cancellationToken);
    }

    private bool HandleSearchEntryForFilesAndDirectories(ref FileSystemEntry entry) {
        if (entry.IsDirectory) {
            Interlocked.Increment(ref _pending);
            _processor.Writer.TryWrite(entry.ToFullPath());
            return false;
        } else {
            var totalLength = entry.Directory.Length + 1 + entry.FileName.Length;
            Span<char> fullPath = stackalloc char[totalLength];
            entry.Directory.CopyTo(fullPath);
            fullPath[entry.Directory.Length] = Path.DirectorySeparatorChar;
            entry.FileName.CopyTo(fullPath.Slice(entry.Directory.Length + 1));
            if (_matcher.TryFindMatches(fullPath, out var match)) {
                _matches.Writer.TryWrite(new SearchMatch(new string(fullPath), match));
            }
            return false;
        }
    }

    private bool HandleSearchEntryForDirectoriesOnly(ref FileSystemEntry entry) {
        if (entry.IsDirectory) {
            Interlocked.Increment(ref _pending);
            _processor.Writer.TryWrite(entry.ToFullPath());
            return false;
        } else {
            return false;
        }
    }

    private static string NormalizePath(string path) {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && !fullPath.Equals(root, StringComparison.Ordinal)) {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath;
    }
}

internal record SearchOptions {
    public required string Query { get; init; }
    public string Root { get; init; } = ".";
    public bool Regex { get; init; }
    public bool CaseSensitive { get; init; }
    public FileAttributes AttributesToSkip { get; init; }
    public SearchType SearchType { get; init; } = SearchType.FilesAndDirectories;
}
