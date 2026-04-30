namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Один вариант пользовательского статуса экземпляра процесса (таблица bpm_instance_status_options).
/// Порядок вариантов управляется полем SortOrder.
/// </summary>
public class BpmInstanceStatusOption
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на бизнес-процесс.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Отображаемое название статуса.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Технический код статуса (латиница; автогенерируется транслитерацией из названия).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Порядок отображения в выпадающих списках и фильтрах.</summary>
    public int SortOrder { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
