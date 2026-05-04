namespace CoreBPM.Server.Domain.Admin;

/// <summary>Настройки SMTP-сервера исходящей почты (таблица admin_smtp_settings, singleton Id=1).</summary>
public class AdminSmtpSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Хост SMTP-сервера.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Порт SMTP-сервера.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Использовать SSL/TLS.</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Имя пользователя (логин).</summary>
    public string? Username { get; set; }

    /// <summary>Пароль (хранится в зашифрованном виде или как plain-text в dev).</summary>
    public string? Password { get; set; }

    /// <summary>Адрес отправителя (From address).</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Имя отправителя (From name).</summary>
    public string FromName { get; set; } = "Core BPM";

    /// <summary>Дата последнего изменения настроек.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
