namespace AskData.KernelMemory;

public class KMConfig
{
    public string EmbeddingModelName { get; set; } = string.Empty;

    public string TextGenerationModelName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    public string FileStorageDirectory { get; set; } = string.Empty; // Directory where files are stored
}
