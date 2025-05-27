using AskData.KernelMemory;

namespace AskData.MCPServer;

internal class RootConfig
{
    public KMConfig KernelMemory { get; set; } = new ();
}
