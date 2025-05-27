using AskData.KernelMemory.CLI.DataProcessor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MongoDB.Driver.Core.Configuration;

namespace AskData.KernelMemory.CLI;

internal static class ServiceExtensions
{    public static IServiceCollection AddServices(
        this IServiceCollection services, KMConfig config)
    {
        services.AddSingleton<Indexer>();

        services.AddTransient<WhisperTranscriptProcessor>();
        services.AddTransient<IContentProcessor, WhisperTranscriptProcessor>();

        services.AddKernelMemory<MemoryServerless>(builder =>
        {
            builder
                .WithOllamaTextGeneration(config.TextGenerationModelName)
                .WithOllamaTextEmbeddingGeneration(config.EmbeddingModelName)
                .WithQdrantMemoryDb("http://127.0.0.1:6333")
                .WithSimpleFileStorage(
                    new Microsoft.KernelMemory.DocumentStorage.DevTools.SimpleFileStorageConfig()
                    {
                        StorageType = Microsoft.KernelMemory.FileSystem.DevTools.FileSystemTypes.Disk,
                        Directory = config.FileStorageDirectory,
                    })
                ;
            // TODO: support image OCR

        });

        return services;
    }
}
