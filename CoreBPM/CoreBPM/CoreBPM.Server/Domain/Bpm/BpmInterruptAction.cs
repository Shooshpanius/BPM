namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Действие над статусом экземпляра процесса при его прерывании.</summary>
public enum BpmInterruptAction
{
    /// <summary>Оставить текущий статус без изменений.</summary>
    KeepCurrent,

    /// <summary>Обнулить статус (сбросить в null).</summary>
    Reset,

    /// <summary>Перевести в следующий статус по порядку.</summary>
    MoveToNext,

    /// <summary>Выполнить сценарий, указанный в OnInterruptScriptId.</summary>
    RunScript,
}
