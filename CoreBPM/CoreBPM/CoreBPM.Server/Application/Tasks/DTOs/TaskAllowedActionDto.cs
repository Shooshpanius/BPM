namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>Допустимое действие над задачей для текущего пользователя (FR-TASK-01.2).</summary>
public class TaskAllowedActionDto
{
    public string Action { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
