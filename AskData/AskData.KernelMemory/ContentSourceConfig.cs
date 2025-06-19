namespace AskData.KernelMemory;

public class ContentSourceConfig
{
    public string Name { get; set; } = string.Empty; // Name of the content source. This needs to be unique.

    public string Directory { get; set; } = string.Empty; // Directory of the content source.

    public string ContentType { get; set; } = string.Empty; // Type of content (e.g., text, image, etc.).

    public bool GenerateSummary { get; set; } = false;  // Perform additional summarization steps.

    public string[] IncludePattern { get; set; } = [];

    public string[] ExcludePattern { get; set; } = [];

    public string UrlPrefix { get; set; } = string.Empty; // URL prefix for eah file in the content source, if applicable.

    public string UrlPostfix { get; set; } = string.Empty; // URL postfix for each file in the content source, if applicable.

    public Dictionary<string, string> Metadata { get; set; } = []; // Additional metadata for the content source.

}
