using AskData.KernelMemory;
using Microsoft.AspNetCore.Builder;
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
        IHost host;

        if (args.Contains("--stdio-transport"))
        {
            host = BuildHost(args);
        }
        else if (args.Contains("--http-transport"))
        {
            host = BuildWebHost(args);
        }
        else
        {
            // default to HTTP transport.
            host = BuildWebHost(args);
        }

        if (host is WebApplication webApplication)
        {
            await webApplication.RunAsync("http://localhost:3001");
        }
        else
        {
            await host.RunAsync();
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var appBuilder = Host.CreateApplicationBuilder(args);

        SetupLogging(appBuilder);
        SetupConfig(appBuilder);

        appBuilder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            ;

        // get a Config.KernelMemoryConfig object and pass it to the KernelMemoryBuilder
        var kernelMemoryConfig = appBuilder.Services.BuildServiceProvider().GetRequiredService<IOptions<KMConfig>>().Value;

        appBuilder.Services.AddKernelMemory(kernelMemoryConfig);

        return appBuilder.Build();
    }

    private static WebApplication BuildWebHost(string[] args)
    {
        var appBuilder = WebApplication.CreateBuilder(args);

        SetupLogging(appBuilder);
        SetupConfig(appBuilder);

        appBuilder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            ;

        // get a Config.KernelMemoryConfig object and pass it to the KernelMemoryBuilder
        var kernelMemoryConfig = appBuilder.Services.BuildServiceProvider().GetRequiredService<IOptions<KMConfig>>().Value;

        appBuilder.Services.AddKernelMemory(kernelMemoryConfig);

        var app = appBuilder.Build();
        app.MapMcp();

        return app;
    }

    private static void SetupLogging(IHostApplicationBuilder appBuilder)
    {
        appBuilder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
        });
    }

    private static void SetupConfig(IHostApplicationBuilder appBuilder)
    {
        // bind config to RootConfig class from appsettings.json and appsettings.Development.json
        appBuilder.Configuration
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{appBuilder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true);

        appBuilder.Services
            .Configure<RootConfig>(appBuilder.Configuration)
            .Configure<KMConfig>(appBuilder.Configuration.GetSection("KernelMemory"));
    }
}
