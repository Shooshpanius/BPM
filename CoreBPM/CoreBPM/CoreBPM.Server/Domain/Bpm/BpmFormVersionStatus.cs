namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус версии формы задачи.</summary>
public enum BpmFormVersionStatus
{
    /// <summary>Черновик — редактируется, не используется в задачах.</summary>
    Draft = 0,

    /// <summary>Опубликована — используется в задачах процессов.</summary>
    Published = 1,

    /// <summary>Архив — была опубликована, но заменена новой версией.</summary>
    Archived = 2
}
