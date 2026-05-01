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
}
