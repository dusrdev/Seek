using Seek.Core;

namespace Seek.Cli;

internal static partial class Commands {
	private static FileSystemSearch CreateFileSystemSearch(
		string query,
		bool regex,
		bool caseSensitive,
		bool hidden,
		bool system,
		bool files,
		bool directories,
		string root,
		CancellationToken cancellationToken) {
		return new FileSystemSearch(new SearchOptions {
			Query = query,
			Root = root,
			Regex = regex,
			CaseSensitive = caseSensitive,
			AttributesToSkip = GetAttributesToSkip(hidden, system),
			SearchType = GetSearchType(files, directories)
		}, cancellationToken);
	}

	private static SearchType GetSearchType(bool files, bool directories) {
		return (files, directories) switch {
			(true, false) => SearchType.Files,
			(false, true) => SearchType.Directories,
			_ => SearchType.FilesAndDirectories
		};
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
