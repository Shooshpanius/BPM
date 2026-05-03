namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Периодичность повтора задачи (FR-TASK-01.5.1).</summary>
public enum TaskPeriodicity
{
    /// <summary>По рабочим дням (с учётом производственного календаря).</summary>
    WorkingDays = 0,
    /// <summary>Ежедневно (без учёта производственного календаря).</summary>
    Daily = 1,
    /// <summary>Еженедельно.</summary>
    Weekly = 2,
    /// <summary>Ежемесячно.</summary>
    Monthly = 3,
    /// <summary>Ежеквартально.</summary>
    Quarterly = 4,
    /// <summary>Ежегодно.</summary>
    Yearly = 5,
}
