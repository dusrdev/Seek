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
	/// <param name="noProgress">Disable progress reporting</param>
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
		bool noProgress,
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
		List<SearchMatch> collapsedCandidates = await CollectDeleteCandidatesAsync(search.SearchAsync(), cancellationToken).ConfigureAwait(false);

		if (collapsedCandidates.Count == 0) return 0;

		if (!apply) {
			foreach (var candidate in collapsedCandidates) {
				WriteRegular(candidate, true);
			}

			Console.NewLine(OutputPipe.Out);
			Console.WriteLineInterpolated($"No changes were made. Re-run with {Markup.Bold}{CliPalette.Warning}--apply{Color.Default}{Markup.ResetBold} to delete these entries.");
			return 0;
		}

		var hadFailure = false;

		using var region = new LiveConsoleRegion(OutputPipe.Out);
		int prgLength = CalculateProgressWidth();

		var count = collapsedCandidates.Count;
		double denominator = (double)count / 100;
		for (var i = 0; i < count; i++) {
			SearchMatch candidate = collapsedCandidates[i];
			try {
				ApplyDeleteCandidate(candidate);
				region.WriteLine($"{CliPalette.Success}OK{Color.Default}   {candidate.Path}");
			} catch (Exception exception) when (exception is not OperationCanceledException) {
				hadFailure = true;
				region.WriteLine($"{CliPalette.Danger}FAIL{Color.Default} {candidate.Path} - {exception.Message}");
			}
			if (!noProgress) {
				region.RenderProgress(i / denominator, (builder, out handler) => {
					handler = builder.Build($"Deleting {i} / {count}");
				}, progressColor: CliPalette.Accent, maxLineWidth: prgLength);
			}
		}

		return hadFailure ? 1 : 0;
	}

	private static async Task<List<SearchMatch>> CollectDeleteCandidatesAsync(
		IAsyncEnumerable<SearchMatch> matches,
		CancellationToken cancellationToken) {
		List<SearchMatch> candidates = [];

		await foreach (var searchMatch in matches.WithCancellation(cancellationToken).ConfigureAwait(false)) {
			candidates.Add(searchMatch);
		}

		return CollapseDescendantCandidates(candidates);
	}

	private static List<SearchMatch> CollapseDescendantCandidates(List<SearchMatch> candidates) {
		candidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

		List<SearchMatch> collapsedCandidates = [];
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

	private static int CalculateProgressWidth() {
		try {
			return (int)(Console.BufferWidth * 0.6);
		} catch {
			return 60;
		}
	}

	private static void ApplyDeleteCandidate(SearchMatch candidate) {
		if (candidate.IsDirectory) {
			Directory.Delete(candidate.Path, recursive: true);
			return;
		}

		File.Delete(candidate.Path);
	}
}
