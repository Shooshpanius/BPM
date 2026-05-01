namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус токена BPMN-экземпляра процесса.</summary>
public enum BpmTokenStatus
{
    /// <summary>Токен активен — узел выполняется или ожидает обработки движком.</summary>
    Active = 0,

    /// <summary>Ожидание действия пользователя (UserTask / ReceiveTask).</summary>
    WaitingUserAction = 1,

    /// <summary>Ожидание сигнала BPMN (IntermediateCatchEvent[Signal] / BoundaryEvent[Signal]).</summary>
    WaitingSignal = 2,

    /// <summary>Ожидание сообщения BPMN (IntermediateCatchEvent[Message] / BoundaryEvent[Message]).</summary>
    WaitingMessage = 3,

    /// <summary>Токен завершён — токен прошёл через узел и движется дальше.</summary>
    Completed = 4,

    /// <summary>Токен ожидает схождения параллельных ветвей в AND-join шлюзе (parallelGateway / inclusiveGateway).</summary>
    WaitingJoin = 5,

    /// <summary>Токен ожидает срабатывания таймера (intermediateCatchEvent с timerEventDefinition).</summary>
    WaitingTimer = 6,

    /// <summary>Токен ожидает завершения дочернего экземпляра (callActivity).</summary>
    WaitingCallActivity = 7,
}
