using System;
using System.Collections.ObjectModel;

namespace NeleDesktop.Models;

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ObservableCollection<ChatAttachment> Attachments { get; set; } = new();
}
