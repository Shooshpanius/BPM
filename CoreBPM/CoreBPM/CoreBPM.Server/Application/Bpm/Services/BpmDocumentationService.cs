using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса документирования процессов (FR-BPM-02.6).</summary>
public class BpmDocumentationService : IBpmDocumentationService
{
    private readonly AppDbContext _db;

    public BpmDocumentationService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task GenerateAndSaveSnapshotAsync(Guid processId, Guid versionId, Guid generatedByUserId, CancellationToken ct = default)
        => RegenerateSnapshotWithSvgAsync(processId, versionId, generatedByUserId, svgContent: null, ct);

    /// <inheritdoc />
    public async Task RegenerateSnapshotWithSvgAsync(Guid processId, Guid versionId, Guid userId, string? svgContent, CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Версия {versionId} не найдена");

        var variables = await _db.BpmProcessVariables
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
            .ToListAsync(ct);

        var raciEntries = await _db.BpmRaciEntries
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .OrderBy(r => r.Stage).ThenBy(r => r.Role)
            .ToListAsync(ct);

        var roles = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .ToListAsync(ct);

        var scriptModule = await _db.BpmScriptModules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProcessVersionId == versionId, ct);

        var html = BuildHtml(process, version, variables, raciEntries, roles, scriptModule, svgContent);

