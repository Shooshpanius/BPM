namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskRelationDto
{
    public Guid Id { get; set; }
    public Guid SourceTaskId { get; set; }
    public Guid TargetTaskId { get; set; }
    public string TargetSubject { get; set; } = string.Empty;
    public int TargetNumber { get; set; }
    public string RelationType { get; set; } = string.Empty;
}
