using System.Text.Json.Serialization;

namespace AskData.KernelMemory;

public class FileMetadataModel
{
    [JsonPropertyName("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonPropertyName("flatten_name")]
    public string FlattenName { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("local_original_full_filepath")]
    public string LocalOriginalFullFilePath { get; set; } = string.Empty;

    [JsonPropertyName("local_original_root_dir")]
    public string LocalOriginalRootDir { get; set; } = string.Empty;

    [JsonPropertyName("local_original_relative_filepath")]
    public string LocalOriginalRelativeFilePath { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("_url_prefix")]
    public string UrlPrefix { get; set; } = string.Empty;

    [JsonPropertyName("_url_postfix")]
    public string UrlPostfix { get; set; } = string.Empty;

    [JsonPropertyName("_output_path")]
    public string OutputPath { get; set; } = string.Empty;

    [JsonPropertyName("generate_summary")]
    public bool GenerateSummary { get; set; } = false;

    [JsonPropertyName("_generate_graph_transform")]
    public bool GenerateGraphTransform { get; set; } = false;

    [JsonPropertyName("content_source_metadata")]
    public Dictionary<string, string> ContentSourceMetadata { get; set; } = [];
}
