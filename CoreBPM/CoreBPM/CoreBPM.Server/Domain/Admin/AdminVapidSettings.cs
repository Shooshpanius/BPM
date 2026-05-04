namespace CoreBPM.Server.Domain.Admin;

/// <summary>VAPID-ключи для Web Push уведомлений (singleton, id=1, таблица admin_vapid_settings).</summary>
public class AdminVapidSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Публичный VAPID-ключ (Base64url, ECDH P-256).</summary>
    public string? PublicKey { get; set; }

    /// <summary>Приватный VAPID-ключ (Base64url, ECDH P-256).</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Subject VAPID — URI mailto или URL (обязательно для Web Push).</summary>
    public string Subject { get; set; } = "mailto:admin@corebpm.local";
}
