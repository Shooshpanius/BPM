namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Режим формирования названия экземпляра процесса.</summary>
public enum BpmInstanceNameMode
{
    /// <summary>Название вводится пользователем при запуске или задаётся вручную.</summary>
    Manual = 0,

    /// <summary>Название берётся из ключевой переменной процесса.</summary>
    KeyVariable = 1,

    /// <summary>Название формируется по шаблону.</summary>
    Template = 2
}
