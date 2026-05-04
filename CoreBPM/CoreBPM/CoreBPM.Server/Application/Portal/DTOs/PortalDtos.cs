namespace CoreBPM.Server.Application.Portal.DTOs;

public record PortalDashboardWidgetDto(
    Guid Id,
    string WidgetType,
    int Col,
    int Row,
    int ColSpan,
    int RowSpan,
    string? Title,
    string? ConfigJson,
    bool IsCollapsed,
    int SortOrder
);

public record SaveDashboardRequest(IReadOnlyList<SaveWidgetRequest> Widgets);

public record SaveWidgetRequest(
    Guid? Id,
    string WidgetType,
    int Col,
    int Row,
    int ColSpan,
    int RowSpan,
    string? Title,
    string? ConfigJson,
    bool IsCollapsed,
    int SortOrder
);

public record AddWidgetRequest(string WidgetType, int Col, int Row, int ColSpan = 2, int RowSpan = 1, string? Title = null, string? ConfigJson = null);

public record UpdateWidgetRequest(int? Col, int? Row, int? ColSpan, int? RowSpan, string? Title, string? ConfigJson, bool? IsCollapsed);

public record PortalBrandingDto(Guid Id, string SystemName, string? LogoUrl, string? FaviconUrl, string? PrimaryColor, string? AccentColor, string GlobalTheme);

public record UpdateBrandingRequest(string? SystemName, string? LogoUrl, string? FaviconUrl, string? PrimaryColor, string? AccentColor, string? GlobalTheme);

public record PortalMenuItemDto(Guid Id, Guid? ParentId, string Label, string? Icon, string? SectionId, string? ExternalUrl, int SortOrder, string? RequiredRole, bool IsVisible);

public record SaveMenuRequest(IReadOnlyList<SaveMenuItemRequest> Items);

public record SaveMenuItemRequest(Guid? Id, Guid? ParentId, string Label, string? Icon, string? SectionId, string? ExternalUrl, int SortOrder, string? RequiredRole, bool IsVisible);
