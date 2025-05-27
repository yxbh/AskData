using AskData.KernelMemory.CLI.DataProcessor;
using Microsoft.Extensions.DependencyInjection;

namespace AskData.KernelMemory.CLI;

internal static class ServiceExtensions
{    public static IServiceCollection AddServices(
        this IServiceCollection services, KMConfig config)
    {
        services.AddSingleton<Indexer>();

        services.AddTransient<WhisperTranscriptProcessor>();
        services.AddTransient<IContentProcessor, WhisperTranscriptProcessor>();

        services.AddKernelMemory(config);

        return services;
    }
}
