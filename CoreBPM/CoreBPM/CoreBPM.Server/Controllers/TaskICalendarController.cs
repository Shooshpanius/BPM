using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Экспорт задач пользователя в формате iCalendar (RFC 5545). FR-TASK-02.3.
/// Внешние клиенты (Google Calendar, Outlook) могут подписаться на URL
/// <c>GET /api/users/me/tasks.ics?token=…</c>.
/// </summary>
[ApiController]
[Authorize]
public class TaskICalendarController : ControllerBase
{
    private readonly AppDbContext _db;

    public TaskICalendarController(AppDbContext db) => _db = db;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>
    /// Экспортировать активные задачи пользователя в формате iCalendar (.ics). FR-TASK-02.3.
    /// Возвращает все открытые задачи, в которых текущий пользователь является исполнителем или автором.
    /// </summary>
    [HttpGet("api/users/me/tasks.ics")]
    [Produces("text/calendar")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportICalendar(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var finalStatuses = new[]
        {
            Domain.Tasks.TaskStatus.Done,
            Domain.Tasks.TaskStatus.DoneControlled,
            Domain.Tasks.TaskStatus.CannotDo,
            Domain.Tasks.TaskStatus.CannotDoControlled,
            Domain.Tasks.TaskStatus.Closed,
        };

        var tasks = await _db.TaskItems.AsNoTracking()
            .Where(t => (t.AssigneeUserId == userId || t.AuthorUserId == userId)
                        && !finalStatuses.Contains(t.Status))
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);

        var userDisplay = (await _db.OrgUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct))?.DisplayName ?? userId.ToString();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//CoreBPM//Task Calendar//RU");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:Мои задачи — {EscapeIcs(userDisplay)}");
        sb.AppendLine("X-WR-CALDESC:Активные задачи CoreBPM");

        var now = DateTimeOffset.UtcNow;
        foreach (var task in tasks)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{task.Id}@corebpm");
            sb.AppendLine($"DTSTAMP:{FormatIcsDate(now)}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{task.DueDate:yyyyMMdd}");
            sb.AppendLine($"DUE:{FormatIcsDate(task.DueDate)}");
            sb.AppendLine($"SUMMARY:T-{task.Number} {EscapeIcs(task.Subject)}");
            if (!string.IsNullOrEmpty(task.Description))
                sb.AppendLine($"DESCRIPTION:{EscapeIcs(task.Description)}");
            sb.AppendLine($"STATUS:{MapTaskStatusToIcs(task.Status)}");
            sb.AppendLine($"PRIORITY:{MapPriorityToIcs(task.Priority)}");
            if (task.ScheduledAt.HasValue)
                sb.AppendLine($"DTSTART:{FormatIcsDate(task.ScheduledAt.Value)}");
            sb.AppendLine($"LAST-MODIFIED:{FormatIcsDate(task.UpdatedAt)}");
            sb.AppendLine($"CREATED:{FormatIcsDate(task.CreatedAt)}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        return Content(sb.ToString(), "text/calendar; charset=utf-8");
    }

    private static string FormatIcsDate(DateTimeOffset dt)
        => dt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    private static string EscapeIcs(string? text)
        => (text ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", "");

    private static string MapTaskStatusToIcs(Domain.Tasks.TaskStatus status) => status switch
    {
        Domain.Tasks.TaskStatus.InProgress => "IN-PROCESS",
        Domain.Tasks.TaskStatus.Done or Domain.Tasks.TaskStatus.DoneControlled => "COMPLETED",
        Domain.Tasks.TaskStatus.Closed => "CANCELLED",
        _ => "NEEDS-ACTION",
    };

    private static int MapPriorityToIcs(Domain.Tasks.TaskPriority priority) => priority switch
    {
        Domain.Tasks.TaskPriority.Critical => 1,
        Domain.Tasks.TaskPriority.High => 3,
        Domain.Tasks.TaskPriority.Medium => 5,
        Domain.Tasks.TaskPriority.Low => 9,
        _ => 5,
    };
}
