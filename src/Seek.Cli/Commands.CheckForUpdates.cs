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
				Your version ({Color.Red}{consoleAppVersion}{Color.Default}) is out of date. Version {Color.Green}{nugetVersion}{Color.Default} is available!

				Update from:
				NUGET  -> {Color.Yellow}dotnet tool update seek{Color.Default}
				GITHUB -> {Markup.Underline}{Color.Yellow}https://github.com/dusrdev/Seek/releases/latest{Color.Default}{Markup.ResetUnderline}
				WINGET -> {Color.Yellow}winget update dusrdev.Seek{Color.Default}
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