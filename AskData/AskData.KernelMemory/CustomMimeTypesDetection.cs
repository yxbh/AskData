using Microsoft.KernelMemory.Pipeline;

namespace AskData.KernelMemory;

internal sealed class CustomMimeTypesDetection
    (
    MimeTypesDetection mimeTypesDetection
    )
    : IMimeTypeDetection
{
    public string GetFileType(string filename)
    {
        if (TryGetFileType(filename, out var mimeType))
        {
            return mimeType!;
        }

        throw new MimeTypeException($"File type not supported: {filename}", isTransient: false);
    }

    public bool TryGetFileType(string filename, out string? mimeType)
    {
        if (!mimeTypesDetection.TryGetFileType(filename, out mimeType))
        {
            var extension = Path.GetExtension(filename);
            return extension != null && _mappings.TryGetValue(extension, out mimeType);
        }

        return true;
    }

    private static readonly Dictionary<string, string> _mappings = new (StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", ExtraMimeTypes.CSharp },
        { ".csproj", MimeTypes.XML },
        { ".proj", MimeTypes.XML },
        { ".mmd", ExtraMimeTypes.Mermaid },
        { ".py", ExtraMimeTypes.Python },

        // Add other custom mappings here
    };

    public static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension != null && _mappings.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream"; // Default MIME type
    }
}
