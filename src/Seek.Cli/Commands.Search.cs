using ConsoleAppFramework;

using PrettyConsole;

using Seek.Core;

namespace Seek.Cli;

internal static partial class Commands {
	/// <summary>
	/// Seek is a fast filesystem search tool for files and directories by David Shnayder (@dusrdev).
	/// </summary>
	/// <param name="query">Search query</param>
	/// <param name="regex">-r, Treat the query as regex pattern</param>
	/// <param name="caseSensitive">Perform a case sensitive search</param>
	/// <param name="plain">-p, Disable matching section highlight</param>
	/// <param name="null">Emit machine-readable NUL-terminated plain paths for safe piping</param>
	/// <param name="hidden">-h, Include hidden files and folders</param>
	/// <param name="system">-s, Include system files and folders</param>
	/// <param name="files">-f, Match only against files</param>
	/// <param name="directories">-d, Match only against directories</param>
	/// <param name="root">The root path from which to scan</param>
	/// <param name="highlightColor">-c, Choose the matching section highlight color</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<int> SearchAsync(
		[Argument] string query,
		bool regex,
		bool caseSensitive,
		bool plain,
		bool @null,
		bool hidden,
		bool system,
		bool files,
		bool directories,
		string root = ".",
		ConsoleColor highlightColor = ConsoleColor.Green,
		CancellationToken cancellationToken = default) {
		SearchType type = (files, directories) switch {
			(true, false) => SearchType.Files,
			(false, true) => SearchType.Directories,
			_ => SearchType.FilesAndDirectories
		};

		var search = new FileSystemSearch(new SearchOptions {
			Query = query,
			Root = root,
			Regex = regex,
			CaseSensitive = caseSensitive,
			AttributesToSkip = GetAttributesToSkip(hidden, system),
			SearchType = type
		});

		Action<SearchMatch, ConsoleColor> outputHandler = (@null, plain) switch {
			(true, _) => WritePlainNullTerminated,
			(false, true) => WritePlain,
			_ => WriteRegular
		};

		await foreach (var searchMatch in search.SearchAsync(cancellationToken).ConfigureAwait(false)) {
			outputHandler(searchMatch, highlightColor);
		}

		return 0;
	}

	private static void WriteRegular(SearchMatch searchMatch, ConsoleColor highlightColor) {
		ReadOnlySpan<char> path = searchMatch.Path;

		foreach (var range in searchMatch.Sections) {
			ConsoleColor color = range.IsMatch ? highlightColor : ConsoleColor.DefaultForeground;
			var slice = path.Slice(range.Start, range.Length);
			Console.WriteInterpolated($"{color}{slice}");
		}
		Console.NewLine();
	}

	private static void WritePlain(SearchMatch searchMatch, ConsoleColor highlightColor) {
		Console.WriteLine(searchMatch.Path.AsSpan(), OutputPipe.Out);
	}

	private static void WritePlainNullTerminated(SearchMatch searchMatch, ConsoleColor highlightColor) {
		Console.WriteInterpolated($"{searchMatch.Path}\0");
	}

	private static FileAttributes GetAttributesToSkip(bool hidden, bool system) {
		FileAttributes attributesToSkip = FileAttributes.ReparsePoint;

		if (!hidden) {
			attributesToSkip |= FileAttributes.Hidden;
		}

		if (!system) {
			attributesToSkip |= FileAttributes.System;
		}

		return attributesToSkip;
	}
}
