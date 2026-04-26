namespace CoreBPM.Server.Exceptions;

/// <summary>Сущность не найдена.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
