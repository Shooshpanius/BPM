using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер управления подразделениями организаций (системная администрация).</summary>
[ApiController]
[Route("api/admin/departments")]
[Authorize(Roles = "Admin")]
public class AdminDepartmentsController : ControllerBase
{
    private readonly IAdminDepartmentService _service;

    public AdminDepartmentsController(IAdminDepartmentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает плоский список подразделений.
    /// Если передан параметр organizationId — фильтрует по организации.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(
        [FromQuery] Guid? organizationId,
        CancellationToken ct)
        => Ok(await _service.GetAllAsync(organizationId, ct));

    /// <summary>Возвращает иерархическое дерево подразделений указанной организации.</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentTreeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DepartmentTreeDto>>> GetTree(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.GetTreeAsync(organizationId, ct));

    /// <summary>Возвращает подразделение по идентификатору.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    /// <summary>Создаёт новое подразделение в указанной организации.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Обновляет данные подразделения.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DepartmentDto>> Update(
        Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Удаляет подразделение. Невозможно, если есть дочерние подразделения или активные сотрудники.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
