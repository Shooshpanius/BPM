namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Запись в истории изменений подразделения (таблица org_department_history).
/// </summary>
public class OrgDepartmentHistory
{
    public Guid Id { get; set; }

    /// <summary>Изменённое подразделение.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Пользователь, выполнивший изменение.</summary>
    public Guid? ChangedByUserId { get; set; }

    /// <summary>Дата и время изменения.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Тип изменения.</summary>
    public DepartmentChangeType ChangeType { get; set; }

    /// <summary>Прежнее значение (JSON-строка).</summary>
    public string? OldValue { get; set; }

    /// <summary>Новое значение (JSON-строка).</summary>
    public string? NewValue { get; set; }

    // Навигационные свойства
    public OrgDepartment Department { get; set; } = null!;
    public OrgUser? ChangedByUser { get; set; }
}
