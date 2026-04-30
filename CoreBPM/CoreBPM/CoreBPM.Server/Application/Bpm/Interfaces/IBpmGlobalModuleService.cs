using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления глобальными C#-модулями (FR-BPM-01.7).</summary>
public interface IBpmGlobalModuleService
{
    /// <summary>Возвращает список глобальных модулей организации.</summary>
    Task<IReadOnlyList<BpmGlobalModuleDto>> ListAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Возвращает модуль по идентификатору.</summary>
    Task<BpmGlobalModuleDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт новый глобальный модуль.</summary>
    Task<BpmGlobalModuleDto> CreateAsync(CreateGlobalModuleRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Обновляет метаданные модуля (название, описание).</summary>
    Task<BpmGlobalModuleDto> UpdateAsync(Guid id, UpdateGlobalModuleRequest request, CancellationToken ct = default);

    /// <summary>Мягко удаляет модуль.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Публикует модуль — все файлы становятся доступны в среде выполнения сценариев.</summary>
    Task<BpmGlobalModuleDto> PublishAsync(Guid id, CancellationToken ct = default);

    // Файлы модуля

    /// <summary>Возвращает список файлов модуля, упорядоченных по полю Order.</summary>
    Task<IReadOnlyList<BpmGlobalModuleFileDto>> ListFilesAsync(Guid moduleId, CancellationToken ct = default);

    /// <summary>Добавляет новый файл в модуль.</summary>
    Task<BpmGlobalModuleFileDto> AddFileAsync(Guid moduleId, CreateGlobalModuleFileRequest request, CancellationToken ct = default);

    /// <summary>Обновляет файл модуля.</summary>
    Task<BpmGlobalModuleFileDto> UpdateFileAsync(Guid moduleId, Guid fileId, UpdateGlobalModuleFileRequest request, CancellationToken ct = default);

    /// <summary>Удаляет файл из модуля.</summary>
    Task DeleteFileAsync(Guid moduleId, Guid fileId, CancellationToken ct = default);

    /// <summary>Изменяет порядок файлов модуля.</summary>
    Task ReorderFilesAsync(Guid moduleId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
