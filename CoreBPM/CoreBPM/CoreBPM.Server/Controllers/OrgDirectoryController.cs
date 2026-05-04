using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер адресной книги — read-only представление оргструктуры для аутентифицированных пользователей.</summary>
[ApiController]
[Route("api/org/directory")]
[Authorize]
public class OrgDirectoryController : ControllerBase
{
    private readonly IOrgDirectoryService _service;

    public OrgDirectoryController(IOrgDirectoryService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список активных организаций.</summary>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(IReadOnlyList<DirectoryOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DirectoryOrganizationDto>>> GetOrganizations(CancellationToken ct)
        => Ok(await _service.GetOrganizationsAsync(ct));

    /// <summary>Возвращает иерархическое дерево подразделений указанной организации.</summary>
    [HttpGet("departments/tree")]
    [ProducesResponseType(typeof(IReadOnlyList<DirectoryDepartmentTreeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DirectoryDepartmentTreeDto>>> GetDepartmentTree(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.GetDepartmentTreeAsync(organizationId, ct));

    /// <summary>
    /// Возвращает страницу сотрудников адресной книги.
    /// Параметры: departmentId, organizationId, search, position, sortBy (name/position/department),
    /// sortDir (asc/desc), page, pageSize.
    /// </summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(DirectoryEmployeesPagedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DirectoryEmployeesPagedDto>> GetEmployees(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        [FromQuery] string? position,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetEmployeesAsync(
            organizationId, departmentId, search, position, sortBy, sortDir, page, pageSize, ct));

    /// <summary>
    /// Экспортирует список сотрудников в формате CSV (без пагинации).
    /// </summary>
    [HttpGet("employees/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportEmployees(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        [FromQuery] string? position,
        [FromQuery] string? sortBy,
        CancellationToken ct)
    {
        var employees = await _service.GetEmployeesForExportAsync(
            organizationId, departmentId, search, position, sortBy, ct);

        var csv = BuildCsv(employees);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv; charset=utf-8", "employees.csv");
    }

    // ── Вспомогательный метод ────────────────────────────────────────────────

    private static string BuildCsv(IReadOnlyList<DirectoryEmployeeDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ФИО;Должность;Подразделение;Организация;Email;Телефон");
        foreach (var e in items)
        {
            var name = CsvEscape(e.DisplayName);
            var pos  = CsvEscape(e.Position ?? string.Empty);
            var dept = CsvEscape(e.DepartmentName ?? string.Empty);
            var org  = CsvEscape(e.OrganizationName);
            var mail = CsvEscape(e.WorkEmail);
            var phone = CsvEscape(e.Phone ?? string.Empty);
            sb.AppendLine($"{name};{pos};{dept};{org};{mail};{phone}");
        }
        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

