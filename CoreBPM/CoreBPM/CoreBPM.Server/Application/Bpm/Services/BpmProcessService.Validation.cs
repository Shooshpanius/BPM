using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Services;

public partial class BpmProcessService
{
    public async Task<BpmValidationResultDto> ValidateProcessAsync(Guid processId, Guid? versionId, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);
        var version = versionId.HasValue
            ? await GetVersionEntityAsync(processId, versionId.Value, ct, asNoTracking: true)
            : await GetCurrentVersionEntityAsync(processId, ct);

        if (string.IsNullOrWhiteSpace(version.DiagramXml))
        {
            return new BpmValidationResultDto(version.Id, version.VersionNumber,
            [
                new BpmValidationIssueDto("Error", "PROCESS_EMPTY", "Диаграмма процесса пуста", null)
            ]);
        }

        ProcessModel processModel;
        try
        {
            processModel = ParseProcessModel(version.DiagramXml);
        }
        catch (Exception ex)
        {
            return new BpmValidationResultDto(version.Id, version.VersionNumber,
            [
                new BpmValidationIssueDto("Error", "BPMN_XML_INVALID", $"Некорректный BPMN XML: {ex.Message}", null)
            ]);
        }

        var issues = new List<BpmValidationIssueDto>();

        foreach (var start in processModel.Nodes.Values.Where(n => n.ElementType == "startEvent"))
        {
            if (!processModel.Outgoing.TryGetValue(start.Id, out var flows) || flows.Count == 0)
                issues.Add(new BpmValidationIssueDto("Error", "START_NOT_CONNECTED", "Стартовое событие должно иметь исходящий поток", start.Id));
        }

        var reachable = GetReachableNodeIds(processModel);
        foreach (var end in processModel.Nodes.Values.Where(n => n.ElementType == "endEvent"))
        {
            if (!reachable.Contains(end.Id))
                issues.Add(new BpmValidationIssueDto("Error", "END_NOT_REACHABLE", "Конечное событие недостижимо из стартового элемента", end.Id));
        }

        foreach (var gateway in processModel.Nodes.Values.Where(n => n.ElementType is "exclusiveGateway" or "inclusiveGateway"))
        {
            if (!processModel.Outgoing.TryGetValue(gateway.Id, out var flows) || flows.Count == 0)
            {
                issues.Add(new BpmValidationIssueDto("Warning", "GATEWAY_WITHOUT_FLOW", "Шлюз не имеет исходящих потоков", gateway.Id));
                continue;
            }

            foreach (var flow in flows)
            {
                if (flow.IsDefault)
                    continue;

                if (string.IsNullOrWhiteSpace(flow.ConditionExpression))
                    issues.Add(new BpmValidationIssueDto("Error", "GATEWAY_FLOW_CONDITION_REQUIRED", "Для исходящего потока шлюза требуется условие", flow.Id));
            }
        }

