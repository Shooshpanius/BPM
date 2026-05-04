using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Notify;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Реализация сервиса чатов и каналов (FR-MSG-01).</summary>
public class MessagingService : IMessagingService
{
    private readonly AppDbContext _db;
    private readonly IBpmNotificationService _notifications;

    public MessagingService(AppDbContext db, IBpmNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // ─── Утилиты ──────────────────────────────────────────────────────────────

    private async Task<UserBriefDto> GetUserBriefAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserBriefDto(u.Id, u.DisplayName, u.AvatarUrl))
            .FirstOrDefaultAsync(ct);
        return user ?? new UserBriefDto(userId, "Неизвестный пользователь", null);
    }

    private async Task<Dictionary<Guid, UserBriefDto>> GetUserBriefsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var unique = ids.Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => unique.Contains(u.Id))
            .Select(u => new UserBriefDto(u.Id, u.DisplayName, u.AvatarUrl))
            .ToListAsync(ct);
        return users.ToDictionary(u => u.Id);
    }

    private async Task EnsureMemberAsync(Guid chatId, Guid userId, CancellationToken ct)
    {
        var isMember = await _db.NotifyChatMembers
            .AnyAsync(m => m.ChatId == chatId && m.UserId == userId, ct);
        if (!isMember)
            throw new ForbiddenException("Вы не являетесь участником этого чата.");
    }

    private MessageDto MapMessage(NotifyMessage m, Guid currentUserId, Dictionary<Guid, UserBriefDto> users)
    {
        var reactions = m.Reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new MessageReactionDto(g.Key, g.Count(), g.Any(r => r.UserId == currentUserId)))
            .ToList();

        users.TryGetValue(m.AuthorUserId, out var author);

        return new MessageDto(
            Id: m.Id,
            ChatId: m.ChatId ?? Guid.Empty,
            AuthorUserId: m.AuthorUserId,
            AuthorName: author?.DisplayName ?? "Пользователь",
            AuthorAvatarUrl: author?.AvatarUrl,
            Text: m.IsDeleted ? "Сообщение удалено" : m.Text,
            IsEdited: m.IsEdited,
            EditedAt: m.EditedAt,
            IsDeleted: m.IsDeleted,
            ReplyToMessageId: m.ReplyToMessageId,
            ReplyToText: m.IsDeleted ? null : m.ReplyToMessage?.Text,
            Reactions: reactions,
            ReadCount: m.Reads.Count,
            IsRead: m.Reads.Any(r => r.UserId == currentUserId),
            CreatedAt: m.CreatedAt);
    }

    // ─── Чаты ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChatSummaryDto>> GetMyChatsAsync(Guid userId, CancellationToken ct = default)
    {
        var chatIds = await _db.NotifyChatMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatId)
            .ToListAsync(ct);

        var chats = await _db.NotifyChats
            .Where(c => chatIds.Contains(c.Id))
            .Include(c => c.Members)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);

        var allUserIds = chats.SelectMany(c => c.Members.Select(m => m.UserId)).Distinct();
        var users = await GetUserBriefsAsync(allUserIds, ct);

        // Подсчёт непрочитанных
        var unreadByChat = await _db.NotifyMessages
            .Where(m => chatIds.Contains(m.ChatId!.Value) && !m.IsDeleted && m.AuthorUserId != userId)
            .Where(m => !_db.NotifyMessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId))
            .GroupBy(m => m.ChatId!.Value)
            .Select(g => new { ChatId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var unreadMap = unreadByChat.ToDictionary(x => x.ChatId, x => x.Count);

        // Последнее сообщение
        var lastMsgByChat = await _db.NotifyMessages
            .Where(m => chatIds.Contains(m.ChatId!.Value) && !m.IsDeleted)
            .Include(m => m.Reactions)
            .Include(m => m.Reads)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        var lastMsgMap = lastMsgByChat
            .GroupBy(m => m.ChatId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return chats.Select(c =>
        {
            var memberBriefs = c.Members.Select(m =>
            {
                users.TryGetValue(m.UserId, out var u);
                return new UserBriefDto(m.UserId, u?.DisplayName ?? "Пользователь", u?.AvatarUrl);
            }).ToList();

            MessageDto? lastMsg = null;
            if (lastMsgMap.TryGetValue(c.Id, out var lm))
                lastMsg = MapMessage(lm, userId, users);

            return new ChatSummaryDto(
                Id: c.Id,
                Name: c.Kind == NotifyChatKind.Direct
                    ? memberBriefs.FirstOrDefault(m => m.Id != userId)?.DisplayName
                    : c.Name,
                Kind: c.Kind.ToString(),
                UnreadCount: unreadMap.GetValueOrDefault(c.Id, 0),
                LastMessage: lastMsg,
                Members: memberBriefs,
                LastMessageAt: c.LastMessageAt,
                CreatedAt: c.CreatedAt);
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<ChatSummaryDto> GetOrCreateDirectChatAsync(Guid userId, Guid withUserId, CancellationToken ct = default)
    {
        // Ищем существующий DM между двумя пользователями
        var existing = await _db.NotifyChats
            .Where(c => c.Kind == NotifyChatKind.Direct)
            .Where(c => _db.NotifyChatMembers.Any(m => m.ChatId == c.Id && m.UserId == userId)
                     && _db.NotifyChatMembers.Any(m => m.ChatId == c.Id && m.UserId == withUserId))
            .Include(c => c.Members)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            var users = await GetUserBriefsAsync(existing.Members.Select(m => m.UserId), ct);
            var other = users.GetValueOrDefault(withUserId);
            return new ChatSummaryDto(
                Id: existing.Id, Name: other?.DisplayName,
                Kind: "Direct", UnreadCount: 0, LastMessage: null,
                Members: existing.Members.Select(m => new UserBriefDto(m.UserId, users.GetValueOrDefault(m.UserId)?.DisplayName ?? "Пользователь", users.GetValueOrDefault(m.UserId)?.AvatarUrl)).ToList(),
                LastMessageAt: existing.LastMessageAt, CreatedAt: existing.CreatedAt);
        }

        // Создаём новый DM
        var now = DateTimeOffset.UtcNow;
        var chat = new NotifyChat { Id = Guid.NewGuid(), Kind = NotifyChatKind.Direct, CreatedByUserId = userId, CreatedAt = now };
        _db.NotifyChats.Add(chat);
        _db.NotifyChatMembers.Add(new NotifyChatMember { Id = Guid.NewGuid(), ChatId = chat.Id, UserId = userId, JoinedAt = now });
        _db.NotifyChatMembers.Add(new NotifyChatMember { Id = Guid.NewGuid(), ChatId = chat.Id, UserId = withUserId, JoinedAt = now });
        await _db.SaveChangesAsync(ct);

        var newUsers = await GetUserBriefsAsync(new[] { userId, withUserId }, ct);
        var otherUser = newUsers.GetValueOrDefault(withUserId);
        return new ChatSummaryDto(
            Id: chat.Id, Name: otherUser?.DisplayName,
            Kind: "Direct", UnreadCount: 0, LastMessage: null,
            Members: newUsers.Values.ToList(),
            LastMessageAt: null, CreatedAt: now);
    }

    /// <inheritdoc/>
    public async Task<ChatSummaryDto> CreateGroupChatAsync(Guid userId, CreateGroupChatRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Название чата обязательно.");

        var now = DateTimeOffset.UtcNow;
        var chat = new NotifyChat { Id = Guid.NewGuid(), Name = req.Name.Trim(), Kind = NotifyChatKind.Group, CreatedByUserId = userId, CreatedAt = now };
        _db.NotifyChats.Add(chat);

        // Создатель — администратор чата
        _db.NotifyChatMembers.Add(new NotifyChatMember { Id = Guid.NewGuid(), ChatId = chat.Id, UserId = userId, IsAdmin = true, JoinedAt = now });

        foreach (var memberId in req.MemberIds.Where(id => id != userId).Distinct())
            _db.NotifyChatMembers.Add(new NotifyChatMember { Id = Guid.NewGuid(), ChatId = chat.Id, UserId = memberId, JoinedAt = now });

        await _db.SaveChangesAsync(ct);

        var allIds = req.MemberIds.Append(userId);
        var users = await GetUserBriefsAsync(allIds, ct);
        return new ChatSummaryDto(
            Id: chat.Id, Name: chat.Name, Kind: "Group",
            UnreadCount: 0, LastMessage: null,
            Members: users.Values.ToList(),
            LastMessageAt: null, CreatedAt: now);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChatMemberDto>> GetChatMembersAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(chatId, userId, ct);
        var members = await _db.NotifyChatMembers
            .Where(m => m.ChatId == chatId)
            .ToListAsync(ct);
        var users = await GetUserBriefsAsync(members.Select(m => m.UserId), ct);
        return members.Select(m =>
        {
            users.TryGetValue(m.UserId, out var u);
            return new ChatMemberDto(m.UserId, u?.DisplayName ?? "Пользователь", u?.AvatarUrl, m.IsAdmin, m.IsMuted, m.JoinedAt);
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<ChatSummaryDto> UpdateChatAsync(Guid chatId, Guid userId, UpdateChatRequest req, CancellationToken ct = default)
    {
        var chat = await _db.NotifyChats
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == chatId, ct)
            ?? throw new NotFoundException("Чат не найден.");

        if (chat.Kind != NotifyChatKind.Group)
            throw new ValidationException("Переименование доступно только для групповых чатов.");

        _ = await _db.NotifyChatMembers
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId && m.IsAdmin, ct)
            ?? throw new ForbiddenException("Только администратор чата может изменять его название.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Название чата не может быть пустым.");

        chat.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);

        var users = await GetUserBriefsAsync(chat.Members.Select(m => m.UserId), ct);
        return new ChatSummaryDto(
            Id: chat.Id, Name: chat.Name, Kind: "Group",
            UnreadCount: 0, LastMessage: null,
            Members: chat.Members.Select(m =>
            {
                users.TryGetValue(m.UserId, out var u);
                return new UserBriefDto(m.UserId, u?.DisplayName ?? "Пользователь", u?.AvatarUrl);
            }).ToList(),
            LastMessageAt: chat.LastMessageAt, CreatedAt: chat.CreatedAt);
    }

    /// <inheritdoc/>
    public async Task LeaveChatAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var chat = await _db.NotifyChats.FindAsync(new object[] { chatId }, ct)
            ?? throw new NotFoundException("Чат не найден.");

        if (chat.Kind != NotifyChatKind.Group)
            throw new ValidationException("Покинуть можно только групповой чат.");

        var member = await _db.NotifyChatMembers
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId, ct)
            ?? throw new NotFoundException("Вы не являетесь участником этого чата.");

        // Если пользователь — единственный администратор, запрещаем покидать
        if (member.IsAdmin)
        {
            var otherAdmin = await _db.NotifyChatMembers
                .AnyAsync(m => m.ChatId == chatId && m.UserId != userId && m.IsAdmin, ct);
            if (!otherAdmin)
            {
                var otherMember = await _db.NotifyChatMembers
                    .AnyAsync(m => m.ChatId == chatId && m.UserId != userId, ct);
                if (otherMember)
                    throw new ValidationException("Вы единственный администратор чата. Назначьте другого администратора перед выходом.");
            }
        }

        _db.NotifyChatMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task AddChatMemberAsync(Guid chatId, Guid adminUserId, Guid newMemberId, CancellationToken ct = default)
    {
        var admin = await _db.NotifyChatMembers
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == adminUserId && m.IsAdmin, ct)
            ?? throw new ForbiddenException("Только администратор чата может добавлять участников.");

        var exists = await _db.NotifyChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == newMemberId, ct);
        if (exists) return;

        _db.NotifyChatMembers.Add(new NotifyChatMember
        {
            Id = Guid.NewGuid(), ChatId = chatId, UserId = newMemberId, JoinedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task RemoveChatMemberAsync(Guid chatId, Guid adminUserId, Guid memberId, CancellationToken ct = default)
    {
        _ = await _db.NotifyChatMembers
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == adminUserId && m.IsAdmin, ct)
            ?? throw new ForbiddenException("Только администратор чата может удалять участников.");

        var member = await _db.NotifyChatMembers
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == memberId, ct);
        if (member is null) return;

        _db.NotifyChatMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Сообщения ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<MessageDto> SendMessageAsync(Guid chatId, Guid userId, SendMessageRequest req, CancellationToken ct = default)
    {
        await EnsureMemberAsync(chatId, userId, ct);

        if (string.IsNullOrWhiteSpace(req.Text))
            throw new ValidationException("Текст сообщения не может быть пустым.");

        var now = DateTimeOffset.UtcNow;
        var msg = new NotifyMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            AuthorUserId = userId,
            Text = req.Text.Trim(),
            ReplyToMessageId = req.ReplyToMessageId,
            CreatedAt = now,
        };
        _db.NotifyMessages.Add(msg);

        // Обновляем LastMessageAt чата
        var chat = await _db.NotifyChats.FindAsync(new object[] { chatId }, ct);
        if (chat != null) chat.LastMessageAt = now;

        await _db.SaveChangesAsync(ct);

        // Real-time: уведомляем всех участников чата через SignalR
        var memberIds = await _db.NotifyChatMembers
            .Where(m => m.ChatId == chatId && m.UserId != userId && !m.IsMuted)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var author = await GetUserBriefAsync(userId, ct);
        foreach (var memberId in memberIds)
        {
            await _notifications.NotifyUserAsync(memberId, "NewMessage", new
            {
                chatId,
                messageId = msg.Id,
                authorName = author.DisplayName,
                textPreview = msg.Text.Length > 100 ? msg.Text[..100] + "…" : msg.Text,
            }, ct);
        }

        var users = await GetUserBriefsAsync(new[] { userId }, ct);
        return MapMessage(msg, userId, users);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid chatId, Guid userId, int limit = 50, DateTimeOffset? before = null, CancellationToken ct = default)
    {
        await EnsureMemberAsync(chatId, userId, ct);

        var query = _db.NotifyMessages
            .Where(m => m.ChatId == chatId)
            .Include(m => m.Reactions)
            .Include(m => m.Reads)
            .Include(m => m.ReplyToMessage)
            .AsQueryable();

        if (before.HasValue)
            query = query.Where(m => m.CreatedAt < before.Value);

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();

        var authorIds = messages.Select(m => m.AuthorUserId).Distinct();
        var users = await GetUserBriefsAsync(authorIds, ct);

        return messages.Select(m => MapMessage(m, userId, users)).ToList();
    }

    /// <inheritdoc/>
    public async Task<MessageDto> EditMessageAsync(Guid messageId, Guid userId, EditMessageRequest req, CancellationToken ct = default)
    {
        var msg = await _db.NotifyMessages
            .Include(m => m.Reactions)
            .Include(m => m.Reads)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct)
            ?? throw new NotFoundException("Сообщение не найдено.");

        if (msg.AuthorUserId != userId)
            throw new ForbiddenException("Редактировать можно только собственные сообщения.");

        if (string.IsNullOrWhiteSpace(req.Text))
            throw new ValidationException("Текст сообщения не может быть пустым.");

        msg.Text = req.Text.Trim();
        msg.IsEdited = true;
        msg.EditedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var users = await GetUserBriefsAsync(new[] { userId }, ct);
        return MapMessage(msg, userId, users);
    }

    /// <inheritdoc/>
    public async Task DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var msg = await _db.NotifyMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, ct)
            ?? throw new NotFoundException("Сообщение не найдено.");

        // Автор может удалить своё; администратор чата — любое
        if (msg.AuthorUserId != userId)
        {
            var isAdmin = await _db.NotifyChatMembers
                .AnyAsync(m => m.ChatId == msg.ChatId && m.UserId == userId && m.IsAdmin, ct);
            if (!isAdmin)
                throw new ForbiddenException("Удалить можно только собственное сообщение.");
        }

        msg.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<MessageDto> ForwardMessageAsync(Guid messageId, Guid userId, ForwardMessageRequest req, CancellationToken ct = default)
    {
        var original = await _db.NotifyMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, ct)
            ?? throw new NotFoundException("Исходное сообщение не найдено.");

        // Проверяем доступ к целевому чату
        await EnsureMemberAsync(req.TargetChatId, userId, ct);

        var now = DateTimeOffset.UtcNow;
        // Помечаем текст как пересланный (добавляем заголовок)
        var forwardedText = $"[Пересланное сообщение]\n{original.Text}";

        var msg = new NotifyMessage
        {
            Id = Guid.NewGuid(),
            ChatId = req.TargetChatId,
            AuthorUserId = userId,
            Text = forwardedText,
            CreatedAt = now,
        };
        _db.NotifyMessages.Add(msg);

        var targetChat = await _db.NotifyChats.FindAsync(new object[] { req.TargetChatId }, ct);
        if (targetChat != null) targetChat.LastMessageAt = now;

        await _db.SaveChangesAsync(ct);

        // Уведомляем участников целевого чата
        var memberIds = await _db.NotifyChatMembers
            .Where(m => m.ChatId == req.TargetChatId && m.UserId != userId && !m.IsMuted)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var author = await GetUserBriefAsync(userId, ct);
        foreach (var memberId in memberIds)
        {
            await _notifications.NotifyUserAsync(memberId, "NewMessage", new
            {
                chatId = req.TargetChatId,
                messageId = msg.Id,
                authorName = author.DisplayName,
                textPreview = forwardedText.Length > 100 ? forwardedText[..100] + "…" : forwardedText,
            }, ct);
        }

        var users = await GetUserBriefsAsync(new[] { userId }, ct);
        return MapMessage(msg, userId, users);
    }

    /// <inheritdoc/>
    public async Task<int> MarkReadAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var msg = await _db.NotifyMessages.FindAsync(new object[] { messageId }, ct)
            ?? throw new NotFoundException("Сообщение не найдено.");

        var already = await _db.NotifyMessageReads
            .AnyAsync(r => r.MessageId == messageId && r.UserId == userId, ct);
        if (!already)
        {
            _db.NotifyMessageReads.Add(new NotifyMessageRead
            {
                Id = Guid.NewGuid(), MessageId = messageId, UserId = userId, ReadAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        // Количество непрочитанных в чате
        return await _db.NotifyMessages
            .Where(m => m.ChatId == msg.ChatId && !m.IsDeleted && m.AuthorUserId != userId)
            .Where(m => !_db.NotifyMessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId))
            .CountAsync(ct);
    }

    /// <inheritdoc/>
    public async Task MarkAllReadAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(chatId, userId, ct);

        var unread = await _db.NotifyMessages
            .Where(m => m.ChatId == chatId && !m.IsDeleted && m.AuthorUserId != userId)
            .Where(m => !_db.NotifyMessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var msgId in unread)
        {
            _db.NotifyMessageReads.Add(new NotifyMessageRead
            {
                Id = Guid.NewGuid(), MessageId = msgId, UserId = userId, ReadAt = now
            });
        }
        if (unread.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageReactionDto>> ToggleReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ValidationException("Emoji не может быть пустым.");

        var existing = await _db.NotifyMessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji, ct);

        if (existing != null)
            _db.NotifyMessageReactions.Remove(existing);
        else
            _db.NotifyMessageReactions.Add(new NotifyMessageReaction
            {
                Id = Guid.NewGuid(), MessageId = messageId, UserId = userId,
                Emoji = emoji, CreatedAt = DateTimeOffset.UtcNow
            });

        await _db.SaveChangesAsync(ct);

        var all = await _db.NotifyMessageReactions
            .Where(r => r.MessageId == messageId)
            .ToListAsync(ct);

        return all.GroupBy(r => r.Emoji)
            .Select(g => new MessageReactionDto(g.Key, g.Count(), g.Any(r => r.UserId == userId)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageSearchResultDto>> SearchMessagesAsync(Guid userId, string query, Guid? chatId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<MessageSearchResultDto>();

        // Чаты, в которых состоит пользователь
        var myChatIds = await _db.NotifyChatMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatId)
            .ToListAsync(ct);

        var msgQuery = _db.NotifyMessages
            .Where(m => !m.IsDeleted && m.ChatId != null && myChatIds.Contains(m.ChatId!.Value))
            .Where(m => m.Text.ToLower().Contains(query.ToLower()));

        if (chatId.HasValue)
            msgQuery = msgQuery.Where(m => m.ChatId == chatId.Value);

        var messages = await msgQuery
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var authorIds = messages.Select(m => m.AuthorUserId).Distinct();
        var users = await GetUserBriefsAsync(authorIds, ct);

        var chatNames = await _db.NotifyChats
            .Where(c => myChatIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return messages.Select(m =>
        {
            users.TryGetValue(m.AuthorUserId, out var author);
            var idx = m.Text.ToLower().IndexOf(query.ToLower());
            var start = Math.Max(0, idx - 30);
            var snippet = (start > 0 ? "…" : "") + m.Text.Substring(start, Math.Min(100, m.Text.Length - start));

            return new MessageSearchResultDto(
                MessageId: m.Id,
                ChatId: m.ChatId!.Value,
                ChatName: chatNames.GetValueOrDefault(m.ChatId!.Value),
                AuthorName: author?.DisplayName ?? "Пользователь",
                TextSnippet: snippet,
                CreatedAt: m.CreatedAt);
        }).ToList();
    }

    // ─── Закреплённые сообщения ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PinnedMessageDto> PinMessageAsync(Guid chatId, Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var isAdmin = await _db.NotifyChatMembers
            .AnyAsync(m => m.ChatId == chatId && m.UserId == userId && m.IsAdmin, ct);
        if (!isAdmin)
            throw new ForbiddenException("Закреплять сообщения может только администратор чата.");

        var msg = await _db.NotifyMessages.FindAsync(new object[] { messageId }, ct)
            ?? throw new NotFoundException("Сообщение не найдено.");

        var already = await _db.NotifyPinnedMessages.AnyAsync(p => p.ChatId == chatId && p.MessageId == messageId, ct);
        if (already)
            throw new ValidationException("Сообщение уже закреплено.");

        var pin = new NotifyPinnedMessage
        {
            Id = Guid.NewGuid(), ChatId = chatId, MessageId = messageId,
            PinnedByUserId = userId, PinnedAt = DateTimeOffset.UtcNow
        };
        _db.NotifyPinnedMessages.Add(pin);
        await _db.SaveChangesAsync(ct);

        var pinner = await GetUserBriefAsync(userId, ct);
        return new PinnedMessageDto(pin.Id, messageId, msg.Text, userId, pinner.DisplayName, pin.PinnedAt);
    }

    /// <inheritdoc/>
    public async Task UnpinMessageAsync(Guid pinId, Guid userId, CancellationToken ct = default)
    {
        var pin = await _db.NotifyPinnedMessages.FindAsync(new object[] { pinId }, ct)
            ?? throw new NotFoundException("Закреплённое сообщение не найдено.");

        var isAdmin = await _db.NotifyChatMembers
            .AnyAsync(m => m.ChatId == pin.ChatId && m.UserId == userId && m.IsAdmin, ct);
        if (!isAdmin)
            throw new ForbiddenException("Открепить сообщение может только администратор чата.");

        _db.NotifyPinnedMessages.Remove(pin);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PinnedMessageDto>> GetPinnedMessagesAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(chatId, userId, ct);

        var pins = await _db.NotifyPinnedMessages
            .Where(p => p.ChatId == chatId)
            .Include(p => p.Message)
            .OrderByDescending(p => p.PinnedAt)
            .ToListAsync(ct);

        var pinnerIds = pins.Select(p => p.PinnedByUserId).Distinct();
        var users = await GetUserBriefsAsync(pinnerIds, ct);

        return pins.Select(p =>
        {
            users.TryGetValue(p.PinnedByUserId, out var pinner);
            return new PinnedMessageDto(p.Id, p.MessageId, p.Message?.Text ?? "", p.PinnedByUserId,
                pinner?.DisplayName ?? "Пользователь", p.PinnedAt);
        }).ToList();
    }

    // ─── Каналы ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChannelSummaryDto>> GetChannelsAsync(Guid userId, CancellationToken ct = default)
    {
        var channels = await _db.NotifyChannels
            .Include(c => c.Subscribers)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var mySubscriptions = await _db.NotifyChannelSubscribers
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.ChannelId, ct);

        return channels
            .Where(c => c.Kind == NotifyChannelKind.Public || mySubscriptions.ContainsKey(c.Id))
            .Select(c =>
            {
                mySubscriptions.TryGetValue(c.Id, out var sub);
                return new ChannelSummaryDto(
                    Id: c.Id, Name: c.Name, Description: c.Description,
                    IconEmoji: c.IconEmoji, Kind: c.Kind.ToString(),
                    SubscriberCount: c.Subscribers.Count,
                    CreatedAt: c.CreatedAt,
                    IsSubscribed: sub != null,
                    IsAdmin: sub?.IsAdmin ?? false);
            }).ToList();
    }

    /// <inheritdoc/>
    public async Task<ChannelSummaryDto> CreateChannelAsync(Guid userId, CreateChannelRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Название канала обязательно.");

        if (!Enum.TryParse<NotifyChannelKind>(req.Kind, true, out var kind))
            throw new ValidationException("Недопустимый тип канала. Допустимые значения: Public, Private.");

        var now = DateTimeOffset.UtcNow;
        var channel = new NotifyChannel
        {
            Id = Guid.NewGuid(), Name = req.Name.Trim(), Description = req.Description?.Trim(),
            IconEmoji = req.IconEmoji, Kind = kind, CreatedByUserId = userId, CreatedAt = now
        };
        _db.NotifyChannels.Add(channel);

        // Создатель — администратор канала и автоматически подписан
        _db.NotifyChannelSubscribers.Add(new NotifyChannelSubscriber
        {
            Id = Guid.NewGuid(), ChannelId = channel.Id, UserId = userId,
            IsAdmin = true, SubscribedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return new ChannelSummaryDto(
            Id: channel.Id, Name: channel.Name, Description: channel.Description,
            IconEmoji: channel.IconEmoji, Kind: channel.Kind.ToString(),
            SubscriberCount: 1, CreatedAt: now, IsSubscribed: true, IsAdmin: true);
    }

    /// <inheritdoc/>
    public async Task<ChannelSummaryDto> UpdateChannelAsync(Guid channelId, Guid userId, UpdateChannelRequest req, CancellationToken ct = default)
    {
        var channel = await _db.NotifyChannels
            .Include(c => c.Subscribers)
            .FirstOrDefaultAsync(c => c.Id == channelId, ct)
            ?? throw new NotFoundException("Канал не найден.");

        var sub = await _db.NotifyChannelSubscribers
            .FirstOrDefaultAsync(s => s.ChannelId == channelId && s.UserId == userId && s.IsAdmin, ct)
            ?? throw new ForbiddenException("Только администратор канала может редактировать его.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Название канала не может быть пустым.");

        channel.Name = req.Name.Trim();
        channel.Description = req.Description?.Trim();
        channel.IconEmoji = req.IconEmoji;
        await _db.SaveChangesAsync(ct);

        return new ChannelSummaryDto(
            Id: channel.Id, Name: channel.Name, Description: channel.Description,
            IconEmoji: channel.IconEmoji, Kind: channel.Kind.ToString(),
            SubscriberCount: channel.Subscribers.Count,
            CreatedAt: channel.CreatedAt, IsSubscribed: true, IsAdmin: true);
    }

    /// <inheritdoc/>
    public async Task DeleteChannelAsync(Guid channelId, Guid userId, CancellationToken ct = default)
    {
        var channel = await _db.NotifyChannels.FindAsync(new object[] { channelId }, ct)
            ?? throw new NotFoundException("Канал не найден.");

        // Только создатель или администратор канала может удалить
        var isAdmin = await _db.NotifyChannelSubscribers
            .AnyAsync(s => s.ChannelId == channelId && s.UserId == userId && s.IsAdmin, ct);
        if (!isAdmin && channel.CreatedByUserId != userId)
            throw new ForbiddenException("Удалить канал может только его создатель или администратор.");

        // Каскадное удаление: посты и подписчики удалятся через FK
        _db.NotifyChannels.Remove(channel);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(Guid channelId, Guid userId, CancellationToken ct = default)
    {
        var channel = await _db.NotifyChannels.FindAsync(new object[] { channelId }, ct)
            ?? throw new NotFoundException("Канал не найден.");

        if (channel.Kind == NotifyChannelKind.Private)
            throw new ForbiddenException("Подписка на приватный канал возможна только по приглашению.");

        var already = await _db.NotifyChannelSubscribers.AnyAsync(s => s.ChannelId == channelId && s.UserId == userId, ct);
        if (already) return;

        _db.NotifyChannelSubscribers.Add(new NotifyChannelSubscriber
        {
            Id = Guid.NewGuid(), ChannelId = channelId, UserId = userId, SubscribedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(Guid channelId, Guid userId, CancellationToken ct = default)
    {
        var sub = await _db.NotifyChannelSubscribers
            .FirstOrDefaultAsync(s => s.ChannelId == channelId && s.UserId == userId, ct);
        if (sub is null) return;

        if (sub.IsAdmin)
        {
            var otherAdmin = await _db.NotifyChannelSubscribers
                .AnyAsync(s => s.ChannelId == channelId && s.UserId != userId && s.IsAdmin, ct);
            if (!otherAdmin)
                throw new ValidationException("Невозможно отписаться: вы единственный администратор канала. Назначьте другого администратора сначала.");
        }

        _db.NotifyChannelSubscribers.Remove(sub);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<ChannelPostDto> CreatePostAsync(Guid channelId, Guid userId, CreateChannelPostRequest req, CancellationToken ct = default)
    {
        var isAdmin = await _db.NotifyChannelSubscribers
            .AnyAsync(s => s.ChannelId == channelId && s.UserId == userId && s.IsAdmin, ct);
        if (!isAdmin)
            throw new ForbiddenException("Публиковать в канале могут только администраторы/модераторы.");

        if (string.IsNullOrWhiteSpace(req.Body))
            throw new ValidationException("Тело публикации обязательно.");

        var now = DateTimeOffset.UtcNow;
        var post = new NotifyChannelPost
        {
            Id = Guid.NewGuid(), ChannelId = channelId, AuthorUserId = userId,
            Title = req.Title?.Trim(), Body = req.Body.Trim(), CreatedAt = now
        };
        _db.NotifyChannelPosts.Add(post);
        await _db.SaveChangesAsync(ct);

        // Уведомляем подписчиков канала
        var subscribers = await _db.NotifyChannelSubscribers
            .Where(s => s.ChannelId == channelId && s.UserId != userId)
            .Select(s => s.UserId)
            .ToListAsync(ct);

        var author = await GetUserBriefAsync(userId, ct);
        var channel = await _db.NotifyChannels.FindAsync(new object[] { channelId }, ct);
        foreach (var subId in subscribers)
        {
            await _notifications.NotifyUserAsync(subId, "NewChannelPost", new
            {
                channelId,
                postId = post.Id,
                channelName = channel?.Name,
                title = post.Title,
                authorName = author.DisplayName,
            }, ct);
        }

        return new ChannelPostDto(post.Id, channelId, userId, author.DisplayName, author.AvatarUrl,
            post.Title, post.Body, false, null, now);
    }

    /// <inheritdoc/>
    public async Task<ChannelPostDto> EditPostAsync(Guid postId, Guid userId, EditChannelPostRequest req, CancellationToken ct = default)
    {
        var post = await _db.NotifyChannelPosts.FindAsync(new object[] { postId }, ct)
            ?? throw new NotFoundException("Публикация не найдена.");

        if (post.AuthorUserId != userId)
        {
            var isAdmin = await _db.NotifyChannelSubscribers
                .AnyAsync(s => s.ChannelId == post.ChannelId && s.UserId == userId && s.IsAdmin, ct);
            if (!isAdmin)
                throw new ForbiddenException("Редактировать публикацию может только автор или администратор канала.");
        }

        if (string.IsNullOrWhiteSpace(req.Body))
            throw new ValidationException("Тело публикации обязательно.");

        post.Title = req.Title?.Trim();
        post.Body = req.Body.Trim();
        post.IsEdited = true;
        post.EditedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var author = await GetUserBriefAsync(post.AuthorUserId, ct);
        return new ChannelPostDto(post.Id, post.ChannelId, post.AuthorUserId, author.DisplayName,
            author.AvatarUrl, post.Title, post.Body, post.IsEdited, post.EditedAt, post.CreatedAt);
    }

    /// <inheritdoc/>
    public async Task DeletePostAsync(Guid postId, Guid userId, CancellationToken ct = default)
    {
        var post = await _db.NotifyChannelPosts.FindAsync(new object[] { postId }, ct)
            ?? throw new NotFoundException("Публикация не найдена.");

        if (post.AuthorUserId != userId)
        {
            var isAdmin = await _db.NotifyChannelSubscribers
                .AnyAsync(s => s.ChannelId == post.ChannelId && s.UserId == userId && s.IsAdmin, ct);
            if (!isAdmin)
                throw new ForbiddenException("Удалить публикацию может только автор или администратор канала.");
        }

        _db.NotifyChannelPosts.Remove(post);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChannelPostDto>> GetPostsAsync(Guid channelId, Guid userId, int limit = 30, DateTimeOffset? before = null, CancellationToken ct = default)
    {
        // Проверяем, что пользователь подписан или канал публичный
        var channel = await _db.NotifyChannels.FindAsync(new object[] { channelId }, ct)
            ?? throw new NotFoundException("Канал не найден.");

        if (channel.Kind == NotifyChannelKind.Private)
        {
            var isSub = await _db.NotifyChannelSubscribers
                .AnyAsync(s => s.ChannelId == channelId && s.UserId == userId, ct);
            if (!isSub)
                throw new ForbiddenException("Доступ к приватному каналу запрещён.");
        }

        var query = _db.NotifyChannelPosts.Where(p => p.ChannelId == channelId);
        if (before.HasValue)
            query = query.Where(p => p.CreatedAt < before.Value);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        posts.Reverse();

        var authorIds = posts.Select(p => p.AuthorUserId).Distinct();
        var users = await GetUserBriefsAsync(authorIds, ct);

        return posts.Select(p =>
        {
            users.TryGetValue(p.AuthorUserId, out var author);
            return new ChannelPostDto(p.Id, p.ChannelId, p.AuthorUserId,
                author?.DisplayName ?? "Пользователь", author?.AvatarUrl,
                p.Title, p.Body, p.IsEdited, p.EditedAt, p.CreatedAt);
        }).ToList();
    }

    // ─── Настройки ленты ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<MessagingPrefsDto> GetMessagingPrefsAsync(Guid userId, CancellationToken ct = default)
    {
        var prefs = await _db.NotifyUserMessagingPrefs
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (prefs is null)
            return new MessagingPrefsDto("by_activity", Array.Empty<string>(), Array.Empty<string>());

        return new MessagingPrefsDto(
            prefs.SortOrder,
            JsonSerializer.Deserialize<List<string>>(prefs.PinnedChatIds) ?? new(),
            JsonSerializer.Deserialize<List<string>>(prefs.HiddenChatIds) ?? new());
    }

    /// <inheritdoc/>
    public async Task<MessagingPrefsDto> UpdateMessagingPrefsAsync(Guid userId, UpdateMessagingPrefsRequest req, CancellationToken ct = default)
    {
        var prefs = await _db.NotifyUserMessagingPrefs.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (prefs is null)
        {
            prefs = new NotifyUserMessagingPrefs { Id = Guid.NewGuid(), UserId = userId };
            _db.NotifyUserMessagingPrefs.Add(prefs);
        }

        prefs.SortOrder = req.SortOrder;
        prefs.PinnedChatIds = JsonSerializer.Serialize(req.PinnedChatIds);
        prefs.HiddenChatIds = JsonSerializer.Serialize(req.HiddenChatIds);
        await _db.SaveChangesAsync(ct);

        return new MessagingPrefsDto(prefs.SortOrder,
            req.PinnedChatIds.ToList(), req.HiddenChatIds.ToList());
    }

    /// <inheritdoc/>
    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var myChatIds = await _db.NotifyChatMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatId)
            .ToListAsync(ct);

        var count = await _db.NotifyMessages
            .Where(m => myChatIds.Contains(m.ChatId!.Value) && !m.IsDeleted && m.AuthorUserId != userId)
            .Where(m => !_db.NotifyMessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId))
            .CountAsync(ct);

        return new UnreadCountDto(count);
    }
}
