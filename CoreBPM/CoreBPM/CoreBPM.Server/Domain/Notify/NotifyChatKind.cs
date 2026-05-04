namespace CoreBPM.Server.Domain.Notify;

/// <summary>Вид чата: личный (DM) или групповой.</summary>
public enum NotifyChatKind
{
    /// <summary>Личный диалог 1:1.</summary>
    Direct,
    /// <summary>Групповой чат с несколькими участниками.</summary>
    Group,
}
