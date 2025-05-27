using AskData.KernelMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskData.MCPServer;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = BuildHost(args);

        await host.RunAsync();
    }

    static IHost BuildHost(string[] args)
    {
        var appBuilder = Host.CreateApplicationBuilder(args);

        appBuilder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
        });


        // bind config to RootConfig class from appsettings.json and appsettings.Development.json
        appBuilder.Configuration
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{appBuilder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true);

        appBuilder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            ;

        appBuilder.Services
            .Configure<RootConfig>(appBuilder.Configuration)
            .Configure<KMConfig>(appBuilder.Configuration.GetSection("KernelMemory"));

        // get a Config.KernelMemoryConfig object and pass it to the KernelMemoryBuilder
        var kernelMemoryConfig = appBuilder.Services.BuildServiceProvider().GetRequiredService<IOptions<KMConfig>>().Value;

        appBuilder.Services.AddKernelMemory(kernelMemoryConfig);

        return appBuilder.Build();
    }
}
