using System;
using System.Collections.Generic;

namespace NeleDesktop.Services;

public static class TranscriptionPolicy
{
    public const long MaxBytes = 200L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensionsSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".flac", ".mp3", ".mpeg", ".mpga", ".m4a", ".ogg", ".wav", ".webm"
    };

    public static IReadOnlyList<string> AllowedExtensions { get; } = new List<string>(AllowedExtensionsSet);

    public static string AllowedExtensionsLabel { get; } = string.Join(", ", AllowedExtensions);

    public static bool IsAllowedExtension(string extension)
        => AllowedExtensionsSet.Contains(extension);
}
