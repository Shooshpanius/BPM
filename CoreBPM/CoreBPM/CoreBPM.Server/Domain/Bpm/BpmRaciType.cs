namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Тип ответственности в RACI-матрице.</summary>
public enum BpmRaciType
{
    /// <summary>Responsible — непосредственный исполнитель (делает работу).</summary>
    R,
    /// <summary>Accountable — несёт ответственность за результат (один в строке).</summary>
    A,
    /// <summary>Consulted — консультирует (двусторонний обмен информацией).</summary>
    C,
    /// <summary>Informed — информируется о результатах (односторонний поток).</summary>
    I,
}
