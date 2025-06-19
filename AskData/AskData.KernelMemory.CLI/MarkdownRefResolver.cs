using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AskData.KernelMemory.CLI;

internal partial class MarkdownRefResolver
    (ILogger<MarkdownRefResolver> logger)
{
    public async Task ResolveAsync(IEnumerable<FileMetadataModel> fileModels, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileModels);

        if (!fileModels.Any())
        {
            logger.LogInformation("No file models provided for reference resolution.");
            return;
        }

        logger.LogInformation("Resolving references for {FileModelCount} file models...", fileModels.Count());

        // bucket file models by their source
        var fileModelsBySource = fileModels.GroupBy(f => f.Source).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in fileModelsBySource)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = kvp.Key;
            var models = kvp.Value;
            logger.LogInformation("Processing source: {Source} with {FileCount} files.", source, models.Count);
            // Resolve references for each file model in the source
            await ResolveReferences(models, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ResolveReferences(List<FileMetadataModel> models, CancellationToken cancellationToken)
    {
        // Build a map of UIDs to file models
        var uidToModel = new Dictionary<string, FileMetadataModel>(StringComparer.OrdinalIgnoreCase);

        // Regex to match YAML front matter and extract uid: <uid>
        var yamlBlockRegex = YamlBlockRegex();
        var uidLineRegex = UidRegex();

        var mdModels = models.Where(m => m.OriginalName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToArray();

        foreach (var model in mdModels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(model.LocalOriginalRelativeFilePath) || !File.Exists(model.LocalOriginalRelativeFilePath))
            {
                logger.LogWarning("File not found or path is empty for model: {OutputPath} in source: {Source}", model.OutputPath, model.Source);
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(model.LocalOriginalRelativeFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                logger.LogError("Failed to read file: {FilePath}", model.LocalOriginalRelativeFilePath);
                continue;
            }

            // Find YAML front matter
            var yamlMatch = yamlBlockRegex.Match(content);
            if (!yamlMatch.Success)
                continue;

            var yamlContent = yamlMatch.Groups[1].Value;

            // Find all uid references in the YAML block
            var uidMatches = uidLineRegex.Matches(yamlContent);
            foreach (Match uidMatch in uidMatches)
            {
                if (uidMatch.Groups.Count > 1)
                {
                    var referencedUid = uidMatch.Groups[1].Value;
                    uidToModel[referencedUid] = model; // Update or add the model for this UID
                }
            }
        }

        logger.LogInformation("Found {UidCount} unique UIDs across all models.", uidToModel.Count);


        // Resolve XRef's and relative path links
        // Replace in-place `[...](xref:uid)` with the URL of the referenced file
        // Replace in-place `[...](relative/path)` with the URL of the referenced file

        // Regex to match [text](xref:uid) and [text](relative/path)
        var xrefRegex = XrefReferenceRegex();
        var relLinkRegex = RelativeReferenceRegex();

        foreach (var model in mdModels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(model.OutputPath) || !File.Exists(model.OutputPath))
                continue;

            string content;
            try
            {
                content = await File.ReadAllTextAsync(model.OutputPath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            var originalContent = content;

            // Replace [text](xref:uid) with [text](url)
            content = xrefRegex.Replace(content, match =>
            {
                var text = match.Groups[1].Value;
                var uid = match.Groups[2].Value;
                if (uidToModel.TryGetValue(uid, out var refModel) && !string.IsNullOrWhiteSpace(refModel.Url))
                {
                    return $"[{text}]({refModel.Url})";
                }
                logger.LogWarning("Unresolved xref: {Uid} in file {FilePath}", uid, model.LocalOriginalRelativeFilePath);
                return match.Value;
            });

            // Replace [text](relative/path) with [text](url) if possible
            content = relLinkRegex.Replace(content, match =>
            {
                var text = match.Groups[1].Value;
                var relPath = match.Groups[2].Value;

                var refPath = Path.Combine(Path.GetDirectoryName(model.LocalOriginalFullFilePath) ?? string.Empty, relPath);
                refPath = Path.GetRelativePath(model.LocalOriginalRootDir, refPath);

                var url = $"{model.UrlPrefix}{refPath}{model.UrlPostfix}";
                url = (new Uri(url)).ToString(); // Ensure URL is properly formatted

                // Try to find a model whose LocalOriginalFilePath matches the relative path
                var targetModel = models.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.LocalOriginalRelativeFilePath) &&
                    m.LocalOriginalRelativeFilePath.Equals(refPath, StringComparison.OrdinalIgnoreCase));
                if (targetModel != null && !string.IsNullOrWhiteSpace(targetModel.Url))
                {
                    return $"[{text}]({targetModel.Url})";
                }
                else
                {
                    return $"[{text}]({url})";
                }
            });

            if (content == originalContent)
            {
                // No changes made, skip writing
                continue;
            }

            // Write back the updated content
            try
            {
                await File.WriteAllTextAsync(model.OutputPath, content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write updated file: {FilePath}. Error: {ErrorMessage}", model.LocalOriginalRelativeFilePath, ex.Message);
                throw;
            }
        }
    }

    [GeneratedRegex(@"^---\s*$(.*?)^---\s*$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex YamlBlockRegex();

    [GeneratedRegex(@"^\s*uid:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex UidRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(xref:([^\)]+)\)", RegexOptions.Compiled)]
    private static partial Regex XrefReferenceRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(((?!http[s]?://|xref:)[^\)]+)\)", RegexOptions.Compiled)]
    private static partial Regex RelativeReferenceRegex();
}
