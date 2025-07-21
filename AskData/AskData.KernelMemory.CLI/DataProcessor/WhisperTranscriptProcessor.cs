using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Path = System.IO.Path;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class WhisperTranscriptProcessor
    (
    IOptions<ContentProcessorConfig> config,
    ILogger<WhisperTranscriptProcessor> logger
    )
    : IContentProcessor
{
    public string SupportedContentType { get; } = "whisper-transcript";

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
        if (contentSourceMetadata.TryGetValue("PodcastTitle", out var contentSourceTitleTemp))
        {
            contentSourceTitle = contentSourceTitleTemp;
        }

        Directory.CreateDirectory(config.Value.OutputDirectory);

        var output = new List<FileMetadataModel>();

        // Get all .json files in the specified directory (non-recursive)
        var jsonFiles = Directory.GetFiles(contentSourceConfig.Directory, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var filePath in jsonFiles)
        {
            try
            {
                // Read the JSON file content
                var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                
                // Deserialize the JSON content to FileMetadataModel
                var whisperSegments = JsonSerializer.Deserialize<List<WhisperSegment>>(jsonContent);
                
                if (whisperSegments is null)
                {
                    continue;
                }

                var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, filePath);
                var fileRelSanitised = Util.SanitisePath(fileRel);
                var sanitisedPrefix = Util.SanitisePath(contentSourceConfig.Name);

                var fileFlattenName = $"{sanitisedPrefix}___{fileRelSanitised}";

                var title = string.IsNullOrWhiteSpace(contentSourceTitle) ? Path.GetFileNameWithoutExtension(filePath) : contentSourceTitle;

                // build a new processed file with only the content we care about

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"# {title}");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"File: {fileRel}");
                if (!string.IsNullOrWhiteSpace(contentSourceTitle))
                {
                    stringBuilder.Append($"Podcast Title: {contentSourceTitle}");
                }
                stringBuilder.AppendLine();

                foreach (var blah in contentSourceConfig.Metadata)
                {
                    stringBuilder.AppendLine($"**{blah.Key}:** {blah.Value}  ");
                }
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("## Transcript");
                stringBuilder.AppendLine();

                foreach (var segment in whisperSegments)
                {
                    // Append each segment's text to the string builder
                    stringBuilder.AppendLine($"[{segment.StartTime} -> {segment.EndTime}]  {segment.Text}  ");
                }

                // write to output file
                var outputFilePath = Path.Combine(config.Value.OutputDirectory, fileFlattenName + ".md");
                await File.WriteAllTextAsync(outputFilePath, stringBuilder.ToString(), cancellationToken).ConfigureAwait(false);

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
            catch (Exception ex)
            {
                logger.LogError("Error processing file {FilePath}: {ExceptionMessage}", filePath, ex.Message);
            }
        }


        return output;
    }
}
