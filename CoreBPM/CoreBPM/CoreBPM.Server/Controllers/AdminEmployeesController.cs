using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер управления сотрудниками организаций (системная администрация).</summary>
[ApiController]
[Route("api/admin/employees")]
[Authorize(Roles = "Admin")]
public class AdminEmployeesController : ControllerBase
{
    private readonly IAdminEmployeeService _service;

    public AdminEmployeesController(IAdminEmployeeService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает список сотрудников. Если передан параметр organizationId — фильтрует по организации.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(
        [FromQuery] Guid? organizationId,
        CancellationToken ct)
        => Ok(await _service.GetAllAsync(organizationId, ct));

    /// <summary>
    /// Создаёт запись сотрудника — привязывает пользователя к организации.
    /// Сотрудник создаётся из карточки пользователя; пара (userId, organizationId) уникальна.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Обновляет данные сотрудника (должность, активность).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Удаляет запись сотрудника.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
