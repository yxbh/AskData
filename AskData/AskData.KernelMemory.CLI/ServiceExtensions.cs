using AskData.KernelMemory.CLI.DataProcessor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;

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
                .WithSimpleFileStorage(
                    new Microsoft.KernelMemory.DocumentStorage.DevTools.SimpleFileStorageConfig()
                    {
                        StorageType = Microsoft.KernelMemory.FileSystem.DevTools.FileSystemTypes.Disk,
                        Directory = config.FileStorageDirectory,
                    })
                ;

            if (config.UseQdrant)
            {
                builder.WithQdrantMemoryDb("http://127.0.0.1:6333");
            }
            else
            {
                builder.WithSimpleVectorDb(
                    new Microsoft.KernelMemory.MemoryStorage.DevTools.SimpleVectorDbConfig()
                    {
                        StorageType = Microsoft.KernelMemory.FileSystem.DevTools.FileSystemTypes.Disk,
                        Directory = config.VectorStorageDirectory,
                    });
            }

            // TODO: support image OCR

        });

        return services;
    }
}
