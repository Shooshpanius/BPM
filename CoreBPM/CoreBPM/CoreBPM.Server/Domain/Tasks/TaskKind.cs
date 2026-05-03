namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Вид (тип) задачи (FR-TASK-01.5).</summary>
public enum TaskKind
{
    /// <summary>Обычная задача.</summary>
    Regular = 0,
    /// <summary>Периодическая задача — повторяется с заданной частотой.</summary>
    Periodic = 1,
    /// <summary>Задача по процессу — создана узлом UserTask BPMN-движка.</summary>
    ProcessTask = 2,
    /// <summary>Задача по резолюции документа.</summary>
    Resolution = 3,
}
