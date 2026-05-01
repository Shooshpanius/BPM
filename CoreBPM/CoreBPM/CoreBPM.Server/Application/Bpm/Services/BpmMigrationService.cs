using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления пакетами миграции версий (FR-BPM-02.7).</summary>
public class BpmMigrationService : IBpmMigrationService
{
    private readonly AppDbContext _db;

    public BpmMigrationService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MigrationPackageListItemDto>> GetPackagesAsync(
        BpmMigrationPackageStatus? status,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.BpmVersionMigrationPackages
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);
        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        // Загружаем статистику по items
        var packageIds = packages.Select(p => p.Id).ToList();
        var stats = await _db.BpmVersionMigrationItems
            .AsNoTracking()
            .Where(i => packageIds.Contains(i.PackageId))
            .GroupBy(i => i.PackageId)
            .Select(g => new
            {
                PackageId = g.Key,
                Total = g.Count(),
                Migrated = g.Count(i => i.Status == BpmMigrationItemStatus.Migrated),
                Errors = g.Count(i =>
                    i.Status == BpmMigrationItemStatus.CriticalError ||
                    i.Status == BpmMigrationItemStatus.OtherError ||
                    i.Status == BpmMigrationItemStatus.RequiresManualHandling)
            })
            .ToDictionaryAsync(x => x.PackageId, ct);

        // Загружаем имена пользователей
        var userIds = packages.Select(p => p.CreatedByUserId).Distinct().ToList();
        var users = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return packages.Select(p =>
        {
            var s = stats.TryGetValue(p.Id, out var st) ? st : null;
            var userName = users.TryGetValue(p.CreatedByUserId, out var n) ? n : string.Empty;
            return new MigrationPackageListItemDto(
                p.Id, p.Name, p.CreatedByUserId, userName,
                p.Status, p.IsActive, p.CreatedAt,
                s?.Total ?? 0, s?.Migrated ?? 0, s?.Errors ?? 0);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<MigrationPackageDetailDto> GetPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await _db.BpmVersionMigrationPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new NotFoundException($"Пакет миграции {packageId} не найден");

        var stats = await _db.BpmVersionMigrationItems
            .AsNoTracking()
            .Where(i => i.PackageId == packageId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Migrated = g.Count(i => i.Status == BpmMigrationItemStatus.Migrated),
                Errors = g.Count(i =>
                    i.Status == BpmMigrationItemStatus.CriticalError ||
                    i.Status == BpmMigrationItemStatus.OtherError ||
                    i.Status == BpmMigrationItemStatus.RequiresManualHandling),
                Pending = g.Count(i =>
                    i.Status == BpmMigrationItemStatus.New ||
                    i.Status == BpmMigrationItemStatus.InProgress)
            })
            .FirstOrDefaultAsync(ct);

        var user = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => u.Id == package.CreatedByUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new MigrationPackageDetailDto(
            package.Id, package.Name,
            package.CreatedByUserId, user,
            package.Status, package.IsActive,
            package.CreatedAt, package.CompletedAt,
            stats?.Total ?? 0,
            stats?.Migrated ?? 0,
            stats?.Errors ?? 0,
            stats?.Pending ?? 0);
    }

    /// <inheritdoc />
    public async Task<MigrationPackageDetailDto> CreatePackageAsync(
        Guid createdByUserId,
        CreateMigrationPackageRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Наименование пакета миграции не может быть пустым");

        if (request.Items == null || request.Items.Count == 0)
            throw new ValidationException("Пакет миграции должен содержать хотя бы один элемент");

        // Проверяем уникальность экземпляров в запросе
        var duplicates = request.Items
            .GroupBy(i => i.InstanceId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new ValidationException("В пакете миграции один экземпляр встречается более одного раза");

        var now = DateTimeOffset.UtcNow;
        var package = new BpmVersionMigrationPackage
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedByUserId = createdByUserId,
            Status = BpmMigrationPackageStatus.New,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.BpmVersionMigrationPackages.Add(package);

        // Создаём элементы пакета
        var instanceIds = request.Items.Select(i => i.InstanceId).ToList();
        var instances = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        foreach (var itemReq in request.Items)
        {
            instances.TryGetValue(itemReq.InstanceId, out var instance);
            var item = new BpmVersionMigrationItem
            {
                Id = Guid.NewGuid(),
                PackageId = package.Id,
                InstanceId = itemReq.InstanceId,
                ProcessId = instance?.ProcessId ?? Guid.Empty,
                TargetVersionId = itemReq.TargetVersionId,
                Status = BpmMigrationItemStatus.New
            };
            _db.BpmVersionMigrationItems.Add(item);
        }

        await _db.SaveChangesAsync(ct);
        return await GetPackageAsync(package.Id, ct);
    }

    /// <inheritdoc />
    public async Task StartPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await _db.BpmVersionMigrationPackages
            .FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new NotFoundException($"Пакет миграции {packageId} не найден");

        if (package.Status != BpmMigrationPackageStatus.New)
            throw new ValidationException("Запустить можно только пакет в статусе «Новый»");

        package.Status = BpmMigrationPackageStatus.Running;
        package.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Загружаем элементы, ожидающие обработки
        var items = await _db.BpmVersionMigrationItems
            .Include(i => i.Instance)
            .Where(i => i.PackageId == packageId && i.Status == BpmMigrationItemStatus.New)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            await ProcessItemAsync(item, ct);
        }

        // Обновляем итоговый статус пакета
        var hasErrors = await _db.BpmVersionMigrationItems
            .AsNoTracking()
            .Where(i => i.PackageId == packageId)
            .AnyAsync(i =>
                i.Status == BpmMigrationItemStatus.CriticalError ||
                i.Status == BpmMigrationItemStatus.OtherError ||
                i.Status == BpmMigrationItemStatus.RequiresManualHandling, ct);

