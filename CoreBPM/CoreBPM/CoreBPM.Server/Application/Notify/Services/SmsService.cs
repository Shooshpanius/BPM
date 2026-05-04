using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Notify;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Сервис SMS-уведомлений через configurable HTTP-провайдер (FR-MSG-02.1).</summary>
public class SmsService : ISmsService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsService> _logger;

    public SmsService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<SmsService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        string phoneNumber,
        string message,
        string eventType,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var settings = await _db.AdminSmsSettings.FirstOrDefaultAsync(ct);
        var log = new NotifySmsLog
        {
            UserId = userId,
            PhoneNumber = phoneNumber,
            EventType = eventType,
            Message = message,
        };

        if (settings is null || !settings.IsEnabled || string.IsNullOrWhiteSpace(settings.ProviderUrl))
        {
            log.Status = "Skipped";
            log.ErrorMessage = "SMS-провайдер не настроен или отключён";
            _db.NotifySmsLogs.Add(log);
            await _db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("sms");
            var formData = new Dictionary<string, string>
            {
                [settings.ApiKeyParamName] = settings.ApiKey ?? "",
                [settings.PhoneParamName] = phoneNumber,
                [settings.MessageParamName] = message,
            };
            if (!string.IsNullOrWhiteSpace(settings.FromNumber))
                formData["from"] = settings.FromNumber;

            var response = await client.PostAsync(
                settings.ProviderUrl,
                new FormUrlEncodedContent(formData),
                ct);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            log.ProviderResponse = $"{(int)response.StatusCode}: {responseBody[..Math.Min(500, responseBody.Length)]}";

            if (response.IsSuccessStatusCode)
            {
                log.Status = "Sent";
                _logger.LogInformation("SMS отправлен: {EventType} → {Phone}", eventType, phoneNumber);
            }
            else
            {
                log.Status = "Failed";
                log.ErrorMessage = $"Провайдер вернул статус {(int)response.StatusCode}";
                _logger.LogWarning("SMS не отправлен (HTTP {Status}): {EventType} → {Phone}", response.StatusCode, eventType, phoneNumber);
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Ошибка отправки SMS: {EventType} → {Phone}", eventType, phoneNumber);
        }

        _db.NotifySmsLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var settings = await _db.AdminSmsSettings.FirstOrDefaultAsync(ct);
        if (settings is null || !settings.IsEnabled || string.IsNullOrWhiteSpace(settings.ProviderUrl))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient("sms");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var response = await client.GetAsync(settings.ProviderUrl, cts.Token);
            // Любой ответ от сервера = провайдер доступен
            return true;
        }
        catch
        {
            return false;
        }
    }
}
