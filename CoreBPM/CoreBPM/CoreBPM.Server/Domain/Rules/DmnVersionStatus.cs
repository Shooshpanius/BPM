namespace CoreBPM.Server.Domain.Rules;

/// <summary>Статус версии DMN-таблицы.</summary>
public enum DmnVersionStatus
{
    /// <summary>Черновик — ещё не опубликован.</summary>
    Draft,
    /// <summary>Опубликована — используется в процессах.</summary>
    Published,
    /// <summary>Архивная — была опубликована, но заменена новой.</summary>
    Archived
}
