namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Модуль C#-сценариев, привязанный к конкретной версии процесса (таблица bpm_script_modules).
/// Один экземпляр — одна версия процесса.
/// </summary>
public class BpmScriptModule
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на версию процесса.</summary>
    public Guid ProcessVersionId { get; set; }

    /// <summary>Тело C#-сценария.</summary>
    public string ScriptBody { get; set; } = string.Empty;

    /// <summary>Язык сценария (по умолчанию CSharp).</summary>
    public string Language { get; set; } = "CSharp";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Дата последней публикации сценария; null — сценарий не публиковался.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    // Навигационные свойства
    public BpmProcessVersion ProcessVersion { get; set; } = null!;
}
