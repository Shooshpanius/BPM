namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Переменная контекста бизнес-процесса (таблица bpm_process_variables).
/// Описывает переменные, доступные во всех экземплярах процесса.
/// </summary>
public class BpmProcessVariable
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Техническое имя переменной (латиница, camelCase).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Тип данных переменной.</summary>
    public BpmVariableType VariableType { get; set; } = BpmVariableType.String;

    /// <summary>Значение по умолчанию (строковое представление).</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Признак «ключевая» — название экземпляра берётся из значения этой переменной.</summary>
    public bool IsKeyVariable { get; set; } = false;

    /// <summary>Признак «входная» — передаётся при запуске из внешних систем.</summary>
    public bool IsInput { get; set; } = false;

    /// <summary>Признак «выходная» — передаётся в дочерние процессы.</summary>
    public bool IsOutput { get; set; } = false;

    /// <summary>Порядок отображения в редакторе (меньше = выше).</summary>
    public int SortOrder { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
