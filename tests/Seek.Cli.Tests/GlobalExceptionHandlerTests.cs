using ConsoleAppFramework;

using PrettyConsole;

namespace Seek.Cli.Tests;

public sealed class GlobalExceptionHandlerTests {
    [Test]
    [NotInParallel("ConsoleContext")]
    public async Task InvokeAsync_Cancellation_WritesCancelMessageAndSetsExitCodeZero(CancellationToken cancellationToken) {
        var originalOut = ConsoleContext.Out;
        var originalError = ConsoleContext.Error;
        var originalExitCode = Environment.ExitCode;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try {
            ConsoleContext.Out = stdout;
            ConsoleContext.Error = stderr;
            Environment.ExitCode = 123;

            var handler = new GlobalExceptionHandler(new CancelingFilter());

            await handler.InvokeAsync(context: null!, cancellationToken);

            await Assert.That(stdout.ToString()).Contains("Operation was canceled.");
            await Assert.That(stderr.ToString()).IsEmpty();
            await Assert.That(Environment.ExitCode).IsEqualTo(0);
        } finally {
            ConsoleContext.Out = originalOut;
            ConsoleContext.Error = originalError;
            Environment.ExitCode = originalExitCode;
        }
    }

    private sealed class CancelingFilter() : ConsoleAppFilter(new TerminalFilter()) {
        public override Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken) {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class TerminalFilter() : ConsoleAppFilter(null!) {
        public override Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }
    }
}
