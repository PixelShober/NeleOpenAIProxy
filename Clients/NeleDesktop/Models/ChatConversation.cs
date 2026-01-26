using System;
using System.Collections.ObjectModel;

namespace NeleDesktop.Models;

public sealed class ChatConversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New chat";
    public string Model { get; set; } = string.Empty;
    public bool UseWebSearch { get; set; }
    public bool IsTemporary { get; set; }
    public string? FolderId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
}
