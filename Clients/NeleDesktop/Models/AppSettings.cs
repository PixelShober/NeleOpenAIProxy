namespace NeleDesktop.Models;

public sealed class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.aieva.io/api:v1/";
    public string SelectedModel { get; set; } = "google-claude-4.5-sonnet";
    public bool DarkMode { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    public string TemporaryHotkey { get; set; } = "Ctrl+Alt+T";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
