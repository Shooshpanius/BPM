namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Условие завершения серии периодических задач (FR-TASK-01.5.1).</summary>
public enum TaskSeriesEndCondition
{
    /// <summary>Не завершать — повторяется до принудительной остановки.</summary>
    Never = 0,
    /// <summary>Завершить в указанную дату.</summary>
    ByDate = 1,
}
