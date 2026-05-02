namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskSavedFilterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
public class CreateTaskSavedFilterRequest
{
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
}
