namespace CoreBPM.Server.Application.Notify.DTOs;

// ─── Запросы ──────────────────────────────────────────────────────────────────

/// <summary>Создание группового чата.</summary>
public record CreateGroupChatRequest(string Name, IReadOnlyList<Guid> MemberIds);

/// <summary>Получение/создание личного диалога с пользователем.</summary>
public record GetOrCreateDirectRequest(Guid WithUserId);

/// <summary>Отправка нового сообщения.</summary>
public record SendMessageRequest(string Text, Guid? ReplyToMessageId = null);

/// <summary>Редактирование сообщения.</summary>
public record EditMessageRequest(string Text);

/// <summary>Добавление/снятие реакции.</summary>
public record ToggleReactionRequest(string Emoji);

/// <summary>Добавление комментария к публикации канала.</summary>
public record AddPostCommentRequest(string Text);

/// <summary>Создание информационного канала.</summary>
public record CreateChannelRequest(string Name, string? Description, string? IconEmoji, string Kind);

/// <summary>Создание публикации в канале.</summary>
public record CreateChannelPostRequest(string? Title, string Body);

/// <summary>Редактирование публикации в канале.</summary>
public record EditChannelPostRequest(string? Title, string Body);

/// <summary>Настройки отображения ленты.</summary>
public record UpdateMessagingPrefsRequest(
    string SortOrder,
    IReadOnlyList<string> PinnedChatIds,
    IReadOnlyList<string> HiddenChatIds);

/// <summary>Обновление группового чата (название).</summary>
public record UpdateChatRequest(string Name);

/// <summary>Пересылка сообщения в другой чат.</summary>
public record ForwardMessageRequest(Guid TargetChatId);

/// <summary>Обновление информационного канала.</summary>
public record UpdateChannelRequest(string Name, string? Description, string? IconEmoji);

/// <summary>Назначение или снятие роли администратора у подписчика.</summary>
public record SetSubscriberRoleRequest(bool IsAdmin);

/// <summary>Приглашение пользователя в приватный канал.</summary>
public record InviteToChannelRequest(Guid UserId);

// ─── DTO ──────────────────────────────────────────────────────────────────────

/// <summary>Краткая информация о пользователе (автор/участник).</summary>
public record UserBriefDto(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary>Сводка по чату (для списка диалогов).</summary>
public record ChatSummaryDto(
    Guid Id,
    string? Name,
    string Kind,
    int UnreadCount,
    MessageDto? LastMessage,
    IReadOnlyList<UserBriefDto> Members,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt);

/// <summary>Участник чата.</summary>
public record ChatMemberDto(Guid UserId, string DisplayName, string? AvatarUrl, bool IsAdmin, bool IsMuted, DateTimeOffset JoinedAt);

/// <summary>Реакция на сообщение.</summary>
public record MessageReactionDto(string Emoji, int Count, bool MyReaction);

/// <summary>Сообщение в чате.</summary>
public record MessageDto(
    Guid Id,
    Guid ChatId,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Text,
    bool IsEdited,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    Guid? ReplyToMessageId,
    string? ReplyToText,
    IReadOnlyList<MessageReactionDto> Reactions,
    int ReadCount,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Результат поиска сообщений.</summary>
public record MessageSearchResultDto(
    Guid MessageId,
    Guid ChatId,
    string? ChatName,
    string AuthorName,
    string TextSnippet,
    DateTimeOffset CreatedAt);

/// <summary>Закреплённое сообщение.</summary>
public record PinnedMessageDto(
    Guid PinId,
    Guid MessageId,
    string MessageText,
    Guid PinnedByUserId,
    string PinnedByName,
    DateTimeOffset PinnedAt);

/// <summary>Сводка по каналу (для списка).</summary>
public record ChannelSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string? IconEmoji,
    string Kind,
    int SubscriberCount,
    DateTimeOffset CreatedAt,
    bool IsSubscribed,
    bool IsAdmin);

/// <summary>Публикация в канале.</summary>
public record ChannelPostDto(
    Guid Id,
    Guid ChannelId,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string? Title,
    string Body,
    bool IsEdited,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MessageReactionDto>? Reactions = null,
    int CommentCount = 0);

/// <summary>Комментарий к публикации канала.</summary>
public record PostCommentDto(
    Guid Id,
    Guid PostId,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Text,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

/// <summary>Подписчик информационного канала.</summary>
public record ChannelSubscriberDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    bool IsAdmin,
    DateTimeOffset SubscribedAt);

/// <summary>Закреплённая публикация канала.</summary>
public record ChannelPinnedPostDto(
    Guid PinId,
    Guid PostId,
    string? PostTitle,
    string PostBodySnippet,
    Guid PinnedByUserId,
    string PinnedByName,
    DateTimeOffset PinnedAt);

/// <summary>Настройки ленты сообщений пользователя.</summary>
public record MessagingPrefsDto(
    string SortOrder,
    IReadOnlyList<string> PinnedChatIds,
    IReadOnlyList<string> HiddenChatIds);

/// <summary>Счётчик непрочитанных сообщений.</summary>
public record UnreadCountDto(int TotalUnread);
