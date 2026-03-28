using PrettyConsole;

namespace Seek.Cli;

/// <summary>
/// Shared semantic colors for CLI output.
/// </summary>
/// <remarks>
/// These tokens are intentionally named by role instead of by command so the
/// search, delete, update, and error flows stay visually consistent. Adjust
/// the token assignments here to re-theme the CLI without touching command
/// handlers.
/// </remarks>
internal static class CliPalette {
	/// <summary>
	/// Accent color for match highlights and actionable update/install commands.
	/// </summary>
	public static readonly AnsiToken Accent = Color.Magenta;

	/// <summary>
	/// Positive outcome color for successful operations and healthy state.
	/// </summary>
	public static readonly AnsiToken Success = Color.Green;

	/// <summary>
	/// Caution color for previews, apply hints, and stale state.
	/// </summary>
	public static readonly AnsiToken Warning = Color.Yellow;

	/// <summary>
	/// Failure color for destructive errors and unsuccessful operations.
	/// </summary>
	public static readonly AnsiToken Danger = Color.Red;
}
