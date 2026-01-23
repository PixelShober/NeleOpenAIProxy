namespace NeleDesktop.Models;

public sealed class ChatFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Folder";
}
