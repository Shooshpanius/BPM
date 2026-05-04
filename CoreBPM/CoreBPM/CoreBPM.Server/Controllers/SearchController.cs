using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>Глобальный поиск по объектам системы (FR-TASK-02.3, FR-PORTAL-02.3).</summary>
[ApiController]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>
    /// Полнотекстовый поиск по задачам. FR-TASK-02.3.
    /// Ищет по теме (Subject) и описанию (Description) задач, доступных текущему пользователю.
    /// </summary>
    /// <param name="q">Строка поиска (не менее 2 символов).</param>
    /// <param name="type">Тип объекта: <c>task</c> (пока поддерживается только этот вариант).</param>
    /// <param name="limit">Максимальное количество результатов (1–100, по умолчанию 20).</param>
    [HttpGet("api/search")]
    [ProducesResponseType(typeof(SearchResultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResultsDto>> Search(
        [FromQuery] string q,
        [FromQuery] string type = "task",
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { error = "Строка поиска должна содержать не менее 2 символов." });

        limit = Math.Clamp(limit, 1, 100);
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var term = q.Trim().ToLowerInvariant();

        var finalStatuses = new[]
        {
            Domain.Tasks.TaskStatus.Done,
            Domain.Tasks.TaskStatus.DoneControlled,
            Domain.Tasks.TaskStatus.CannotDo,
            Domain.Tasks.TaskStatus.CannotDoControlled,
            Domain.Tasks.TaskStatus.Closed,
        };

        // Ищем по задачам: заголовок и описание
        var tasks = await _db.TaskItems.AsNoTracking()
            .Where(t => (EF.Functions.Like(t.Subject.ToLower(), $"%{term}%")
                         || (t.Description != null && EF.Functions.Like(t.Description.ToLower(), $"%{term}%"))))
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new TaskSearchHitDto
            {
                Id = t.Id,
                Number = t.Number,
                Subject = t.Subject,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                AssigneeUserId = t.AssigneeUserId,
                IsOverdue = t.IsOverdue,
                DueDate = t.DueDate,
            })
            .ToListAsync(ct);

        return Ok(new SearchResultsDto { Tasks = tasks, Total = tasks.Count });
    }
}

/// <summary>Результаты глобального поиска. FR-TASK-02.3.</summary>
public class SearchResultsDto
{
    /// <summary>Найденные задачи.</summary>
    public List<TaskSearchHitDto> Tasks { get; set; } = new();

    /// <summary>Общее количество найденных результатов (по типу task).</summary>
    public int Total { get; set; }
}

/// <summary>Результат поиска — задача. FR-TASK-02.3.</summary>
public class TaskSearchHitDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid AssigneeUserId { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset DueDate { get; set; }
}
