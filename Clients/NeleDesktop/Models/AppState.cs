using System.Collections.Generic;

namespace NeleDesktop.Models;

public sealed class AppState
{
    public string? ActiveChatId { get; set; }
    public List<ChatFolder> Folders { get; set; } = new();
    public List<ChatConversation> Conversations { get; set; } = new();
}
