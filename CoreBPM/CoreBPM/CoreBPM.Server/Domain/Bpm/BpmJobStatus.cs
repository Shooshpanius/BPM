namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус задания в очереди исполнения.</summary>
public enum BpmJobStatus
{
    /// <summary>Ожидает первого запуска.</summary>
    Pending = 0,

    /// <summary>Выполняется в данный момент.</summary>
    Running = 1,

    /// <summary>Запланировано к повторному выполнению (ожидание retry или следующего срабатывания).</summary>
    Scheduled = 2,

    /// <summary>Завершено успешно.</summary>
    Completed = 3,

    /// <summary>Ошибка — исчерпаны все попытки; требует ручного вмешательства.</summary>
    Failed = 4,

    /// <summary>Отменено (например, при срабатывании прерывающего граничного события).</summary>
    Cancelled = 5,
}
