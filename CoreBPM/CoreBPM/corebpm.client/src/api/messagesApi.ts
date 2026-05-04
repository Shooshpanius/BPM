// API-клиент для чатов и сообщений (FR-MSG-01)

export interface UserBriefDto {
    id: string;
    displayName: string;
    avatarUrl: string | null;
}

export interface MessageReactionDto {
    emoji: string;
    count: number;
    myReaction: boolean;
}

export interface MessageDto {
    id: string;
    chatId: string;
    authorUserId: string;
    authorName: string;
    authorAvatarUrl: string | null;
    text: string;
    isEdited: boolean;
    editedAt: string | null;
    isDeleted: boolean;
    replyToMessageId: string | null;
    replyToText: string | null;
    reactions: MessageReactionDto[];
    readCount: number;
    isRead: boolean;
    createdAt: string;
}

export interface ChatSummaryDto {
    id: string;
    name: string | null;
    kind: 'Direct' | 'Group';
    unreadCount: number;
    lastMessage: MessageDto | null;
    members: UserBriefDto[];
    lastMessageAt: string | null;
    createdAt: string;
}

export interface ChatMemberDto {
    userId: string;
    displayName: string;
    avatarUrl: string | null;
    isAdmin: boolean;
    isMuted: boolean;
    joinedAt: string;
}

export interface MessageSearchResultDto {
    messageId: string;
    chatId: string;
    chatName: string | null;
    authorName: string;
    textSnippet: string;
    createdAt: string;
}

export interface PinnedMessageDto {
    pinId: string;
    messageId: string;
    messageText: string;
    pinnedByUserId: string;
    pinnedByName: string;
    pinnedAt: string;
}

export interface ChannelSummaryDto {
    id: string;
    name: string;
    description: string | null;
    iconEmoji: string | null;
    kind: 'Public' | 'Private';
    subscriberCount: number;
    createdAt: string;
    isSubscribed: boolean;
    isAdmin: boolean;
}

export interface ChannelPostDto {
    id: string;
    channelId: string;
    authorUserId: string;
    authorName: string;
    authorAvatarUrl: string | null;
    title: string | null;
    body: string;
    isEdited: boolean;
    editedAt: string | null;
    createdAt: string;
    reactions: MessageReactionDto[] | null;
    commentCount: number;
}

export interface PostCommentDto {
    id: string;
    postId: string;
    authorUserId: string;
    authorName: string;
    authorAvatarUrl: string | null;
    text: string;
    isDeleted: boolean;
    createdAt: string;
}

export interface ChannelSubscriberDto {
    userId: string;
    displayName: string;
    avatarUrl: string | null;
    isAdmin: boolean;
    subscribedAt: string;
}

export interface MessagingPrefsDto {
    sortOrder: string;
    pinnedChatIds: string[];
    hiddenChatIds: string[];
}

export interface UnreadCountDto {
    totalUnread: number;
}

// ─── Чаты ─────────────────────────────────────────────────────────────────────

export async function getMyChats(token: string): Promise<ChatSummaryDto[]> {
    const r = await fetch('/api/messages/chats', {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка загрузки чатов');
    return r.json();
}

export async function getOrCreateDirectChat(token: string, withUserId: string): Promise<ChatSummaryDto> {
    const r = await fetch('/api/messages/direct', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ withUserId }),
    });
    if (!r.ok) throw new Error('Ошибка открытия диалога');
    return r.json();
}

export async function createGroupChat(token: string, name: string, memberIds: string[]): Promise<ChatSummaryDto> {
    const r = await fetch('/api/messages/chats', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, memberIds }),
    });
    if (!r.ok) throw new Error('Ошибка создания чата');
    return r.json();
}

export async function getChatMembers(token: string, chatId: string): Promise<ChatMemberDto[]> {
    const r = await fetch(`/api/messages/chats/${chatId}/members`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка загрузки участников');
    return r.json();
}

export async function addChatMember(token: string, chatId: string, memberId: string): Promise<void> {
    await fetch(`/api/messages/chats/${chatId}/members`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(memberId),
    });
}

export async function removeChatMember(token: string, chatId: string, memberId: string): Promise<void> {
    await fetch(`/api/messages/chats/${chatId}/members/${memberId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function updateChat(token: string, chatId: string, name: string): Promise<ChatSummaryDto> {
    const r = await fetch(`/api/messages/chats/${chatId}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
    });
    if (!r.ok) throw new Error('Ошибка обновления чата');
    return r.json();
}

