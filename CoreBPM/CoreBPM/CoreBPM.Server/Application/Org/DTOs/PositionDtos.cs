using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Org.DTOs;

// ─── Запросы ────────────────────────────────────────────────────────────────

/// <summary>Запрос на создание должности.</summary>
public class CreatePositionRequest
{
    /// <summary>Наименование должности.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Уникальный код должности в рамках подразделения.</summary>
    public string? Code { get; set; }

    /// <summary>Описание должности.</summary>
    public string? Description { get; set; }

    /// <summary>Идентификатор подразделения.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Категория должности.</summary>
    public PositionCategory Category { get; set; } = PositionCategory.Regular;

    /// <summary>Плановое число ставок.</summary>
    public decimal PlannedHeadcount { get; set; } = 1;
}

/// <summary>Запрос на обновление должности.</summary>
public class UpdatePositionRequest
{
    /// <summary>Наименование должности.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Уникальный код должности в рамках подразделения.</summary>
    public string? Code { get; set; }

    /// <summary>Описание должности.</summary>
    public string? Description { get; set; }

    /// <summary>Идентификатор подразделения.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Категория должности.</summary>
    public PositionCategory Category { get; set; }

    /// <summary>Статус должности.</summary>
    public PositionStatus Status { get; set; }

    /// <summary>Плановое число ставок.</summary>
    public decimal PlannedHeadcount { get; set; }
}

/// <summary>Запрос на задание матрицы ролей должности.</summary>
public class SetPositionRoleMappingsRequest
{
    /// <summary>Список названий ролей, которые должны быть закреплены за должностью.</summary>
    public IReadOnlyList<string> RoleNames { get; set; } = [];
}

// ─── Ответы ─────────────────────────────────────────────────────────────────

/// <summary>DTO должности.</summary>
public class PositionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }

    /// <summary>Краткое название подразделения.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    public PositionCategory Category { get; set; }
    public PositionStatus Status { get; set; }

    /// <summary>Плановое число ставок.</summary>
    public decimal PlannedHeadcount { get; set; }

    /// <summary>Занятых ставок (вычисляется при наличии назначений; пока 0 до FR-ORG-01.3).</summary>
    public decimal OccupiedHeadcount { get; set; }

    /// <summary>Количество вакантных ставок (PlannedHeadcount − OccupiedHeadcount).</summary>
    public decimal VacancyCount => PlannedHeadcount - OccupiedHeadcount;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Матрица ролей, закреплённых за должностью.</summary>
    public IReadOnlyList<PositionRoleMappingResponse> RoleMappings { get; set; } = [];

    /// <summary>Вложения (должностные инструкции).</summary>
    public IReadOnlyList<PositionAttachmentResponse> Attachments { get; set; } = [];
}

/// <summary>DTO записи матрицы ролей должности.</summary>
public class PositionRoleMappingResponse
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>DTO вложения должности.</summary>
public class PositionAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
