using System.Text.Json.Serialization;

namespace CoreBPM.Server.Application.Auth.DTOs;

/// <summary>Ответ на обновление токена.</summary>
public class RefreshResponse
{
    public string AccessToken { get; set; } = string.Empty;
    /// <summary>Время жизни access token в секундах.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Новый refresh token — не сериализуется в JSON, используется только для обновления HttpOnly cookie.</summary>
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
}
