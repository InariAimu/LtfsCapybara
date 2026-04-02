using System.Reflection;

namespace LtfsServer.Features.AI.Tools;

public static class AiToolModuleRegistrationExtensions
{
    public static IServiceCollection AddAiToolModules(this IServiceCollection services, params Assembly[] assemblies)
    {
        var targetAssemblies = (assemblies is { Length: > 0 } ? assemblies : new[] { typeof(AiToolModuleRegistrationExtensions).Assembly })
            .Distinct()
            .ToArray();

        var moduleTypes = targetAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetCustomAttribute<AIToolModuleAttribute>() is not null)
            .Distinct()
            .ToArray();

        foreach (var moduleType in moduleTypes)
        {
            services.AddSingleton(moduleType);
        }

        return services;
    }
}