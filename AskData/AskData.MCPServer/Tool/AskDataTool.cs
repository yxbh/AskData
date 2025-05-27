using AskData.KernelMemory;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

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
    public async Task<List<Content>> SearchAskData(
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

        foreach (var result in results.Results)
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
