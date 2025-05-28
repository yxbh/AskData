
using AskData.KernelMemory;
using AskData.MCPServer.Tool;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using ModelContextProtocol.Server;
using NSubstitute;

namespace AskData.MCPServer.UnitTests;

public class AskDataToolTests
{
    [Fact]
    public async Task TestSearchAskDataAsync()
    {
        var services = new ServiceCollection();

        // load config from appsettings.json and appsettings.Development.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true)
            .Build();

        services.Configure<RootConfig>(config);
        services.Configure<KMConfig>(config.GetSection("KernelMemory"));

        var serviceProvider = services.BuildServiceProvider();

        var kmConfig = serviceProvider.GetRequiredService<IOptions<KMConfig>>();

        services.AddKernelMemory(kmConfig.Value);

        serviceProvider = services.BuildServiceProvider();

        var tool = new AskDataTool(
            serviceProvider.GetRequiredService<IKernelMemory>(),
            Substitute.For<IMcpServer>(),
            kmConfig
        );

        var result = await tool.SearchAskDataAsync("what does the podcast say about punishment when toilet training", default);

        result.Should().NotBeNull();
    }
}
