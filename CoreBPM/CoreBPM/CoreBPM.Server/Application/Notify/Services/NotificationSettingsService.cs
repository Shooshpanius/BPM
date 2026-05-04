using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Domain.Notify;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Сервис настроек DND, throttle, журнала доставки и статистики (FR-MSG-02.2).</summary>
public class NotificationSettingsService : INotificationSettingsService
{
    private readonly AppDbContext _db;

    public NotificationSettingsService(AppDbContext db) => _db = db;

    // ── DND ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DndSettingsDto> GetDndSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var entry = await _db.NotifyDndSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (entry is null)
            return new DndSettingsDto { StartHour = 22, EndHour = 8, ApplyToPush = true, ApplyToSms = true };

        return MapDnd(entry);
    }

    /// <inheritdoc />
    public async Task<DndSettingsDto> UpdateDndSettingsAsync(Guid userId, DndSettingsDto dto, CancellationToken ct = default)
    {
        var entry = await _db.NotifyDndSettings
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (entry is null)
        {
            entry = new NotifyDndSettings { UserId = userId };
            _db.NotifyDndSettings.Add(entry);
        }

        entry.IsEnabled = dto.IsEnabled;
        entry.StartHour = Math.Clamp(dto.StartHour, 0, 23);
        entry.EndHour = Math.Clamp(dto.EndHour, 0, 23);
        entry.DisabledDays = string.Join(",", (dto.DisabledDays ?? []).Distinct().OrderBy(d => d));
        entry.TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? "UTC" : dto.TimeZone;
        entry.ApplyToPush = dto.ApplyToPush;
        entry.ApplyToSms = dto.ApplyToSms;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapDnd(entry);
    }

    /// <inheritdoc />
    public async Task<bool> IsInDndAsync(Guid userId, CancellationToken ct = default)
    {
        var entry = await _db.NotifyDndSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (entry is null || !entry.IsEnabled) return false;

        // Получаем текущее время в часовом поясе пользователя
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(entry.TimeZone); }
        catch { tz = TimeZoneInfo.Utc; }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        // Проверяем день недели
        if (!string.IsNullOrWhiteSpace(entry.DisabledDays))
        {
            var disabledDays = entry.DisabledDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var d) ? (int?)d : null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            if (disabledDays.Contains((int)local.DayOfWeek)) return true;
        }

        // Проверяем диапазон времени
        var hour = local.Hour;
        if (entry.StartHour <= entry.EndHour)
        {
            // Простой диапазон: 9–18
            return hour >= entry.StartHour && hour < entry.EndHour;
        }
        else
        {
            // Переночной диапазон: 22–8 (через полночь)
            return hour >= entry.StartHour || hour < entry.EndHour;
        }
    }

    // ── Throttle (ограничение частоты) ────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ThrottleSettingDto>> GetThrottleSettingsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var items = await _db.NotifyThrottleSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        return items.Select(MapThrottle).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ThrottleSettingDto>> UpdateThrottleSettingsAsync(
        Guid userId, UpdateThrottleSettingsRequest request, CancellationToken ct = default)
    {
        foreach (var dto in request.Settings)
        {
            if (!Enum.TryParse<DeliveryChannel>(dto.Channel, true, out var channel)) continue;

            var existing = await _db.NotifyThrottleSettings
                .FirstOrDefaultAsync(s => s.UserId == userId
                    && s.EventType == dto.EventType
                    && s.Channel == channel, ct);

            if (existing is null)
            {
                existing = new NotifyThrottleSetting { UserId = userId, EventType = dto.EventType, Channel = channel };
                _db.NotifyThrottleSettings.Add(existing);
            }

            existing.MinIntervalMinutes = Math.Max(0, dto.MinIntervalMinutes);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await GetThrottleSettingsAsync(userId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsThrottledAsync(
        Guid userId, string eventType, DeliveryChannel channel, CancellationToken ct = default)
    {
        // Получаем настройку ограничения
        var setting = await _db.NotifyThrottleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == eventType && s.Channel == channel, ct);

        if (setting is null || setting.MinIntervalMinutes <= 0)
            return false; // ограничение не задано

        // Проверяем время последней отправки
        var lastLog = await _db.NotifyThrottleLogs
            .FirstOrDefaultAsync(l => l.UserId == userId && l.EventType == eventType && l.Channel == channel, ct);

        var now = DateTimeOffset.UtcNow;

        if (lastLog is not null
            && (now - lastLog.LastSentAt).TotalMinutes < setting.MinIntervalMinutes)
        {
            // Throttle активен — не обновляем время
            return true;
        }

        // Обновляем/создаём запись в throttle-журнале
        if (lastLog is null)
        {
            _db.NotifyThrottleLogs.Add(new NotifyThrottleLog
            {
                UserId = userId,
                EventType = eventType,
                Channel = channel,
                LastSentAt = now,
            });
        }
        else
        {
            lastLog.LastSentAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return false;
    }

    // ── Журнал доставки ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task LogDeliveryAsync(
        Guid userId,
        string eventType,
        DeliveryChannel channel,
        NotifyDeliveryStatus status,
        string? error = null,
        CancellationToken ct = default)
    {
        _db.NotifyDeliveryLogs.Add(new NotifyDeliveryLog
        {
            UserId = userId,
            EventType = eventType,
            Channel = channel,
            Status = status,
            Error = error,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<DeliveryLogEntryDto> Items, int Total)> GetDeliveryLogsAsync(
        DeliveryLogFilterRequest filter, CancellationToken ct = default)
    {
        var query = _db.NotifyDeliveryLogs.AsNoTracking();

        if (filter.UserId.HasValue)
            query = query.Where(l => l.UserId == filter.UserId.Value);

        if (!string.IsNullOrWhiteSpace(filter.EventType))
            query = query.Where(l => l.EventType == filter.EventType);

        if (!string.IsNullOrWhiteSpace(filter.Channel)
            && Enum.TryParse<DeliveryChannel>(filter.Channel, true, out var ch))
            query = query.Where(l => l.Channel == ch);

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<NotifyDeliveryStatus>(filter.Status, true, out var st))
            query = query.Where(l => l.Status == st);

        if (filter.From.HasValue)
            query = query.Where(l => l.CreatedAt >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(l => l.CreatedAt <= filter.To.Value);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var skip = (Math.Max(1, filter.Page) - 1) * pageSize;

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        // Присоединяем имена пользователей
        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var items = logs.Select(l => new DeliveryLogEntryDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserFullName = userMap.TryGetValue(l.UserId, out var name) ? name : l.UserId.ToString(),
            EventType = l.EventType,
            Channel = l.Channel.ToString(),
            Status = l.Status.ToString(),
            Error = l.Error,
            CreatedAt = l.CreatedAt,
        }).ToList();

        return (items, total);
    }

    // ── Retention (хранение журнала) ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<NotificationLogRetentionDto> GetRetentionSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.AdminNotificationLogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return new NotificationLogRetentionDto
        {
            RetentionDays = settings?.RetentionDays ?? 90,
        };
    }

    /// <inheritdoc />
    public async Task<NotificationLogRetentionDto> UpdateRetentionSettingsAsync(
        NotificationLogRetentionDto dto, CancellationToken ct = default)
    {
        var settings = await _db.AdminNotificationLogSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new AdminNotificationLogSettings();
            _db.AdminNotificationLogSettings.Add(settings);
        }

        settings.RetentionDays = Math.Max(0, dto.RetentionDays);
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new NotificationLogRetentionDto { RetentionDays = settings.RetentionDays };
    }

    /// <inheritdoc />
    public async Task<int> PurgeOldLogsAsync(int olderThanDays, CancellationToken ct = default)
    {
        if (olderThanDays <= 0) return 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        var deleted = await _db.NotifyDeliveryLogs
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        return deleted;
    }

    // ── Статистика доставки ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DeliveryStatsDto> GetDeliveryStatsAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = _db.NotifyDeliveryLogs.AsNoTracking();

        if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(l => l.CreatedAt <= to.Value);

        var logs = await query
            .Select(l => new { l.Channel, l.Status, l.EventType })
            .ToListAsync(ct);

        var channels = Enum.GetValues<DeliveryChannel>();
        var byChannel = channels.Select(ch =>
        {
            var chLogs = logs.Where(l => l.Channel == ch).ToList();
            return new DeliveryStatDto
            {
                Channel = ch.ToString(),
                Sent = chLogs.Count(l => l.Status == NotifyDeliveryStatus.Sent),
                Failed = chLogs.Count(l => l.Status == NotifyDeliveryStatus.Failed),
                SkippedUserSettings = chLogs.Count(l => l.Status == NotifyDeliveryStatus.SkippedUserSettings),
                SkippedDnd = chLogs.Count(l => l.Status == NotifyDeliveryStatus.SkippedDnd),
                SkippedThrottle = chLogs.Count(l => l.Status == NotifyDeliveryStatus.SkippedThrottle),
                Total = chLogs.Count,
            };
        }).ToList();

        var topEventTypes = logs
            .GroupBy(l => l.EventType)
            .Select(g => new EventTypeStatDto
            {
                EventType = g.Key,
                Total = g.Count(),
                Sent = g.Count(l => l.Status == NotifyDeliveryStatus.Sent),
                Failed = g.Count(l => l.Status == NotifyDeliveryStatus.Failed),
            })
            .OrderByDescending(e => e.Total)
            .Take(10)
            .ToList();

        return new DeliveryStatsDto
        {
            From = from,
            To = to,
            ByChannel = byChannel,
            TopEventTypes = topEventTypes,
        };
    }

    // ─── маппинг ─────────────────────────────────────────────────────────────

    private static DndSettingsDto MapDnd(NotifyDndSettings e)
    {
        var days = string.IsNullOrWhiteSpace(e.DisabledDays)
            ? []
            : e.DisabledDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var d) ? (int?)d : null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

        return new DndSettingsDto
        {
            IsEnabled = e.IsEnabled,
            StartHour = e.StartHour,
            EndHour = e.EndHour,
            DisabledDays = days,
            TimeZone = e.TimeZone,
            ApplyToPush = e.ApplyToPush,
            ApplyToSms = e.ApplyToSms,
        };
    }

    private static ThrottleSettingDto MapThrottle(NotifyThrottleSetting s) => new()
    {
        EventType = s.EventType,
        Channel = s.Channel.ToString(),
        MinIntervalMinutes = s.MinIntervalMinutes,
    };
}

