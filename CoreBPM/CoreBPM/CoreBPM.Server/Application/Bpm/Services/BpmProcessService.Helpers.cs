using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Application.Bpm.Services;

public partial class BpmProcessService
{
    private async Task<BpmProcess> GetProcessEntityAsync(Guid processId, CancellationToken ct)
        => await _db.BpmProcesses.FirstOrDefaultAsync(p => p.Id == processId, ct)
           ?? throw new NotFoundException($"Процесс {processId} не найден");

    private async Task EnsureProcessExistsAsync(Guid processId, CancellationToken ct)
    {
        if (!await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct))
            throw new NotFoundException($"Процесс {processId} не найден");
    }

    private static void EnsureXmlLooksLikeBpmn(string xml)
    {
        if (!xml.Contains("definitions", StringComparison.OrdinalIgnoreCase) || !xml.Contains("process", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("XML не похож на BPMN 2.0 диаграмму");
    }
}