        var configMap = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == processId)
            .ToDictionaryAsync(c => c.ElementId, c => ParseJsonObject(c.ConfigJson), ct);

        foreach (var userTask in processModel.Nodes.Values.Where(n => n.ElementType == "userTask"))
        {
            if (!configMap.TryGetValue(userTask.Id, out var config))
            {
                issues.Add(new BpmValidationIssueDto("Error", "USER_TASK_ASSIGNEE_MISSING", "Для User Task необходимо указать исполнителя", userTask.Id));
                issues.Add(new BpmValidationIssueDto("Error", "USER_TASK_FORM_MISSING", "Для User Task необходимо выбрать форму", userTask.Id));
                continue;
            }

            if (string.IsNullOrWhiteSpace(GetString(config, "assigneeValue")))
                issues.Add(new BpmValidationIssueDto("Error", "USER_TASK_ASSIGNEE_MISSING", "Для User Task необходимо указать исполнителя", userTask.Id));
            if (string.IsNullOrWhiteSpace(GetString(config, "formId")))
                issues.Add(new BpmValidationIssueDto("Error", "USER_TASK_FORM_MISSING", "Для User Task необходимо выбрать форму", userTask.Id));
        }

        foreach (var serviceTask in processModel.Nodes.Values.Where(n => n.ElementType == "serviceTask"))
        {
            if (!configMap.TryGetValue(serviceTask.Id, out var config) || string.IsNullOrWhiteSpace(GetString(config, "url")))
                issues.Add(new BpmValidationIssueDto("Error", "SERVICE_TASK_URL_MISSING", "Для Service Task необходимо указать URL", serviceTask.Id));
        }

        foreach (var receiveTask in processModel.Nodes.Values.Where(n => n.ElementType == "receiveTask"))
        {
            if (string.IsNullOrWhiteSpace(receiveTask.Name))
                issues.Add(new BpmValidationIssueDto("Error", "RECEIVE_TASK_MESSAGE_MISSING", "Для Receive Task необходимо задать имя сообщения", receiveTask.Id));
        }

        return new BpmValidationResultDto(version.Id, version.VersionNumber, issues);
    }

    private static ProcessModel ParseProcessModel(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new ProcessModel(
                new Dictionary<string, ProcessNode>(StringComparer.Ordinal),
                new Dictionary<string, SequenceFlowInfo>(StringComparer.Ordinal),
                new Dictionary<string, List<SequenceFlowInfo>>(StringComparer.Ordinal));

        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var nodes = new Dictionary<string, ProcessNode>(StringComparer.Ordinal);
        var flows = new Dictionary<string, SequenceFlowInfo>(StringComparer.Ordinal);
        var outgoing = new Dictionary<string, List<SequenceFlowInfo>>(StringComparer.Ordinal);

        foreach (var element in document.Descendants().Where(x => x.Attribute("id") is not null))
        {
            var local = element.Name.LocalName;
            var id = element.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (local == "sequenceFlow")
            {
                flows[id] = new SequenceFlowInfo(
                    id,
                    local,
                    element.Attribute("name")?.Value,
                    element.Attribute("sourceRef")?.Value ?? string.Empty,
                    element.Attribute("targetRef")?.Value ?? string.Empty,
                    element.Elements().FirstOrDefault(e => e.Name.LocalName == "conditionExpression")?.Value?.Trim(),
                    false);
                continue;
            }

            if (!IsTrackedBpmnNode(local))
                continue;

            nodes[id] = new ProcessNode(
                id,
                local,
                element.Attribute("name")?.Value,
                string.Join("\n", element.Elements().Where(e => e.Name.LocalName == "documentation").Select(e => e.Value.Trim())));
        }

        var defaultFlowIds = document.Descendants()
            .Where(x => x.Attribute("default") is not null)
            .Select(x => x.Attribute("default")!.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var flow in flows.Values)
        {
            var updated = flow with { IsDefault = defaultFlowIds.Contains(flow.Id) };
            flows[flow.Id] = updated;
            if (!outgoing.TryGetValue(updated.SourceRef, out var list))
            {
                list = [];
                outgoing[updated.SourceRef] = list;
            }

            list.Add(updated);
        }

        return new ProcessModel(nodes, flows, outgoing);
    }

    private static HashSet<string> GetReachableNodeIds(ProcessModel model)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(model.Nodes.Values.Where(n => n.ElementType == "startEvent").Select(n => n.Id));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (!model.Outgoing.TryGetValue(current, out var flows))
                continue;

            foreach (var flow in flows)
            {
                if (!string.IsNullOrWhiteSpace(flow.TargetRef))
                    queue.Enqueue(flow.TargetRef);
            }
        }

        return visited;
    }

    private static bool IsTrackedBpmnNode(string localName)
        => localName is "startEvent" or "endEvent" or "userTask" or "serviceTask" or "receiveTask" or "exclusiveGateway" or "inclusiveGateway" or "scriptTask" or "sendTask" or "intermediateCatchEvent" or "boundaryEvent";

    private static Dictionary<string, JsonElement> ParseJsonObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return [];

        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> json, string key)
        => json.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record ProcessNode(string Id, string ElementType, string? Name, string? Documentation);
    private sealed record SequenceFlowInfo(string Id, string ElementType, string? Name, string SourceRef, string TargetRef, string? ConditionExpression, bool IsDefault);
    private sealed record ProcessModel(IReadOnlyDictionary<string, ProcessNode> Nodes, IReadOnlyDictionary<string, SequenceFlowInfo> Flows, IReadOnlyDictionary<string, List<SequenceFlowInfo>> Outgoing);
}
