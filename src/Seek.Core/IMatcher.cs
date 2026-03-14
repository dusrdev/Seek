using System.Text.RegularExpressions;

namespace Seek.Core;

internal interface IMatcher {
	bool TryFindMatches(ReadOnlySpan<char> path, out Sections match);
}

internal sealed class RegexMatcher : IMatcher {
	private readonly Regex _regex;

	public RegexMatcher(string query, bool caseSensitive) {
		RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

		if (!caseSensitive) options |= RegexOptions.IgnoreCase;

		_regex = new Regex(query, options);
	}

	public bool TryFindMatches(ReadOnlySpan<char> path, out Sections match) {
		Sections? localMatch = null;
		var index = 0;

		foreach (var m in _regex.EnumerateMatches(path)) {
			localMatch ??= [];

			if (index < m.Index) {
				localMatch.Add(new MatchRanges(index, m.Index - index));
			}

			localMatch.Add(new MatchRanges(m.Index, m.Length, true));
			index = m.Index + m.Length;
		}

		if (localMatch is null) {
			match = Sections.None;
			return false;
		}

		if (index < path.Length) {
			localMatch.Add(new MatchRanges(index, path.Length - index));
		}

		match = localMatch;
		return true;
	}
}

internal sealed class ContainsMatcher : IMatcher {
	private readonly StringComparison _comparison;
	private readonly string _query;
	private readonly int _length;

	public ContainsMatcher(string query, bool caseSensitive) {
		_comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		_query = query;
		_length = _query.Length;
	}

	private static bool TryFindIndexOf(ReadOnlySpan<char> searchSpace, string query, StringComparison stringComparison, out int index) {
		index = searchSpace.IndexOf(query, stringComparison);
		return index != -1;
	}

	public bool TryFindMatches(ReadOnlySpan<char> path, out Sections match) {
		var originalLength = path.Length;
		var offset = 0;
		Sections? matches = null;

		while (path.Length > 0 && TryFindIndexOf(path, _query, _comparison, out var index)) {
			matches ??= [];

			if (index > 0) {
				matches.Add(new MatchRanges(offset, index));
			}

			matches.Add(new MatchRanges(offset + index, _length, true));

			var newStart = index + _length;
			path = path.Slice(newStart);
			offset += newStart;
		}

		if (matches is null) {
			match = Sections.None;
			return false;
		}

		if (offset < originalLength) {
			matches.Add(new MatchRanges(offset, originalLength - offset));
		}

		match = matches;
		return true;
	}
}

internal sealed record SearchMatch(string Path, Sections Sections);

internal sealed class Sections : List<MatchRanges> {
	public static readonly Sections None = new(0);

	public Sections() { }

	public Sections(int capacity) : base(capacity) { }
}

/// <summary>
/// Represents a slice that matches a query
/// </summary>
/// <param name="Source"></param>
/// <param name="Start"></param>
/// <param name="Length"></param>
/// <param name="IsMatch"></param>
internal readonly record struct MatchRanges(int Start, int Length, bool IsMatch = false);
