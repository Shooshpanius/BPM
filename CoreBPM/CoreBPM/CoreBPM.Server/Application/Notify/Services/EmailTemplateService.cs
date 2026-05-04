using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Сервис шаблонов email-уведомлений (FR-MSG-02.1 Rich email).</summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly AppDbContext _db;

    public EmailTemplateService(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        var templates = await _db.AdminEmailTemplates.AsNoTracking().ToListAsync(ct);
        return templates.Select(Map).ToList();
    }

    /// <inheritdoc/>
    public async Task<EmailTemplateDto?> GetByEventTypeAsync(string eventType, CancellationToken ct = default)
    {
        var t = await _db.AdminEmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventType == eventType, ct);
        return t is null ? null : Map(t);
    }

    /// <inheritdoc/>
    public async Task<EmailTemplateDto> UpsertAsync(UpsertEmailTemplateRequest request, CancellationToken ct = default)
    {
        var existing = await _db.AdminEmailTemplates
            .FirstOrDefaultAsync(x => x.EventType == request.EventType, ct);

        if (existing is null)
        {
            existing = new AdminEmailTemplate
            {
                EventType = request.EventType,
            };
            _db.AdminEmailTemplates.Add(existing);
        }

        existing.Subject = request.Subject;
        existing.HtmlTemplate = request.HtmlTemplate;
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(existing);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string eventType, CancellationToken ct = default)
    {
        var existing = await _db.AdminEmailTemplates
            .FirstOrDefaultAsync(x => x.EventType == eventType, ct);
        if (existing is not null)
        {
            _db.AdminEmailTemplates.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc/>
    public async Task<(string Subject, string HtmlBody)> RenderAsync(
        string eventType,
        string title,
        string body,
        string? link,
        IReadOnlyList<EmailActionButton>? actions = null,
        CancellationToken ct = default)
    {
        var template = await _db.AdminEmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventType == eventType && x.IsActive, ct);

        // Формируем HTML кнопок действий
        var actionsHtml = BuildActionsHtml(actions);
        var linkHtml = !string.IsNullOrWhiteSpace(link)
            ? $"""<p><a href="{link}" style="color:#3b82f6">Перейти →</a></p>"""
            : "";

        if (template is not null)
        {
            // Подстановка переменных в пользовательский шаблон
            var html = template.HtmlTemplate
                .Replace("{{title}}", System.Web.HttpUtility.HtmlEncode(title))
                .Replace("{{body}}", System.Web.HttpUtility.HtmlEncode(body))
                .Replace("{{link}}", link ?? "")
                .Replace("{{linkHtml}}", linkHtml)
                .Replace("{{actions}}", actionsHtml);
            var subject = template.Subject
                .Replace("{{title}}", title)
                .Replace("{{body}}", body);
            return (subject, html);
        }

        // Дефолтный HTML-шаблон с поддержкой кнопок
        var bodyHtml = !string.IsNullOrWhiteSpace(body)
            ? $"""<p style="color:#374151">{System.Web.HttpUtility.HtmlEncode(body)}</p>"""
            : "";

        var defaultHtml = $"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width,initial-scale=1"/>
            </head>
            <body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:0;background:#f9fafb">
              <div style="background:#ffffff;border-radius:8px;overflow:hidden;margin:24px auto;box-shadow:0 1px 3px rgba(0,0,0,.1)">
                <div style="background:#3b82f6;padding:24px 32px">
                  <h1 style="color:#ffffff;margin:0;font-size:18px;font-weight:600">Core BPM</h1>
                </div>
                <div style="padding:32px">
                  <h2 style="color:#111827;margin:0 0 12px;font-size:20px">{System.Web.HttpUtility.HtmlEncode(title)}</h2>
                  {bodyHtml}
                  {actionsHtml}
                  {linkHtml}
                </div>
                <div style="background:#f3f4f6;padding:16px 32px;text-align:center">
                  <p style="color:#9ca3af;font-size:12px;margin:0">Core BPM — автоматическое уведомление. Не отвечайте на это письмо.</p>
                </div>
              </div>
            </body>
            </html>
            """;

        return (title, defaultHtml);
    }

    private static string BuildActionsHtml(IReadOnlyList<EmailActionButton>? actions)
    {
        if (actions is null || actions.Count == 0) return "";
        var buttons = string.Join(" ", actions.Select(a =>
            $"""<a href="{a.Url}" style="display:inline-block;padding:10px 20px;background:{a.Color};color:#ffffff;text-decoration:none;border-radius:6px;font-size:14px;font-weight:500;margin:4px">{System.Web.HttpUtility.HtmlEncode(a.Label)}</a>"""));
        return $"""<div style="margin:24px 0">{buttons}</div>""";
    }

    private static EmailTemplateDto Map(AdminEmailTemplate t) =>
        new(t.Id, t.EventType, t.Subject, t.HtmlTemplate, t.IsActive, t.UpdatedAt);
}
