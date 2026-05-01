namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус отдельного элемента пакета миграции версий.</summary>
public enum BpmMigrationItemStatus
{
    /// <summary>Новый — ожидает обработки.</summary>
    New = 0,

    /// <summary>В работе — перевод выполняется в данный момент.</summary>
    InProgress = 1,

    /// <summary>Переведён на новую версию — успешно завершено.</summary>
    Migrated = 2,

    /// <summary>Критические ошибки — перевод невозможен из-за несовместимости.</summary>
    CriticalError = 3,

    /// <summary>Занят — экземпляр уже переводится другим пакетом миграции.</summary>
    Busy = 4,

    /// <summary>Требуется ручная обработка — автоматический перевод требует участия оператора.</summary>
    RequiresManualHandling = 5,

    /// <summary>Другая ошибка — непредвиденная ошибка при выполнении.</summary>
    OtherError = 6,

    /// <summary>Не подходит — экземпляр не соответствует условиям (неактивен, принадлежит другому процессу и т.п.).</summary>
    NotApplicable = 7,

    /// <summary>Обновление не требуется — версия экземпляра уже совпадает с целевой.</summary>
    NoMigrationNeeded = 8,
}
