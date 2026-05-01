using Microsoft.Extensions.Logging;

namespace CoreBPM.Server.Application.Bpm.Scripting;

/// <summary>
/// Контекст выполнения C#-сценария ScriptTask.
/// Доступен в скрипте через глобальную переменную <c>ctx</c>.
/// </summary>
public sealed class ScriptContext
{
    private readonly Dictionary<string, string?> _variables;
    private readonly List<(string Name, string? Value)> _outputVariables = new();

    /// <summary>Идентификатор экземпляра процесса.</summary>
    public Guid InstanceId { get; }

    /// <summary>Идентификатор процесса.</summary>
    public Guid ProcessId { get; }

    /// <summary>Идентификатор версии процесса.</summary>
    public Guid ProcessVersionId { get; }

    /// <summary>Идентификатор элемента BPMN, исполняющего скрипт.</summary>
    public string ElementId { get; }

    /// <summary>Логгер процесса (пишет в стандартный журнал приложения).</summary>
    public ILogger Logger { get; }

    /// <summary>HTTP-клиент для внешних вызовов из скрипта.</summary>
    public HttpClient HttpClient { get; }

    internal ScriptContext(
        Guid instanceId,
        Guid processId,
        Guid processVersionId,
        string elementId,
        IReadOnlyDictionary<string, string?> variables,
        ILogger logger,
        HttpClient httpClient)
    {
        InstanceId = instanceId;
        ProcessId = processId;
        ProcessVersionId = processVersionId;
        ElementId = elementId;
        _variables = new Dictionary<string, string?>(variables, StringComparer.OrdinalIgnoreCase);
        Logger = logger;
        HttpClient = httpClient;
    }

    /// <summary>Получить значение переменной экземпляра по имени.</summary>
    public string? GetVariable(string name) =>
        _variables.TryGetValue(name, out var v) ? v : null;

    /// <summary>Установить значение переменной (будет сохранено в БД после завершения скрипта).</summary>
    public void SetVariable(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _variables[name] = value;
        _outputVariables.Add((name, value));
    }

    /// <summary>Проверить, существует ли переменная.</summary>
    public bool HasVariable(string name) => _variables.ContainsKey(name);

    /// <summary>Получить все переменные как словарь (только для чтения).</summary>
    public IReadOnlyDictionary<string, string?> GetAllVariables() =>
        _variables.AsReadOnly();

    /// <summary>Переменные, изменённые скриптом. Используется движком для записи в БД.</summary>
    internal IReadOnlyList<(string Name, string? Value)> OutputVariables => _outputVariables;
}
