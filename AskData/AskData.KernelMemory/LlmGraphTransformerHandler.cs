using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace AskData.KernelMemory;

public class LlmGraphTransformerHandler(
    string stepName,
    IPipelineOrchestrator orchestrator,
    LoggerFactory? loggerFactory = null
    //ILogger<LlmGraphTransformerHandler> logger
    )
    : IHostedService, IPipelineStepHandler
{
    /// <inheritdoc />
    public string StepName => stepName;

    private readonly ILogger logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LlmGraphTransformerHandler>();

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        /* ... your custom ...
         * ... handler ...
         * ... business logic ... */

        logger.LogInformation("Running handler {0}...", GetType().FullName);

        // Remove this - here only to avoid build errors
        await Task.Delay(0, cancellationToken).ConfigureAwait(false);

        return (ReturnType.Success, pipeline);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting handler {Handler}...", GetType().FullName);
        return orchestrator.AddHandlerAsync(this, cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping handler {Handler}...", GetType().FullName);
        return orchestrator.StopAllPipelinesAsync();
    }
}
