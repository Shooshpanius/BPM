using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Эндпоинты задач по резолюции в разрезе документов (FR-TASK-01.5.3).
/// </summary>
[ApiController]
[Authorize]
public class DocumentResolutionsController : ControllerBase
{
    private readonly ITaskService _service;

    public DocumentResolutionsController(ITaskService service)
    {
        _service = service;
    }

    /// <summary>Получить все задачи-резолюции по документу.</summary>
    /// <param name="documentId">Идентификатор документа.</param>
    [HttpGet("api/documents/{documentId:guid}/resolutions")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetResolutions(Guid documentId, CancellationToken ct)
        => Ok(await _service.GetDocumentResolutionsAsync(documentId, ct));
}
