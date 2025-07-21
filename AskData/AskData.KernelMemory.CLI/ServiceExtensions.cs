using AskData.KernelMemory.CLI.DataProcessor;
using Microsoft.Extensions.DependencyInjection;

namespace AskData.KernelMemory.CLI;

internal static class ServiceExtensions
{    public static IServiceCollection AddServices(
        this IServiceCollection services, KMConfig config)
    {
        services.AddSingleton<Indexer>();

        services.AddTransient<PlainTextProcessor>();
        services.AddTransient<IContentProcessor, PlainTextProcessor>();

        services.AddTransient<GitRepoFileProcessor>();
        services.AddTransient<IContentProcessor, GitRepoFileProcessor>();

        services.AddTransient<WhisperTranscriptProcessor>();
        services.AddTransient<IContentProcessor, WhisperTranscriptProcessor>();

        services.AddTransient<StuffYouShouldKnowDataProcessor>();
        services.AddTransient<IContentProcessor, StuffYouShouldKnowDataProcessor>();

        services.AddTransient<MarkdownRefResolver>();

        services.AddKernelMemory(config);

        return services;
    }
}
