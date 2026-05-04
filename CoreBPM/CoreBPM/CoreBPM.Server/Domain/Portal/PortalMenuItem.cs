namespace CoreBPM.Server.Domain.Portal;

public class PortalMenuItem
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? SectionId { get; set; }  // соответствует SidebarSection на фронтенде
    public string? ExternalUrl { get; set; }
    public int SortOrder { get; set; }
    public string? RequiredRole { get; set; } // null = все авторизованные
    public bool IsVisible { get; set; } = true;
}
