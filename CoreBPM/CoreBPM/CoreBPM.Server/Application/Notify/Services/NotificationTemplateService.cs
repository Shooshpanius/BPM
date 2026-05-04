using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Реализация сервиса глобальных шаблонов уведомлений (FR-MSG-02.2).</summary>
public class NotificationTemplateService : INotificationTemplateService
{
    private readonly AppDbContext _db;

    public NotificationTemplateService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _db.AdminNotificationTemplates
            .AsNoTracking()
            .OrderBy(t => t.EventType)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto?> GetByEventTypeAsync(string eventType, CancellationToken ct = default)
    {
        var item = await _db.AdminNotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventType == eventType, ct);
        return item is null ? null : Map(item);
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateDto> UpsertAsync(
        string eventType, UpsertNotificationTemplateRequest req, CancellationToken ct = default)
    {
        var item = await _db.AdminNotificationTemplates
            .FirstOrDefaultAsync(t => t.EventType == eventType, ct);

        if (item is null)
        {
            item = new AdminNotificationTemplate { EventType = eventType };
            _db.AdminNotificationTemplates.Add(item);
        }

        item.EventLabel = req.EventLabel;
        item.EmailSubjectTemplate = req.EmailSubjectTemplate;
        item.EmailBodyTemplate = req.EmailBodyTemplate;
        item.ShortTemplate = req.ShortTemplate;
        item.IsMandatoryInApp = req.IsMandatoryInApp;
        item.IsMandatoryEmail = req.IsMandatoryEmail;
        item.IsMandatorySms = req.IsMandatorySms;
        item.IsMandatoryPush = req.IsMandatoryPush;
        item.IsActive = req.IsActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(item);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.AdminNotificationTemplates.FindAsync([id], ct);
        if (item is null) throw new KeyNotFoundException($"Шаблон {id} не найден.");
        _db.AdminNotificationTemplates.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public string Render(string template, IDictionary<string, string> variables)
    {
        // Заменяем {{key}} на соответствующее значение
        return Regex.Replace(template, @"\{\{(\w+(?:\.\w+)*)\}\}", m =>
        {
            var key = m.Groups[1].Value;
            return variables.TryGetValue(key, out var val) ? val : m.Value;
        });
    }

    /// <inheritdoc />
    public async Task<(bool MandatoryInApp, bool MandatoryEmail, bool MandatorySms, bool MandatoryPush)>
        GetMandatoryFlagsAsync(string eventType, CancellationToken ct = default)
    {
        var tmpl = await _db.AdminNotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventType == eventType, ct);

        if (tmpl is null)
            return (false, false, false, false);

        return (tmpl.IsMandatoryInApp, tmpl.IsMandatoryEmail, tmpl.IsMandatorySms, tmpl.IsMandatoryPush);
    }

    // ─── маппинг ─────────────────────────────────────────────────────────────

    private static NotificationTemplateDto Map(AdminNotificationTemplate t) => new()
    {
        Id = t.Id,
        EventType = t.EventType,
        EventLabel = t.EventLabel,
        EmailSubjectTemplate = t.EmailSubjectTemplate,
        EmailBodyTemplate = t.EmailBodyTemplate,
        ShortTemplate = t.ShortTemplate,
        IsMandatoryInApp = t.IsMandatoryInApp,
        IsMandatoryEmail = t.IsMandatoryEmail,
        IsMandatorySms = t.IsMandatorySms,
        IsMandatoryPush = t.IsMandatoryPush,
        IsActive = t.IsActive,
        UpdatedAt = t.UpdatedAt,
    };
}
