namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Предложение по улучшению бизнес-процесса (таблица bpm_improvements, FR-BPM-03.1).
/// Инициатор создаёт предложение, владелец процесса рассматривает его (принимает или отклоняет),
/// назначенный исполнитель реализует и указывает резолюцию.
/// </summary>
public class BpmImprovement
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение бизнес-процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Тема предложения (краткая формулировка).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Подробное описание предложения.</summary>
    public string? Description { get; set; }

    /// <summary>Текущий статус предложения.</summary>
    public BpmImprovementStatus Status { get; set; } = BpmImprovementStatus.Pending;

    /// <summary>Пользователь-инициатор предложения.</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>Исполнитель, назначенный владельцем при принятии предложения.</summary>
    public Guid? AssignedUserId { get; set; }

    /// <summary>Срок исполнения, заполняется при принятии предложения.</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Комментарий владельца процесса (при принятии или отклонении).</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Резолюция исполнителя (при завершении).</summary>
    public string? Resolution { get; set; }

    /// <summary>Опциональная ссылка на экземпляр процесса, из которого создано предложение.</summary>
    public Guid? SourceInstanceId { get; set; }

    /// <summary>Идентификатор узла задачи-источника (elementId) в экземпляре процесса.</summary>
    public string? SourceTaskElementId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Дата рассмотрения предложения владельцем.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Дата завершения предложения (реализации или отклонения).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