export async function leaveChat(token: string, chatId: string): Promise<void> {
    const r = await fetch(`/api/messages/chats/${chatId}/leave`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка выхода из чата');
}

export async function forwardMessage(token: string, messageId: string, targetChatId: string): Promise<MessageDto> {
    const r = await fetch(`/api/messages/${messageId}/forward`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ targetChatId }),
    });
    if (!r.ok) throw new Error('Ошибка пересылки сообщения');
    return r.json();
}

// ─── Сообщения ────────────────────────────────────────────────────────────────

export async function getMessages(token: string, chatId: string, limit = 50, before?: string): Promise<MessageDto[]> {
    const params = new URLSearchParams({ limit: String(limit) });
    if (before) params.set('before', before);
    const r = await fetch(`/api/messages/chats/${chatId}/messages?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка загрузки сообщений');
    return r.json();
}

export async function sendMessage(token: string, chatId: string, text: string, replyToMessageId?: string): Promise<MessageDto> {
    const r = await fetch(`/api/messages/chats/${chatId}/messages`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ text, replyToMessageId: replyToMessageId ?? null }),
    });
    if (!r.ok) throw new Error('Ошибка отправки сообщения');
    return r.json();
}

export async function editMessage(token: string, messageId: string, text: string): Promise<MessageDto> {
    const r = await fetch(`/api/messages/${messageId}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ text }),
    });
    if (!r.ok) throw new Error('Ошибка редактирования');
    return r.json();
}

export async function deleteMessage(token: string, messageId: string): Promise<void> {
    await fetch(`/api/messages/${messageId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function markMessageRead(token: string, messageId: string): Promise<{ unreadCount: number }> {
    const r = await fetch(`/api/messages/${messageId}/read`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return { unreadCount: 0 };
    return r.json();
}

export async function markAllRead(token: string, chatId: string): Promise<void> {
    await fetch(`/api/messages/chats/${chatId}/read-all`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function toggleReaction(token: string, messageId: string, emoji: string): Promise<MessageReactionDto[]> {
    const r = await fetch(`/api/messages/${messageId}/reactions`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ emoji }),
    });
    if (!r.ok) throw new Error('Ошибка реакции');
    return r.json();
}

export async function searchMessages(token: string, q: string, chatId?: string): Promise<MessageSearchResultDto[]> {
    const params = new URLSearchParams({ q });
    if (chatId) params.set('chatId', chatId);
    const r = await fetch(`/api/messages/search?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function sendTyping(token: string, chatId: string): Promise<void> {
    await fetch(`/api/messages/chats/${chatId}/typing`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
    });
}

// ─── Закреплённые сообщения ───────────────────────────────────────────────────

export async function getPinnedMessages(token: string, chatId: string): Promise<PinnedMessageDto[]> {
    const r = await fetch(`/api/messages/chats/${chatId}/pinned`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function pinMessage(token: string, chatId: string, messageId: string): Promise<PinnedMessageDto> {
    const r = await fetch(`/api/messages/chats/${chatId}/pinned`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(messageId),
    });
    if (!r.ok) throw new Error('Ошибка закрепления');
    return r.json();
}

export async function unpinMessage(token: string, chatId: string, pinId: string): Promise<void> {
    await fetch(`/api/messages/chats/${chatId}/pinned/${pinId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

// ─── Каналы ───────────────────────────────────────────────────────────────────

export async function getChannels(token: string): Promise<ChannelSummaryDto[]> {
    const r = await fetch('/api/messages/channels', {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function createChannel(
    token: string,
    name: string,
    description: string | null,
    iconEmoji: string | null,
    kind: string,
): Promise<ChannelSummaryDto> {
    const r = await fetch('/api/messages/channels', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, description, iconEmoji, kind }),
    });
    if (!r.ok) throw new Error('Ошибка создания канала');
    return r.json();
}

export async function updateChannel(
    token: string,
    channelId: string,
    name: string,
    description: string | null,
    iconEmoji: string | null,
): Promise<ChannelSummaryDto> {
    const r = await fetch(`/api/messages/channels/${channelId}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, description, iconEmoji }),
    });
    if (!r.ok) throw new Error('Ошибка обновления канала');
    return r.json();
}

