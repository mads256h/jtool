using JetBrains.Annotations;

namespace JTool.Tools.Implementation
{
    [UsedImplicitly]
    internal interface ITool
    {
        string Name { get; }

        IReadOnlyCollection<string> Aliases { get; }

        string Description { get; }

        IReadOnlyList<ToolArgument> Arguments { get; }

        Task ExecuteAsync(string[] args);
    }
}
