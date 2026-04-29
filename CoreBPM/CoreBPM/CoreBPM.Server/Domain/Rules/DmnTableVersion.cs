namespace CoreBPM.Server.Domain.Rules;

/// <summary>Версия DMN-таблицы (таблица rules_dmn_table_versions).</summary>
public class DmnTableVersion
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на заголовок таблицы.</summary>
    public Guid TableId { get; set; }

    /// <summary>Инкрементальный номер версии (1, 2, 3, …).</summary>
    public int VersionNumber { get; set; }

    /// <summary>Статус версии: черновик, опубликована, архивная.</summary>
    public DmnVersionStatus Status { get; set; } = DmnVersionStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    // Навигационные свойства
    public DmnTable Table { get; set; } = null!;
    public ICollection<DmnColumn> Columns { get; set; } = new List<DmnColumn>();
    public ICollection<DmnRow> Rows { get; set; } = new List<DmnRow>();
}
