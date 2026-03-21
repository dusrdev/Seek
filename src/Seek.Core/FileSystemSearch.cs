using System.IO.Enumeration;
using System.Threading.Channels;

namespace Seek.Core;

internal sealed class FileSystemSearch {
#if DEBUG
    private static readonly int Workers = 1;
#else
    private static readonly int Workers = Math.Max(1, Environment.ProcessorCount - 1);
#endif

    private readonly EnumerationOptions _directoryEnumerationOptions;
    private readonly string _rootPath;
    private readonly int _relativePathOffset;
    private int _remainingWorkers = Workers;
    private readonly IMatcher _matcher;
    private readonly SearchType _searchType;
    private long _pending = 1;
    private readonly FileSystemEnumerable<string>.FindPredicate _handleEntryPredicate;
    private Exception? _workerFailure;
    private readonly CancellationToken _cancellationToken;
    private readonly bool _matchDirectories;

    private readonly Channel<SearchMatch> _matches = Channel.CreateUnbounded<SearchMatch>(new UnboundedChannelOptions {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<string> _processor = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
        SingleWriter = false,
        SingleReader = false
    });

    public FileSystemSearch(SearchOptions options, CancellationToken token = default) {
        _rootPath = NormalizePath(options.Root);
        _relativePathOffset = Path.EndsInDirectorySeparator(_rootPath) ? _rootPath.Length : _rootPath.Length + 1;

        _matcher = (options.Query.Length, options.Regex) switch {
            (0, _) => new MatchAllMatcher(), // empty non-regex input
            (_, false) => new ContainsMatcher(options.Query, options.CaseSensitive),
            (_, true) => new RegexMatcher(options.Query, options.CaseSensitive)
        };

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
        _cancellationToken = token;
        _matchDirectories = _searchType.HasFlag(SearchType.Directories);
    }

    public IAsyncEnumerable<SearchMatch> SearchAsync() {
        if (!Directory.Exists(_rootPath)) {
            throw new DirectoryNotFoundException($"Root path does not exist: {_rootPath}");
        }

        for (int i = 0; i < Workers; i++) {
            _ = Task.Run(RunWorkerAsync, _cancellationToken)
                    .ContinueWith(static (task, state) => {
                        HandleError(task, state);
                    }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        Interlocked.Increment(ref _remainingWorkers);
        _ = Task.Run(() => EnumerateExceptCurrentRoot(_rootPath), _cancellationToken)
                .ContinueWith(static (task, state) => {
                    HandleError(task, state);
                }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return _matches.Reader.ReadAllAsync(_cancellationToken);
    }

    private async Task RunWorkerAsync() {
        while (await _processor.Reader.WaitToReadAsync(_cancellationToken)) {
            while (_processor.Reader.TryRead(out var searchEntry)) {
                var relativeSearchEntry = searchEntry.AsSpan(_relativePathOffset);

                if (_matchDirectories && _matcher.TryFindMatches(relativeSearchEntry, out var match)) {
                    _matches.Writer.TryWrite(new SearchMatch(searchEntry, _relativePathOffset, match, true));
                }

                EnumerateExceptCurrentRoot(searchEntry);
            }
        }
    }

    private static void HandleError(Task task, object? state) {
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
    }

    private void EnumerateExceptCurrentRoot(string searchEntry) {
        var enumerable = new FileSystemEnumerable<string>(
                                    searchEntry,
                                    (ref fileSystemEntry) => string.Empty,
                                    _directoryEnumerationOptions) {
            ShouldIncludePredicate = _handleEntryPredicate
        };

        _ = enumerable.Count();

        if (Interlocked.Decrement(ref _pending) == 0) _processor.Writer.TryComplete();
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
            ReadOnlySpan<char> relativePath = fullPath.Slice(_relativePathOffset);
            if (_matcher.TryFindMatches(relativePath, out var match)) {
                _matches.Writer.TryWrite(new SearchMatch(new string(fullPath), _relativePathOffset, match, false));
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
