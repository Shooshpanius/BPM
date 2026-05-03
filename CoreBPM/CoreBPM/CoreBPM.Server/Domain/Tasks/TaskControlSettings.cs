namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Системные настройки контроля и трудозатрат по задачам (FR-TASK-01.4). Singleton-запись (Id = 1).</summary>
public class TaskControlSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Тип контроля, устанавливаемый по умолчанию при создании задачи.</summary>
    public TaskControlType DefaultControlType { get; set; } = TaskControlType.None;

    /// <summary>Если true — пользователь обязан добавить трудозатраты перед нажатием «Сделано».</summary>
    public bool IsEffortRequired { get; set; } = false;

    /// <summary>Если true — при вводе трудозатрат поле «Вид деятельности» является обязательным.</summary>
    public bool IsActivityTypeRequired { get; set; } = false;

    public DateTimeOffset UpdatedAt { get; set; }
}
