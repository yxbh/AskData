using System.Security.Cryptography;

namespace AskData.KernelMemory;

/// <summary>
/// https://github.com/microsoft/kernel-memory/blob/main/service/Core/Extensions/BinaryDataExtensions.cs
/// </summary>
internal static class BinaryDataExtensions
{
    public static string CalculateSHA256(this BinaryData binaryData)
    {
        byte[] byteArray = SHA256.HashData(binaryData.ToMemory().Span);
        return Convert.ToHexString(byteArray).ToLowerInvariant();
    }
}
