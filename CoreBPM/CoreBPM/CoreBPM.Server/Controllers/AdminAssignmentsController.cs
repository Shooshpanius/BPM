using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Административный контроллер управления назначениями пользователей на должности.
/// Доступен только пользователям с ролью Admin.
/// </summary>
[ApiController]
[Route("api/admin/assignments")]
[Authorize(Roles = "Admin")]
public class AdminAssignmentsController : ControllerBase
{
    private readonly IOrgAssignmentService _service;

    public AdminAssignmentsController(IOrgAssignmentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Создаёт назначение пользователя на должность.
    /// При IsPrimary=true проверяет отсутствие другого активного основного назначения.
    /// Автоматически применяет матрицу ролей должности.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> Create(
        [FromBody] CreateAssignmentRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Изменяет параметры назначения: ставку, тип (основное/совмещение) и даты.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> Update(
        Guid id,
        [FromBody] UpdateAssignmentRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>
    /// Завершает назначение (устанавливает дату окончания = вчера).
    /// Снимает роли должности, если у пользователя нет других активных назначений с теми же ролями.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
