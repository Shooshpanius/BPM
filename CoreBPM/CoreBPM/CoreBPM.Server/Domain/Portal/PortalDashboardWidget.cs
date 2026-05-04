namespace CoreBPM.Server.Domain.Portal;

public class PortalDashboardWidget
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string WidgetType { get; set; } = string.Empty; // "my-tasks", "my-processes", "awaiting-action", "notifications", "quick-actions", "news", "my-colleagues", "recent-documents", "html", "rss"
    public int Col { get; set; }     // 0-based column
    public int Row { get; set; }     // 0-based row
    public int ColSpan { get; set; } = 2;
    public int RowSpan { get; set; } = 1;
    public string? Title { get; set; }
    public string? ConfigJson { get; set; } // JSON для настроек виджета
    public bool IsCollapsed { get; set; }
    public int SortOrder { get; set; }
}
