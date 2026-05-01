using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис предложений по улучшению бизнес-процессов (FR-BPM-03.1).</summary>
public interface IBpmImprovementService
{
    /// <summary>Создаёт предложение по улучшению процесса.</summary>
    Task<ImprovementDto> CreateAsync(
        Guid processId,
        CreateImprovementRequest request,
        Guid initiatorId,
        CancellationToken ct = default);

    /// <summary>Возвращает список предложений по конкретному процессу.</summary>
    Task<IReadOnlyList<ImprovementDto>> ListByProcessAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает общий список предложений с фильтрацией.
    /// role: "All" — все, где пользователь участвует; "My" — инициатор = я; "Current" — активные.
    /// </summary>
    Task<IReadOnlyList<ImprovementDto>> ListAsync(
        Guid userId,
        bool isAdmin,
        string role,
        Guid? processId = null,
        BpmImprovementStatus? status = null,
        Guid? authorId = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        CancellationToken ct = default);

    /// <summary>Возвращает детальное представление предложения.</summary>
    Task<ImprovementDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Принимает предложение (только владелец процесса).</summary>
    Task<ImprovementDto> AcceptAsync(
        Guid id,
        AcceptImprovementRequest request,
        Guid reviewerId,
        bool isAdmin,
        CancellationToken ct = default);

    /// <summary>Отклоняет предложение (только владелец процесса).</summary>
    Task<ImprovementDto> RejectAsync(
        Guid id,
        RejectImprovementRequest request,
        Guid reviewerId,
        bool isAdmin,
        CancellationToken ct = default);

    /// <summary>Завершает реализацию улучшения (только назначенный исполнитель или Admin).</summary>
    Task<ImprovementDto> CompleteAsync(
        Guid id,
        CompleteImprovementRequest request,
        Guid executorId,
        bool isAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает монитор улучшений для процессов, где пользователь — владелец или куратор.
    /// </summary>
    Task<IReadOnlyList<ImprovementMonitorItemDto>> GetMonitorMyAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>Возвращает полный монитор улучшений по всем процессам. Только для Admin.</summary>
    Task<IReadOnlyList<ImprovementMonitorItemDto>> GetMonitorFullAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Генерирует CSV-файл со списком предложений по улучшению (FR-BPM-03.1).
    /// Столбцы: процесс, тема, статус, инициатор, дата создания, дата завершения, резолюция.
    /// </summary>
    Task<byte[]> ExportToCsvAsync(
        Guid userId,
        bool isAdmin,
        CancellationToken ct = default);
}
