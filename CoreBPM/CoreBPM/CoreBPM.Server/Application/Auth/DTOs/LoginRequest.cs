namespace CoreBPM.Server.Application.Auth.DTOs;

/// <summary>Запрос на вход в систему.</summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
