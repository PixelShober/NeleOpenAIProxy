namespace NeleDesktop.Models;

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }
    public string Language { get; set; } = "de";
    public string Country { get; set; } = "ALL";
    public int Results { get; set; } = 5;
    public int QueriesMin { get; set; } = 1;
    public int QueriesMax { get; set; } = 1;
}
