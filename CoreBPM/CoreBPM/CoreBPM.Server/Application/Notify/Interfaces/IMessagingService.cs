using CoreBPM.Server.Application.Notify.DTOs;

namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис корпоративных чатов и информационных каналов (FR-MSG-01).</summary>
public interface IMessagingService
{
    // ─── Чаты (FR-MSG-01.1) ───────────────────────────────────────────────────

    /// <summary>Возвращает список чатов текущего пользователя, отсортированных по последней активности.</summary>
    Task<IReadOnlyList<ChatSummaryDto>> GetMyChatsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Возвращает или создаёт личный диалог (DM) с указанным пользователем.</summary>
    Task<ChatSummaryDto> GetOrCreateDirectChatAsync(Guid userId, Guid withUserId, CancellationToken ct = default);

    /// <summary>Создаёт групповой чат.</summary>
    Task<ChatSummaryDto> CreateGroupChatAsync(Guid userId, CreateGroupChatRequest req, CancellationToken ct = default);

    /// <summary>Возвращает список участников чата.</summary>
    Task<IReadOnlyList<ChatMemberDto>> GetChatMembersAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    /// <summary>Добавляет участника в групповой чат (только администратор чата).</summary>
    Task AddChatMemberAsync(Guid chatId, Guid adminUserId, Guid newMemberId, CancellationToken ct = default);

    /// <summary>Удаляет участника из группового чата (только администратор чата).</summary>
    Task RemoveChatMemberAsync(Guid chatId, Guid adminUserId, Guid memberId, CancellationToken ct = default);

    // ─── Сообщения (FR-MSG-01.1) ──────────────────────────────────────────────

    /// <summary>Отправляет сообщение в чат.</summary>
    Task<MessageDto> SendMessageAsync(Guid chatId, Guid userId, SendMessageRequest req, CancellationToken ct = default);

    /// <summary>Возвращает список сообщений чата с пагинацией (cursor-based).</summary>
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid chatId, Guid userId, int limit = 50, DateTimeOffset? before = null, CancellationToken ct = default);

    /// <summary>Редактирует текст сообщения (только автор).</summary>
    Task<MessageDto> EditMessageAsync(Guid messageId, Guid userId, EditMessageRequest req, CancellationToken ct = default);

    /// <summary>Удаляет (soft-delete) сообщение (автор или администратор чата).</summary>
    Task DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>Помечает сообщение прочитанным. Возвращает новое количество непрочитанных в чате.</summary>
    Task<int> MarkReadAsync(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>Помечает все сообщения чата прочитанными.</summary>
    Task MarkAllReadAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    /// <summary>Добавляет или снимает реакцию на сообщение (toggle).</summary>
    Task<IReadOnlyList<MessageReactionDto>> ToggleReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken ct = default);

    /// <summary>Полнотекстовый поиск по сообщениям (в конкретном чате или глобально).</summary>
    Task<IReadOnlyList<MessageSearchResultDto>> SearchMessagesAsync(Guid userId, string query, Guid? chatId = null, CancellationToken ct = default);

    // ─── Закреплённые сообщения (FR-MSG-01.2) ────────────────────────────────

    /// <summary>Закрепляет сообщение в чате.</summary>
    Task<PinnedMessageDto> PinMessageAsync(Guid chatId, Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>Открепляет сообщение.</summary>
    Task UnpinMessageAsync(Guid pinId, Guid userId, CancellationToken ct = default);

    /// <summary>Возвращает список закреплённых сообщений в чате.</summary>
    Task<IReadOnlyList<PinnedMessageDto>> GetPinnedMessagesAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    // ─── Каналы (FR-MSG-01.2) ─────────────────────────────────────────────────

    /// <summary>Возвращает список доступных каналов с признаком подписки.</summary>
    Task<IReadOnlyList<ChannelSummaryDto>> GetChannelsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Создаёт информационный канал.</summary>
    Task<ChannelSummaryDto> CreateChannelAsync(Guid userId, CreateChannelRequest req, CancellationToken ct = default);

    /// <summary>Подписывает пользователя на канал.</summary>
    Task SubscribeAsync(Guid channelId, Guid userId, CancellationToken ct = default);

    /// <summary>Отписывает пользователя от канала.</summary>
    Task UnsubscribeAsync(Guid channelId, Guid userId, CancellationToken ct = default);

    /// <summary>Создаёт публикацию в канале (только администратор/модератор).</summary>
    Task<ChannelPostDto> CreatePostAsync(Guid channelId, Guid userId, CreateChannelPostRequest req, CancellationToken ct = default);

    /// <summary>Редактирует публикацию (только автор или администратор канала).</summary>
    Task<ChannelPostDto> EditPostAsync(Guid postId, Guid userId, EditChannelPostRequest req, CancellationToken ct = default);

    /// <summary>Удаляет публикацию (только автор или администратор канала).</summary>
    Task DeletePostAsync(Guid postId, Guid userId, CancellationToken ct = default);

    /// <summary>Возвращает список публикаций канала в хронологическом порядке.</summary>
    Task<IReadOnlyList<ChannelPostDto>> GetPostsAsync(Guid channelId, Guid userId, int limit = 30, DateTimeOffset? before = null, CancellationToken ct = default);

    // ─── Настройки ленты (FR-MSG-01.2) ───────────────────────────────────────

    /// <summary>Возвращает настройки отображения ленты пользователя.</summary>
    Task<MessagingPrefsDto> GetMessagingPrefsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Сохраняет настройки отображения ленты пользователя.</summary>
    Task<MessagingPrefsDto> UpdateMessagingPrefsAsync(Guid userId, UpdateMessagingPrefsRequest req, CancellationToken ct = default);

    /// <summary>Возвращает суммарное количество непрочитанных сообщений.</summary>
    Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
