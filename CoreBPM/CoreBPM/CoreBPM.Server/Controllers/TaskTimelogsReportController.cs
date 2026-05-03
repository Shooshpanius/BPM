using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>Отчёт по трудозатратам задач (FR-TASK-01.4).</summary>
[ApiController]
[Authorize]
public class TaskTimelogsReportController : ControllerBase
{
    private readonly AppDbContext _db;
    public TaskTimelogsReportController(AppDbContext db) => _db = db;

    /// <summary>Возвращает список записей трудозатрат с фильтрацией.</summary>
    /// <param name="userId">Фильтр по пользователю.</param>
    /// <param name="taskId">Фильтр по задаче.</param>
    /// <param name="dateFrom">Начало периода (включительно).</param>
    /// <param name="dateTo">Конец периода (включительно).</param>
    /// <param name="page">Номер страницы (с 1).</param>
    /// <param name="perPage">Размер страницы (max 100).</param>
    [HttpGet("api/reports/timelogs")]
    [ProducesResponseType(typeof(TimelogReportPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TimelogReportPageDto>> Get(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? taskId,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 50,
        CancellationToken ct = default)
    {
        perPage = Math.Clamp(perPage, 1, 100);
        page = Math.Max(1, page);

        var query = _db.TaskTimeLogs
            .AsNoTracking()
            .Include(l => l.Task)
            .AsQueryable();

        if (userId.HasValue) query = query.Where(l => l.UserId == userId.Value);
        if (taskId.HasValue) query = query.Where(l => l.TaskId == taskId.Value);
        if (dateFrom.HasValue) query = query.Where(l => l.StartDate >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(l => l.StartDate <= dateTo.Value);

        var total = await query.CountAsync(ct);
        var totalMinutes = await query.SumAsync(l => (int?)l.DurationMinutes, ct) ?? 0;

        var logs = await query
            .OrderByDescending(l => l.StartDate)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);

        // Загружаем имена пользователей
        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        // Загружаем виды деятельности
        var activityIds = logs.Where(l => l.ActivityTypeId.HasValue).Select(l => l.ActivityTypeId!.Value).Distinct().ToList();
        var activityNames = activityIds.Any()
            ? await _db.TaskActivityTypes.AsNoTracking()
                .Where(a => activityIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct)
            : new Dictionary<Guid, string>();

        var items = logs.Select(l => new TimelogReportItemDto
        {
            Id = l.Id,
            TaskId = l.TaskId,
            TaskNumber = l.Task?.Number ?? 0,
            TaskSubject = l.Task?.Subject ?? string.Empty,
            UserId = l.UserId,
            UserName = userNames.GetValueOrDefault(l.UserId, l.UserId.ToString()),
            ActivityTypeId = l.ActivityTypeId,
            ActivityTypeName = l.ActivityTypeId.HasValue ? activityNames.GetValueOrDefault(l.ActivityTypeId.Value) : null,
            DurationMinutes = l.DurationMinutes,
            StartDate = l.StartDate,
            Comment = l.Comment,
            CreatedAt = l.CreatedAt,
        }).ToList();

        return Ok(new TimelogReportPageDto
        {
            Items = items,
            TotalCount = total,
            TotalMinutes = totalMinutes,
            Page = page,
            PerPage = perPage,
        });
    }

    /// <summary>Экспортирует трудозатраты в CSV.</summary>
    [HttpGet("api/reports/timelogs/export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? taskId,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken ct = default)
    {
        var query = _db.TaskTimeLogs
            .AsNoTracking()
            .Include(l => l.Task)
            .AsQueryable();

        if (userId.HasValue) query = query.Where(l => l.UserId == userId.Value);
        if (taskId.HasValue) query = query.Where(l => l.TaskId == taskId.Value);
        if (dateFrom.HasValue) query = query.Where(l => l.StartDate >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(l => l.StartDate <= dateTo.Value);

        var logs = await query.OrderByDescending(l => l.StartDate).ToListAsync(ct);

        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var activityIds = logs.Where(l => l.ActivityTypeId.HasValue).Select(l => l.ActivityTypeId!.Value).Distinct().ToList();
        var activityNames = activityIds.Any()
            ? await _db.TaskActivityTypes.AsNoTracking()
                .Where(a => activityIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct)
            : new Dictionary<Guid, string>();

        var sb = new StringBuilder();
        sb.AppendLine("Задача,Номер,Пользователь,Вид деятельности,Длительность (мин),Дата начала,Комментарий");

        foreach (var l in logs)
        {
            var taskSubject = CsvEscape(l.Task?.Subject ?? string.Empty);
            var taskNum = l.Task?.Number.ToString() ?? string.Empty;
            var uName = CsvEscape(userNames.GetValueOrDefault(l.UserId, l.UserId.ToString()));
            var actName = CsvEscape(l.ActivityTypeId.HasValue ? activityNames.GetValueOrDefault(l.ActivityTypeId.Value, string.Empty) : string.Empty);
            var startDate = l.StartDate.ToString("yyyy-MM-dd HH:mm");
            var comment = CsvEscape(l.Comment ?? string.Empty);
            sb.AppendLine($"{taskSubject},{taskNum},{uName},{actName},{l.DurationMinutes},{startDate},{comment}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv;charset=utf-8", "timelogs-report.csv");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
