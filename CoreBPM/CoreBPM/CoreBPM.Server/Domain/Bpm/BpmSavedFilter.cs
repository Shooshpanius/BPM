namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Сохранённый пользовательский фильтр раздела «Мои процессы» (таблица bpm_saved_filters).
/// </summary>
public class BpmSavedFilter
{
    public Guid Id { get; set; }

    /// <summary>Владелец фильтра.</summary>
    public Guid UserId { get; set; }

    /// <summary>Отображаемое имя фильтра.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON-сериализованные параметры фильтра.</summary>
    public string FiltersJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
