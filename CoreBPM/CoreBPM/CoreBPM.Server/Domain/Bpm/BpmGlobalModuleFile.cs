namespace CoreBPM.Server.Domain.Bpm;

/// <summary>
/// Файл-сценарий глобального модуля (таблица bpm_global_module_files).
/// Несколько файлов образуют единый модуль, компилируемый вместе.
/// </summary>
public class BpmGlobalModuleFile
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на глобальный модуль.</summary>
    public Guid ModuleId { get; set; }

    /// <summary>Имя файла (например «Helpers.cs»).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Тело C#-кода файла.</summary>
    public string ScriptBody { get; set; } = string.Empty;

    /// <summary>Порядок отображения файла в модуле (0-based).</summary>
    public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Навигационные свойства
    public BpmGlobalModule Module { get; set; } = null!;
}
