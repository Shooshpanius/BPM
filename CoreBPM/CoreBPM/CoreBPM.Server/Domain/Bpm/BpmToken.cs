namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Токен выполнения BPMN (таблица bpm_tokens).
/// Представляет позицию потока управления внутри экземпляра процесса.
/// Каждый токен соответствует одному активному или завершённому узлу диаграммы.
/// </summary>
public class BpmToken
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Идентификатор BPMN-элемента, на котором находится токен.</summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>Тип BPMN-элемента (startEvent, userTask, serviceTask и т.д.).</summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>Имя элемента (кешируется для отображения без разбора XML).</summary>
    public string? ElementName { get; set; }

    /// <summary>Текущий статус токена.</summary>
    public BpmTokenStatus Status { get; set; } = BpmTokenStatus.Active;

    /// <summary>Код сигнала (для токенов в состоянии WaitingSignal).</summary>
    public string? SignalCode { get; set; }

    /// <summary>Код сообщения (для токенов в состоянии WaitingMessage).</summary>
    public string? MessageCode { get; set; }

    /// <summary>Ключ корреляции сообщения (для интеллектуальной маршрутизации сообщений).</summary>
    public string? CorrelationKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Время завершения токена (переход к следующему узлу).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    // Навигационные свойства
    public BpmInstance Instance { get; set; } = null!;
}
