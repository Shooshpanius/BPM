namespace CoreBPM.Server.Exceptions;

/// <summary>Доступ запрещён.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
