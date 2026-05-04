namespace CoreBPM.Server.Domain.Admin;

/// <summary>Настройки SMS-провайдера (singleton, id=1, таблица admin_sms_settings).</summary>
public class AdminSmsSettings
{
    public int Id { get; set; } = 1;

    /// <summary>URL API провайдера (например https://api.smsru.ru/sms/send).</summary>
    public string? ProviderUrl { get; set; }

    /// <summary>API-ключ провайдера.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Номер отправителя (отображается как From).</summary>
    public string? FromNumber { get; set; }

    /// <summary>Флаг активности SMS-канала.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Имя HTTP-параметра для номера получателя (дефолт: "to").</summary>
    public string PhoneParamName { get; set; } = "to";

    /// <summary>Имя HTTP-параметра для текста сообщения (дефолт: "msg").</summary>
    public string MessageParamName { get; set; } = "msg";

    /// <summary>Имя HTTP-параметра для API-ключа (дефолт: "api_id").</summary>
    public string ApiKeyParamName { get; set; } = "api_id";
}
