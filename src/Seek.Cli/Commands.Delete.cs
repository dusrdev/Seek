using ConsoleAppFramework;

using PrettyConsole;

using Seek.Core;

namespace Seek.Cli;

internal static partial class Commands {
	/// <summary>
	/// Delete matching files and directories.
	/// </summary>
	/// <param name="query">Search query</param>
	/// <param name="regex">-r, Treat the query as regex pattern</param>
	/// <param name="caseSensitive">Perform a case sensitive search</param>
	/// <param name="hidden">-h, Include hidden files and folders</param>
	/// <param name="system">-s, Include system files and folders</param>
	/// <param name="files">-f, Match only against files</param>
	/// <param name="directories">-d, Match only against directories</param>
	/// <param name="root">The root path from which to scan</param>
	/// <param name="apply">Perform the deletion instead of previewing candidates</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<int> DeleteAsync(
		[Argument] string query,
		bool regex,
		bool caseSensitive,
		bool hidden,
		bool system,
		bool files,
		bool directories,
		string root = ".",
		bool apply = false,
		CancellationToken cancellationToken = default) {
		var search = CreateFileSystemSearch(
			query,
			regex,
			caseSensitive,
			hidden,
			system,
			files,
			directories,
			root,
			cancellationToken);
		var collapsedCandidates = await CollectDeleteCandidatesAsync(search.SearchAsync(), cancellationToken).ConfigureAwait(false);

		if (!apply) {
			foreach (var candidate in collapsedCandidates) {
				Console.WriteLineInterpolated(OutputPipe.Out, $"{candidate.Path}");
			}

			Console.NewLine(OutputPipe.Out);
			Console.WriteLineInterpolated(OutputPipe.Out, $"{ConsoleColor.Yellow}No changes were made. Re-run with --apply to delete these entries.");
			return 0;
		}

		var hadFailure = false;
		foreach (var candidate in collapsedCandidates) {
			try {
				ApplyDeleteCandidate(candidate);
				Console.WriteLineInterpolated(OutputPipe.Out, $"{ConsoleColor.Green}SUCCESS{ConsoleColor.DefaultForeground} {candidate.Path}");
			} catch (Exception exception) when (exception is not OperationCanceledException) {
				hadFailure = true;
				Console.WriteLineInterpolated(OutputPipe.Out, $"{ConsoleColor.Red}FAIL{ConsoleColor.DefaultForeground} {candidate.Path} - {exception.Message}");
			}
		}

		return hadFailure ? 1 : 0;
	}

	private static async Task<List<DeleteCandidate>> CollectDeleteCandidatesAsync(
		IAsyncEnumerable<SearchMatch> matches,
		CancellationToken cancellationToken) {
		List<DeleteCandidate> candidates = [];

		await foreach (var searchMatch in matches.WithCancellation(cancellationToken).ConfigureAwait(false)) {
			candidates.Add(new DeleteCandidate(searchMatch.Path, searchMatch.IsDirectory));
		}

		return CollapseDescendantCandidates(candidates);
	}

	private static List<DeleteCandidate> CollapseDescendantCandidates(List<DeleteCandidate> candidates) {
		candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

		List<DeleteCandidate> collapsedCandidates = [];
		List<string> retainedDirectories = [];

		foreach (var candidate in candidates) {
			var isNestedUnderMatchedDirectory = false;

			foreach (var directoryPath in retainedDirectories) {
				if (IsDescendantPath(directoryPath, candidate.Path)) {
					isNestedUnderMatchedDirectory = true;
					break;
				}
			}

			if (!isNestedUnderMatchedDirectory) {
				collapsedCandidates.Add(candidate);
				if (candidate.IsDirectory) {
					retainedDirectories.Add(candidate.Path);
				}
			}
		}

		return collapsedCandidates;
	}

	private static bool IsDescendantPath(string directoryPath, string path) {
		if (path.Length <= directoryPath.Length || !path.StartsWith(directoryPath, StringComparison.Ordinal)) {
			return false;
		}

		var separator = path[directoryPath.Length];
		return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
	}

	private static void ApplyDeleteCandidate(DeleteCandidate candidate) {
		if (candidate.IsDirectory) {
			Directory.Delete(candidate.Path, recursive: true);
			return;
		}

		File.Delete(candidate.Path);
	}

	private readonly record struct DeleteCandidate(string Path, bool IsDirectory);
}
