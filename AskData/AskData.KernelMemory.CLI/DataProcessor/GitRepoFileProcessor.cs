using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCompress.Common;
using System.Collections.Immutable;
using System.Text.Json;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class GitRepoFileProcessor
    (
    IOptions<ContentProcessorConfig> config,
    ILogger<StuffYouShouldKnowDataProcessor> logger
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

        // Ensure the output directory exists
        Directory.CreateDirectory(config.Value.OutputDirectory);

        Matcher matcher = new();

        if (contentSourceConfig.Metadata.TryGetValue("_include_pattern", out var userDefinedGlobPattern))
        {
            var includePatterns = userDefinedGlobPattern.Split(',', StringSplitOptions.RemoveEmptyEntries);
            matcher.AddIncludePatterns(includePatterns);
        }
        else
        {
            var includePattern = "./**/*.*";
            matcher.AddIncludePatterns([includePattern]);
        }
        if (contentSourceConfig.Metadata.TryGetValue("_exclude_pattern", out var userDefinedExcludePattern))
        {
            var excludePatterns = userDefinedExcludePattern.Split(',', StringSplitOptions.RemoveEmptyEntries);
            matcher.AddExcludePatterns(excludePatterns);
        }

        var output = new List<FileMetadataModel>();

       var files = matcher.GetResultsInFullPath(contentSourceConfig.Directory);

        foreach (var filePath in files)
        {
            if (!File.Exists(filePath))
            {
                logger.LogError($"File not found: {filePath}");
                continue;
            }

            var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, filePath);
            var fileRelSanitised = Util.SanitisePath(fileRel);
            var sanitisedPrefix = Util.SanitisePath(contentSourceConfig.Name);

            var fileFlattenName = $"{sanitisedPrefix}___{fileRelSanitised}";

            var outputFilePath = Path.Combine(config.Value.OutputDirectory, $"{fileFlattenName}");

            File.Copy(filePath, outputFilePath, true);

            var title = Path.GetFileNameWithoutExtension(filePath);

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
                OriginalFilePath = filePath,
                LocalOriginalRootDir = contentSourceConfig.Directory,
                LocalOriginalFilePath = Path.GetRelativePath(contentSourceConfig.Directory, filePath),
                FlattenName = fileFlattenName,
                Title = title,
                OutputPath = outputFilePath,
                Source = contentSourceConfig.Name,
                GenerateSummary = contentSourceConfig.GenerateSummary,
                ContentSourceMetadata = contentSourceMetadata,
            };

            output.Add(fileMetadata);

            logger.LogInformation($"Processed file: {filePath} -> {outputFilePath}");
            logger.LogInformation($"File metadata:\n{JsonSerializer.Serialize(fileMetadata, jsonSerializerOptions)}");
        }

        return output;
    }

    private readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true
    };
}
