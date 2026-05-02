using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления формами задач (FR-BPM-01.4).</summary>
public interface IFormService
{
    // ─── CRUD форм ────────────────────────────────────────────────────────────

    /// <summary>Возвращает список форм (опционально фильтр по processId).</summary>
    Task<IReadOnlyList<FormListItemDto>> GetFormsAsync(Guid? processId, CancellationToken ct = default);

    /// <summary>Возвращает форму по идентификатору.</summary>
    Task<FormDto> GetFormByIdAsync(Guid formId, CancellationToken ct = default);

    /// <summary>Создаёт новую форму с пустым черновиком версии 1.</summary>
    Task<FormDto> CreateFormAsync(CreateFormRequest request, CancellationToken ct = default);

    /// <summary>Обновляет метаданные формы.</summary>
    Task<FormDto> UpdateFormAsync(Guid formId, UpdateFormRequest request, CancellationToken ct = default);

    /// <summary>Удаляет форму (запрещено при наличии опубликованных версий).</summary>
    Task DeleteFormAsync(Guid formId, CancellationToken ct = default);

    // ─── Версионирование ──────────────────────────────────────────────────────

    /// <summary>Возвращает историю версий формы.</summary>
    Task<IReadOnlyList<FormVersionInfoDto>> GetVersionsAsync(Guid formId, CancellationToken ct = default);

    /// <summary>Возвращает указанную версию формы со схемой.</summary>
    Task<FormVersionDto> GetVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default);

    /// <summary>Сохраняет новый черновик формы (инкрементальный номер версии).</summary>
    Task<FormVersionDto> SaveDraftAsync(Guid formId, SaveFormVersionRequest request, CancellationToken ct = default);

    /// <summary>Публикует версию (предыдущая опубликованная → Archived).</summary>
    Task<FormVersionInfoDto> PublishVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default);

    /// <summary>Откат к версии: создаёт копию с новым номером версии.</summary>
    Task<FormVersionDto> RollbackVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default);

    /// <summary>Экспортирует версию формы как JSON-байты.</summary>
    Task<byte[]> ExportVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default);

    /// <summary>Импортирует JSON-данные как новый черновик версии формы.</summary>
    Task<FormVersionDto> ImportVersionAsync(Guid formId, byte[] jsonData, CancellationToken ct = default);
}
