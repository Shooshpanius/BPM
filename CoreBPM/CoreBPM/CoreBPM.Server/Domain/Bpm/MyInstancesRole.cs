namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Роль пользователя по отношению к экземпляру процесса — используется для фильтрации «Мои процессы».</summary>
public enum MyInstancesRole
{
    /// <summary>Все экземпляры, где пользователь является инициатором, ответственным или участником.</summary>
    All = 0,

    /// <summary>Только экземпляры, где пользователь — инициатор.</summary>
    Initiator = 1,

    /// <summary>Только экземпляры, где пользователь — ответственный.</summary>
    Responsible = 2,

    /// <summary>Только экземпляры, где пользователь добавлен как участник.</summary>
    Participant = 3,
}
