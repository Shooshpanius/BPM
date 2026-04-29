using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления RACI-матрицей ответственности бизнес-процесса.</summary>
public interface IBpmRaciService
{
    /// <summary>Возвращает все RACI-записи процесса.</summary>
    Task<IReadOnlyList<BpmRaciEntryDto>> GetEntriesAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Полностью заменяет RACI-матрицу процесса переданным набором записей (batch upsert).
    /// </summary>
    Task<IReadOnlyList<BpmRaciEntryDto>> ReplaceEntriesAsync(Guid processId, IReadOnlyList<UpsertRaciEntryRequest> entries, CancellationToken ct = default);
}
