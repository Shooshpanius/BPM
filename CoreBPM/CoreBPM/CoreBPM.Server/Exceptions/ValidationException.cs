namespace CoreBPM.Server.Exceptions;

/// <summary>Ошибка валидации входных данных.</summary>
public class ValidationException : Exception
{
    /// <summary>Список деталей ошибок.</summary>
    public IReadOnlyList<string> Details { get; }

    public ValidationException(string message, IEnumerable<string>? details = null)
        : base(message)
    {
        Details = details?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
    }
}
