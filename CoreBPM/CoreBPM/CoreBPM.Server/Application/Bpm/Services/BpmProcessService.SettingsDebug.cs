using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Application.Bpm.Services;

public partial class BpmProcessService
{
    private static readonly ConcurrentDictionary<Guid, DebugSessionState> DebugSessions = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedExternalMethods = ["GET", "POST", "SOAP"];

    public async Task<BpmProcessSettingsDto> GetSettingsAsync(Guid processId, CancellationToken ct = default)
        => await MapSettingsAsync(await GetProcessEntityAsync(processId, ct), ct);

    public async Task<BpmProcessSettingsDto> UpdateSettingsAsync(Guid processId, UpdateBpmProcessSettingsRequest request, CancellationToken ct = default)
    {
        var process = await GetProcessEntityAsync(processId, ct);
        var methods = (request.ExternalStartMethods ?? [])
            .Select(m => (m ?? string.Empty).Trim().ToUpperInvariant())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();

        if (methods.Any(m => !AllowedExternalMethods.Contains(m, StringComparer.Ordinal)))
            throw new ValidationException("Поддерживаются только методы GET, POST и SOAP");

        process.LaunchFromPortalEnabled = request.LaunchFromPortalEnabled;
        process.ShowInStartList = request.ShowInStartList;
        process.ExternalStartEnabled = request.ExternalStartEnabled;
        process.ExternalStartMethodsJson = JsonSerializer.Serialize(methods);
        process.ExternalStartAllowedIps = request.ExternalStartAllowedIps?.Trim();
        process.InstanceNameMode = request.InstanceNameMode;
        process.RequestInstanceNameOnStart = request.RequestInstanceNameOnStart;
        process.InstanceNameTemplate = request.InstanceNameTemplate?.Trim();
        process.DataClassName = NormalizeTechnicalName(request.DataClassName, process.DataClassName, forTable: false);
        process.DataTableName = NormalizeTechnicalName(request.DataTableName, process.DataTableName, forTable: true);
        process.ProcessMetricsClassName = NormalizeTechnicalName(request.ProcessMetricsClassName, process.ProcessMetricsClassName, forTable: false);
        process.ProcessMetricsTableName = NormalizeTechnicalName(request.ProcessMetricsTableName, process.ProcessMetricsTableName, forTable: true);
        process.InstanceMetricsClassName = NormalizeTechnicalName(request.InstanceMetricsClassName, process.InstanceMetricsClassName, forTable: false);
        process.InstanceMetricsTableName = NormalizeTechnicalName(request.InstanceMetricsTableName, process.InstanceMetricsTableName, forTable: true);
        process.SecondRuntimeUpgradedAt = request.SecondRuntimeEnabled && !process.SecondRuntimeEnabled
            ? DateTimeOffset.UtcNow
            : request.SecondRuntimeEnabled ? process.SecondRuntimeUpgradedAt : null;
        process.SecondRuntimeEnabled = request.SecondRuntimeEnabled;

        await UpdateKeyVariableAsync(processId, request.KeyVariableName, ct);

        process.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await MapSettingsAsync(process, ct);
    }

    public async Task<RotateExternalTokenResponse> RotateExternalTokenAsync(Guid processId, CancellationToken ct = default)
    {
        var process = await GetProcessEntityAsync(processId, ct);
        var token = GenerateExternalToken();
        var preview = token.Length <= 4 ? token : $"••••{token[^4..]}";
        var now = DateTimeOffset.UtcNow;

        process.ExternalStartTokenHash = ComputeSha256(token);
        process.ExternalStartTokenPreview = preview;
        process.ExternalStartTokenUpdatedAt = now;
        process.ExternalStartEnabled = true;
        process.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return new RotateExternalTokenResponse(token, preview, now);
    }

