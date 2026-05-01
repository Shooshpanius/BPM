namespace CoreBPM.Server.Application.Bpm.Scripting;

/// <summary>
/// Сервис выполнения C#-сценариев ScriptTask через Roslyn Scripting API.
/// </summary>
public interface IBpmScriptExecutor
{
    /// <summary>
    /// Выполнить C#-сценарий с заданным контекстом.
    /// Скрипт получает объект <c>ctx</c> типа <see cref="ScriptContext"/> как глобальную переменную.
    /// </summary>
    /// <param name="scriptCode">Исходный код C#-сценария.</param>
    /// <param name="context">Контекст выполнения (переменные, логгер, HTTP).</param>
    /// <param name="timeoutMs">Таймаут выполнения в миллисекундах (по умолчанию 30 000 мс).</param>
    /// <param name="ct">Токен отмены.</param>
    Task ExecuteAsync(string scriptCode, ScriptContext context, int timeoutMs = 30_000, CancellationToken ct = default);
}
