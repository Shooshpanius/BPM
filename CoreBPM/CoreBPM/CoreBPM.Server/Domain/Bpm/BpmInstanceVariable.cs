namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Переменная экземпляра бизнес-процесса (таблица bpm_instance_variables).
/// Хранит текущее значение переменной в конкретном экземпляре.
/// </summary>
public class BpmInstanceVariable
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Ссылка на определение переменной (может быть null для динамических переменных).</summary>
    public Guid? ProcessVariableId { get; set; }

    /// <summary>Техническое имя переменной.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON-сериализованное значение переменной.</summary>
    public string? ValueJson { get; set; }

    public DateTimeOffset SetAt { get; set; }

    // Навигационные свойства
    public BpmInstance Instance { get; set; } = null!;
}
