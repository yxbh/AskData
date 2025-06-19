namespace AskData.KernelMemory.CLI;

internal class Util
{
    public static readonly HashSet<char> AllowedChars = ['_', '.'];

    public static string SanitisePath(string path)
    {
        path = path.Replace(Path.DirectorySeparatorChar, '_');
        return new string([.. path.Where(c => char.IsLetterOrDigit(c) || AllowedChars.Contains(c))]);
    }

    public static async Task CopyFileAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceFile);
        ArgumentException.ThrowIfNullOrEmpty(destinationFile);

        using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }
}
