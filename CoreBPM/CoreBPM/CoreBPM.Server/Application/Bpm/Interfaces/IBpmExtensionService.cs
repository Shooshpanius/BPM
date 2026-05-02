using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления пользовательскими расширениями палитры дизайнера (FR-BPM-01.7).</summary>
public interface IBpmExtensionService
{
    /// <summary>Возвращает список расширений организации, сгруппированных по папкам.</summary>
    Task<IReadOnlyList<BpmDesignerExtensionDto>> ListAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Возвращает расширение по идентификатору.</summary>
    Task<BpmDesignerExtensionDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт новое расширение.</summary>
    Task<BpmDesignerExtensionDto> CreateAsync(CreateDesignerExtensionRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Обновляет расширение.</summary>
    Task<BpmDesignerExtensionDto> UpdateAsync(Guid id, UpdateDesignerExtensionRequest request, CancellationToken ct = default);

    /// <summary>Мягко удаляет расширение.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Публикует расширение — оно становится доступным в палитре дизайнера.</summary>
    Task<BpmDesignerExtensionDto> PublishAsync(Guid id, CancellationToken ct = default);

    /// <summary>Копирует расширение как основу для нового (черновик).</summary>
    Task<BpmDesignerExtensionDto> CopyAsync(Guid id, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Экспортирует все расширения организации как JSON-байты.</summary>
    Task<byte[]> ExportAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Импортирует расширения из JSON-байт (создаёт или обновляет по имени).</summary>
    Task<IReadOnlyList<BpmDesignerExtensionDto>> ImportAsync(Guid organizationId, byte[] jsonData, Guid createdByUserId, CancellationToken ct = default);
}
