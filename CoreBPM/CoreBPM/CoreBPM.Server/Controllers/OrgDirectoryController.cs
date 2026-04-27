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
    /// Возвращает сотрудников адресной книги.
    /// Параметры фильтрации: departmentId, organizationId, search (поиск по имени / email / должности).
    /// </summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(IReadOnlyList<DirectoryEmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DirectoryEmployeeDto>>> GetEmployees(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _service.GetEmployeesAsync(organizationId, departmentId, search, ct));
}
