namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>Текущее состояние согласования задачи (FR-TASK-01.3).</summary>
public class TaskApprovalStateDto
{
    /// <summary>Идентификатор согласующего (null, если не назначен).</summary>
    public Guid? ApproverUserId { get; set; }

    /// <summary>Отображаемое имя согласующего.</summary>
    public string? ApproverName { get; set; }

    /// <summary>Текущий статус задачи.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Комментарий к последнему решению согласующего (если есть).</summary>
    public string? LastDecisionComment { get; set; }

    /// <summary>Дата последнего решения.</summary>
    public DateTimeOffset? LastDecisionAt { get; set; }
}
