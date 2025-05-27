using AskData.KernelMemory.CLI.DataProcessor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskData.KernelMemory.CLI;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = BuildHost(args);

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var config = host.Services.GetRequiredService<IOptions<RootConfig>>().Value;

        // Get a CancellationTokenSource from the host's lifetime
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        var cancellationToken = cancellationTokenSource.Token;

        var indexer = host.Services.GetRequiredService<Indexer>();

        await indexer.RunIndexAsync(config.ContentSources, false, cancellationToken).ConfigureAwait(false);
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
            .Configure<RootConfig>(appBuilder.Configuration)
            .Configure<KMConfig>(appBuilder.Configuration.GetSection("KernelMemory"))
            .Configure<ContentProcessorConfig>(appBuilder.Configuration.GetSection("ContentProcessing"));

        // get a Config.KernelMemoryConfig object and pass it to the KernelMemoryBuilder
        var kernelMemoryConfig = appBuilder.Services.BuildServiceProvider().GetRequiredService<IOptions<KMConfig>>().Value;

        appBuilder.Services.AddServices(kernelMemoryConfig);

        return appBuilder.Build();
    }
}
