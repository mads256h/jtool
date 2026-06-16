using Spectre.Console;

namespace JTool.Services
{
    internal static class ConsoleService
    {
        public static void LogHint(string value)
        {
            AnsiConsole.MarkupLineInterpolated($"[LightSlateGrey]HINT: {value}[/]");
        }

        public static void LogInfo(string value)
        {
            AnsiConsole.WriteLine(value);
        }

        public static void LogWarning(string value)
        {
            AnsiConsole.MarkupLineInterpolated($"[orange]{value}[/]");
        }

        public static void LogError(string value)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{value}[/]");
        }
    }
}
