namespace CoreBPM.Server.Domain.Org;

/// <summary>
/// Подразделение организации (таблица org_departments).
/// Поддерживает иерархическую структуру через самоссылающийся FK ParentId.
/// </summary>
public class OrgDepartment
{
    public Guid Id { get; set; }

    /// <summary>Организация, к которой относится подразделение.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Родительское подразделение. Null — подразделение является корневым в оргструктуре.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>Наименование подразделения.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Краткое описание подразделения.</summary>
    public string? Description { get; set; }

    /// <summary>Краткое название (аббревиатура).</summary>
    public string? ShortName { get; set; }

    /// <summary>Уникальный код подразделения в рамках организации.</summary>
    public string? Code { get; set; }

    /// <summary>
    /// Материализованный путь для быстрых запросов потомков.
    /// Формат: /id1/id2/id3 (от корня до текущего узла включительно).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Статус подразделения (активное / архивное).</summary>
    public DepartmentStatus Status { get; set; } = DepartmentStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public OrgOrganization Organization { get; set; } = null!;
    public OrgDepartment? Parent { get; set; }
    public ICollection<OrgDepartment> Children { get; set; } = new List<OrgDepartment>();
}
