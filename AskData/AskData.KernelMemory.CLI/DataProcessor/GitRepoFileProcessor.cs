using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class GitRepoFileProcessor
    (
    IOptions<ContentProcessorConfig> config,
    ILogger<GitRepoFileProcessor> logger
    )
    : IContentProcessor
{
    public string SupportedContentType => "source-repo";

    public async Task<List<FileMetadataModel>> ProcessAsync(ContentSourceConfig contentSourceConfig, CancellationToken cancellationToken)
    {
        if (!contentSourceConfig.ContentType.Equals(SupportedContentType, StringComparison.InvariantCultureIgnoreCase))
        {
            return [];
        }

        Matcher matcher = new();

        matcher.AddIncludePatterns(contentSourceConfig.IncludePattern);
        matcher.AddExcludePatterns(contentSourceConfig.ExcludePattern);

        // Ensure the output directory exists
        Directory.CreateDirectory(config.Value.OutputDirectory);

        var output = new List<FileMetadataModel>();

        var files = matcher.GetResultsInFullPath(contentSourceConfig.Directory);
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                logger.LogError("File not found: {FilePath}", filePath);
                continue;
            }

            var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, filePath);
            var fileRelSanitised = Util.SanitisePath(fileRel);
            var sanitisedPrefix = Util.SanitisePath(contentSourceConfig.Name);

            var fileFlattenName = $"{sanitisedPrefix}___{fileRelSanitised}";

            var outputFilePath = Path.Combine(config.Value.OutputDirectory, $"{fileFlattenName}");

            await Util.CopyFileAsync(filePath, outputFilePath, cancellationToken).ConfigureAwait(false);

            var title = Path.GetFileNameWithoutExtension(filePath);

            var url = $"{contentSourceConfig.UrlPrefix}{fileRel}{contentSourceConfig.UrlPostfix}";
            url = (new Uri(url)).ToString(); // Ensure URL is properly formatted

            var contentSourceMetadata = new Dictionary<string, string>();
            foreach (var kvp in contentSourceConfig.Metadata)
            {
                // strip processor specific metadata keys
                if (kvp.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip metadata keys that start with an underscore
                    continue;
                }

                contentSourceMetadata[kvp.Key] = kvp.Value;
            }

            var fileMetadata = new FileMetadataModel
            {
                OriginalName = Path.GetFileName(filePath),
                LocalOriginalFullFilePath = filePath,
                LocalOriginalRootDir = contentSourceConfig.Directory,
                LocalOriginalRelativeFilePath = Path.GetRelativePath(contentSourceConfig.Directory, filePath),
                FlattenName = fileFlattenName,
                Title = title,
                OutputPath = outputFilePath,
                Source = contentSourceConfig.Name,
                Url = url,
                UrlPrefix = contentSourceConfig.UrlPrefix,
                UrlPostfix = contentSourceConfig.UrlPostfix,
                GenerateSummary = contentSourceConfig.GenerateSummary,
                GenerateGraphTransform = contentSourceConfig.GenerateGraphTransform,
                ContentSourceMetadata = contentSourceMetadata,
            };

            output.Add(fileMetadata);

            logger.LogInformation("Processed file: {FilePath} -> {OutputFilePath}", filePath, outputFilePath);
        }

        return output;
    }

    private readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true
    };
}
