using AskData.KernelMemory.CLI.DataProcessor;

namespace AskData.KernelMemory.CLI;

internal class RootConfig
{
    public KMConfig KernelMemory { get; set; } = new ();

    public ContentProcessorConfig ContentProcessing { get; set; } = new ();

    public List<ContentSourceConfig> ContentSources { get; set; } = [];
}
