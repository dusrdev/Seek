using System.ComponentModel.DataAnnotations;

using ConsoleAppFramework;

using PrettyConsole;

namespace Seek.Cli;

internal sealed class GlobalExceptionHandler(ConsoleAppFilter next) : ConsoleAppFilter(next) {
	public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken) {
		try {
			await Next.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
		} catch (Exception e) when (e is ValidationException or ArgumentParseFailedException) {
			throw;
		} catch (Exception e) when (e is TaskCanceledException or OperationCanceledException) {
			Console.WriteLineInterpolated($"{Color.Yellow}Operation was canceled.");
			Environment.ExitCode = 0;
		} catch (Exception exception) {
			Console.WriteLineInterpolated(OutputPipe.Error, $"{Color.Red}{exception.Message}");
			Environment.ExitCode = 1;
		}
	}
}