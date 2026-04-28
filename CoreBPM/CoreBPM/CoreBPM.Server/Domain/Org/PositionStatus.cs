namespace CoreBPM.Server.Domain.Org;

/// <summary>Статус должности.</summary>
public enum PositionStatus
{
    /// <summary>Активная должность.</summary>
    Active = 0,

    /// <summary>Архивная (мягко удалённая) должность.</summary>
    Archived = 1
}
