namespace CoreBPM.Server.Domain.Org;

/// <summary>Статус подразделения.</summary>
public enum DepartmentStatus
{
    /// <summary>Активное подразделение.</summary>
    Active = 0,

    /// <summary>Архивное (мягко удалённое) подразделение.</summary>
    Archived = 1
}
