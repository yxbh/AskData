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
            limit: 5, // Limit the number of results to 5
            minRelevance: 0.5, // Minimum relevance score
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var response = new List<Content>();

        if (results is null || results.Results.Count == 0)
        {
            return response; // Return empty response if no results found
        }

        // create doc ID to filename map
        var docIdFilenameMap = new Dictionary<string, string>();
        foreach (var result in results.Results)
        {
            docIdFilenameMap[result.DocumentId] = result.SourceName;
        }

        // create doc ID to tags map
        var docIdTagsMap = new Dictionary<string, TagCollection>();
        foreach (var result in results.Results)
        {
            docIdTagsMap[result.DocumentId] = result.Partitions.First().Tags;
        }

        // create a doc ID to max relevance map
        var docIdMaxRelevanceMap = new Dictionary<string, float>();
        foreach (var result in results.Results)
        {
            docIdMaxRelevanceMap[result.DocumentId] = result.Partitions.Select(p => p.Relevance).Max();
        }

        // gather all the document IDs sorted by the highest relevance in their partitions
        string[] docIds = [.. results.Results
        .Select(r => new { r.DocumentId, MaxRelevance = r.Partitions.Max(p => p.Relevance) })
        .OrderByDescending(x => x.MaxRelevance)
        .Select(x => x.DocumentId)];

        var contentTextLength = response.Select(c => c?.Text?.Length ?? 0).Sum();

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

                response.Add(new Content()
                {
                    Text = $"""
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


            response.Add(new Content
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

        return response;

        //return GetSimpleResponse(results);
    }

    private List<Content> GetSimpleResponse(SearchResult? searchResult)
    {
        var response = new List<Content>();

        foreach (var result in searchResult?.Results ?? [])
        {
            var content = new Content
            {
                Text = string.Join(string.Empty, result.Partitions.Select(p => p.Text)),
                Annotations = new Annotations()
                {
                    Audience = [Role.Assistant],
                    Priority = result.Partitions.Max(p => p.Relevance),
                }
            };
            response.Add(content);
        }

        return response;
    }
}
