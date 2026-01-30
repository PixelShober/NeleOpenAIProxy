using System;
using System.Text.Json.Serialization;

namespace NeleDesktop.Models;

public sealed class ChatAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "text";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Encoding { get; set; } = "text";
    public string? Detail { get; set; }

    [JsonIgnore]
    public string SourcePath { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsText => string.Equals(Type, "text", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsImage => string.Equals(Type, "image", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasPreview => IsImage && !string.IsNullOrWhiteSpace(SourcePath);

    [JsonIgnore]
    public bool IsUploading { get; set; }

    [JsonIgnore]
    public string TypeLabel
    {
        get
        {
            var extension = System.IO.Path.GetExtension(FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "FILE";
            }

            return extension.TrimStart('.').ToUpperInvariant();
        }
    }
}