    public async Task<BpmDebugSessionDto> StartDebugSessionAsync(Guid processId, StartBpmDebugSessionRequest request, CancellationToken ct = default)
    {
        var version = request.VersionId.HasValue
            ? await GetVersionEntityAsync(processId, request.VersionId.Value, ct, asNoTracking: true)
            : await GetCurrentVersionEntityAsync(processId, ct);

        var model = ParseProcessModel(version.DiagramXml ?? string.Empty);
        var start = model.Nodes.Values.FirstOrDefault(n => n.ElementType == "startEvent")
            ?? throw new ValidationException("В диаграмме отсутствует стартовое событие для debug-режима");

        var state = new DebugSessionState
        {
            SessionId = Guid.NewGuid(),
            ProcessId = processId,
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            Model = model,
            CurrentElementId = start.Id,
            Variables = request.Variables?.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        state.Events.Add(new BpmDebugEventDto(DateTimeOffset.UtcNow, "Started", start.Id, $"Debug-сессия запущена с узла {start.Id}"));
        DebugSessions[state.SessionId] = state;
        return MapDebugSession(state);
    }

    public async Task<BpmDebugSessionDto> GetDebugSessionAsync(Guid processId, Guid sessionId, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);
        return MapDebugSession(GetDebugSessionState(processId, sessionId));
    }

    public async Task<BpmDebugSessionDto> StepDebugSessionAsync(Guid processId, Guid sessionId, string action, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);
        var state = GetDebugSessionState(processId, sessionId);
        if (state.IsCompleted)
            return MapDebugSession(state);

        if (string.IsNullOrWhiteSpace(state.CurrentElementId) || !state.Model.Nodes.TryGetValue(state.CurrentElementId, out var current))
            throw new ValidationException("Текущий узел debug-сессии не найден");

        var normalizedAction = (action ?? "step").Trim().ToLowerInvariant();
        if ((current.ElementType == "userTask" || current.ElementType == "receiveTask") && normalizedAction == "step")
            throw new ValidationException("Для текущего шага требуется действие complete или skip");

        var outgoing = state.Model.Outgoing.TryGetValue(current.Id, out var flows) ? flows : [];
        if (outgoing.Count == 0)
        {
            state.IsCompleted = true;
            state.Events.Add(new BpmDebugEventDto(DateTimeOffset.UtcNow, "Completed", current.Id, "Debug-сессия завершена"));
            return MapDebugSession(state);
        }

        var nextFlow = outgoing.First();
        var nextNode = state.Model.Nodes.GetValueOrDefault(nextFlow.TargetRef)
            ?? throw new ValidationException("Не удалось найти следующий узел debug-сессии");
        var eventType = normalizedAction switch
        {
            "complete" => "TaskCompleted",
            "skip" => "TaskSkipped",
            _ => "Step"
        };

        state.Events.Add(new BpmDebugEventDto(DateTimeOffset.UtcNow, eventType, current.Id, $"Переход {current.Id} → {nextNode.Id}"));
        state.CurrentElementId = nextNode.Id;
        if (nextNode.ElementType == "endEvent")
        {
            state.IsCompleted = true;
            state.Events.Add(new BpmDebugEventDto(DateTimeOffset.UtcNow, "Completed", nextNode.Id, "Debug-сессия достигла конечного события"));
        }

