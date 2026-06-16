using System.Reflection;

using JTool.Tools.Implementation;

using Microsoft.Extensions.DependencyInjection;

namespace JTool.Services.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterTools(this IServiceCollection services)
        {
            var toolTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(ITool)) && t is { IsInterface: false, IsAbstract: false });

            foreach (var type in toolTypes)
            {
                services.AddTransient(type);
                services.AddTransient(typeof(ITool), sp => sp.GetRequiredService(type));
            }

            return services;
        }
    }
}
