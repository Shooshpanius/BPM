namespace CoreBPM.Server.Domain.Rules;

/// <summary>Строка DMN-таблицы (таблица rules_dmn_rows).</summary>
public class DmnRow
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на версию таблицы.</summary>
    public Guid VersionId { get; set; }

    /// <summary>Порядок строки (0-based).</summary>
    public int Order { get; set; }

    // Навигационные свойства
    public DmnTableVersion Version { get; set; } = null!;
    public ICollection<DmnCell> Cells { get; set; } = new List<DmnCell>();
}
