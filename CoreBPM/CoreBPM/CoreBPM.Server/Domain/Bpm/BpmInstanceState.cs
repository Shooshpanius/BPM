namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Состояние экземпляра бизнес-процесса.</summary>
public enum BpmInstanceState
{
    /// <summary>Выполняется — токен активен, задачи обрабатываются.</summary>
    Active = 0,

    /// <summary>Завершён нормально — достигнуто конечное событие.</summary>
    Completed = 1,

    /// <summary>Прерван — остановлен вручную владельцем / инициатором с указанием причины.</summary>
    Cancelled = 2,

    /// <summary>Приостановлен — все активные задачи заморожены; возобновление возможно.</summary>
    Suspended = 3,

    /// <summary>Ошибка — асинхронный узел исчерпал все попытки выполнения; требуется ручное вмешательство.</summary>
    Faulted = 4,
}
