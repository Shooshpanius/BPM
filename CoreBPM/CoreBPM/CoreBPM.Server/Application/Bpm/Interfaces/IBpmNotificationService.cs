namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис отправки in-app уведомлений через SignalR.</summary>
public interface IBpmNotificationService
{
    /// <summary>Уведомляет администраторов о переходе задания в статус Failed.</summary>
    Task NotifyJobFailedAsync(Guid jobId, string processName, string? instanceName, string? error, CancellationToken ct = default);

    /// <summary>
    /// Уведомляет администраторов и создателя пакета о завершении пакета миграции версий (FR-BPM-02.7).
    /// </summary>
    Task NotifyMigrationPackageCompletedAsync(
        Guid packageId,
        string packageName,
        bool hasErrors,
        int total,
        int migrated,
        int failed,
        Guid createdByUserId,
        CancellationToken ct = default);

    /// <summary>Отправляет произвольное уведомление конкретному пользователю.</summary>
    Task NotifyUserAsync(Guid userId, string eventType, object payload, CancellationToken ct = default);
}
