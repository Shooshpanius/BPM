namespace CoreBPM.Server.Application.Org.DTOs;

/// <summary>Настройки интерфейса пользователя.</summary>
public class UserPreferencesDto
{
    public string Language { get; set; } = "ru";
    public string? TimeZone { get; set; }
    public string Theme { get; set; } = "system";
    public string? DateFormat { get; set; }
    public int PageSize { get; set; } = 25;
}

/// <summary>Запрос обновления настроек пользователя.</summary>
public class UpdatePreferencesRequest
{
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public string? Theme { get; set; }
    public string? DateFormat { get; set; }
    public int? PageSize { get; set; }
}
