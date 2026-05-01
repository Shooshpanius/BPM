using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Движок выполнения BPMN-процессов.</summary>
public interface IBpmExecutionEngine
{
    /// <summary>Инициирует выполнение экземпляра от StartEvent.</summary>
    Task StartAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>Продвигает поток управления вперёд от завершённого элемента.</summary>
    Task AdvanceFromAsync(Guid instanceId, string fromElementId, CancellationToken ct = default);

    /// <summary>Завершает UserTask/ReceiveTask с передачей выходных переменных.</summary>
    Task<BpmTokenDto> CompleteUserTaskAsync(
        Guid instanceId,
        string elementId,
        Guid actorUserId,
        IDictionary<string, string?>? outputVariables,
        CancellationToken ct = default);

    /// <summary>Рассылает сигнал всем экземплярам, ожидающим данный сигнал.</summary>
    Task SendSignalAsync(string signalCode, CancellationToken ct = default);

    /// <summary>Доставляет сообщение экземплярам, ожидающим данный код сообщения (с опциональной корреляцией).</summary>
    Task SendMessageAsync(string messageCode, string? correlationKey, CancellationToken ct = default);

    /// <summary>Выполняет одно задание из очереди (вызывается фоновым воркером).</summary>
    Task ExecuteJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Возвращает список активных токенов экземпляра.</summary>
    Task<IReadOnlyList<BpmTokenDto>> GetTokensAsync(Guid instanceId, CancellationToken ct = default);
}