        package.Status = hasErrors
            ? BpmMigrationPackageStatus.CompletedWithErrors
            : BpmMigrationPackageStatus.Completed;
        package.IsActive = false;
        package.CompletedAt = DateTimeOffset.UtcNow;
        package.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CancelPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        var package = await _db.BpmVersionMigrationPackages
            .FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new NotFoundException($"Пакет миграции {packageId} не найден");

        if (package.Status != BpmMigrationPackageStatus.New &&
            package.Status != BpmMigrationPackageStatus.Running)
            throw new ValidationException("Отменить можно только пакет в статусе «Новый» или «Выполняется»");

        package.Status = BpmMigrationPackageStatus.Cancelled;
        package.IsActive = false;
        package.CompletedAt = DateTimeOffset.UtcNow;
        package.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MigrationItemDto>> GetPackageItemsAsync(
        Guid packageId,
        BpmMigrationItemStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Проверяем существование пакета
        if (!await _db.BpmVersionMigrationPackages.AnyAsync(p => p.Id == packageId, ct))
            throw new NotFoundException($"Пакет миграции {packageId} не найден");

        var query = _db.BpmVersionMigrationItems
            .AsNoTracking()
            .Include(i => i.Instance)
            .Include(i => i.Process)
            .Include(i => i.TargetVersion)
            .Where(i => i.PackageId == packageId);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);
        var items = await query
            .OrderBy(i => i.Status)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        return items.Select(i => new MigrationItemDto(
            i.Id, i.PackageId,
            i.InstanceId, i.Instance?.Name ?? string.Empty,
            i.ProcessId, i.Process?.Name ?? string.Empty,
            i.TargetVersionId, i.TargetVersion?.VersionNumber ?? 0,
            i.Status,
            i.ErrorComment, i.ManualChangeUrl,
            i.ProcessedAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task ManualMigrateItemAsync(
        Guid packageId,
        Guid itemId,
        ManualMigrateItemRequest request,
        CancellationToken ct = default)
    {
        var item = await _db.BpmVersionMigrationItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.PackageId == packageId, ct)
            ?? throw new NotFoundException($"Элемент {itemId} пакета {packageId} не найден");

        if (item.Status != BpmMigrationItemStatus.RequiresManualHandling &&
            item.Status != BpmMigrationItemStatus.CriticalError &&
            item.Status != BpmMigrationItemStatus.OtherError)
            throw new ValidationException("Ручная обработка доступна только для элементов с ошибками или требующих ручной обработки");

        item.Status = BpmMigrationItemStatus.Migrated;
        item.ManualChangeUrl = request.ManualChangeUrl;
        item.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Вспомогательные методы
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Обрабатывает один элемент пакета: проверяет условия и переводит экземпляр на новую версию.</summary>
    private async Task ProcessItemAsync(BpmVersionMigrationItem item, CancellationToken ct)
    {
        item.Status = BpmMigrationItemStatus.InProgress;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Проверяем: не переводится ли экземпляр другим активным пакетом
            var busyInOtherPackage = await _db.BpmVersionMigrationItems
                .AsNoTracking()
                .AnyAsync(i =>
                    i.InstanceId == item.InstanceId &&
                    i.Id != item.Id &&
                    i.Status == BpmMigrationItemStatus.InProgress, ct);

            if (busyInOtherPackage)
            {
                item.Status = BpmMigrationItemStatus.Busy;
                item.ErrorComment = "Экземпляр уже переводится другим пакетом миграции";
                item.ProcessedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }

            // Загружаем экземпляр
            var instance = await _db.BpmInstances
                .FirstOrDefaultAsync(i => i.Id == item.InstanceId, ct);

            if (instance == null ||
                instance.State != BpmInstanceState.Active &&
                instance.State != BpmInstanceState.Suspended)
            {
                item.Status = BpmMigrationItemStatus.NotApplicable;
                item.ErrorComment = instance == null
                    ? "Экземпляр процесса не найден"
                    : $"Экземпляр находится в статусе {instance.State} — перевод невозможен";
                item.ProcessedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }

            // Проверяем: экземпляр относится к тому же процессу
            if (instance.ProcessId != item.ProcessId)
            {
                item.ProcessId = instance.ProcessId;
            }

            // Проверяем: версия уже совпадает с целевой
            if (instance.ProcessVersionId == item.TargetVersionId)
            {
                item.Status = BpmMigrationItemStatus.NoMigrationNeeded;
                item.ProcessedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }

            // Проверяем существование целевой версии
            var targetVersion = await _db.BpmProcessVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == item.TargetVersionId, ct);

            if (targetVersion == null ||
                targetVersion.ProcessId != instance.ProcessId ||
                targetVersion.Status != BpmProcessVersionStatus.Active)
            {
                item.Status = BpmMigrationItemStatus.CriticalError;
                item.ErrorComment = targetVersion == null
                    ? "Целевая версия не найдена"
                    : targetVersion.ProcessId != instance.ProcessId
                        ? "Целевая версия принадлежит другому процессу"
                        : "Целевая версия не является активной";
                item.ProcessedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }

            // Выполняем перевод
            instance.ProcessVersionId = item.TargetVersionId;
            instance.UpdatedAt = DateTimeOffset.UtcNow;

            item.Status = BpmMigrationItemStatus.Migrated;
            item.ProcessedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            item.Status = BpmMigrationItemStatus.OtherError;
            item.ErrorComment = ex.Message;
            item.ProcessedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
