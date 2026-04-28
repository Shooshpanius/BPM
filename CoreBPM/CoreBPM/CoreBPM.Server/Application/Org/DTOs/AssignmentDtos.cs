namespace CoreBPM.Server.Application.Org.DTOs;

// ─── Запросы ────────────────────────────────────────────────────────────────

/// <summary>Запрос на создание назначения пользователя на должность.</summary>
public class CreateAssignmentRequest
{
    /// <summary>Идентификатор пользователя.</summary>
    public Guid UserId { get; set; }

    /// <summary>Идентификатор должности.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Ставка занятости. Допустимые значения: 0.25, 0.5, 0.75, 1.0.</summary>
    public decimal Rate { get; set; } = 1.0m;

    /// <summary>Признак основного назначения (true — основное, false — совмещение).</summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>Дата начала назначения (формат: YYYY-MM-DD).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Дата окончания назначения. Если не указана — назначение бессрочное.</summary>
    public DateOnly? EndDate { get; set; }
}

/// <summary>Запрос на обновление назначения (изменяются должность, ставка, тип и даты).</summary>
public class UpdateAssignmentRequest
{
    /// <summary>
    /// Новая должность. Если null — должность не изменяется.
    /// При смене должности автоматически пересчитываются роли пользователя.
    /// </summary>
    public Guid? PositionId { get; set; }

    /// <summary>Ставка занятости. Допустимые значения: 0.25, 0.5, 0.75, 1.0.</summary>
    public decimal Rate { get; set; }

    /// <summary>Признак основного назначения.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Дата начала назначения.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Дата окончания назначения (null — бессрочное).</summary>
    public DateOnly? EndDate { get; set; }
}

// ─── Ответы ─────────────────────────────────────────────────────────────────

/// <summary>DTO назначения пользователя на должность.</summary>
public class AssignmentDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserWorkEmail { get; set; } = string.Empty;

    public Guid PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>Ставка занятости (0.25, 0.5, 0.75, 1.0).</summary>
    public decimal Rate { get; set; }

    /// <summary>Основное назначение или совмещение.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Дата начала назначения.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Дата окончания назначения. Null означает бессрочное назначение.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Признак активного назначения: EndDate не указана или ещё не наступила.</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
