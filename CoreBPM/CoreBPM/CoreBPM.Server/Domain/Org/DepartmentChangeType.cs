namespace CoreBPM.Server.Domain.Org;

/// <summary>Тип изменения в истории подразделения.</summary>
public enum DepartmentChangeType
{
    /// <summary>Подразделение создано.</summary>
    Created = 0,

    /// <summary>Подразделение переименовано или обновлены его данные.</summary>
    Updated = 1,

    /// <summary>Подразделение перемещено в другой родительский узел.</summary>
    Moved = 2,

    /// <summary>Подразделение архивировано.</summary>
    Archived = 3,

    /// <summary>Подразделение восстановлено из архива.</summary>
    Restored = 4
}
