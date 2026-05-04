using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Infrastructure.Hubs;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса уведомлений через SignalR + in-app inbox + email + SMS + push (FR-MSG-02.1).</summary>
public class BpmNotificationService : IBpmNotificationService
{
    private readonly IHubContext<BpmNotificationHub> _hub;
    private readonly IInAppNotificationService _inbox;
    private readonly IEmailService _email;
    private readonly IEmailTemplateService _emailTemplates;
    private readonly ISmsService _sms;
    private readonly IPushNotificationService _push;
    private readonly AppDbContext _db;

    public BpmNotificationService(
        IHubContext<BpmNotificationHub> hub,
        IInAppNotificationService inbox,
        IEmailService email,
        IEmailTemplateService emailTemplates,
        ISmsService sms,
        IPushNotificationService push,
        AppDbContext db)
    {
        _hub = hub;
        _inbox = inbox;
        _email = email;
        _emailTemplates = emailTemplates;
        _sms = sms;
        _push = push;
        _db = db;
    }

    /// <inheritdoc />
    public async Task NotifyJobFailedAsync(
        Guid jobId,
        string processName,
        string? instanceName,
        string? error,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "JobFailed",
            jobId,
            processName,
            instanceName,
            error,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyMigrationPackageCompletedAsync(
        Guid packageId,
        string packageName,
        bool hasErrors,
        int total,
        int migrated,
        int failed,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "MigrationPackageCompleted",
            packageId,
            packageName,
            hasErrors,
            total,
            migrated,
            failed,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);

        // Уведомляем создателя пакета (если он не является администратором)
        await _hub.Clients
            .Group($"user:{createdByUserId}")
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyUserTaskActivatedAsync(
        Guid instanceId,
        string instanceName,
        string processName,
        string elementId,
        string? elementName,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "UserTaskActivated",
            instanceId,
            instanceName,
            processName,
            elementId,
            elementName,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех участников процесса — они сами подпишутся на группы экземпляра
        await _hub.Clients
            .Group($"instance:{instanceId}")
            .SendAsync("bpm:notification", payload, ct);

        // Также уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyUserAsync(
        Guid userId,
        string eventType,
        object payload,
        CancellationToken ct = default)
    {
        // 1. SignalR push
        await _hub.Clients
            .Group($"user:{userId}")
            .SendAsync("bpm:notification", new { type = eventType, data = payload }, ct);

        // 2. Сохранение в persistent inbox
        var title = BuildTitle(eventType, payload);
        var body = BuildBody(eventType, payload);
        var link = BuildLink(eventType, payload);
        var payloadJson = JsonSerializer.Serialize(payload);

        await _inbox.SaveAsync(new SaveInboxEntryRequest(
            userId, eventType, title, body, link, payloadJson), ct);

        // 3. Получаем данные пользователя
        var orgUser = await _db.OrgUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (orgUser is null) return;

        var displayName = $"{orgUser.FirstName} {orgUser.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = orgUser.WorkEmail ?? userId.ToString();

        // 4. Rich email (с шаблонами и кнопками действий)
        if (!string.IsNullOrWhiteSpace(orgUser.WorkEmail))
        {
            var actions = BuildActionButtons(eventType, payload);
            var (emailSubject, htmlBody) = await _emailTemplates.RenderAsync(
                eventType, title, body, link, actions, ct);

            await _email.SendAsync(orgUser.WorkEmail, displayName, emailSubject, htmlBody, ct);
        }

        // 5. SMS (если у пользователя есть номер телефона)
        if (!string.IsNullOrWhiteSpace(orgUser.MobilePhone))
        {
            var smsText = BuildSmsText(eventType, title, body);
            await _sms.SendAsync(orgUser.MobilePhone, smsText, eventType, userId, ct);
        }

        // 6. Web Push
        await _push.SendAsync(userId, title, body, link, ct);
    }

    /// <inheritdoc />
    public async Task NotifyImprovementStatusChangedAsync(
        Guid improvementId,
        string subject,
        string processName,
        string newStatus,
        Guid initiatorUserId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "ImprovementStatusChanged",
            improvementId,
            subject,
            processName,
            newStatus,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем инициатора предложения
        await _hub.Clients
            .Group($"user:{initiatorUserId}")
            .SendAsync("bpm:notification", payload, ct);

        // Уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc/>
    public async Task NotifyTaskCountersUpdatedAsync(Guid userId, CancellationToken ct = default)
    {
        var payload = new { type = "TaskCountersUpdated", occurredAt = DateTimeOffset.UtcNow };
        await _hub.Clients
            .Group($"user:{userId}")
            .SendAsync("bpm:notification", payload, ct);
    }

    // ─── Вспомогательные методы для построения текстов уведомлений ──────────────

    private static string BuildTitle(string eventType, object payload) => eventType switch
    {
        "TaskAssigned" => "Вам назначена задача",
        "TaskDone" => "Задача выполнена",
        "TaskReminder" => "Напоминание по задаче",
        "ResolutionTaskDone" => "Резолюция выполнена",
        "ChannelInvite" => "Приглашение в канал",
        "NewChannelPost" => "Новая публикация в канале",
        "ImprovementStatusChanged" => "Статус предложения изменён",
        "MigrationPackageCompleted" => "Пакет миграции завершён",
        "JobFailed" => "Ошибка задания процесса",
        _ => $"Уведомление: {eventType}",
    };

    private static string BuildBody(string eventType, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return eventType switch
            {
                "TaskAssigned" when root.TryGetProperty("taskTitle", out var t) => $"Задача «{t.GetString()}»",
                "ChannelInvite" when root.TryGetProperty("channelName", out var c) => $"Канал «{c.GetString()}»",
                "NewChannelPost" when root.TryGetProperty("title", out var t) => t.GetString() ?? "",
                "ImprovementStatusChanged" when root.TryGetProperty("subject", out var s) => s.GetString() ?? "",
                _ => "",
            };
        }
        catch { return ""; }
    }

    private static string? BuildLink(string eventType, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return eventType switch
            {
                "TaskAssigned" when root.TryGetProperty("taskId", out var id) => $"/tasks/{id.GetGuid()}",
                "TaskDone" when root.TryGetProperty("taskId", out var id) => $"/tasks/{id.GetGuid()}",
                "TaskReminder" when root.TryGetProperty("taskId", out var id) => $"/tasks/{id.GetGuid()}",
                "ChannelInvite" when root.TryGetProperty("channelId", out var id) => $"/channels",
                "NewChannelPost" when root.TryGetProperty("channelId", out var id) => $"/channels",
                _ => null,
            };
        }
        catch { return null; }
    }

