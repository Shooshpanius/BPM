namespace CoreBPM.Server.Domain.Rules;

/// <summary>Колонка DMN-таблицы (таблица rules_dmn_columns).</summary>
public class DmnColumn
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на версию таблицы.</summary>
    public Guid VersionId { get; set; }

    /// <summary>Название колонки.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Вид колонки: входное условие или выходной результат.</summary>
    public DmnColumnKind ColumnKind { get; set; } = DmnColumnKind.Input;

    /// <summary>Тип значения в ячейках колонки.</summary>
    public DmnValueType ValueType { get; set; } = DmnValueType.String;

    /// <summary>Порядок отображения колонки (0-based).</summary>
    public int Order { get; set; }

    // Навигационные свойства
    public DmnTableVersion Version { get; set; } = null!;
    public ICollection<DmnCell> Cells { get; set; } = new List<DmnCell>();
}
