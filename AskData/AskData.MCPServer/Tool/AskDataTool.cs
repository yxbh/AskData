using AskData.KernelMemory;
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
    IOptions<KMConfig> config
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

        // gather all the document IDs sorted by the highest relevance in their partitions
        string[] docIds = [.. results.Results
        .Select(r => new { r.DocumentId, MaxRelevance = r.Partitions.Max(p => p.Relevance) })
        .OrderByDescending(x => x.MaxRelevance)
        .Select(x => x.DocumentId)];

        // Retrieve documents from memory using the docIds
        var documents = new List<Document>();
        foreach (var docId in docIds)
        {
            var streamable = await memory.ExportFileAsync(
                docId, fileName: docIdFilenameMap[docId], index: config.Value.IndexName, cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            var memoryStream = new MemoryStream();
            var stream = await streamable.GetStreamAsync().ConfigureAwait(false);
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            var bytes = memoryStream.ToArray();
            var fileContent = Encoding.UTF8.GetString(bytes);

            response.Add(new Content
            {
                Text = fileContent,
                Annotations = new Annotations()
                {
                    Audience = [Role.Assistant],
                    //Priority = result.Partitions.Max(p => p.Relevance),
                }
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