        // Обновляем существующий снапшот или создаём новый
        var existing = await _db.BpmProcessDocSnapshots
            .Where(s => s.ProcessVersionId == versionId)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.HtmlContent = html;
            existing.DiagramSvg = svgContent;
            existing.GeneratedAt = DateTimeOffset.UtcNow;
            existing.GeneratedByUserId = userId;
        }
        else
        {
            _db.BpmProcessDocSnapshots.Add(new BpmProcessDocSnapshot
            {
                Id = Guid.NewGuid(),
                ProcessId = processId,
                ProcessVersionId = versionId,
                HtmlContent = html,
                DiagramSvg = svgContent,
                GeneratedAt = DateTimeOffset.UtcNow,
                GeneratedByUserId = userId
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessDocumentationItemDto>> GetMyDocumentationAsync(Guid userId, CancellationToken ct = default)
    {
        // Процессы, где пользователь — Владелец или Куратор
        var myProcessIds = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.AssigneeType == BpmAssigneeType.User && r.AssigneeId == userId.ToString())
            .Select(r => r.ProcessId)
            .Distinct()
            .ToListAsync(ct);

        return await BuildDocumentationListAsync(
            q => q.Where(p => myProcessIds.Contains(p.Id) && !p.IsDeleted),
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessDocumentationItemDto>> GetAllDocumentationAsync(bool includeDeleted, CancellationToken ct = default)
    {
        return await BuildDocumentationListAsync(
            q => includeDeleted ? q : q.Where(p => !p.IsDeleted),
            ct);
    }

    /// <inheritdoc />
    public async Task<DocSnapshotDto> GetDocSnapshotAsync(Guid processId, Guid versionId, CancellationToken ct = default)
    {
        var snapshot = await _db.BpmProcessDocSnapshots
            .AsNoTracking()
            .Include(s => s.ProcessVersion)
            .Include(s => s.Process)
            .FirstOrDefaultAsync(s => s.ProcessId == processId && s.ProcessVersionId == versionId, ct)
            ?? throw new NotFoundException($"Снапшот документации для версии {versionId} не найден");

        return new DocSnapshotDto(
            snapshot.Id,
            processId,
            snapshot.Process.Name,
            versionId,
            snapshot.ProcessVersion.VersionNumber,
            snapshot.GeneratedAt,
            snapshot.HtmlContent);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private async Task<IReadOnlyList<ProcessDocumentationItemDto>> BuildDocumentationListAsync(
        Func<IQueryable<BpmProcess>, IQueryable<BpmProcess>> filter,
        CancellationToken ct)
    {
        var processes = await filter(_db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        if (processes.Count == 0)
            return Array.Empty<ProcessDocumentationItemDto>();

        var processIds = processes.Select(p => p.Id).ToList();

        var snapshotVersionIds = await _db.BpmProcessDocSnapshots
            .AsNoTracking()
            .Where(s => processIds.Contains(s.ProcessId))
            .Select(s => s.ProcessVersionId)
            .ToHashSetAsync(ct);

        return processes.Select(p => new ProcessDocumentationItemDto(
            p.Id,
            p.Name,
            p.Description,
            p.IsDeleted,
            ParseTags(p.TagsJson),
            p.Versions
                .Where(v => v.Status == BpmProcessVersionStatus.Active || v.Status == BpmProcessVersionStatus.Obsolete)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new ProcessDocVersionDto(
                    v.Id,
                    v.VersionNumber,
                    v.PublishedAt,
                    v.CreatedByUserId,
                    v.ReleaseNotes,
                    snapshotVersionIds.Contains(v.Id)))
                .ToList()
        )).ToList();
    }

    private static string[] ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson)) return Array.Empty<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildHtml(
        BpmProcess process,
        BpmProcessVersion version,
        List<BpmProcessVariable> variables,
        List<BpmRaciEntry> raciEntries,
        List<BpmProcessRoleConfig> roles,
        BpmScriptModule? scriptModule,
        string? svgContent = null)
    {
        var model = ParseBpmnModel(version.DiagramXml ?? string.Empty);
        var owners = roles.Where(r => r.RoleType == BpmProcessRoleType.Owner).Select(r => r.DisplayName).ToList();
        var curators = roles.Where(r => r.RoleType == BpmProcessRoleType.Curator).Select(r => r.DisplayName).ToList();
        var now = DateTimeOffset.UtcNow;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>Документация: {HtmlEncode(process.Name)} v{version.VersionNumber}</title>");
        sb.AppendLine(GetStyles());
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // ─── Заголовок ───────────────────────────────────────────────────────
        sb.AppendLine("<header class=\"doc-header\">");
        sb.AppendLine($"  <h1>{HtmlEncode(process.Name)}</h1>");
        sb.AppendLine($"  <div class=\"doc-meta\">Версия {version.VersionNumber} &bull; Опубликована: {version.PublishedAt?.ToString("dd.MM.yyyy HH:mm") ?? "—"} &bull; Сформировано: {now:dd.MM.yyyy HH:mm}</div>");
        sb.AppendLine("</header>");

        // ─── SVG-диаграмма (если передана) ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(svgContent))
        {
            sb.AppendLine("<div class=\"doc-diagram\">");
            sb.AppendLine(svgContent);
            sb.AppendLine("</div>");
        }

        sb.AppendLine("<main>");

        // ─── Описание ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(process.Description))
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Описание</h2>");
            sb.AppendLine($"  <p>{HtmlEncode(process.Description)}</p>");
            sb.AppendLine("</section>");
        }

        if (!string.IsNullOrWhiteSpace(version.ReleaseNotes))
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Что изменилось в этой версии</h2>");
            sb.AppendLine($"  <p>{HtmlEncode(version.ReleaseNotes)}</p>");
            sb.AppendLine("</section>");
        }

        // ─── Зоны ответственности ─────────────────────────────────────────
        if (owners.Count > 0 || curators.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Зоны ответственности</h2>");
            sb.AppendLine("  <table><thead><tr><th>Роль</th><th>Участники</th></tr></thead><tbody>");
            if (owners.Count > 0)
                sb.AppendLine($"    <tr><td>Владелец</td><td>{HtmlEncode(string.Join(", ", owners))}</td></tr>");
            if (curators.Count > 0)
                sb.AppendLine($"    <tr><td>Куратор</td><td>{HtmlEncode(string.Join(", ", curators))}</td></tr>");
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── События ─────────────────────────────────────────────────────────
        var events = model.Nodes.Values
            .Where(n => n.ElementType.Contains("Event", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.ElementType).ThenBy(n => n.Name ?? n.Id)
            .ToList();
        if (events.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>События</h2>");
            sb.AppendLine("  <table><thead><tr><th>Тип</th><th>Название</th><th>Описание</th></tr></thead><tbody>");
            foreach (var ev in events)
            {
                sb.AppendLine($"    <tr>");
                sb.AppendLine($"      <td><span class=\"badge badge-event\">{HtmlEncode(LocalizeBpmnType(ev.ElementType))}</span></td>");
                sb.AppendLine($"      <td>{HtmlEncode(ev.Name ?? ev.Id)}</td>");
                sb.AppendLine($"      <td class=\"muted\">{HtmlEncode(ev.Documentation ?? string.Empty)}</td>");
                sb.AppendLine($"    </tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── Задачи ──────────────────────────────────────────────────────────
        var tasks = model.Nodes.Values
            .Where(n => n.ElementType.Contains("Task", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.ElementType).ThenBy(n => n.Name ?? n.Id)
            .ToList();
        if (tasks.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Задачи</h2>");
            sb.AppendLine("  <table><thead><tr><th>Тип</th><th>Название</th><th>Описание</th></tr></thead><tbody>");
            foreach (var t in tasks)
            {
                sb.AppendLine($"    <tr>");
                sb.AppendLine($"      <td><span class=\"badge badge-task\">{HtmlEncode(LocalizeBpmnType(t.ElementType))}</span></td>");
                sb.AppendLine($"      <td>{HtmlEncode(t.Name ?? t.Id)}</td>");
                sb.AppendLine($"      <td class=\"muted\">{HtmlEncode(t.Documentation ?? string.Empty)}</td>");
                sb.AppendLine($"    </tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── Шлюзы ───────────────────────────────────────────────────────────
        var gateways = model.Nodes.Values
            .Where(n => n.ElementType.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Name ?? n.Id)
            .ToList();
        if (gateways.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Условия и шлюзы</h2>");
            sb.AppendLine("  <table><thead><tr><th>Тип</th><th>Название</th><th>Описание</th></tr></thead><tbody>");
            foreach (var gw in gateways)
            {
                sb.AppendLine($"    <tr>");
                sb.AppendLine($"      <td><span class=\"badge badge-gw\">{HtmlEncode(LocalizeBpmnType(gw.ElementType))}</span></td>");
                sb.AppendLine($"      <td>{HtmlEncode(gw.Name ?? gw.Id)}</td>");
                sb.AppendLine($"      <td class=\"muted\">{HtmlEncode(gw.Documentation ?? string.Empty)}</td>");
                sb.AppendLine($"    </tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── Переходы ────────────────────────────────────────────────────────
        if (model.Flows.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Переходы (Sequence Flows)</h2>");
            sb.AppendLine("  <table><thead><tr><th>Откуда</th><th>Куда</th><th>Условие</th></tr></thead><tbody>");
            foreach (var f in model.Flows.Values.OrderBy(f => f.Id))
            {
                var srcName = model.Nodes.TryGetValue(f.SourceRef, out var src) ? (src.Name ?? src.Id) : f.SourceRef;
                var tgtName = model.Nodes.TryGetValue(f.TargetRef, out var tgt) ? (tgt.Name ?? tgt.Id) : f.TargetRef;
                sb.AppendLine($"    <tr><td>{HtmlEncode(srcName)}</td><td>{HtmlEncode(tgtName)}</td><td class=\"muted\">{HtmlEncode(f.ConditionExpression ?? (f.IsDefault ? "[по умолчанию]" : string.Empty))}</td></tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── Переменные ──────────────────────────────────────────────────────
        if (variables.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Переменные процесса</h2>");
            sb.AppendLine("  <table><thead><tr><th>Имя</th><th>Тип</th><th>Ключевая</th><th>Входная</th><th>Описание</th></tr></thead><tbody>");
            foreach (var v in variables)
            {
                sb.AppendLine($"    <tr>");
                sb.AppendLine($"      <td><code>{HtmlEncode(v.Name)}</code></td>");
                sb.AppendLine($"      <td>{HtmlEncode(v.VariableType.ToString())}</td>");
                sb.AppendLine($"      <td>{(v.IsKeyVariable ? "✓" : "")}</td>");
                sb.AppendLine($"      <td>{(v.IsInput ? "✓" : "")}</td>");
                sb.AppendLine($"      <td class=\"muted\"></td>");
                sb.AppendLine($"    </tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── RACI-матрица ────────────────────────────────────────────────────
        if (raciEntries.Count > 0)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>RACI-матрица</h2>");
            sb.AppendLine("  <table><thead><tr><th>Этап / Задача</th><th>Роль</th><th>Тип</th></tr></thead><tbody>");
            foreach (var r in raciEntries)
            {
                sb.AppendLine($"    <tr>");
                sb.AppendLine($"      <td>{HtmlEncode(r.Stage)}</td>");
                sb.AppendLine($"      <td>{HtmlEncode(r.Role)}</td>");
                sb.AppendLine($"      <td><span class=\"badge badge-raci badge-raci-{r.RaciType.ToString().ToLowerInvariant()}\">{r.RaciType}</span></td>");
                sb.AppendLine($"    </tr>");
            }
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("</section>");
        }

        // ─── Сценарии ────────────────────────────────────────────────────────
        if (scriptModule is not null && !string.IsNullOrWhiteSpace(scriptModule.ScriptBody))
        {
            sb.AppendLine("<section>");
            sb.AppendLine("  <h2>Сценарии C# (ScriptModule)</h2>");
            sb.AppendLine($"  <pre class=\"code-block\"><code>{HtmlEncode(scriptModule.ScriptBody)}</code></pre>");
            sb.AppendLine("</section>");
        }

        sb.AppendLine("</main>");
        sb.AppendLine("<footer class=\"doc-footer\">");
        sb.AppendLine($"  <p>Core BPM &bull; Автоматически сгенерировано {now:dd.MM.yyyy HH:mm} UTC</p>");
        sb.AppendLine("</footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GetStyles() => """
        <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 14px; color: #1f2937; background: #fff; line-height: 1.6; }
        .doc-header { background: #1e40af; color: #fff; padding: 24px 40px; }
        .doc-header h1 { font-size: 24px; font-weight: 700; }
        .doc-meta { margin-top: 6px; font-size: 13px; opacity: .85; }
        main { padding: 32px 40px; max-width: 1100px; }
        section { margin-bottom: 36px; }
        h2 { font-size: 17px; font-weight: 600; color: #1e40af; border-bottom: 2px solid #e5e7eb; padding-bottom: 6px; margin-bottom: 14px; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th { background: #f3f4f6; text-align: left; padding: 8px 12px; border: 1px solid #e5e7eb; font-weight: 600; color: #374151; }
        td { padding: 8px 12px; border: 1px solid #e5e7eb; vertical-align: top; }
        tr:nth-child(even) td { background: #f9fafb; }
        .muted { color: #6b7280; }
        code { font-family: 'Consolas', 'Courier New', monospace; background: #f3f4f6; padding: 1px 4px; border-radius: 3px; font-size: 12px; }
        pre.code-block { background: #1e293b; color: #e2e8f0; padding: 20px; border-radius: 8px; overflow-x: auto; font-size: 12px; line-height: 1.5; }
        pre.code-block code { background: none; padding: 0; color: inherit; font-size: 12px; }
        .badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px; font-weight: 600; }
        .badge-event { background: #dbeafe; color: #1d4ed8; }
        .badge-task  { background: #d1fae5; color: #065f46; }
        .badge-gw    { background: #fef3c7; color: #92400e; }
        .badge-raci  { background: #e5e7eb; color: #374151; }
        .badge-raci-r { background: #fee2e2; color: #991b1b; }
        .badge-raci-a { background: #fef3c7; color: #92400e; }
        .badge-raci-c { background: #dbeafe; color: #1d4ed8; }
        .badge-raci-i { background: #d1fae5; color: #065f46; }
        p { margin-bottom: 8px; }
        .doc-footer { margin-top: 48px; padding: 16px 40px; border-top: 1px solid #e5e7eb; font-size: 12px; color: #9ca3af; text-align: center; }
        .doc-diagram { padding: 24px 40px; border-bottom: 1px solid #e5e7eb; overflow-x: auto; background: #f9fafb; }
        .doc-diagram svg { max-width: 100%; height: auto; }
        </style>
        """;

    private static string HtmlEncode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
    }

    private static string LocalizeBpmnType(string t) => t switch
    {
        "userTask"              => "Задача пользователя",
        "serviceTask"           => "Сервисная задача",
        "scriptTask"            => "Сценарий",
        "receiveTask"           => "Задача получения",
        "sendTask"              => "Задача отправки",
        "startEvent"            => "Начальное событие",
        "endEvent"              => "Конечное событие",
        "intermediateCatchEvent"=> "Промежуточное событие",
        "boundaryEvent"         => "Граничное событие",
        "exclusiveGateway"      => "Исключающий шлюз",
        "inclusiveGateway"      => "Включающий шлюз",
        "parallelGateway"       => "Параллельный шлюз",
        _                       => t
    };

    // Упрощённый парсер модели (без зависимости от partial-класса BpmProcessService)
    private static DocProcessModel ParseBpmnModel(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new DocProcessModel(
                new Dictionary<string, DocBpmnNode>(StringComparer.Ordinal),
                new Dictionary<string, DocBpmnFlow>(StringComparer.Ordinal));

        var nodes = new Dictionary<string, DocBpmnNode>(StringComparer.Ordinal);
        var flows = new Dictionary<string, DocBpmnFlow>(StringComparer.Ordinal);

        try
        {
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            foreach (var el in doc.Descendants().Where(x => x.Attribute("id") is not null))
            {
                var local = el.Name.LocalName;
                var id = el.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (local == "sequenceFlow")
                {
                    flows[id] = new DocBpmnFlow(
                        id,
                        el.Attribute("sourceRef")?.Value ?? string.Empty,
                        el.Attribute("targetRef")?.Value ?? string.Empty,
                        el.Elements().FirstOrDefault(e => e.Name.LocalName == "conditionExpression")?.Value?.Trim(),
                        false);
                    continue;
                }

                nodes[id] = new DocBpmnNode(
                    id, local,
                    el.Attribute("name")?.Value,
                    string.Join("\n", el.Elements()
                        .Where(e => e.Name.LocalName == "documentation")
                        .Select(e => e.Value.Trim()))
                        .Trim());
            }

            // Пометить дефолтные переходы
            var defaultFlowIds = doc.Descendants()
                .Where(x => x.Attribute("default") is not null)
                .Select(x => x.Attribute("default")!.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var fid in defaultFlowIds.Where(flows.ContainsKey))
                flows[fid] = flows[fid] with { IsDefault = true };
        }
        catch { /* игнорируем невалидный XML */ }

        return new DocProcessModel(nodes, flows);
    }

    private sealed record DocBpmnNode(string Id, string ElementType, string? Name, string? Documentation);
    private sealed record DocBpmnFlow(string Id, string SourceRef, string TargetRef, string? ConditionExpression, bool IsDefault);
    private sealed record DocProcessModel(
        IReadOnlyDictionary<string, DocBpmnNode> Nodes,
        IReadOnlyDictionary<string, DocBpmnFlow> Flows);
}
