using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class StuffYouShouldKnowDataProcessor
    (
    IOptions<ContentProcessorConfig> config,
    ILogger<StuffYouShouldKnowDataProcessor> logger
    )
    : IContentProcessor
{
    public string SupportedContentType => "sysk-transcript";

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

        // Ensure the output directory exists
        Directory.CreateDirectory(config.Value.OutputDirectory);

        var output = new List<FileMetadataModel>();

        var episodeMetaJsonFile = Directory.GetFiles(contentSourceConfig.Directory, "*.meta.json").ToImmutableSortedSet();

        // Process each file in the source directory
        foreach (var metaFilePath in episodeMetaJsonFile)
        {
            var transcriptFilePath = metaFilePath.Replace(".meta.json", string.Empty);
            var fileName = Path.GetFileNameWithoutExtension(transcriptFilePath);

            if (!File.Exists(metaFilePath))
            {
                logger.LogError($"Missing meta.json for {fileName}");
                continue;
            }

            var transcription = string.Empty;
            if (File.Exists(transcriptFilePath))
            {
                // Read transcription text
                var transcriptionLines = await File.ReadAllLinesAsync(transcriptFilePath, cancellationToken).ConfigureAwait(false);

                var temp = transcriptionLines.Select(l =>
                {
                    if (l.StartsWith("Speaker") && l.Contains(':'))
                    {
                        return l.Split(':', 2).Last().Trim();
                    }

                    return l.Trim();
                });

                transcription = string.Join("\n", temp);
            }
            else
            {
                logger.LogError($"Missing transcript file for {fileName}");
            }


            // Read and parse meta data
            var metaDataText = await File.ReadAllTextAsync(metaFilePath, cancellationToken).ConfigureAwait(false);

            var metaData = JsonSerializer.Deserialize<PodcastEpisodeModel>(metaDataText);

            var title = metaData?.Title ?? Path.GetFileNameWithoutExtension(transcriptFilePath);

            var podcastContentSummary = metaData?.Content ?? string.Empty;
            podcastContentSummary = metaData?.Itunes?.Summary ?? podcastContentSummary;

            // get rid of some stuff to save context space
            if (metaData != null)
            {
                metaData.Itunes = null;
                metaData.Enclosure = null;
                metaData.PodcastTranscripts = null;
                metaData.ContentEncoded = null;
                metaData.ContentEncodedSnippet = null;

                metaDataText = JsonSerializer.Serialize(metaData);
            }

            // build a new processed file with only the content we care about
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"# {title}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"File: {transcriptFilePath}");
            if (!string.IsNullOrWhiteSpace(contentSourceTitle))
            {
                stringBuilder.AppendLine($"Podcast Title: {contentSourceTitle}");
            }
            stringBuilder.AppendLine($"Episode Title: {title}");
            stringBuilder.AppendLine($"Published: {metaData?.IsoDate}");
            stringBuilder.AppendLine($"Link: {metaData?.Link}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Summary");
            stringBuilder.AppendLine(podcastContentSummary);
            stringBuilder.AppendLine();
            //stringBuilder.AppendLine("## Episode Metadata");
            //stringBuilder.AppendLine();
            //stringBuilder.AppendLine("```json");
            //stringBuilder.Append(metaDataText);
            //stringBuilder.AppendLine("```");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Transcript");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.IsNullOrWhiteSpace(transcription) ? "No transcript available for this episode." : transcription);

            var fileRel = Path.GetRelativePath(contentSourceConfig.Directory, transcriptFilePath);
            var fileRelSanitised = Util.SanitisePath(fileRel);

            var fileFlattenName = $"{contentSourceConfig.Name}___{fileRelSanitised}";

            // Save as markdown file
            var outputFilePath = Path.Combine(config.Value.OutputDirectory, $"{fileFlattenName}.md");

            var fileMetadata = new FileMetadataModel
            {
                OriginalName = Path.GetFileName(transcriptFilePath),
                OriginalFilePath = transcriptFilePath,
                LocalOriginalRootDir = contentSourceConfig.Directory,
                LocalOriginalFilePath = Path.GetRelativePath(contentSourceConfig.Directory, transcriptFilePath),
                FlattenName = fileFlattenName,
                Title = title,
                OutputPath = outputFilePath,
                Source = contentSourceConfig.Name,
                GenerateSummary = contentSourceConfig.GenerateSummary,
            };

            await File.WriteAllTextAsync(outputFilePath, stringBuilder.ToString(), cancellationToken).ConfigureAwait(false);

            output.Add(fileMetadata);
        }

        return output;
    }
}
