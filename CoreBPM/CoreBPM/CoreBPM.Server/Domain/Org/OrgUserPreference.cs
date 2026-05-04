namespace CoreBPM.Server.Domain.Org;

/// <summary>Настройки интерфейса пользователя (таблица org_user_preferences).</summary>
public class OrgUserPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Язык интерфейса (ru/en).</summary>
    public string Language { get; set; } = "ru";

    /// <summary>Часовой пояс (IANA, например Europe/Moscow).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Тема оформления: light / dark / system.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>Формат отображения дат (dd.MM.yyyy и т.п.).</summary>
    public string? DateFormat { get; set; }

    /// <summary>Количество строк на странице по умолчанию.</summary>
    public int PageSize { get; set; } = 25;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационное свойство
    public OrgUser? User { get; set; }
}
