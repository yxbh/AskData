using AskData.KernelMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace AskData.MCPServer.Tool;

[McpServerToolType]
internal class AskDataTool(
    IKernelMemory memory,
    IMcpServer server,
    IOptions<KMConfig> config,
    ILogger<AskDataTool> logger
    )
{
    [McpServerTool(Name = "AskData", Destructive = false, Idempotent = true, OpenWorld = true, ReadOnly = true)]
    [Description("Search AskData memory for relevant content based on the input query. The query should be as detailed as possible. Returns a list of content with relevance scores.")]
    public async Task<List<Content>> SearchAskDataAsync(
        [Description("Input text to process.")] string query,
        CancellationToken cancellationToken = default
    )
    {
        var results = await memory.SearchAsync(
            query,
            index: config.Value.IndexName, // Use the index name from the configuration
            limit: 10, // Limit the number of results to 5
            minRelevance: 0.5, // Minimum relevance score
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var response = new List<Content>();

        if (results is null || results.Results.Count == 0)
        {
            return response; // Return empty response if no results found
        }


        await ExpandedPartionsStrategyAsync(memory, response, results, cancellationToken).ConfigureAwait(false);
        //await WholeFileOrSummaryResponseStrategyAsync(memory, response, results, cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task ExpandedPartionsStrategyAsync(
        IKernelMemory memory, List<Content> toolResponse, SearchResult searchResult, CancellationToken cancellationToken)
    {
        foreach (var citation in searchResult.Results)
        {
            // Collect partitions in a sorted collection
            var partitions = new SortedDictionary<int, Citation.Partition>();

            // For each relevant partition fetch the partition before and one after
            foreach (var partition in citation.Partitions)
            {
                partitions[partition.PartitionNumber] = partition;

                // Filters to fetch adjacent partitions
                var filters = new List<MemoryFilter>
                {
                    //MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber - 2}"),
                    MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber - 1}"),
                    MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber + 1}"),
                    //MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber + 2}"),
                };

                // Fetch adjacent partitions and add them to the sorted collection
                var adjacentList = await memory.SearchAsync(
                    string.Empty,
                    index: config.Value.IndexName, // Use the index name from the configuration
                    filters: filters,
                    limit: filters.Count,
                    cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                if (!adjacentList.NoResult)
                {
                    foreach (var adjacent in adjacentList.Results.First().Partitions)
                    {
                        partitions[adjacent.PartitionNumber] = adjacent;
                    }
                }
            }

            // Print partitions in order
            foreach (var kvp in partitions)
            {
                var partition = kvp.Value;

                var fileName = string.Empty;
                if (partition.Tags.TryGetValue("original_name", out var value))
                {
                    fileName = value.FirstOrDefault() ?? string.Empty;
                }

                var originalRelativePath = string.Empty;
                if (partition.Tags.TryGetValue("original_filepath", out var originalRelativePaths))
                {
                    originalRelativePath = originalRelativePaths.FirstOrDefault() ?? string.Empty;
                }

                var source = string.Empty;
                if (partition.Tags.TryGetValue("source", out var sourceValue))
                {
                    source = sourceValue.FirstOrDefault() ?? string.Empty;
                }

                var url = "URL not available";
                if (partition.Tags.TryGetValue("remote_url", out var urlValue))
                {
                    url = urlValue.FirstOrDefault() ?? "URL not available";
                }

                toolResponse.Add(new()
                {
                    Text = $"""
---
Partion Source: {fileName}
Partition: {partition.PartitionNumber}
Content Source: {source}
Original Relative Path: {originalRelativePath}
Query relevance: {partition.Relevance}
URL: {url}
---

````
{partition.Text}
````
"""
                });
            }
        }
    }

    public async Task WholeFileOrSummaryResponseStrategyAsync(
        IKernelMemory memory, List<Content> toolResponse, SearchResult searchResult, CancellationToken cancellationToken)
    {
        // create doc ID to filename map
        var docIdFilenameMap = new Dictionary<string, string>();
        foreach (var result in searchResult.Results)
        {
            docIdFilenameMap[result.DocumentId] = result.SourceName;
        }

        // create doc ID to tags map
        var docIdTagsMap = new Dictionary<string, TagCollection>();
        foreach (var result in searchResult.Results)
        {
            docIdTagsMap[result.DocumentId] = result.Partitions.First().Tags;
        }

        // create a doc ID to max relevance map
        var docIdMaxRelevanceMap = new Dictionary<string, float>();
        foreach (var result in searchResult.Results)
        {
            docIdMaxRelevanceMap[result.DocumentId] = result.Partitions.Select(p => p.Relevance).Max();
        }

        // gather all the document IDs sorted by the highest relevance in their partitions
        string[] docIds = [.. searchResult.Results
        .Select(r => new { r.DocumentId, MaxRelevance = r.Partitions.Max(p => p.Relevance) })
        .OrderByDescending(x => x.MaxRelevance)
        .Select(x => x.DocumentId)];

        var contentTextLength = toolResponse.Select(c => c?.Text?.Length ?? 0).Sum();

        // Retrieve documents from memory using the docIds
        var documents = new List<Document>();
        foreach (var docId in docIds)
        {
            logger.LogInformation($"Found document ID \"{docId}\" with relevance score {docIdMaxRelevanceMap[docId]}");

            var contentAnnotations = new Annotations()
            {
                Audience = [Role.Assistant],
                //Priority = result.Partitions.Max(p => p.Relevance),
            };

            if (contentTextLength >= 56000)
            {
                var title = string.Empty;
                if (docIdTagsMap[docId].TryGetValue("title", out var titleTag))
                {
                    title = titleTag.First();
                }
                else
                {
                    logger.LogError($"No title found for document ID: {docId}");
                }

                var text = "NO SUMMARY AVAILABLE";

                var summaryResults = await memory.SearchSummariesAsync(
                    filter: MemoryFilters.ByDocument(docId),
                    index: config.Value.IndexName,
                    cancellationToken: cancellationToken
                    ).ConfigureAwait(false);

                if (summaryResults.Count > 0)
                {
                    // We only worry about the first one for now.
                    var docSummary = summaryResults.First().Partitions.First().Text;

                    // remove anything wrapped in a `<think>` block
                    docSummary = docSummary.Split("</think>", 2).Last();

                    text = docSummary.Trim();
                }
                else
                {
                    logger.LogError($"No summary found available for document ID: {docId}");
                    //continue;
                }

                toolResponse.Add(new Content()
                {
                    Text = $"""
---
Query Relevance: {docIdMaxRelevanceMap[docId]}
---

# {title}

{text}
""",
                    Annotations = contentAnnotations,
                });

                continue;
            }

            var streamable = await memory.ExportFileAsync(
                docId, fileName: docIdFilenameMap[docId], index: config.Value.IndexName, cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            var memoryStream = new MemoryStream();
            var stream = await streamable.GetStreamAsync().ConfigureAwait(false);
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            var bytes = memoryStream.ToArray();

            var fileContent = string.Empty;
            // Detect and skip UTF8 BOM if present
            int bomLength = Encoding.UTF8.GetPreamble().Length;
            if (bytes.Length >= bomLength && Encoding.UTF8.GetPreamble().SequenceEqual(bytes.Take(bomLength)))
            {
                // Skip BOM
                fileContent = Encoding.UTF8.GetString(bytes, bomLength, bytes.Length - bomLength);
            }
            else
            {
                fileContent = Encoding.UTF8.GetString(bytes);
            }

            contentTextLength += fileContent.Length;


            toolResponse.Add(new Content
            {
                Text = $"""
---
Query Relevance: {docIdMaxRelevanceMap[docId]}
---
                
{fileContent}
                
"""
                ,
                Annotations = contentAnnotations,
            });
        }
    }
}
