using ConsoleAppFramework;

using PrettyConsole;

using Seek.Core;

namespace Seek.Cli;

internal static partial class Commands {
	/// <summary>
	/// Seek is a fast filesystem searcher made by David Shnayder (@dusrdev).
	///
	/// Search the filesystem for a query
	/// </summary>
	/// <param name="plain">-p, Disable matching section highlight</param>
	/// <param name="absolute">Emit absolute paths instead of paths relative to the selected root</param>
	/// <param name="null">Emit machine-readable NUL-terminated paths for safe piping, implies --plain and --absolute</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<int> SearchAsync(
		[AsParameters] SearchParameters @params,
		bool plain,
		bool absolute,
		bool @null,
		CancellationToken cancellationToken = default) {
		var search = CreateFileSystemSearch(
			@params.Query,
			@params.Regex,
			@params.CaseSensitive,
			@params.Hidden,
			@params.System,
			@params.Files,
			@params.Directories,
			@params.Root,
			cancellationToken);

		Action<SearchMatch, bool> outputHandler = (@null, plain) switch {
			(true, _) => WritePlainNullTerminated,
			(false, true) => WritePlain,
			_ => WriteRegular
		};

		await foreach (var searchMatch in search.SearchAsync().ConfigureAwait(false)) {
			outputHandler(searchMatch, absolute);
		}

		return 0;
	}

	/// <summary>
	/// Prints a search match with match highlight
	/// </summary>
	/// <param name="searchMatch">The <see cref="SearchMatch"/> to print</param>
	/// <param name="absolute">Whether to print as absolute path</param>
	internal static void WriteRegular(SearchMatch searchMatch, bool absolute) {
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
			AnsiToken color = range.IsMatch ? CliPalette.Accent : Color.DefaultForeground;
			var slice = path.Slice(offset + range.Start, range.Length);
			Console.WriteInterpolated($"{color}{slice}");
		}
		Console.NewLine();
	}

	private static void WritePlain(SearchMatch searchMatch, bool absolute) {
		var path = !absolute ? searchMatch.Path.AsSpan(searchMatch.RelativePathOffset) : searchMatch.Path.AsSpan();

		Console.WriteLine(path, OutputPipe.Out);
	}

	private static void WritePlainNullTerminated(SearchMatch searchMatch, bool absolute) {
		var path = searchMatch.Path.AsSpan();

		Console.WriteInterpolated($"{path}\0");
	}
}
