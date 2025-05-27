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

    [JsonPropertyName("original_filepath")]
    public string OriginalFilePath { get; set; } = string.Empty;

    [JsonPropertyName("local_original_root_dir")]
    public string LocalOriginalRootDir { get; set; } = string.Empty;

    [JsonPropertyName("local_original_filepath")]
    public string LocalOriginalFilePath { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("_output_path")]
    public string OutputPath { get; set; } = string.Empty;
}
