namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Метаданные формы задачи (таблица bpm_task_forms).
/// Форма может быть независимой (ProcessId = null) или привязанной к конкретному UserTask процесса.
/// </summary>
public class BpmTaskForm
{
    public Guid Id { get; set; }

    /// <summary>Название формы.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание формы.</summary>
    public string? Description { get; set; }

    /// <summary>FK на процесс (nullable — форма может быть независимой).</summary>
    public Guid? ProcessId { get; set; }

    /// <summary>ID элемента BPMN (UserTask) к которому привязана форма.</summary>
    public string? ElementId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmProcess? Process { get; set; }
    public ICollection<BpmTaskFormVersion> Versions { get; set; } = new List<BpmTaskFormVersion>();
}
