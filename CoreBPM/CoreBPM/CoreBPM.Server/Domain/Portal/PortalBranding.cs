namespace CoreBPM.Server.Domain.Portal;

public class PortalBranding
{
    public Guid Id { get; set; }
    public string SystemName { get; set; } = "Core BPM";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string GlobalTheme { get; set; } = "light"; // "light" | "dark"
    public DateTimeOffset UpdatedAt { get; set; }
}
