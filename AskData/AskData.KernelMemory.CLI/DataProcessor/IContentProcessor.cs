namespace AskData.KernelMemory.CLI.DataProcessor;

internal interface IContentProcessor
{
    string SupportedContentType { get; }
    Task<List<FileMetadataModel>> ProcessAsync(ContentSourceConfig contentSourceConfig, CancellationToken cancellationToken);
}
