namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Источник запуска экземпляра бизнес-процесса.</summary>
public enum BpmInstanceLaunchSource
{
    /// <summary>Ручной запуск пользователем из UI.</summary>
    Manual = 0,

    /// <summary>Запуск через внешний REST-вебхук.</summary>
    Webhook = 1,

    /// <summary>Запуск по расписанию (таймерное стартовое событие).</summary>
    Scheduler = 2,

    /// <summary>Запуск при получении сообщения (Message Start Event).</summary>
    Message = 3,

    /// <summary>Запуск по сигналу (Signal Start Event).</summary>
    Signal = 4,

    /// <summary>Запуск как дочерний экземпляр (Call Activity).</summary>
    CallActivity = 5,

    /// <summary>Пакетный запуск (из CSV/Excel-файла).</summary>
    Batch = 6,
}
