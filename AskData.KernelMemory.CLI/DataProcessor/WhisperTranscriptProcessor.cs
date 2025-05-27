using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
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

    public async Task<List<FileMetadataModel>> Process(ContentSourceConfig contentSourceConfig)
    {
        if (!contentSourceConfig.ContentType.Equals(SupportedContentType, StringComparison.InvariantCultureIgnoreCase))
        {
            return [];
        }

        Directory.CreateDirectory(config.Value.OutputDirectory);

        var output = new List<FileMetadataModel>();

        // Get all .json files in the specified directory (non-recursive)
        var jsonFiles = Directory.GetFiles(contentSourceConfig.Directory, "*.json", SearchOption.TopDirectoryOnly);
        var allowedChars = new HashSet<char>() { '_', '.' };

        foreach (var filePath in jsonFiles)
        {
            try
            {
                // Read the JSON file content
                var jsonContent = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                
                // Deserialize the JSON content to FileMetadataModel
                var whisperSegments = System.Text.Json.JsonSerializer.Deserialize<List<WhisperSegment>>(jsonContent);
                
                if (whisperSegments != null)
                {
                    var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, filePath);
                    var fileRelSanitised = fileRel
                        .Replace(Path.DirectorySeparatorChar, '_')
                        ;
                    fileRelSanitised = new string([.. fileRelSanitised.Where(c => char.IsLetterOrDigit(c) || allowedChars.Contains(c))]);

                    var fileFlattenName = $"{contentSourceConfig.Name}___{fileRelSanitised}";

                    // build a new processed file with only the content we care about

                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("# Podcast Transcript");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"Title: {Path.GetFileNameWithoutExtension(filePath)}");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"File: {fileRel}");
                    stringBuilder.AppendLine();

                    foreach (var segment in whisperSegments)
                    {
                        // Append each segment's text to the string builder
                        stringBuilder.AppendLine($"[{segment.StartTime} -> {segment.EndTime}]  {segment.Text}  ");
                    }

                    // write to output file
                    var outputFilePath = Path.Combine(config.Value.OutputDirectory, fileFlattenName + ".md");
                    await File.WriteAllTextAsync(outputFilePath, stringBuilder.ToString()).ConfigureAwait(false);

                    var fileMetadata = new FileMetadataModel
                    {
                        OriginalName = Path.GetFileName(filePath),
                        OriginalFilePath = filePath,
                        LocalOriginalRootDir = contentSourceConfig.Directory,
                        LocalOriginalFilePath = Path.GetRelativePath(contentSourceConfig.Directory, filePath),
                        FlattenName = fileFlattenName,
                        Title = Path.GetFileNameWithoutExtension(filePath),
                        OutputPath = outputFilePath,
                        Source = contentSourceConfig.Name,
                    };

                    output.Add(fileMetadata);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error processing file {filePath}: {ex.Message}");
            }
        }


        return output;
    }
}
