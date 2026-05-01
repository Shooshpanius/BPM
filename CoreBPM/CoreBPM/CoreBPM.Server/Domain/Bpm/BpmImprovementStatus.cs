namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус предложения по улучшению бизнес-процесса (FR-BPM-03.1).</summary>
public enum BpmImprovementStatus
{
    /// <summary>Создано — ожидает рассмотрения владельцем процесса.</summary>
    Pending = 0,

    /// <summary>Принято — исполнитель получил задачу на реализацию.</summary>
    Accepted = 1,

    /// <summary>В работе — исполнитель начал реализацию.</summary>
    InProgress = 2,

    /// <summary>Завершено — исполнитель внёс улучшение и указал резолюцию.</summary>
    Completed = 3,

    /// <summary>Отклонено — владелец процесса отклонил предложение.</summary>
    Rejected = 4,
}
