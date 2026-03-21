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
	/// <param name="absolute">Emit absolute paths instead of paths relative to the selected root</param>
	/// <param name="null">Emit machine-readable NUL-terminated paths for safe piping, implies --plain and --absolute</param>
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
		bool absolute,
		bool @null,
		bool hidden,
		bool system,
		bool files,
		bool directories,
		string root = ".",
		ConsoleColor highlightColor = ConsoleColor.Green,
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

		Action<SearchMatch, bool, ConsoleColor> outputHandler = (@null, plain) switch {
			(true, _) => WritePlainNullTerminated,
			(false, true) => WritePlain,
			_ => WriteRegular
		};

		await foreach (var searchMatch in search.SearchAsync().ConfigureAwait(false)) {
			outputHandler(searchMatch, absolute, highlightColor);
		}

		return 0;
	}

	private static void WriteRegular(SearchMatch searchMatch, bool absolute, ConsoleColor highlightColor) {
		ReadOnlySpan<char> path; int offset;
		if (!absolute) {
			path = searchMatch.Path.AsSpan(searchMatch.RelativePathOffset);
			offset = 0;
		} else {
			path = searchMatch.Path.AsSpan();
			offset = searchMatch.RelativePathOffset;
			var slice = path.Slice(0, offset);
			Console.Write(slice, OutputPipe.Out);
		}

		foreach (var range in searchMatch.Sections) {
			ConsoleColor color = range.IsMatch ? highlightColor : ConsoleColor.DefaultForeground;
			var slice = path.Slice(offset + range.Start, range.Length);
			Console.WriteInterpolated($"{color}{slice}");
		}
		Console.NewLine();
	}

	private static void WritePlain(SearchMatch searchMatch, bool absolute, ConsoleColor highlightColor) {
		var path = !absolute ? searchMatch.Path.AsSpan(searchMatch.RelativePathOffset) : searchMatch.Path.AsSpan();

		Console.WriteLine(path, OutputPipe.Out);
	}

	private static void WritePlainNullTerminated(SearchMatch searchMatch, bool absolute, ConsoleColor highlightColor) {
		var path = searchMatch.Path.AsSpan();

		Console.WriteInterpolated($"{path}\0");
	}
}
