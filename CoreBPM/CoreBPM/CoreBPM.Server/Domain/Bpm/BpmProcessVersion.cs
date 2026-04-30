namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Версия диаграммы бизнес-процесса (таблица bpm_process_versions).</summary>
public class BpmProcessVersion
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на определение процесса.</summary>
    public Guid ProcessId { get; set; }

    /// <summary>Инкрементальный номер версии внутри процесса (1, 2, 3, …).</summary>
    public int VersionNumber { get; set; }

    /// <summary>Статус версии: черновик, активная, устаревшая.</summary>
    public BpmProcessVersionStatus Status { get; set; } = BpmProcessVersionStatus.Draft;

    /// <summary>XML-содержимое диаграммы в формате BPMN 2.0.</summary>
    public string? DiagramXml { get; set; }

    /// <summary>Идентификатор пользователя, создавшего версию.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Комментарий к публикации версии (что изменилось).</summary>
    public string? ReleaseNotes { get; set; }

    // Навигационные свойства
    public BpmProcess Process { get; set; } = null!;

    /// <summary>Модуль C#-сценариев версии (создаётся лениво).</summary>
    public BpmScriptModule? ScriptModule { get; set; }
}
