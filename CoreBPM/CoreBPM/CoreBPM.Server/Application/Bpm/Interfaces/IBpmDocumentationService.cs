using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис документирования процессов (FR-BPM-02.6).</summary>
public interface IBpmDocumentationService
{
    /// <summary>
    /// Генерирует HTML-снапшот документации для указанной версии процесса и сохраняет в БД.
    /// Вызывается автоматически при публикации версии.
    /// </summary>
    Task GenerateAndSaveSnapshotAsync(Guid processId, Guid versionId, Guid generatedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает список процессов пользователя (Владелец или Куратор)
    /// с таблицей опубликованных версий.
    /// </summary>
    Task<IReadOnlyList<ProcessDocumentationItemDto>> GetMyDocumentationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает полный список процессов системы с таблицей опубликованных версий.
    /// Доступно только администраторам.
    /// </summary>
    Task<IReadOnlyList<ProcessDocumentationItemDto>> GetAllDocumentationAsync(bool includeDeleted, CancellationToken ct = default);

    /// <summary>Возвращает HTML-снапшот документации указанной версии процесса.</summary>
    Task<DocSnapshotDto> GetDocSnapshotAsync(Guid processId, Guid versionId, CancellationToken ct = default);
}
