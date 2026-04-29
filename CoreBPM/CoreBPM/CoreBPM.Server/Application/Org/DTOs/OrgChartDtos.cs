using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Org.DTOs;

// ─── Ответы оргчарта (FR-ORG-01.4) ──────────────────────────────────────────

/// <summary>
/// Узел дерева оргструктуры — подразделение с сотрудниками и дочерними узлами.
/// </summary>
public class OrgChartNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    /// <summary>Общее число сотрудников в подразделении (включая дочерние).</summary>
    public int TotalEmployeesCount { get; set; }

    /// <summary>Список сотрудников, чьё эффективное подразделение совпадает с данным узлом.</summary>
    public IReadOnlyList<OrgChartEmployeeDto> Employees { get; set; } = [];

    /// <summary>Вакантные ставки в подразделении. Заполняется только при extended=true.</summary>
    public IReadOnlyList<OrgChartVacancyDto> Vacancies { get; set; } = [];

    /// <summary>Дочерние подразделения.</summary>
    public IReadOnlyList<OrgChartNodeDto> Children { get; set; } = [];
}

/// <summary>
/// Карточка сотрудника в узле оргструктуры.
/// </summary>
public class OrgChartEmployeeDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? WorkEmail { get; set; }
    public string? Phone { get; set; }

    public Guid? PositionId { get; set; }
    public string? PositionName { get; set; }

    /// <summary>Категория должности (Managerial / Regular / Project).</summary>
    public PositionCategory? PositionCategory { get; set; }

    /// <summary>Ставка занятости. Заполняется только при extended=true.</summary>
    public decimal? Rate { get; set; }
}

/// <summary>
/// Вакантная ставка в подразделении. Возвращается только при extended=true.
/// </summary>
public class OrgChartVacancyDto
{
    public Guid PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;

    /// <summary>Число вакантных ставок (PlannedHeadcount − OccupiedHeadcount).</summary>
    public decimal VacancyCount { get; set; }
}

/// <summary>
/// Корневой ответ GET /api/org/chart.
/// Содержит дерево подразделений и сотрудников без назначенного подразделения.
/// </summary>
public class OrgChartDto
{
    /// <summary>Дерево активных подразделений с сотрудниками.</summary>
    public IReadOnlyList<OrgChartNodeDto> Departments { get; set; } = [];

    /// <summary>Сотрудники без подразделения (назначение не привязано ни к одному узлу).</summary>
    public IReadOnlyList<OrgChartEmployeeDto> UnassignedEmployees { get; set; } = [];
}
