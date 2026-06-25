using System.Globalization;
using System.Text.RegularExpressions;
using Atlassian.Jira;
using JTool.Config;
using JTool.Services;
using JTool.Tools.Implementation;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace JTool.Tools
{
    internal partial class CreateSubtasksTool(Jira jira, IOptions<JiraConfig> jiraOptions, IOptions<CreateSubtasksTool.Config> toolOptions) : ITool
    {
        [GeneratedRegex(@"^(.+):\s*(\d+)\s*(timer?|hours?|h|t)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex SubtaskRegex { get; }

        /// <inheritdoc />
        public string Name { get; } = "create-subtasks";

        /// <inheritdoc />
        public IReadOnlyCollection<string> Aliases { get; } = [
            "cs"
            ];

        /// <inheritdoc />
        public string Description { get; } = "Create subtasks from PBI description";

        /// <inheritdoc />
        public IReadOnlyList<ToolArgument> Arguments { get; } =
        [
            new ToolArgument("issueKey", "The id of the PBI to create subtasks for (e.g. SER-1234)")
        ];

        /// <inheritdoc />
        public async Task ExecuteAsync(string[] args)
        {
            var issueKey = args[0];

            var (issue, extractedSubtasks) = await AnsiConsole.Status()
                .StartAsync($"Fetching Issue {issueKey}", async ctx =>
                {
                    var issue = await jira.Issues.GetIssueAsync(issueKey);

                    ValidateIssue(issue);

                    ConsoleService.LogInfo($"Summary: {issue.Summary}");

                    var extractedSubtasks = ExtractSubtasks(issue);

                    return (issue, extractedSubtasks);
                });

            ConsoleService.LogInfo("Extracted the following subtasks:");
            foreach (var extractedSubtask in extractedSubtasks)
            {
                ConsoleService.LogInfo($"{extractedSubtask.Name}: {extractedSubtask.Hours} hours");
            }

            var total = extractedSubtasks.Sum(s => decimal.Parse(s.Hours, CultureInfo.InvariantCulture));
            ConsoleService.LogInfo($"Total: {total}h");
            if (!await AnsiConsole.ConfirmAsync("Is this correct?"))
            {
                Environment.Exit(0);
            }

            var subtasks = await FetchSubtasks(issue);

            var subtasksToModify = await CreatePlaceholderSubtasksIfNeeded(issueKey, extractedSubtasks, subtasks, issue);
            await ModifySubtasks(extractedSubtasks, subtasksToModify, issue);

            await AnsiConsole.Status()
                .StartAsync("Setting Original Estimate to 0h", async ctx =>
                {
                    ctx.Status("Setting Original Estimate to 0h");
                    await issue.SetOriginalEstimateAsync("0h");
                    await issue.SetTimeRemainingAsync("0h");
                });
        }

        private void ValidateIssue(Issue issue)
        {
            var allowedTypes = toolOptions.Value.AllowedTypes;
            if (allowedTypes.Contains(issue.Type.Name))
            {
                return;
            }

            ConsoleService.LogError($"The issue type \"{issue.Type.Name}\" is not allowed.");
            ConsoleService.LogHint($"Allowed types: {string.Join(", ", allowedTypes.Order())}");
            Environment.Exit(1);
        }

        private async Task ModifySubtasks(List<ExtractedSubtask> extractedSubtasks, List<Issue> subtasksToModify, Issue issue)
        {
            await AnsiConsole.Progress()
                .Columns([
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn()
                ])
                .StartAsync(async ctx =>
                {
                    var subtasks = extractedSubtasks.Zip(subtasksToModify).ToList();
                    var subtaskWorkflow = toolOptions.Value.SubtaskWorkflow;
                    var task = ctx.AddTask("Modifying subtasks", maxValue: subtasks.Count);
                    task.StartTask();
                    foreach (var (extractedSubtask, subtask) in subtasks)
                    {
                        task.Description = $"Modifying subtask '{extractedSubtask.Name}'";
                        subtask.Summary = extractedSubtask.Name;

                        await subtask.SaveChangesAsync();
                        await subtask.SetOriginalEstimateAsync($"{extractedSubtask.Hours}h");
                        await subtask.AssignAsync(issue.AssigneeUser.AccountId);

                        if (subtask.Status.Name.Equals(subtaskWorkflow.FromStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            await subtask.WorkflowTransitionAsync(subtaskWorkflow.TransitionName);
                        }

                        task.Increment(1);
                    }

                    task.StopTask();
                });
        }

        private async Task<List<Issue>> CreatePlaceholderSubtasksIfNeeded(string issueKey, List<ExtractedSubtask> extractedSubtasks, List<Issue> subtasks, Issue issue)
        {
            var subtasksToModify = subtasks.ToList();

            var subtasksToCreate = extractedSubtasks.Count - subtasks.Count;
            if (subtasksToCreate > 0)
            {
                await AnsiConsole.Progress()
                    .Columns([
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                    ])
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Creating subtasks", maxValue: subtasksToCreate);
                        task.StartTask();
                        for (var i = 0; i < subtasksToCreate; i++)
                        {
                            var subtask = jira.CreateIssue(new CreateIssueFields(issue.Project)
                            {
                                ParentIssueKey = issueKey,
                            });
                            subtask.Type = new IssueType(jiraOptions.Value.SubTaskId, isSubTask: true);
                            subtask.Summary = $"INVALID TODO DELETE ME - {i}";
                            subtask.Assignee = issue.Assignee;
                            await subtask.SaveChangesAsync();

                            subtasksToModify.Add(subtask);
                            task.Value = i;
                        }

                        task.StopTask();
                    });
            }

            return subtasksToModify;
        }

        private static async Task<List<Issue>> FetchSubtasks(Issue issue)
        {
            return await AnsiConsole.Status()
                .StartAsync("Fetching subtasks", async ctx =>
                {
                    return (await issue.GetSubTasksAsync()).OrderBy(i => i.JiraIdentifier).ToList();
                });
        }

        private static List<ExtractedSubtask> ExtractSubtasks(Issue issue)
        {
            var subtasks = new List<ExtractedSubtask>();

            var lines = issue.Description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var subtaskLines = lines
                .SkipWhile(l => !l.Contains("h3. Estimatnedbrydning", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .Where(l => !l.Contains("total:", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Trim());

            foreach (var subtaskLine in subtaskLines)
            {
                var match = SubtaskRegex.Match(subtaskLine);
                if (!match.Success)
                {
                    ConsoleService.LogWarning($"Line \"{subtaskLine}\" does not match pattern. Ignoring!");
                    continue;
                }

                var name = match.Groups[1].Value.Trim();
                var hours = match.Groups[2].Value;

                subtasks.Add(new ExtractedSubtask(name, hours));
            }

            return subtasks;
        }

        private record ExtractedSubtask(string Name, string Hours);

        public class Config
        {
            public IReadOnlySet<string> AllowedTypes { get; set; } = new HashSet<string>();

            public SubtaskWorkflowConfig SubtaskWorkflow { get; set; } = new SubtaskWorkflowConfig();

            public class SubtaskWorkflowConfig
            {
                public string FromStatus { get; set; } = string.Empty;

                public string TransitionName { get; set; } = string.Empty;
            }
        }
    }
}
