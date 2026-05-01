namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Счётчик входящих токенов для AND-join шлюзов (parallelGateway / inclusiveGateway).
/// Таблица bpm_join_counters. При каждом входящем токене IncrementArrivedCount; как только
/// ArrivedCount >= ExpectedCount — шлюз пропускается вперёд и запись сбрасывается.
/// </summary>
public class BpmJoinCounter
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на экземпляр процесса.</summary>
    public Guid InstanceId { get; set; }

    /// <summary>Идентификатор BPMN-элемента шлюза (parallelGateway / inclusiveGateway).</summary>
    public string GatewayElementId { get; set; } = string.Empty;

    /// <summary>Ожидаемое число входящих токенов (количество входящих дуг шлюза).</summary>
    public int ExpectedCount { get; set; }

    /// <summary>Число уже пришедших токенов.</summary>
    public int ArrivedCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmInstance Instance { get; set; } = null!;
}
