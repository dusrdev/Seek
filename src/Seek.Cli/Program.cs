using ConsoleAppFramework;

using Seek.Cli;
ConsoleApp.Version = "1.1.1";

var app = ConsoleApp.Create();
app.UseFilter<GlobalExceptionHandler>();
app.Add("", Commands.SearchAsync);
app.Add("delete", Commands.DeleteAsync);
app.Add("check-for-updates", Commands.CheckForUpdatesAsync);

await app.RunAsync(args);
