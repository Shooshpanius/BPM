namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Запись матрицы ролей должности — системная роль, автоматически назначаемая
/// при назначении сотрудника на данную должность (таблица org_position_role_mappings).
/// </summary>
public class OrgPositionRoleMapping
{
    public Guid Id { get; set; }

    /// <summary>Должность.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Название системной роли (например, «HR», «Manager»).</summary>
    public string RoleName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Навигационные свойства
    public OrgPosition Position { get; set; } = null!;
}
