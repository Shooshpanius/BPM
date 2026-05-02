namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Связь между задачами (таблица task_relations).</summary>
public class TaskRelation
{
    public Guid Id { get; set; }
    public Guid SourceTaskId { get; set; }
    public Guid TargetTaskId { get; set; }
    public TaskRelationType RelationType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem SourceTask { get; set; } = null!;
    public TaskItem TargetTask { get; set; } = null!;
}
