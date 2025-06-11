using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Pipeline;
using System.Text;

namespace AskData.KernelMemory;

internal sealed class CustomContentDecoder
    (
    ILogger<CustomContentDecoder> logger
    )
    : IContentDecoder
{
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return DecodeAsync(stream, filename, cancellationToken);
    }

    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return DecodeAsync(stream, cancellationToken);
    }

    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        return DecodeAsync(data, string.Empty, cancellationToken);
    }

    private async Task<FileContent> DecodeAsync(Stream data, string filename, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(data);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var extension = Path.GetExtension(filename);
        var codeBlockType = ConvertToMarkdownCodeBlockType(extension);

        var stringBuilder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(filename))
        {
            stringBuilder.AppendLine($"# {Path.GetFileNameWithoutExtension(filename)}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"File: {filename}");
            stringBuilder.AppendLine();
        }
        stringBuilder.AppendLine($"```{codeBlockType}");
        stringBuilder.AppendLine(content.ToString().Trim());
        stringBuilder.AppendLine("```");

        var result = new FileContent(MimeTypes.MarkDown);
        result.Sections.Add(new(stringBuilder.ToString(), 1, Chunk.Meta(sentencesAreComplete: true)));

        return result;
    }

    public bool SupportsMimeType(string mimeType)
    {
        return mimeType.Equals(ExtraMimeTypes.CSharp, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(ExtraMimeTypes.CSharpProject, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(MimeTypes.Html, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(ExtraMimeTypes.Mermaid, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(ExtraMimeTypes.Proj, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(ExtraMimeTypes.Python, StringComparison.InvariantCultureIgnoreCase) ||
            mimeType.Equals(MimeTypes.XML, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string ConvertToMarkdownCodeBlockType(string extension)
    {
        extension = extension.ToLowerInvariant();

        return extension switch
        {
            ".cs" => "csharp",
            ".csproj" => "xml",
            ".json" => "json",
            ".mmd" => "mermaid",
            ".proj" => "xml",
            ".py" => "python",
            ".xml" => "xml",
            _ => string.Empty,
        };
    }
}
