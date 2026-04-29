namespace CoreBPM.Server.Domain.Rules;

/// <summary>Ячейка DMN-таблицы (таблица rules_dmn_cells).</summary>
public class DmnCell
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на строку.</summary>
    public Guid RowId { get; set; }

    /// <summary>Ссылка на колонку.</summary>
    public Guid ColumnId { get; set; }

    /// <summary>
    /// Значение ячейки.
    /// Null означает «любое значение» (совпадает всегда).
    /// Поддерживаемый синтаксис: строка, число, диапазон ([1..10], &lt;5, &gt;=3), список ("a","b"), булево (true/false).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Аннотация (необязательный комментарий к правилу).</summary>
    public string? Annotation { get; set; }

    // Навигационные свойства
    public DmnRow Row { get; set; } = null!;
    public DmnColumn Column { get; set; } = null!;
}
