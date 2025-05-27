namespace AskData.KernelMemory.CLI.DataProcessor;

internal interface IContentProcessor
{
    string SupportedContentType { get; }
    Task<List<FileMetadataModel>> Process(ContentSourceConfig contentSourceConfig);
}
