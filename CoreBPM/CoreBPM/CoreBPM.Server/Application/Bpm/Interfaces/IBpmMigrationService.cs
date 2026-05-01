using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления пакетами миграции версий экземпляров (FR-BPM-02.7).</summary>
public interface IBpmMigrationService
{
    /// <summary>Возвращает список пакетов миграции с фильтрацией и пагинацией.</summary>
    Task<IReadOnlyList<MigrationPackageListItemDto>> GetPackagesAsync(
        BpmMigrationPackageStatus? status,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Возвращает детальное представление пакета.</summary>
    Task<MigrationPackageDetailDto> GetPackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>Создаёт новый пакет миграции в статусе New.</summary>
    Task<MigrationPackageDetailDto> CreatePackageAsync(
        Guid createdByUserId,
        CreateMigrationPackageRequest request,
        CancellationToken ct = default);

    /// <summary>Запускает выполнение пакета: последовательно обрабатывает каждый элемент.</summary>
    Task StartPackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>Отменяет пакет (допустимо только из статусов New или Running).</summary>
    Task CancelPackageAsync(Guid packageId, CancellationToken ct = default);

    /// <summary>Возвращает список элементов пакета с фильтрацией и пагинацией.</summary>
    Task<IReadOnlyList<MigrationItemDto>> GetPackageItemsAsync(
        Guid packageId,
        BpmMigrationItemStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Отмечает элемент пакета как обработанный вручную:
    /// статус меняется на Migrated, сохраняется ссылка на ручное изменение.
    /// </summary>
    Task ManualMigrateItemAsync(
        Guid packageId,
        Guid itemId,
        ManualMigrateItemRequest request,
        CancellationToken ct = default);
}