export async function deleteChannel(token: string, channelId: string): Promise<void> {
    const r = await fetch(`/api/messages/channels/${channelId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка удаления канала');
}

export async function subscribeChannel(token: string, channelId: string): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/subscribe`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function unsubscribeChannel(token: string, channelId: string): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/subscribe`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

export async function getChannelPosts(token: string, channelId: string, limit = 30, before?: string, q?: string): Promise<ChannelPostDto[]> {
    const params = new URLSearchParams({ limit: String(limit) });
    if (before) params.set('before', before);
    if (q) params.set('q', q);
    const r = await fetch(`/api/messages/channels/${channelId}/posts?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function createChannelPost(token: string, channelId: string, title: string | null, body: string): Promise<ChannelPostDto> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ title, body }),
    });
    if (!r.ok) throw new Error('Ошибка публикации');
    return r.json();
}

export async function editChannelPost(token: string, channelId: string, postId: string, title: string | null, body: string): Promise<ChannelPostDto> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts/${postId}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ title, body }),
    });
    if (!r.ok) throw new Error('Ошибка редактирования');
    return r.json();
}

export async function deleteChannelPost(token: string, channelId: string, postId: string): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/posts/${postId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

// ─── Реакции на публикации ────────────────────────────────────────────────────

export async function togglePostReaction(token: string, channelId: string, postId: string, emoji: string): Promise<MessageReactionDto[]> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts/${postId}/react`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ emoji }),
    });
    if (!r.ok) throw new Error('Ошибка реакции');
    return r.json();
}

// ─── Комментарии к публикациям ────────────────────────────────────────────────

export async function getPostComments(token: string, channelId: string, postId: string): Promise<PostCommentDto[]> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts/${postId}/comments`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function addPostComment(token: string, channelId: string, postId: string, text: string): Promise<PostCommentDto> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts/${postId}/comments`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ text }),
    });
    if (!r.ok) throw new Error('Ошибка добавления комментария');
    return r.json();
}

export async function deletePostComment(token: string, channelId: string, postId: string, commentId: string): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/posts/${postId}/comments/${commentId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

// ─── Подписчики канала ────────────────────────────────────────────────────────

export async function getChannelSubscribers(token: string, channelId: string): Promise<ChannelSubscriberDto[]> {
    const r = await fetch(`/api/messages/channels/${channelId}/subscribers`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function setSubscriberRole(token: string, channelId: string, targetUserId: string, isAdmin: boolean): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/subscribers/${targetUserId}/role`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ isAdmin }),
    });
}

export async function inviteToChannel(token: string, channelId: string, userId: string): Promise<void> {
    const r = await fetch(`/api/messages/channels/${channelId}/invite`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId }),
    });
    if (!r.ok) throw new Error('Ошибка приглашения');
}

// ─── Закреплённые публикации канала ──────────────────────────────────────────

export interface ChannelPinnedPostDto {
    pinId: string;
    postId: string;
    postTitle: string | null;
    postBodySnippet: string;
    pinnedByUserId: string;
    pinnedByName: string;
    pinnedAt: string;
}

export async function getPinnedPosts(token: string, channelId: string): Promise<ChannelPinnedPostDto[]> {
    const r = await fetch(`/api/messages/channels/${channelId}/pinned-posts`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return [];
    return r.json();
}

export async function pinPost(token: string, channelId: string, postId: string): Promise<ChannelPinnedPostDto> {
    const r = await fetch(`/api/messages/channels/${channelId}/posts/${postId}/pin`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) throw new Error('Ошибка закрепления');
    return r.json();
}

export async function unpinPost(token: string, channelId: string, postId: string): Promise<void> {
    await fetch(`/api/messages/channels/${channelId}/posts/${postId}/pin`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
    });
}

// ─── Счётчик непрочитанных ────────────────────────────────────────────────────

export async function getUnreadCount(token: string): Promise<UnreadCountDto> {
    const r = await fetch('/api/messages/unread-count', {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return { totalUnread: 0 };
    return r.json();
}

// ─── Настройки ленты ──────────────────────────────────────────────────────────

export async function getMessagingPrefs(token: string): Promise<MessagingPrefsDto> {
    const r = await fetch('/api/users/me/messaging-prefs', {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return { sortOrder: 'by_activity', pinnedChatIds: [], hiddenChatIds: [] };
    return r.json();
}

export async function updateMessagingPrefs(token: string, prefs: MessagingPrefsDto): Promise<MessagingPrefsDto> {
    const r = await fetch('/api/users/me/messaging-prefs', {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(prefs),
    });
    if (!r.ok) throw new Error('Ошибка сохранения настроек');
    return r.json();
}
