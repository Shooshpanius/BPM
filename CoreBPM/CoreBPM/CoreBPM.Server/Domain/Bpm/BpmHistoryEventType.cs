namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Тип события в истории экземпляра процесса.</summary>
public enum BpmHistoryEventType
{
    /// <summary>Экземпляр запущен.</summary>
    Started = 0,

    /// <summary>Экземпляр прерван (отменён).</summary>
    Cancelled = 1,

    /// <summary>Экземпляр завершён нормально.</summary>
    Completed = 2,

    /// <summary>Экземпляр приостановлен.</summary>
    Suspended = 3,

    /// <summary>Экземпляр возобновлён после приостановки.</summary>
    Resumed = 4,

    /// <summary>Изменён ответственный за экземпляр.</summary>
    ResponsibleChanged = 5,

    /// <summary>Добавлен комментарий.</summary>
    CommentAdded = 6,

    /// <summary>Задан вопрос.</summary>
    QuestionAdded = 7,

    /// <summary>Изменено значение переменной.</summary>
    VariableUpdated = 8,

    /// <summary>Добавлен участник.</summary>
    ParticipantAdded = 9,

    /// <summary>Удалён участник.</summary>
    ParticipantRemoved = 10,
}
