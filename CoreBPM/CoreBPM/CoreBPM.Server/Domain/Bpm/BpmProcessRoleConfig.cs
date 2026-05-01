namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Настройка роли в определении бизнес-процесса (таблица bpm_process_role_configs).
/// Хранит назначение Владельца и Кураторов процесса; несколько записей могут иметь одинаковый тип для кураторов.
/// </summary>
public class BpmProcessRoleConfig
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Тип роли: Владелец или Куратор.</summary>
    public BpmProcessRoleType RoleType { get; set; }

    /// <summary>Тип назначенца: сотрудник, должность или подразделение.</summary>
    public BpmAssigneeType AssigneeType { get; set; }

    /// <summary>Идентификатор назначенца (GUID сотрудника, должности или подразделения).</summary>
    public string AssigneeId { get; set; } = string.Empty;

    /// <summary>Отображаемое имя назначенца (кешируется для отображения без дополнительных запросов).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Порядок сортировки (для кураторов).</summary>
    public int SortOrder { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;
}
