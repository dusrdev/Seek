using System.Net;
using System.Text.Json;

using ConsoleAppFramework;

using PrettyConsole;

namespace Seek.Cli;

internal static partial class Commands {
	/// <summary>
	/// Check whether a new version is available
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<int> CheckForUpdatesAsync(CancellationToken cancellationToken = default) {
		const string packageId = "Seek";
		string consoleAppVersion = ConsoleApp.Version ?? string.Empty;

		if (!Version.TryParse(consoleAppVersion, out var currentVersion)) {
			throw new ArgumentException("Current app version is invalid", nameof(consoleAppVersion));
		}

		var nugetVersion = await GetLatestPublishedVersionAsync(packageId, cancellationToken).ConfigureAwait(false);

		if (nugetVersion > currentVersion) {
			Console.WriteLineInterpolated(
				$"""
				Your version ({ConsoleColor.Red}{consoleAppVersion}{ConsoleColor.DefaultForeground}) is out of date. Version {ConsoleColor.Green}{nugetVersion}{ConsoleColor.DefaultForeground} is available!

				Update from Nuget:
				{ConsoleColor.Yellow}dotnet tool update seek{ConsoleColor.Default}
				or
				{ConsoleColor.Yellow}dotnet tool update --global seek{ConsoleColor.DefaultForeground} (if installed globally)

				Download from GitHub releases:
				{Markup.Underline}{ConsoleColor.Yellow}https://github.com/dusrdev/Seek/releases/latest{ConsoleColor.DefaultForeground}{Markup.ResetUnderline}
				"""
				);
		} else {
			Console.WriteLineInterpolated($"Your version is up-to-date.");
		}

		return 0;
	}

	private static async Task<Version> GetLatestPublishedVersionAsync(
		string packageId,
		CancellationToken cancellationToken) {
		using var httpClient = new HttpClient() {
			Timeout = TimeSpan.FromSeconds(30)
		};

		var packageIndexUri = new Uri($"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json", UriKind.Absolute);

		using var response = await httpClient.GetAsync(packageIndexUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NotFound) {
			throw new InvalidOperationException($"Package '{packageId}' was not found on NuGet.");
		}

		response.EnsureSuccessStatusCode();

		await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

		if (!document.RootElement.TryGetProperty("versions", out var versionsElement) || versionsElement.ValueKind != JsonValueKind.Array) {
			throw new InvalidOperationException($"NuGet returned an invalid versions payload for package '{packageId}'.");
		}

		var lastVersionElement = versionsElement.EnumerateArray().Last();
		var version = lastVersionElement.GetString();

		if (!Version.TryParse(version, out var nugetVersion)) {
			throw new ArgumentException("Failed to parse last version from nuget", nameof(version));
		}

		return nugetVersion;
	}
}