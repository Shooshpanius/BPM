using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис сохранённых фильтров раздела «Мои процессы».</summary>
public interface IBpmSavedFilterService
{
    /// <summary>Возвращает все сохранённые фильтры пользователя.</summary>
    Task<IReadOnlyList<BpmSavedFilterDto>> GetFiltersAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Создаёт новый сохранённый фильтр.</summary>
    Task<BpmSavedFilterDto> CreateFilterAsync(Guid userId, SaveFilterRequest request, CancellationToken ct = default);

    /// <summary>Обновляет существующий сохранённый фильтр.</summary>
    Task<BpmSavedFilterDto> UpdateFilterAsync(Guid filterId, Guid userId, SaveFilterRequest request, CancellationToken ct = default);

    /// <summary>Удаляет сохранённый фильтр.</summary>
    Task DeleteFilterAsync(Guid filterId, Guid userId, CancellationToken ct = default);
}
