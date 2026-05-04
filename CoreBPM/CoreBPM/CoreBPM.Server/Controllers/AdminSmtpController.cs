using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>API настроек исходящей почты SMTP (FR-ADM-02.1).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/settings/smtp")]
public class AdminSmtpController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public AdminSmtpController(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    /// <summary>Возвращает текущие настройки SMTP. Пароль не возвращается.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var s = await _db.AdminSmtpSettings.FirstOrDefaultAsync(ct);
        if (s is null)
            return Ok(new SmtpSettingsDto("", 587, true, null, null, "", "Core BPM"));

        return Ok(new SmtpSettingsDto(
            s.Host, s.Port, s.UseSsl, s.Username,
            null, // пароль не передаём клиенту
            s.FromAddress, s.FromName));
    }

    /// <summary>Сохраняет настройки SMTP (singleton, Id=1).</summary>
    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SmtpSettingsDto dto, CancellationToken ct = default)
    {
        var s = await _db.AdminSmtpSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new AdminSmtpSettings { Id = 1 };
            _db.AdminSmtpSettings.Add(s);
        }

        s.Host = dto.Host ?? "";
        s.Port = dto.Port > 0 ? dto.Port : 587;
        s.UseSsl = dto.UseSsl;
        s.Username = dto.Username;
        if (!string.IsNullOrEmpty(dto.Password))
            s.Password = dto.Password;
        s.FromAddress = dto.FromAddress ?? "";
        s.FromName = dto.FromName ?? "Core BPM";
        s.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Тестирует подключение к SMTP-серверу, отправляя письмо на адрес From.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct = default)
    {
        var ok = await _email.TestConnectionAsync(ct);
        return Ok(new { success = ok });
    }
}
