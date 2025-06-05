using AskData.KernelMemory.CLI.DataProcessor;
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
        // Get list of IContentProcessor from DI container mapped by SupportedContentType
        var contentProcessors = serviceProvider.GetServices<IContentProcessor>()
            .ToDictionary(p => p.SupportedContentType, p => p);

        logger.LogInformation($"Starting indexing with {contentProcessors.Count} content processors.");

        // For each content source, find the appropriate processor and process the content.
        var fileMetadataCollection = new List<FileMetadataModel>();
        foreach (var contentSourceConfig in contentSources)
        {
            if (!contentProcessors.TryGetValue(contentSourceConfig.ContentType, out var contentProcessor))
            {
                logger.LogWarning($"No processor found for content type '{contentSourceConfig.ContentType}'. Skipping.");
                continue;
            }
            try
            {
                logger.LogInformation($"Processing content source: {contentSourceConfig.Directory} ({contentSourceConfig.ContentType})");
                var fileMetadataCollectionTemp = await contentProcessor.ProcessAsync(contentSourceConfig, cancellationToken).ConfigureAwait(false);
                fileMetadataCollection.AddRange(fileMetadataCollectionTemp);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error processing content source {contentSourceConfig.Directory}: {ex.Message}");
            }            
        }

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
            logger.LogInformation($"Deleting {docIdsToDelete.Count} documents from the index.");
            foreach (var docId in docIdsToDelete)
            {
                logger.LogInformation($"Deleting document with ID: {docId}");
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

        logger.LogInformation($"Indexing {fileMetadataCollection.Count} files into memory.");

        var idx = 0;
        foreach (var fileMetadata in fileMetadataCollection)
        {
            ++idx;

            var file = fileMetadata.OutputPath;

            if (!File.Exists(file))
            {
                logger.LogWarning($"File not found, skipping for now: {file}");
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
                { "org_filepath", file },
                { "org_filename", Path.GetFileName(file) },
                { "org_filename_upper", Path.GetFileName(file).ToUpperInvariant() },
                { "org_filetype", Path.GetExtension(file) },
                { "org_filesize", new FileInfo(file).Length.ToString() },
                { "org_filedate", File.GetLastWriteTime(file).ToString() },
                { "sha256", fileHash },
                { "remote_url", fileMetadata.Url },
                { "original_name", fileMetadata.OriginalName },
                { "flatten_name", fileMetadata.FlattenName },
                { "title", fileMetadata.Title },
                { "title_upper", fileMetadata.Title.ToUpperInvariant() },
                { "source", fileMetadata.Source },
                { "original_filepath", fileMetadata.OriginalFilePath },
                { "local_original_root_dir", fileMetadata.LocalOriginalRootDir },
                { "local_original_filepath", fileMetadata.LocalOriginalFilePath },
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
                    foreach (var tag in resultTags)
                    {
                        if (resultTags.TryGetValue(tag.Key, out var values))
                        {
                            if (tag.Value != values)
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
                    logger.LogInformation($"Document with the same hash already imported. Skipping: {documentId}");
                    continue;
                }
            }

            logger.LogInformation($"Importing ({idx}/{fileMetadataCollection.Count}) {file} with document ID {documentId}");

            var steps = fileMetadata.GenerateSummary ? Constants.PipelineWithSummary : Constants.DefaultPipeline;

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
