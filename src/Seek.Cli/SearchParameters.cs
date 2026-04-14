using ConsoleAppFramework;

namespace Seek.Cli;

/// <summary>
/// Common search based parameters
/// </summary>
/// <param name="Query">Search query</param>
/// <param name="Regex">-r, Treat the query as regex pattern</param>
/// <param name="CaseSensitive">Perform a case sensitive search</param>
/// <param name="Hidden">-h, Include hidden files and folders</param>
/// <param name="System">-s, Include system files and folders</param>
/// <param name="Files">-f, Match only against files</param>
/// <param name="Directories">-d, Match only against directories</param>
/// <param name="Root">The root path from which to scan</param>
internal sealed record SearchParameters(
	[Argument] string Query,
	bool Regex,
	bool CaseSensitive,
	bool Hidden,
	bool System,
	bool Files,
	bool Directories,
	string Root = "."
);