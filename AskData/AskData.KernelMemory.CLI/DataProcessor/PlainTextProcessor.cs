using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class PlainTextProcessor
    (
    IOptions<ContentProcessorConfig> config,
    ILogger<PlainTextProcessor> logger
    )
    : IContentProcessor
{
    public string SupportedContentType => "plaintext";

    private readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<List<FileMetadataModel>> ProcessAsync(ContentSourceConfig contentSourceConfig, CancellationToken cancellationToken)
    {
        if (!contentSourceConfig.ContentType.Equals(SupportedContentType, StringComparison.InvariantCultureIgnoreCase))
        {
            return [];
        }

        var contentSourceMetadata = contentSourceConfig.Metadata;
        var contentSourceTitle = string.Empty;

        Directory.CreateDirectory(config.Value.OutputDirectory);

        var output = new List<FileMetadataModel>();

        Matcher matcher = new();

        matcher.AddIncludePatterns(contentSourceConfig.IncludePattern);
        matcher.AddExcludePatterns(contentSourceConfig.ExcludePattern);

        var plainTextFiles = matcher.GetResultsInFullPath(contentSourceConfig.Directory);

        foreach (var filePath in plainTextFiles)
        {
            var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, filePath);
            var fileRelSanitised = Util.SanitisePath(fileRel);
            var sanitisedPrefix = Util.SanitisePath(contentSourceConfig.Name);

            var fileFlattenName = $"{sanitisedPrefix}___{fileRelSanitised}";

            var title = string.IsNullOrWhiteSpace(contentSourceTitle) ? Path.GetFileNameWithoutExtension(filePath) : contentSourceTitle;

            var outputFilePath = Path.Combine(config.Value.OutputDirectory, $"{fileFlattenName}");

            try
            {
                await Util.CopyFileAsync(filePath, outputFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                logger.LogError("Failure copying file from input to output: {ExceptionMessage}", ex.Message);
                continue;
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
                GenerateSummary = contentSourceConfig.GenerateSummary,
                ContentSourceMetadata = contentSourceConfig.Metadata,
            };

            output.Add(fileMetadata);

            logger.LogInformation("Processed file: {FilePath} -> {OutputFilePath}", filePath, outputFilePath);
            logger.LogInformation("File metadata:\n{Metadata}", JsonSerializer.Serialize(fileMetadata, jsonSerializerOptions));
        }

        return output;
    }
}
