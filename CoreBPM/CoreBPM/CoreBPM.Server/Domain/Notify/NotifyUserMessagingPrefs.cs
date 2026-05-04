namespace CoreBPM.Server.Domain.Notify;

/// <summary>Настройки отображения ленты сообщений для пользователя (таблица notify_user_messaging_prefs, FR-MSG-01.2).</summary>
public class NotifyUserMessagingPrefs
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, которому принадлежат настройки.</summary>
    public Guid UserId { get; set; }

    /// <summary>Сортировка ленты: by_activity (по активности), alphabetical, manual.</summary>
    public string SortOrder { get; set; } = "by_activity";

    /// <summary>JSON-массив идентификаторов закреплённых чатов (сверху списка).</summary>
    public string PinnedChatIds { get; set; } = "[]";

    /// <summary>JSON-массив идентификаторов скрытых чатов.</summary>
    public string HiddenChatIds { get; set; } = "[]";
}
