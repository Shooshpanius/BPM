using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления бизнес-процессами (CRUD + версии + диаграммы + настройки).</summary>
public interface IBpmProcessService
{
    /// <summary>Возвращает список процессов организации.</summary>
    Task<IReadOnlyList<BpmProcessListItemDto>> GetProcessesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Возвращает процесс по идентификатору.</summary>
    Task<BpmProcessDto> GetProcessByIdAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Создаёт новый процесс с пустым черновиком версии 1.</summary>
    Task<BpmProcessDto> CreateProcessAsync(CreateBpmProcessRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Обновляет метаданные процесса (название, описание).</summary>
    Task<BpmProcessDto> UpdateProcessAsync(Guid processId, UpdateBpmProcessRequest request, CancellationToken ct = default);

    /// <summary>Мягко удаляет процесс.</summary>
    Task DeleteProcessAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает список версий процесса (без XML).</summary>
    Task<IReadOnlyList<BpmProcessVersionInfoDto>> GetVersionsAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает текущую редактируемую диаграмму (последний черновик или активную версию).</summary>
    Task<BpmDiagramDto> GetDiagramAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Возвращает указанную версию диаграммы.</summary>
    Task<BpmDiagramDto> GetVersionAsync(Guid processId, Guid versionId, CancellationToken ct = default);

    /// <summary>Сохраняет XML-диаграмму в новый черновик-снапшот.</summary>
    Task<BpmDiagramDto> SaveDiagramAsync(Guid processId, SaveDiagramRequest request, Guid savedByUserId, CancellationToken ct = default);

    /// <summary>Публикует указанную версию процесса.</summary>
    Task<BpmProcessVersionInfoDto> PublishVersionAsync(Guid processId, Guid versionId, string? releaseNotes, CancellationToken ct = default);

    /// <summary>Возвращает список шаблонов организации.</summary>
    Task<IReadOnlyList<BpmProcessListItemDto>> GetTemplatesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Создаёт процесс из шаблона (копирует активную версию шаблона).</summary>
    Task<BpmProcessDto> CreateFromTemplateAsync(Guid templateId, CreateProcessFromTemplateRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Создаёт новый черновик-копию из выбранной версии.</summary>
    Task<BpmDiagramDto> RollbackVersionAsync(Guid processId, Guid versionId, Guid userId, CancellationToken ct = default);

    /// <summary>Выполняет валидацию версии процесса.</summary>
    Task<BpmValidationResultDto> ValidateProcessAsync(Guid processId, Guid? versionId, CancellationToken ct = default);

    /// <summary>Сравнивает две версии процесса.</summary>
    Task<BpmVersionDiffDto> DiffVersionsAsync(Guid processId, Guid leftVersionId, Guid rightVersionId, CancellationToken ct = default);

    /// <summary>Возвращает настройки процесса.</summary>
    Task<BpmProcessSettingsDto> GetSettingsAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Обновляет настройки процесса.</summary>
    Task<BpmProcessSettingsDto> UpdateSettingsAsync(Guid processId, UpdateBpmProcessSettingsRequest request, CancellationToken ct = default);

    /// <summary>Генерирует новый токен внешнего запуска процесса.</summary>
    Task<RotateExternalTokenResponse> RotateExternalTokenAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Запускает debug-сессию процесса.</summary>
    Task<BpmDebugSessionDto> StartDebugSessionAsync(Guid processId, StartBpmDebugSessionRequest request, CancellationToken ct = default);

    /// <summary>Возвращает состояние debug-сессии.</summary>
    Task<BpmDebugSessionDto> GetDebugSessionAsync(Guid processId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Продвигает debug-сессию на следующий шаг.</summary>
    Task<BpmDebugSessionDto> StepDebugSessionAsync(Guid processId, Guid sessionId, string action, CancellationToken ct = default);

    /// <summary>Генерирует PDF-регламент по активной версии процесса.</summary>
    Task<(byte[] Content, string FileName)> GenerateDocumentAsync(Guid processId, CancellationToken ct = default);
}
