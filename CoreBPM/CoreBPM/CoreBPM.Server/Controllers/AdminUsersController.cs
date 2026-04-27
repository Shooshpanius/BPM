using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер управления пользователями системы (системная администрация).</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;
    private readonly IAdminEmployeeService _employeeService;

    public AdminUsersController(IAdminUserService userService, IAdminEmployeeService employeeService)
    {
        _userService = userService;
        _employeeService = employeeService;
    }

    /// <summary>Возвращает список всех пользователей системы.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserListItemDto>>> GetAll(CancellationToken ct)
        => Ok(await _userService.GetAllAsync(ct));

    /// <summary>Возвращает пользователя по идентификатору.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserListItemDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _userService.GetByIdAsync(id, ct));

    /// <summary>Создаёт нового пользователя системы (профиль + учётная запись).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserListItemDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var result = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Обновляет профиль пользователя.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserListItemDto>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
        => Ok(await _userService.UpdateAsync(id, request, ct));

    /// <summary>Деактивирует пользователя системы.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _userService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Возвращает список записей сотрудника для указанного пользователя.</summary>
    [HttpGet("{id:guid}/employees")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetEmployees(Guid id, CancellationToken ct)
        => Ok(await _employeeService.GetByUserAsync(id, ct));
}
