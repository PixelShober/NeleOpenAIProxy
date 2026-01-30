using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeleDesktop.Services;

public readonly record struct AttachmentValidationResult(bool IsValid, string Title, string Subtitle);

public static class AttachmentPolicy
{
    public const string DefaultTitle = "Datei hier ablegen";

    private static readonly HashSet<string> AllowedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".yaml", ".yml", ".log"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public static IReadOnlyList<string> AllowedExtensions { get; } = AllowedTextExtensions
        .Concat(AllowedImageExtensions)
        .Select(extension => extension.TrimStart('.').ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static string AllowedExtensionsLabel { get; } = string.Join(", ", AllowedExtensions);
    public static string DefaultSubtitle => $"Erlaubt: {AllowedExtensionsLabel}";

    public static bool IsTextExtension(string extension) => AllowedTextExtensions.Contains(extension);
    public static bool IsImageExtension(string extension) => AllowedImageExtensions.Contains(extension);

    public static bool IsAllowedExtension(string extension)
        => AllowedTextExtensions.Contains(extension) || AllowedImageExtensions.Contains(extension);

    public static string GetAttachmentType(string extension)
        => IsImageExtension(extension) ? "image" : "text";

    public static string ResolveContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".yaml" => "text/yaml",
            ".yml" => "text/yaml",
            ".log" => "text/plain",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    public static AttachmentValidationResult ValidateFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            var result = ValidateFile(file);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return new AttachmentValidationResult(true, DefaultTitle, DefaultSubtitle);
    }

    public static AttachmentValidationResult ValidateFile(string path)
    {
        if (!File.Exists(path))
        {
            return new AttachmentValidationResult(false, "Datei nicht gefunden", "Bitte pruefe den Pfad.");
        }

        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension) || !IsAllowedExtension(extension))
        {
            return new AttachmentValidationResult(false, "Dateityp nicht unterstuetzt", $"Erlaubt: {AllowedExtensionsLabel}");
        }

        return new AttachmentValidationResult(true, DefaultTitle, DefaultSubtitle);
    }
}
