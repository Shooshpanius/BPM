namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Статус пакета миграции версий экземпляров.</summary>
public enum BpmMigrationPackageStatus
{
    /// <summary>Новый — пакет создан, ещё не запущен.</summary>
    New = 0,

    /// <summary>Выполняется — операция перевода запущена.</summary>
    Running = 1,

    /// <summary>Выполнено — все элементы обработаны успешно.</summary>
    Completed = 2,

    /// <summary>Выполнено с ошибками — часть элементов завершилась с ошибкой.</summary>
    CompletedWithErrors = 3,

    /// <summary>Отменён.</summary>
    Cancelled = 4,
}
