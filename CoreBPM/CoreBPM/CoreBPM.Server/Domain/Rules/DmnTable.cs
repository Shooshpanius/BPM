namespace CoreBPM.Server.Domain.Rules;

/// <summary>Заголовок DMN-таблицы бизнес-правил (таблица rules_dmn_tables).</summary>
public class DmnTable
{
    public Guid Id { get; set; }

    /// <summary>Название таблицы правил.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание назначения таблицы.</summary>
    public string? Description { get; set; }

    /// <summary>Хит-политика: определяет, сколько строк может совпасть и как обрабатывается результат.</summary>
    public DmnHitPolicy HitPolicy { get; set; } = DmnHitPolicy.Unique;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<DmnTableVersion> Versions { get; set; } = new List<DmnTableVersion>();
}
