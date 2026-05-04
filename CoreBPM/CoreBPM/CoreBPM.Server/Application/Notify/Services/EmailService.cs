using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Реализация email-сервиса через System.Net.Mail.SmtpClient (FR-MSG-02.1).</summary>
public class EmailService : IEmailService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(AppDbContext db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var smtp = await _db.AdminSmtpSettings.FirstOrDefaultAsync(ct);
        if (smtp is null || string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            _logger.LogWarning("SMTP не настроен, письмо не отправлено: {Subject} → {To}", subject, toAddress);
            return;
        }

        try
        {
            using var client = BuildClient(smtp);
            using var msg = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(toAddress, toName));
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email отправлен: {Subject} → {To}", subject, toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки email: {Subject} → {To}", subject, toAddress);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var smtp = await _db.AdminSmtpSettings.FirstOrDefaultAsync(ct);
        if (smtp is null || string.IsNullOrWhiteSpace(smtp.Host)) return false;

        try
        {
            using var client = BuildClient(smtp);
            // Проверяем подключение, отправляя тестовое письмо на адрес-отправитель
            using var msg = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = "Core BPM — тест SMTP",
                Body = "<p>Тестовое письмо для проверки настроек SMTP.</p>",
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(smtp.FromAddress, smtp.FromName));
            await client.SendMailAsync(msg, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Тест SMTP-соединения не прошёл");
            return false;
        }
    }

    private static SmtpClient BuildClient(Domain.Admin.AdminSmtpSettings smtp)
    {
        var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password ?? string.Empty);

        return client;
    }
}
