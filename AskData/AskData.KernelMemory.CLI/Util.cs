namespace AskData.KernelMemory.CLI;

internal class Util
{
    public static readonly HashSet<char> AllowedChars = ['_', '.'];

    public static string SanitisePath(string path)
    {
        path = path.Replace(Path.DirectorySeparatorChar, '_');
        return new string([.. path.Where(c => char.IsLetterOrDigit(c) || AllowedChars.Contains(c))]);
    }
}
