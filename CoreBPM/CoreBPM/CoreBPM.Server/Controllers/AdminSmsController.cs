using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Infrastructure.Persistence;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Управление настройками SMS-провайдера (FR-MSG-02.1).</summary>
[ApiController]
[Route("api/admin/settings/sms")]
[Authorize(Roles = "Admin")]
public class AdminSmsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISmsService _sms;

    public AdminSmsController(AppDbContext db, ISmsService sms)
    {
        _db = db;
        _sms = sms;
    }

    /// <summary>Получить текущие настройки SMS.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _db.AdminSmsSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
            return Ok(new SmsSettingsDto(null, null, null, false, "to", "msg", "api_id"));

        return Ok(new SmsSettingsDto(
            settings.ProviderUrl,
            settings.ApiKey,
            settings.FromNumber,
            settings.IsEnabled,
            settings.PhoneParamName,
            settings.MessageParamName,
            settings.ApiKeyParamName));
    }

    /// <summary>Обновить настройки SMS-провайдера.</summary>
    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SmsSettingsDto dto, CancellationToken ct)
    {
        var settings = await _db.AdminSmsSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new AdminSmsSettings();
            _db.AdminSmsSettings.Add(settings);
        }

        settings.ProviderUrl = dto.ProviderUrl;
        settings.ApiKey = dto.ApiKey;
        settings.FromNumber = dto.FromNumber;
        settings.IsEnabled = dto.IsEnabled;
        settings.PhoneParamName = dto.PhoneParamName ?? "to";
        settings.MessageParamName = dto.MessageParamName ?? "msg";
        settings.ApiKeyParamName = dto.ApiKeyParamName ?? "api_id";

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>Тест SMS-соединения (отправляет запрос к провайдеру).</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var ok = await _sms.TestConnectionAsync(ct);
        return Ok(new { success = ok, message = ok ? "Провайдер доступен" : "Провайдер недоступен" });
    }
}

/// <summary>DTO настроек SMS.</summary>
public sealed record SmsSettingsDto(
    string? ProviderUrl,
    string? ApiKey,
    string? FromNumber,
    bool IsEnabled,
    string? PhoneParamName,
    string? MessageParamName,
    string? ApiKeyParamName
);
