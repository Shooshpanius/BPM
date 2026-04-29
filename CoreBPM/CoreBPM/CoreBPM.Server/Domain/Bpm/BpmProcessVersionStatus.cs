namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус версии бизнес-процесса.</summary>
public enum BpmProcessVersionStatus
{
    /// <summary>Черновик — редактируется, не опубликован.</summary>
    Draft = 0,

    /// <summary>Активная версия — используется для запуска новых экземпляров.</summary>
    Active = 1,

    /// <summary>Устаревшая — была активной, заменена новой версией.</summary>
    Obsolete = 2
}
