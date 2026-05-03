namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>Одна запись отчёта по трудозатратам (FR-TASK-01.4).</summary>
public class TimelogReportItemDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public int TaskNumber { get; set; }
    public string TaskSubject { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid? ActivityTypeId { get; set; }
    public string? ActivityTypeName { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Страница отчёта по трудозатратам (FR-TASK-01.4).</summary>
public class TimelogReportPageDto
{
    public IReadOnlyList<TimelogReportItemDto> Items { get; set; } = Array.Empty<TimelogReportItemDto>();
    public int TotalCount { get; set; }
    /// <summary>Суммарная длительность по всем записям (минуты).</summary>
    public int TotalMinutes { get; set; }
    public int Page { get; set; }
    public int PerPage { get; set; }
}
