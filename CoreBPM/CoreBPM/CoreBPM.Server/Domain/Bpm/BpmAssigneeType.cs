namespace CoreBPM.Server.Domain.Bpm;

/// <summary>Тип назначенца в роли бизнес-процесса.</summary>
public enum BpmAssigneeType
{
    /// <summary>Конкретный пользователь системы (по OrgUser.Id).</summary>
    User = 1,

    /// <summary>Должность (по OrgPosition.Id).</summary>
    Position = 2,

    /// <summary>Подразделение (по OrgDepartment.Id).</summary>
    Department = 3,
}
