namespace CoreBPM.Server.Domain.Rules;

/// <summary>Хит-политика DMN-таблицы решений.</summary>
public enum DmnHitPolicy
{
    /// <summary>Ровно одна строка должна совпасть.</summary>
    Unique,
    /// <summary>Первая совпавшая строка.</summary>
    First,
    /// <summary>Все совпавшие строки; результаты должны быть одинаковы.</summary>
    Any,
    /// <summary>Все совпавшие строки собираются в список.</summary>
    Collect,
    /// <summary>Все совпавшие строки в порядке следования правил.</summary>
    RuleOrder,
    /// <summary>Все совпавшие строки, отсортированные по выходному значению.</summary>
    OutputOrder
}
