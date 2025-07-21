using AskData.KernelMemory.Graph;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;

namespace AskData.KernelMemory;

public static class ServiceExtensions
{    public static IServiceCollection AddKernelMemory(
        this IServiceCollection services, KMConfig config)
    {
        services.AddSingleton<LlmGraphTransformerHandler>(serviceProvider =>
        {
            return new LlmGraphTransformerHandler(
                "graph_transform",
                serviceProvider.GetRequiredService<IPipelineOrchestrator>(),
                serviceProvider.GetService<ILoggerFactory>()
            );
        });

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

            builder.WithContentDecoder<CustomContentDecoder>();

            builder.Services.AddTransient<MimeTypesDetection>();  // needed for CustomMimeTypesDetection
            builder.WithCustomMimeTypeDetection<CustomMimeTypesDetection>();
        });

        return services;
    }
}
