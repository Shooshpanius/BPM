namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Вид деятельности для трудозатрат (FR-TASK-01.4).</summary>
public class TaskActivityType
{
    public Guid Id { get; set; }
    /// <summary>Название вида деятельности.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Активен ли вид деятельности (неактивные недоступны при добавлении трудозатрат).</summary>
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
