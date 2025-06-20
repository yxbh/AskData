using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using System.Text;

namespace AskData.KernelMemory;

public class LlmGraphTransformerHandler(
    string stepName,
    IPipelineOrchestrator orchestrator,
    ILoggerFactory? loggerFactory = null
    //ILogger<LlmGraphTransformerHandler> logger
    )
    : IPipelineStepHandler
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

        logger.LogInformation("Running handler {Handler}...", GetType().FullName);

        logger.LogDebug("Extracting text, pipeline '{Index}/{DocumentId}'", pipeline.Index, pipeline.DocumentId);

        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            if (uploadedFile.AlreadyProcessedBy(this))
            {
                logger.LogTrace("File {FileName} already processed by this handler", uploadedFile.Name);
                continue;
            }

            // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
            Dictionary<string, DataPipeline.GeneratedFileDetails> graphTransformFiles = [];

            foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
            {
                DataPipeline.GeneratedFileDetails partitionFile = generatedFile.Value;

                // Do graph transforms only for partitions (text chunks) and synthetic data
                if (partitionFile.ArtifactType is not DataPipeline.ArtifactTypes.TextPartition
                    and not DataPipeline.ArtifactTypes.SyntheticData)
                {
                    logger.LogTrace("Skipping file {FileName} (not a partition, not synthetic data)", partitionFile.Name);
                    continue;
                }

                // TODO: cost/perf: if the partition SHA256 is the same and the embedding exists, avoid generating it again
                switch (partitionFile.MimeType)
                {
                    case MimeTypes.PlainText:
                    case MimeTypes.MarkDown:
                        var partitionContent = await orchestrator.ReadTextFileAsync(pipeline, partitionFile.Name, cancellationToken).ConfigureAwait(false);

                        var (transformedContent, success) = await GraphTransformAsync(partitionContent, pipeline.GetContext(), cancellationToken).ConfigureAwait(false);
                        //logger.LogTrace("Transformed content for file {FileName} with {TransformedContentCount} items", partitionFile.Name, transformedContent.Count);

                        if (success)
                        {
                            var graphTransformText = transformedContent;
                            var graphTransform = new BinaryData(graphTransformText);
                            var destFile = uploadedFile.GetHandlerOutputFileName(this);
                            await orchestrator.WriteFileAsync(pipeline, destFile, graphTransform, cancellationToken).ConfigureAwait(false);

                            graphTransformFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                ParentId = uploadedFile.Id,
                                Name = destFile,
                                Size = graphTransformText.Length,
                                MimeType = MimeTypes.PlainText,
                                ArtifactType = DataPipeline.ArtifactTypes.SyntheticData,
                                Tags = pipeline.Tags.Clone().AddSyntheticTag("graph"),
                                ContentSHA256 = graphTransform.CalculateSHA256(),
                            });
                        }

                        break;

                    default:
                        logger.LogWarning("File {PartionFileName} cannot be used to generate embeddings, type not supported", partitionFile.Name);
                        continue;
                }
            }

            uploadedFile.MarkProcessedBy(this);
        }

        return (ReturnType.Success, pipeline);
    }

    private async Task<(string, bool)> GraphTransformAsync(string content, IContext context, CancellationToken cancellationToken)
    {
        ITextGenerator textGenerator = orchestrator.GetTextGenerator();
        int contentLength = textGenerator.CountTokens(content);
        logger.LogTrace("Size of the content to summarize: {ContentLength} tokens", contentLength);

        var graphTransformPrompt = """
            You are a top-tier algorithm designed for extracting information in structured formats to build a knowledge graph.
            Your task is to identify the entities and relations requested with the user prompt from a given text.
            You must generate the output in a JSON format containing a list with JSON objects.
            Each object should have the keys: "head", "head_type", "relation", "tail", and "tail_type".
            
            The "head_type" key must contain the type of the extracted head entity.
            The "relation" key must contain the type of relation between the "head" and the "tail".

            The "tail" key must represent the text of an extracted entity which is the tail of the relation.

            Attempt to extract as many entities and relations as you can.

            Maintain Entity Consistency: When extracting entities, it's vital to ensure consistency.

            If an entity, such as "John Doe", is mentioned multiple times in the text but is referred to by different names or pronouns e.g., "Joe", "he"), always use the most complete identifier for that entity.
            
            The knowledge graph should be coherent and easily understandable, so maintaining consistency in entity references is crucial.
            
            IMPORTANT NOTES:
            - Don't add any explanation and text.      
            
            Here is one example (do not include in output):
            ---
            Given the text: "Adam is a software engineer in Microsoft since 2009"
            You can extract a relationship in the following format:
            [{
              "head" "Adam",
              "head_type": "Person",
              "relation": "WORKS_FOR",
              "tail": "Microsoft",
              "tail_type": "Company"
            }]

            Analyze and extract the relationships as JSON from below text:
            ---
            {{$input}}
            """;

        var filledPrompt = graphTransformPrompt.Replace("{{$input}}", content, StringComparison.OrdinalIgnoreCase);
        var newContent = new StringBuilder();
        await foreach (var token in textGenerator.GenerateTextAsync(filledPrompt, new TextGenerationOptions(), cancellationToken).ConfigureAwait(false))
        {
            newContent.Append(token);
        }

        newContent.AppendLine();

        content = newContent.ToString();

        return (content, true); // Placeholder return, replace with actual graph transformation logic
    }
}
