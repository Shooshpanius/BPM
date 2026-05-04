namespace CoreBPM.Server.Domain.Notify;

/// <summary>Тип информационного канала.</summary>
public enum NotifyChannelKind
{
    /// <summary>Публичный канал — любой пользователь может подписаться.</summary>
    Public,
    /// <summary>Приватный канал — доступ только по приглашению.</summary>
    Private,
}