    private static string BuildEmailHtml(string title, string body, string? link)
    {
        var linkHtml = !string.IsNullOrWhiteSpace(link)
            ? $"<p><a href=\"{link}\" style=\"color:#3b82f6\">Перейти →</a></p>"
            : "";
        var bodyHtml = !string.IsNullOrWhiteSpace(body)
            ? $"<p style=\"color:#374151\">{body}</p>"
            : "";

        return $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"/></head>
            <body style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
              <h2 style="color:#111827;margin-bottom:8px">{title}</h2>
              {bodyHtml}
              {linkHtml}
              <hr style="border:none;border-top:1px solid #e5e7eb;margin-top:24px"/>
              <p style="color:#9ca3af;font-size:12px">Core BPM — автоматическое уведомление</p>
            </body>
            </html>
            """;
    }

    /// <summary>Формирует кнопки действий для actionable email.</summary>
    private static IReadOnlyList<EmailActionButton>? BuildActionButtons(string eventType, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return eventType switch
            {
                "TaskAssigned" when root.TryGetProperty("approveToken", out var at)
                                 && root.TryGetProperty("rejectToken", out var rt) =>
                [
                    new("/action/approve?token=" + at.GetString(), "Согласовать", "#16a34a"),
                    new("/action/reject?token=" + rt.GetString(), "Отклонить", "#dc2626"),
                ],
                _ => null,
            };
        }
        catch { return null; }
    }

    /// <summary>Формирует краткий текст SMS-уведомления.</summary>
    private static string BuildSmsText(string eventType, string title, string body)
    {
        var text = string.IsNullOrWhiteSpace(body) ? title : $"{title}: {body}";
        // SMS ограничен 160 символами для однопакетного сообщения
        return text.Length > 160 ? text[..157] + "..." : text;
    }
}
