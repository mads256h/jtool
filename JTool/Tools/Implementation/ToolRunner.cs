using Spectre.Console;

namespace JTool.Tools.Implementation
{
    internal class ToolRunner
    {
        private readonly ITool[] _tools;
        private readonly Dictionary<string, ITool> _toolDictionary;

        public ToolRunner(IEnumerable<ITool> tools)
        {
            _tools = [.. tools.OrderBy(t => t.Name)];

            var toolsDictionary = new Dictionary<string, ITool>();

            foreach (var tool in _tools)
            {
                toolsDictionary.Add(tool.Name, tool);
                foreach (var alias in tool.Aliases)
                {
                    toolsDictionary.Add(alias, tool);
                }
            }

            _toolDictionary = toolsDictionary;
        }

        public async Task Run(string toolName, string[] args)
        {
            if (!_toolDictionary.TryGetValue(toolName, out var tool))
            {
                // TODO: Add tool name to exception
                throw new InvalidToolException();
            }

            if (args.Length != tool.Arguments.Count)
            {
                // TODO: Add Invalid Arguments count exception
                throw new InvalidToolException();
            }

            await tool.ExecuteAsync(args);
        }

        public async Task RunInteractively(string toolName)
        {
            if (!_toolDictionary.TryGetValue(toolName, out var tool))
            {
                // TODO: Add tool name to exception
                throw new InvalidToolException();
            }

            var args = new string[tool.Arguments.Count];

            for (var i = 0; i < tool.Arguments.Count; i++)
            {
                var argument = tool.Arguments[i];
                args[i] = AnsiConsole.Ask<string>($"{argument.Name}:");
            }

            await Run(toolName, args);
        }

        public void PrintTools(bool withArguments)
        {
            foreach (var tool in _tools)
            {
                Console.Write($"{tool.Name} ");

                if (tool.Aliases.Count != 0)
                {
                    var aliases = string.Join('|', tool.Aliases);
                    Console.WriteLine($" ({aliases}) ");
                }

                if (withArguments && tool.Arguments.Any())
                {
                    var arguments = string.Join(' ', tool.Arguments.Select(a => $"<{a.Name}>"));
                    Console.WriteLine($" {arguments}");
                }

                Console.WriteLine($"    {tool.Description}");

                if (withArguments)
                {
                    foreach (var argument in tool.Arguments)
                    {
                        Console.WriteLine($"    - {argument.Name} - {argument.Description}");
                    }
                }
            }
        }
    }
}
