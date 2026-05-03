namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>DTO вида деятельности (FR-TASK-01.4).</summary>
public class TaskActivityTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на создание / обновление вида деятельности.</summary>
public class UpsertActivityTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>DTO записи трудозатрат (FR-TASK-01.4).</summary>
public class TaskTimeLogDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid? ActivityTypeId { get; set; }
    public string? ActivityTypeName { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Запрос на добавление трудозатрат к задаче.</summary>
public class AddTimeLogRequest
{
    public Guid? ActivityTypeId { get; set; }
    /// <summary>Длительность в минутах (обязательное поле, > 0).</summary>
    public int DurationMinutes { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Запрос на изменение контролёра / типа контроля задачи (FR-TASK-01.4).</summary>
public class UpdateControlRequest
{
    /// <summary>Новый контролёр. Передайте null, чтобы снять контролёра.</summary>
    public Guid? ControllerUserId { get; set; }
    public string? ControlType { get; set; }
}
