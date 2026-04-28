using Microsoft.AspNetCore.Http;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис управления должностями.</summary>
public interface IOrgPositionsService
{
    /// <summary>
    /// Возвращает список должностей с опциональной фильтрацией.
    /// </summary>
    /// <param name="departmentId">Фильтр по подразделению. Null — все подразделения.</param>
    /// <param name="category">Фильтр по категории. Null — все категории.</param>
    /// <param name="status">Фильтр по статусу. Null — только активные.</param>
    Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(
        Guid? departmentId = null,
        PositionCategory? category = null,
        PositionStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Возвращает должность по идентификатору.</summary>
    Task<PositionResponse> GetPositionByIdAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>Создаёт новую должность.</summary>
    Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken ct = default);

    /// <summary>Обновляет данные должности.</summary>
    Task<PositionResponse> UpdatePositionAsync(Guid positionId, UpdatePositionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Архивирует должность (мягкое удаление).
    /// Запрет при наличии действующих назначений (будет проверяться при реализации FR-ORG-01.3).
    /// </summary>
    Task ArchivePositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>Возвращает матрицу ролей должности.</summary>
    Task<IReadOnlyList<PositionRoleMappingResponse>> GetRoleMappingsAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>
    /// Полностью заменяет матрицу ролей должности.
    /// Существующие привязки, не вошедшие в новый список, удаляются.
    /// </summary>
    Task<IReadOnlyList<PositionRoleMappingResponse>> SetRoleMappingsAsync(
        Guid positionId,
        SetPositionRoleMappingsRequest request,
        CancellationToken ct = default);

    /// <summary>Сохраняет вложение должностной инструкции.</summary>
    Task<PositionAttachmentResponse> AddAttachmentAsync(
        Guid positionId,
        IFormFile file,
        string? description,
        CancellationToken ct = default);

    /// <summary>Удаляет вложение должности.</summary>
    Task DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
}
