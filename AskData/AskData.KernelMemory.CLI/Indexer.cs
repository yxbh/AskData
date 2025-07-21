using AskData.KernelMemory.CLI.DataProcessor;
using AskData.KernelMemory.Graph;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DocumentStorage;
using Microsoft.KernelMemory.MemoryStorage;
using System.Security.Cryptography;

namespace AskData.KernelMemory.CLI;

internal class Indexer(
    IKernelMemory memory,
    IMemoryDb memoryDb,
    IDocumentStorage storage,
    IOptions<KMConfig> configOptions,
    MarkdownRefResolver markdownRefResolver,
    LlmGraphTransformerHandler llmGraphTransformerHandler,
    IServiceProvider serviceProvider,
    ILogger<Indexer> logger
    )
{
    public async Task RunIndexAsync(
        List<ContentSourceConfig> contentSources,
        bool overwrite,
        CancellationToken cancellationToken = default
    )
    {
        if (memory is MemoryServerless memoryServerless)
        {
            memoryServerless.Orchestrator.AddHandler(llmGraphTransformerHandler);
        }
        else
        {
            throw new NotImplementedException($"Support for IKernelMemory implementation other than {nameof(MemoryServerless)} is not implemented.");
        }

        // Get list of IContentProcessor from DI container mapped by SupportedContentType
        var contentProcessors = serviceProvider.GetServices<IContentProcessor>()
            .ToDictionary(p => p.SupportedContentType, p => p);

        logger.LogInformation("Starting indexing with {ContentProcessorCount} content processors.", contentProcessors.Count);

        // For each content source, find the appropriate processor and process the content.
        var fileMetadataCollection = new List<FileMetadataModel>();
        foreach (var contentSourceConfig in contentSources)
        {
            if (!contentProcessors.TryGetValue(contentSourceConfig.ContentType, out var contentProcessor))
            {
                logger.LogWarning("No processor found for content type '{ContentType}'. Skipping.", contentSourceConfig.ContentType);
                continue;
            }

            try
            {
                logger.LogInformation("Processing content source: {Directory} ({ContentType})", contentSourceConfig.Directory, contentSourceConfig.ContentType);

                if (!Directory.Exists(contentSourceConfig.Directory))
                {
                    throw new DirectoryNotFoundException($"Content source directory does not exist: {contentSourceConfig.Directory}");
                }

                var fileMetadataCollectionTemp = await contentProcessor.ProcessAsync(contentSourceConfig, cancellationToken).ConfigureAwait(false);
                fileMetadataCollection.AddRange(fileMetadataCollectionTemp);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing content source {Directory}: {ErrorMessage}", contentSourceConfig.Directory, ex.Message);
            }            
        }

        await markdownRefResolver.ResolveAsync(fileMetadataCollection, cancellationToken).ConfigureAwait(false);

        //
        // Delete documents that are not in the input list.
        //
        var knownMemoryRecords = await memoryDb
            .GetListAsync(configOptions.Value.IndexName, limit: -1, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentlyIndexedDocIds = knownMemoryRecords.Select(x => x.GetDocumentId()).ToHashSet();
        var inputDocIds = fileMetadataCollection.Select(x => x.FlattenName).ToHashSet();
        var docIdsToDelete = currentlyIndexedDocIds.Except(inputDocIds).ToList();
        if (docIdsToDelete.Count > 0)
        {
            logger.LogInformation("Deleting {DocumentCount} documents from the index.", docIdsToDelete.Count);
            foreach (var docId in docIdsToDelete)
            {
                logger.LogInformation("Deleting document with ID: {DocumentId}", docId);
                await memory.DeleteDocumentAsync(docId, index: configOptions.Value.IndexName, cancellationToken: cancellationToken).ConfigureAwait(false);

                await storage.DeleteDocumentDirectoryAsync(configOptions.Value.IndexName, docId, cancellationToken).ConfigureAwait(false);
            }
        }

        // Take all the processed files and index them into the memory
        if (fileMetadataCollection.Count == 0)
        {
            logger.LogWarning("No files processed. Exiting indexing.");
            return;
        }

        logger.LogInformation("Indexing {FileCount} files into memory.", fileMetadataCollection.Count);

        var idx = 0;
        foreach (var fileMetadata in fileMetadataCollection)
        {
            ++idx;

            var file = fileMetadata.OutputPath;

            if (!File.Exists(file))
            {
                logger.LogWarning("File not found, skipping for now: {FilePath}", file);
                continue;
            }

            var documentId = fileMetadata.FlattenName;

            // generate file hash for the file
            var fileHash = string.Empty;
            using (var stream = File.OpenRead(file))
            using (var sha256 = SHA256.Create())
            {
                var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
                fileHash = Convert.ToHexStringLower(hash);
            }

            var memoryFilter = new MemoryFilter();
            memoryFilter.ByDocument(documentId);
            var searchResult = await memory.SearchAsync(
                string.Empty,
                index: configOptions.Value.IndexName,
                filter: memoryFilter,
                cancellationToken: cancellationToken
                ).ConfigureAwait(false);

            var tags = new TagCollection
            {
                { "orginal_filename", Path.GetFileName(file) },
                { "orginal_filename_upper", Path.GetFileName(file).ToUpperInvariant() },
                { "orginal_relative_filepath", fileMetadata.LocalOriginalRelativeFilePath },
                { "orginal_relative_filepath_upper", fileMetadata.LocalOriginalRelativeFilePath.ToUpperInvariant() },
                { "orginal_filetype", Path.GetExtension(file) },
                { "orginal_filesize", new FileInfo(file).Length.ToString() },
                { "orginal_filedate", File.GetLastWriteTime(fileMetadata.LocalOriginalFullFilePath).ToString() },
                { "sha256", fileHash },
                { "remote_url", fileMetadata.Url },
                { "original_name", fileMetadata.OriginalName },
                { "original_name_upper", fileMetadata.OriginalName.ToUpperInvariant() },
                { "flatten_name", fileMetadata.FlattenName },
                { "title", fileMetadata.Title },
                { "title_upper", fileMetadata.Title.ToUpperInvariant() },
                { "source", fileMetadata.Source },
                { "local_original_full_filepath", fileMetadata.LocalOriginalFullFilePath },
                { "local_original_root_dir", fileMetadata.LocalOriginalRootDir },
                { "local_original_relative_filepath", fileMetadata.LocalOriginalRelativeFilePath },
                { "_output_path", fileMetadata.OutputPath },
            };

            foreach (var kvp in fileMetadata.ContentSourceMetadata)
            {
                tags.Add($"content_source_metadata__{kvp.Key}", kvp.Value);
                tags.Add($"content_source_metadata__{kvp.Key}__upper", kvp.Value.ToUpperInvariant());
            }

            if (!searchResult.NoResult)
            {
                var resultTags = searchResult.Results.First().Partitions.First().Tags;
                
                ///
                /// KM injects additonal tags, we need to compare only the ones we add/mod.
                ///
                var isTagsMatching = true;
                if (resultTags != null)
                {
                    // Check number of tags matches.
                    // Ignore tags that start with "__" as they are internal tags to KM.
                    if (resultTags.Where(t => !t.Key.StartsWith("__")).Count() != tags.Count)
                    {
                        isTagsMatching = false;
                    }

                    // Check if all tags match.
                    foreach (var tag in tags)
                    {
                        if (resultTags.TryGetValue(tag.Key, out var resultTagValues))
                        {
                            if (!tag.Value.SequenceEqual(resultTagValues))
                            {
                                isTagsMatching = false;
                                break;
                            }
                        }
                        else
                        {
                            isTagsMatching = false;
                            break;
                        }
                    }
                }
                else
                {
                    isTagsMatching = false;
                }

                // get all the sha256 hashes if available
                var sha256Hashes = searchResult.Results
                    .SelectMany(x => x.Partitions.Select(
                            p => p.Tags.ContainsKey("sha256") ? p.Tags["sha256"].FirstOrDefault() : string.Empty
                        ))
                    .Where(h => !string.IsNullOrEmpty(h))
                    .ToHashSet();

                if (sha256Hashes.Contains(fileHash) && isTagsMatching && !overwrite)
                {
                    logger.LogInformation("Document with the same hash already imported. Skipping: {DocumentId}", documentId);
                    continue;
                }
            }

            logger.LogInformation("Importing ({CurrentIndex}/{TotalFiles}) {FilePath} with document ID {DocumentId}", idx, fileMetadataCollection.Count, file, documentId);

            var steps = fileMetadata.GenerateSummary ? Constants.PipelineWithSummary : Constants.DefaultPipeline;
            if (fileMetadata.GenerateGraphTransform)
            {
                steps = [.. steps, "graph_transform"];
            }

            var context = new RequestContext();
            if (fileMetadata.GenerateSummary)
            {
                context.SetArg(
                    Constants.CustomContext.Summary.Prompt,
// original summarization prompt: https://github.com/microsoft/kernel-memory/blob/bd8d34e67dcd2b52acb408661d58b648453efbd3/service/Core/Prompts/summarize.txt
"""
[SUMMARIZATION RULES]
DON'T WASTE WORDS.
USE SHORT, CLEAR, COMPLETE SENTENCES.
DO NOT USE BULLET POINTS OR DASHES.
USE ACTIVE VOICE.
MAXIMIZE DETAIL, MEANING.
FOCUS ON THE CONTENT.
[END RULES]

[BANNED PHRASES]
This article
This document
This page
This material
[END LIST]

Summarize this:
Hello how are you?
+++++
Hello

Summarize this:
{{$input}}
+++++
"""
                );
                context.SetArg(
                    Constants.CustomContext.Summary.TargetTokenSize,
                    300  // Try to generate a token no longer than X tokens
                );
            }

            // Delete the document if it already exists, to ensure we have the latest version.
            await memory.DeleteDocumentAsync(
                documentId,
                index: configOptions.Value.IndexName,
                cancellationToken: cancellationToken
                ).ConfigureAwait(false);

            await memory.ImportDocumentAsync(
                file,
                documentId,
                tags,
                index: configOptions.Value.IndexName,
                context: context,
                steps: steps,
                cancellationToken: cancellationToken
                ).ConfigureAwait(false);
        }
    }
}
