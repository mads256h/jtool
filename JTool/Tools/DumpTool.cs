using Atlassian.Jira;

using JTool.Services;
using JTool.Tools.Implementation;

using Spectre.Console;

namespace JTool.Tools
{
    internal class DumpTool(Jira jira) : ITool
    {
        public string Name => "dump";

        public IReadOnlyCollection<string> Aliases => ["json"];

        public string Description => "Dumps the issue as json";

        public IReadOnlyList<ToolArgument> Arguments => [
            new ToolArgument("issueKey", "The issue to dump")
            ];

        public async Task ExecuteAsync(string[] args)
        {
            var issueKey = args[0];

            var issue = await FetchIssue(issueKey);

            ConsoleService.LogInfo(issue);
        }

        private async Task<string> FetchIssue(string issueKey)
        {
            return await AnsiConsole.Status()
                .StartAsync($"Fetching Issue {issueKey}", async ctx => await jira.Issues.GetIssueJsonAsync(issueKey));
        }
    }
}