        return MapDebugSession(state);
    }

    public async Task<(byte[] Content, string FileName)> GenerateDocumentAsync(Guid processId, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var version = process.Versions
            .Where(v => v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault()
            ?? throw new ValidationException("Для формирования регламента требуется активная версия процесса");

        var variables = await _db.BpmProcessVariables
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

        var raci = await _db.BpmRaciEntries
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .OrderBy(r => r.Stage)
            .ThenBy(r => r.Role)
            .ToListAsync(ct);

        var model = ParseProcessModel(version.DiagramXml ?? string.Empty);
        var tasks = model.Nodes.Values.Where(n => n.ElementType is "userTask" or "serviceTask" or "receiveTask").OrderBy(n => n.Name).ToList();

        var lines = new List<string>
        {
            "Core BPM Process Regulation",
            $"Process: {AsciiOnly(process.Name)}",
            $"Version: v{version.VersionNumber}",
            $"Description: {AsciiOnly(process.Description ?? string.Empty)}",
            " ",
            "Tasks:"
        };

        lines.AddRange(tasks.Select(t => $"- {AsciiOnly(t.Name ?? t.Id)} [{t.ElementType}]"));
        lines.Add(" ");
        lines.Add("Variables:");
        lines.AddRange(variables.Select(v => $"- {AsciiOnly(v.Name)} ({v.VariableType}) key={v.IsKeyVariable}"));
        lines.Add(" ");
        lines.Add("RACI:");
        lines.AddRange(raci.Select(r => $"- {AsciiOnly(r.Stage)} / {AsciiOnly(r.Role)} / {r.RaciType}"));
        lines.Add(" ");
        lines.Add("Transitions:");
        lines.AddRange(model.Flows.Values.Select(f => $"- {AsciiOnly(f.Id)}: {AsciiOnly(f.SourceRef)} -> {AsciiOnly(f.TargetRef)}"));

        return (BuildSimplePdf(lines), $"process-{Slugify(process.Name)}-v{version.VersionNumber}.pdf");
    }

    private async Task<BpmProcessSettingsDto> MapSettingsAsync(BpmProcess process, CancellationToken ct)
    {
        var keyVariableName = await _db.BpmProcessVariables
            .AsNoTracking()
            .Where(v => v.ProcessId == process.Id && v.IsKeyVariable)
            .Select(v => v.Name)
            .FirstOrDefaultAsync(ct);

        return new BpmProcessSettingsDto(
            process.Id,
            process.LaunchFromPortalEnabled,
            process.ShowInStartList,
            process.ExternalStartEnabled,
            ParseExternalMethods(process.ExternalStartMethodsJson),
            process.ExternalStartAllowedIps,
            !string.IsNullOrWhiteSpace(process.ExternalStartTokenHash),
            process.ExternalStartTokenPreview,
            process.ExternalStartTokenUpdatedAt,
            process.InstanceNameMode,
            process.RequestInstanceNameOnStart,
            process.InstanceNameTemplate,
            keyVariableName,
            process.DataClassName,
            process.DataTableName,
            process.ProcessMetricsClassName,
            process.ProcessMetricsTableName,
            process.InstanceMetricsClassName,
            process.InstanceMetricsTableName,
            process.SecondRuntimeEnabled,
            process.SecondRuntimeUpgradedAt);
    }

    private async Task UpdateKeyVariableAsync(Guid processId, string? keyVariableName, CancellationToken ct)
    {
        var variables = await _db.BpmProcessVariables.Where(v => v.ProcessId == processId).ToListAsync(ct);
        foreach (var variable in variables)
        {
            var shouldBeKey = !string.IsNullOrWhiteSpace(keyVariableName) && string.Equals(variable.Name, keyVariableName.Trim(), StringComparison.Ordinal);
            if (variable.IsKeyVariable != shouldBeKey)
            {
                variable.IsKeyVariable = shouldBeKey;
                variable.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (!string.IsNullOrWhiteSpace(keyVariableName) && variables.All(v => !string.Equals(v.Name, keyVariableName.Trim(), StringComparison.Ordinal)))
            throw new ValidationException($"Переменная «{keyVariableName}» не найдена");
    }

    private static DebugSessionState GetDebugSessionState(Guid processId, Guid sessionId)
    {
        if (!DebugSessions.TryGetValue(sessionId, out var state) || state.ProcessId != processId)
            throw new NotFoundException($"Debug-сессия {sessionId} не найдена");
        return state;
    }

    private static BpmDebugSessionDto MapDebugSession(DebugSessionState state)
    {
        var currentType = state.CurrentElementId is not null && state.Model.Nodes.TryGetValue(state.CurrentElementId, out var current)
            ? current.ElementType
            : null;

        return new BpmDebugSessionDto(
            state.SessionId,
            state.ProcessId,
            state.VersionId,
            state.VersionNumber,
            state.IsCompleted,
            state.CurrentElementId,
            currentType,
            new Dictionary<string, string>(state.Variables, StringComparer.OrdinalIgnoreCase),
            state.Events.ToList());
    }

    private static string GenerateExternalToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ComputeSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IReadOnlyList<string> ParseExternalMethods(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)?.Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeTechnicalName(string? requested, string fallback, bool forTable)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return fallback;

        var normalized = requested.Trim();
        normalized = forTable ? Slugify(normalized).Replace('-', '_') : ToPascalCase(Slugify(normalized));
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "process";

        var transliterated = Transliterate(value);
        var slug = Regex.Replace(transliterated.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "process" : slug;
    }

    private static TechnicalNames GenerateTechnicalNames(string processName)
    {
        var slug = Slugify(processName);
        var pascal = ToPascalCase(slug);
        return new TechnicalNames(
            $"{pascal}Data",
            $"bpm_{slug}_data",
            $"{pascal}Metrics",
            $"bpm_{slug}_metrics",
            $"{pascal}InstanceMetrics",
            $"bpm_{slug}_instance_metrics");
    }

    private static string ToPascalCase(string slug)
        => string.Concat(slug.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(part => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part)));

    private static string Transliterate(string value)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z",
            ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
            ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch",
            ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
        };

        var sb = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (map.TryGetValue(ch, out var mapped))
                sb.Append(mapped);
            else if (ch <= 127)
                sb.Append(ch);
            else if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string AsciiOnly(string value)
        => Regex.Replace(Transliterate(value ?? string.Empty), "[^\\u0009\\u000A\\u000D\\u0020-\\u007E]", string.Empty);

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
    {
        var safeLines = lines.Select(EscapePdf).ToList();
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("50 780 Td");
        for (var i = 0; i < safeLines.Count; i++)
        {
            if (i > 0)
                content.AppendLine("T*");
            content.Append('(').Append(safeLines[i]).AppendLine(") Tj");
        }
        content.AppendLine("ET");

        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
            "2 0 obj << /Type /Pages /Count 1 /Kids [3 0 R] >> endobj",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
            $"5 0 obj << /Length {Encoding.ASCII.GetByteCount(content.ToString())} >> stream\n{content}endstream endobj"
        };

        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");
        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.AppendLine(obj);
        }

        var xrefPos = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.AppendLine($"xref\n0 {objects.Count + 1}\n0000000000 65535 f ");
        foreach (var offset in offsets)
            sb.AppendLine($"{offset:0000000000} 00000 n ");
        sb.AppendLine($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefPos.ToString(CultureInfo.InvariantCulture));
        sb.Append("%%EOF");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string EscapePdf(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);

    private sealed record TechnicalNames(string DataClassName, string DataTableName, string ProcessMetricsClassName, string ProcessMetricsTableName, string InstanceMetricsClassName, string InstanceMetricsTableName);

    private sealed class DebugSessionState
    {
        public Guid SessionId { get; init; }
        public Guid ProcessId { get; init; }
        public Guid VersionId { get; init; }
        public int VersionNumber { get; init; }
        public ProcessModel Model { get; init; } = new(
            new Dictionary<string, ProcessNode>(StringComparer.Ordinal),
            new Dictionary<string, SequenceFlowInfo>(StringComparer.Ordinal),
            new Dictionary<string, List<SequenceFlowInfo>>(StringComparer.Ordinal));
        public string? CurrentElementId { get; set; }
        public bool IsCompleted { get; set; }
        public Dictionary<string, string> Variables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<BpmDebugEventDto> Events { get; } = [];
    }
}
