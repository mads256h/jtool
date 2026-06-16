using System.Text;
using Atlassian.Jira;
using JTool.Config;
using JTool.Services;
using JTool.Services.Extensions;
using JTool.Tools;
using JTool.Tools.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace JTool
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<JiraConfig>(configuration.GetRequiredSection("Jira"));
            services.Configure<CreateSubtasksTool.Config>(configuration.GetRequiredSection("Tools").GetRequiredSection(nameof(CreateSubtasksTool)));

            services.AddSingleton<Jira>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<JiraConfig>>().Value;
                var jiraRestSettings = new JiraRestClientSettings
                {
                    EnableUserPrivacyMode = true,
                };
                return Jira.CreateRestClient(config.BaseUrl, config.Username, config.ApiToken, jiraRestSettings);
            });
            services.AddSingleton<ToolRunner>();

            services.RegisterTools();

            var provider = services.BuildServiceProvider();

            var toolRunner = provider.GetRequiredService<ToolRunner>();

            if (args.Length == 0)
            {
                toolRunner.PrintTools(withArguments: false);
                var toolName = await AnsiConsole.AskAsync<string>("Tool:");
                await toolRunner.RunInteractively(toolName);
                return;
            }

            if (args.Length == 1 && (args[0] == "help" || args[0] == "h" || args[0] == "?"))
            {
                ConsoleService.LogInfo("Usage: jtool <tool> <tool args>");
                toolRunner.PrintTools(withArguments: true);
                return;
            }

            await toolRunner.Run(args[0], args[1..]);
        }
    }
}
