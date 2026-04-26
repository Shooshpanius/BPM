using System.Text.Json.Serialization;

namespace CoreBPM.Server.Application.Auth.DTOs;

/// <summary>Ответ на успешный вход в систему.</summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    /// <summary>Время жизни access token в секундах.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Refresh token — не сериализуется в JSON, используется только для установки HttpOnly cookie.</summary>
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
}
