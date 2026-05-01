using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace CoreBPM.Server.Application.Bpm.Scripting;

/// <summary>
/// Реализация выполнения C#-сценариев через Roslyn Scripting API.
/// Компиляция кэшируется по SHA-256 хэшу кода сценария для повторного использования.
/// </summary>
public sealed class BpmRoslynScriptExecutor : IBpmScriptExecutor
{
    // Кэш скомпилированных скриптов: ключ — SHA-256 хэш кода
    private static readonly ConcurrentDictionary<string, Script<object?>> _compiledCache = new();

    private static readonly ScriptOptions _scriptOptions = ScriptOptions.Default
        .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest)
        .AddReferences(
            typeof(object).Assembly,                       // System.Private.CoreLib
            typeof(Console).Assembly,                      // System.Console
            typeof(Task).Assembly,                         // System.Threading.Tasks
            typeof(HttpClient).Assembly,                   // System.Net.Http
            typeof(System.Text.Json.JsonDocument).Assembly, // System.Text.Json
            typeof(Enumerable).Assembly,                   // System.Linq
            typeof(ScriptContext).Assembly                 // CoreBPM.Server (контекст)
        )
        .AddImports(
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Text",
            "System.Text.Json",
            "System.Net.Http",
            "CoreBPM.Server.Application.Bpm.Scripting"
        );

    private readonly ILogger<BpmRoslynScriptExecutor> _logger;

    public BpmRoslynScriptExecutor(ILogger<BpmRoslynScriptExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        string scriptCode,
        ScriptContext context,
        int timeoutMs = 30_000,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scriptCode))
            throw new ArgumentException("Тело сценария не может быть пустым", nameof(scriptCode));

        var codeHash = ComputeHash(scriptCode);

        // Получаем скомпилированный скрипт из кэша или компилируем
        Script<object?> script;
        if (!_compiledCache.TryGetValue(codeHash, out script!))
        {
            _logger.LogDebug("BpmRoslynScriptExecutor: компиляция сценария {Hash}", codeHash);

            try
            {
                script = CSharpScript.Create<object?>(
                    scriptCode,
                    _scriptOptions,
                    globalsType: typeof(ScriptGlobals));
                script.Compile(ct);
            }
            catch (CompilationErrorException ex)
            {
                var errors = string.Join("; ", ex.Diagnostics.Select(d => d.ToString()));
                throw new InvalidOperationException($"Ошибка компиляции сценария: {errors}", ex);
            }

            _compiledCache.TryAdd(codeHash, script);
            _logger.LogDebug("BpmRoslynScriptExecutor: сценарий {Hash} скомпилирован и закэширован", codeHash);
        }

        // Создаём CancellationToken с таймаутом
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        var globals = new ScriptGlobals(context);

        try
        {
            _logger.LogDebug(
                "BpmRoslynScriptExecutor: запуск сценария {Hash} для экземпляра {InstanceId} / {ElementId}",
                codeHash, context.InstanceId, context.ElementId);

            await script.RunAsync(globals, catchException: null, cancellationToken: timeoutCts.Token);

            _logger.LogInformation(
                "BpmRoslynScriptExecutor: сценарий {Hash} успешно выполнен (экземпляр {InstanceId}, элемент {ElementId})",
                codeHash, context.InstanceId, context.ElementId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Сценарий ScriptTask превысил лимит времени выполнения ({timeoutMs} мс)");
        }
    }

    private static string ComputeHash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexStringLower(bytes); // полный 64-символьный хэш
    }
}

/// <summary>
/// Глобальный объект, доступный в скрипте как <c>ctx</c>.
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>Контекст выполнения сценария (переменные, логгер, HTTP).</summary>
    public ScriptContext ctx { get; }

    public ScriptGlobals(ScriptContext context) => ctx = context;
}
