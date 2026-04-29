namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Конфигурация BPMN-элемента процесса (таблица bpm_element_configs).
/// Хранит кастомные BPM-свойства элемента в виде JSON (исполнитель, URL, таймаут и т.п.),
/// которые не входят в стандартную спецификацию BPMN 2.0.
/// </summary>
public class BpmElementConfig
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Идентификатор элемента в BPMN-диаграмме (значение атрибута id).</summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>
    /// JSON-объект с кастомными свойствами элемента.
    /// Структура зависит от типа элемента (UserTask, ServiceTask, Gateway и т.п.).
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
