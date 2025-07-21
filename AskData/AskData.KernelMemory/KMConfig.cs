namespace AskData.KernelMemory;

public class KMConfig
{
    public string EmbeddingModelName { get; set; } = string.Empty;

    public string TextGenerationModelName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// File storage directory for storing files when using the simple file storage system.
    /// </summary>
    public string FileStorageDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Directory for storing vector embeddings when using the simple vector storage system.
    /// </summary>
    public string VectorStorageDirectory { get; set; } = string.Empty;

    public bool UseQdrant { get; set; } = true;

    public float SearchMinRelevance { get; set; } = 0.5f;

    public int SearchLimit { get; set; } = 10;
}
