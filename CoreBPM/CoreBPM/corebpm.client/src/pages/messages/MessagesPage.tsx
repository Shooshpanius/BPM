import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useBpmNotifications } from '../../context/BpmNotificationsContext';
import {
    getMyChats, getOrCreateDirectChat, createGroupChat,
    getMessages, sendMessage, editMessage, deleteMessage,
    markAllRead, toggleReaction, searchMessages,
    type ChatSummaryDto, type MessageDto, type MessageSearchResultDto,
} from '../../api/messagesApi';
import { getDirectoryEmployees, type DirectoryEmployeeDto } from '../../api/orgDirectoryApi';

interface MessagesPageProps {
    /** Открыть чат с конкретным пользователем (из адресной книги / задачи) */
    openChatWithUserId?: string | null;
}

/** Страница корпоративного чата (FR-MSG-01.1). */
export function MessagesPage({ openChatWithUserId }: MessagesPageProps) {
    const { accessToken, userId } = useAuth();
    const { notifications } = useBpmNotifications();

    const [chats, setChats] = useState<ChatSummaryDto[]>([]);
    const [selectedChatId, setSelectedChatId] = useState<string | null>(null);
    const [messages, setMessages] = useState<MessageDto[]>([]);
    const [msgInput, setMsgInput] = useState('');
    const [replyTo, setReplyTo] = useState<MessageDto | null>(null);
    const [editingMsg, setEditingMsg] = useState<MessageDto | null>(null);
    const [searchQ, setSearchQ] = useState('');
    const [searchResults, setSearchResults] = useState<MessageSearchResultDto[]>([]);
    const [showSearch, setShowSearch] = useState(false);
    const [showNewGroupDialog, setShowNewGroupDialog] = useState(false);
    const [newGroupName, setNewGroupName] = useState('');
    const [allUsers, setAllUsers] = useState<DirectoryEmployeeDto[]>([]);
    const [selectedMemberIds, setSelectedMemberIds] = useState<string[]>([]);
    const [loading, setLoading] = useState(false);
    const [sendingMsg, setSendingMsg] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const selectedChat = chats.find(c => c.id === selectedChatId);

    // Загрузка списка чатов
    const loadChats = useCallback(async () => {
        if (!accessToken) return;
        try {
            const data = await getMyChats(accessToken);
            setChats(data);
        } catch { /* игнорируем */ }
    }, [accessToken]);

    useEffect(() => {
        loadChats();
    }, [loadChats]);

    // Открыть DM с конкретным пользователем
    useEffect(() => {
        if (!openChatWithUserId || !accessToken) return;
        getOrCreateDirectChat(accessToken, openChatWithUserId)
            .then(chat => {
                setChats(prev => {
                    const exists = prev.find(c => c.id === chat.id);
                    return exists ? prev : [chat, ...prev];
                });
                setSelectedChatId(chat.id);
            })
            .catch(() => {});
    }, [openChatWithUserId, accessToken]);

    // Загрузка сообщений выбранного чата
    useEffect(() => {
        if (!selectedChatId || !accessToken) return;
        setLoading(true);
        getMessages(accessToken, selectedChatId)
            .then(data => { setMessages(data); setLoading(false); })
            .catch(() => setLoading(false));

        if (accessToken && selectedChatId)
            markAllRead(accessToken, selectedChatId).catch(() => {});
    }, [selectedChatId, accessToken]);

    // Прокрутка вниз при новых сообщениях
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    // SignalR: получение новых сообщений в чате
    useEffect(() => {
        const last = notifications[0];
        if (!last) return;
        if (last.type === 'ChatMessage') {
            const data = last.payload as Record<string, unknown>;
            const msg = data?.data as MessageDto | undefined;
            if (msg?.chatId === selectedChatId) {
                setMessages(prev => [...prev, msg]);
                if (accessToken && selectedChatId)
                    markAllRead(accessToken, selectedChatId).catch(() => {});
            }
            // Обновляем счётчик непрочитанных в списке чатов
            loadChats();
        }
        if (last.type === 'NewMessage') {
            loadChats();
        }
    }, [notifications, selectedChatId, accessToken, loadChats]);

    // Поиск
    useEffect(() => {
        if (!searchQ.trim() || !accessToken) { setSearchResults([]); return; }
        const t = setTimeout(() => {
            searchMessages(accessToken, searchQ).then(setSearchResults).catch(() => {});
        }, 400);
        return () => clearTimeout(t);
    }, [searchQ, accessToken]);

    const handleSend = async () => {
        if (!msgInput.trim() || !selectedChatId || !accessToken || sendingMsg) return;
        setSendingMsg(true);
        try {
            if (editingMsg) {
                const updated = await editMessage(accessToken, editingMsg.id, msgInput.trim());
                setMessages(prev => prev.map(m => m.id === updated.id ? updated : m));
                setEditingMsg(null);
            } else {
                const msg = await sendMessage(accessToken, selectedChatId, msgInput.trim(), replyTo?.id);
                setMessages(prev => [...prev, msg]);
                setReplyTo(null);
            }
            setMsgInput('');
            loadChats();
        } catch { /* ошибка */ } finally {
            setSendingMsg(false);
        }
    };

    const handleDeleteMessage = async (msgId: string) => {
        if (!accessToken) return;
        await deleteMessage(accessToken, msgId);
        setMessages(prev => prev.map(m => m.id === msgId ? { ...m, isDeleted: true, text: 'Сообщение удалено' } : m));
    };

    const handleReact = async (msgId: string, emoji: string) => {
        if (!accessToken) return;
        const reactions = await toggleReaction(accessToken, msgId, emoji);
        setMessages(prev => prev.map(m => m.id === msgId ? { ...m, reactions } : m));
    };

    const handleCreateGroup = async () => {
        if (!newGroupName.trim() || !accessToken) return;
        try {
            const chat = await createGroupChat(accessToken, newGroupName.trim(), selectedMemberIds);
            setChats(prev => [chat, ...prev]);
            setSelectedChatId(chat.id);
            setShowNewGroupDialog(false);
            setNewGroupName('');
            setSelectedMemberIds([]);
        } catch { /* ошибка */ }
    };

    const loadUsers = async () => {
        if (!accessToken || allUsers.length > 0) return;
        try {
            const data = await getDirectoryEmployees(accessToken, { page: 1, pageSize: 200 });
            setAllUsers(data.items);
        } catch { /* ошибка */ }
    };

    const formatTime = (iso: string) => {
        const d = new Date(iso);
        return d.toLocaleTimeString('ru', { hour: '2-digit', minute: '2-digit' });
    };

    const formatDate = (iso: string) => {
        const d = new Date(iso);
        return d.toLocaleDateString('ru', { day: 'numeric', month: 'short' });
    };

    return (
        <div style={{ display: 'flex', height: '100%', minHeight: 0, overflow: 'hidden' }}>
            {/* ─── Левая панель: список чатов ─── */}
            <aside style={{
                width: 280, minWidth: 240, borderRight: '1px solid #e5e7eb',
                display: 'flex', flexDirection: 'column', background: '#fff'
            }}>
                <div style={{ padding: '12px 14px', borderBottom: '1px solid #e5e7eb', display: 'flex', alignItems: 'center', gap: 8 }}>
                    <input
                        value={searchQ}
                        onChange={e => { setSearchQ(e.target.value); setShowSearch(!!e.target.value); }}
                        placeholder="Поиск сообщений..."
                        style={{ flex: 1, padding: '6px 10px', border: '1px solid #d1d5db', borderRadius: 6, fontSize: 13 }}
                    />
                    <button
                        onClick={() => { setShowNewGroupDialog(true); loadUsers(); }}
                        title="Новый групповой чат"
                        style={{ background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 6, padding: '6px 10px', cursor: 'pointer', fontSize: 16 }}
                    >+</button>
                </div>

                {showSearch && searchResults.length > 0 ? (
                    <div style={{ flex: 1, overflowY: 'auto' }}>
                        <div style={{ padding: '8px 14px', fontSize: 11, color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                            Результаты поиска
                        </div>
                        {searchResults.map(r => (
                            <button key={r.messageId}
                                onClick={() => { setSelectedChatId(r.chatId); setShowSearch(false); setSearchQ(''); }}
                                style={{
                                    display: 'block', width: '100%', textAlign: 'left', padding: '10px 14px',
                                    background: 'none', border: 'none', cursor: 'pointer', borderBottom: '1px solid #f3f4f6'
                                }}
                            >
                                <div style={{ fontSize: 12, color: '#6b7280' }}>{r.chatName ?? 'Диалог'} · {r.authorName}</div>
                                <div style={{ fontSize: 13, marginTop: 2 }}>{r.textSnippet}</div>
                                <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 2 }}>{formatDate(r.createdAt)}</div>
                            </button>
                        ))}
                    </div>
                ) : (
                    <div style={{ flex: 1, overflowY: 'auto' }}>
                        {chats.length === 0 && (
                            <div style={{ padding: 20, color: '#9ca3af', fontSize: 13, textAlign: 'center' }}>
                                Нет чатов. Начните переписку, выбрав коллегу в адресной книге.
                            </div>
                        )}
                        {chats.map(chat => (
                            <button
                                key={chat.id}
                                onClick={() => setSelectedChatId(chat.id)}
                                style={{
                                    display: 'flex', alignItems: 'center', gap: 10, width: '100%', textAlign: 'left',
                                    padding: '10px 14px', border: 'none', cursor: 'pointer',
                                    background: selectedChatId === chat.id ? '#eff6ff' : 'none',
                                    borderBottom: '1px solid #f3f4f6'
                                }}
                            >
                                <div style={{
                                    width: 38, height: 38, borderRadius: '50%', background: '#e0e7ff',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    fontSize: 15, fontWeight: 600, color: '#3730a3', flexShrink: 0
                                }}>
                                    {chat.kind === 'Group' ? '👥' : (chat.name?.[0] ?? '?').toUpperCase()}
                                </div>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                        <span style={{ fontSize: 13, fontWeight: 600, color: '#111827', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                            {chat.name ?? 'Диалог'}
                                        </span>
                                        <span style={{ fontSize: 11, color: '#9ca3af', flexShrink: 0, marginLeft: 6 }}>
                                            {chat.lastMessageAt ? formatTime(chat.lastMessageAt) : ''}
                                        </span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 2 }}>
                                        <span style={{ fontSize: 12, color: '#6b7280', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                            {chat.lastMessage?.isDeleted ? 'Сообщение удалено' : (chat.lastMessage?.text ?? '')}
                                        </span>
                                        {chat.unreadCount > 0 && (
                                            <span style={{
                                                background: '#3b82f6', color: '#fff', borderRadius: 10, fontSize: 11,
                                                padding: '1px 6px', fontWeight: 700, flexShrink: 0, marginLeft: 6
                                            }}>{chat.unreadCount}</span>
                                        )}
                                    </div>
                                </div>
                            </button>
                        ))}
                    </div>
                )}
            </aside>

            {/* ─── Правая панель: окно чата ─── */}
            {selectedChatId && selectedChat ? (
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
                    {/* Шапка чата */}
                    <div style={{
                        padding: '10px 16px', borderBottom: '1px solid #e5e7eb', background: '#fff',
                        display: 'flex', alignItems: 'center', gap: 10
                    }}>
                        <div style={{ fontWeight: 600, fontSize: 15, color: '#111827' }}>
                            {selectedChat.kind === 'Group' ? '👥 ' : ''}{selectedChat.name ?? 'Диалог'}
                        </div>
                        {selectedChat.kind === 'Group' && (
                            <span style={{ fontSize: 12, color: '#6b7280' }}>
                                {selectedChat.members.length} участн.
                            </span>
                        )}
                    </div>

                    {/* Ответ на сообщение */}
                    {replyTo && (
                        <div style={{
                            padding: '6px 16px', background: '#f0f9ff', borderBottom: '1px solid #bae6fd',
                            display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: '#0369a1'
                        }}>
                            <span>↩ Ответ на: «{replyTo.text.slice(0, 60)}{replyTo.text.length > 60 ? '…' : ''}»</span>
                            <button onClick={() => setReplyTo(null)} style={{ marginLeft: 'auto', background: 'none', border: 'none', cursor: 'pointer', color: '#6b7280', fontSize: 16 }}>✕</button>
                        </div>
                    )}

                    {/* Редактирование сообщения */}
                    {editingMsg && (
                        <div style={{
                            padding: '6px 16px', background: '#fefce8', borderBottom: '1px solid #fde047',
                            display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: '#92400e'
                        }}>
                            <span>✏️ Редактирование: «{editingMsg.text.slice(0, 60)}»</span>
                            <button onClick={() => { setEditingMsg(null); setMsgInput(''); }} style={{ marginLeft: 'auto', background: 'none', border: 'none', cursor: 'pointer', color: '#6b7280', fontSize: 16 }}>✕</button>
                        </div>
                    )}

                    {/* Сообщения */}
                    <div style={{ flex: 1, overflowY: 'auto', padding: '16px', display: 'flex', flexDirection: 'column', gap: 2, background: '#f9fafb' }}>
                        {loading && <div style={{ textAlign: 'center', color: '#9ca3af', padding: 20 }}>Загрузка...</div>}
                        {messages.map((msg, idx) => {
                            const isOwn = msg.authorUserId === userId;
                            const showDate = idx === 0 || new Date(messages[idx - 1].createdAt).toDateString() !== new Date(msg.createdAt).toDateString();
                            return (
                                <div key={msg.id}>
                                    {showDate && (
                                        <div style={{ textAlign: 'center', margin: '12px 0 8px', fontSize: 11, color: '#9ca3af' }}>
                                            {formatDate(msg.createdAt)}
                                        </div>
                                    )}
                                    <div
                                        style={{
                                            display: 'flex', flexDirection: isOwn ? 'row-reverse' : 'row',
                                            gap: 8, marginBottom: 4, alignItems: 'flex-end',
                                        }}
                                    >
                                        {!isOwn && (
                                            <div style={{
                                                width: 28, height: 28, borderRadius: '50%', background: '#e0e7ff',
                                                display: 'flex', alignItems: 'center', justifyContent: 'center',
                                                fontSize: 12, fontWeight: 600, color: '#3730a3', flexShrink: 0
                                            }}>
                                                {msg.authorName?.[0]?.toUpperCase() ?? '?'}
                                            </div>
                                        )}
                                        <div style={{ maxWidth: '70%' }}>
                                            {!isOwn && (
                                                <div style={{ fontSize: 11, color: '#6b7280', marginBottom: 2 }}>
                                                    {msg.authorName}
                                                </div>
                                            )}
                                            {/* Цитата ответа */}
                                            {msg.replyToText && (
                                                <div style={{
                                                    padding: '4px 8px', background: '#e5e7eb', borderLeft: '3px solid #6b7280',
                                                    borderRadius: '4px 4px 0 0', fontSize: 12, color: '#4b5563',
                                                    maxWidth: '100%', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'
                                                }}>
                                                    {msg.replyToText}
                                                </div>
                                            )}
                                            <div
                                                style={{
                                                    padding: '8px 12px',
                                                    background: isOwn ? '#3b82f6' : '#fff',
                                                    color: isOwn ? '#fff' : '#111827',
                                                    borderRadius: msg.replyToText
                                                        ? (isOwn ? '8px 0 8px 8px' : '0 8px 8px 8px')
                                                        : (isOwn ? '12px 12px 0 12px' : '12px 12px 12px 0'),
                                                    fontSize: 14, lineHeight: 1.5,
                                                    boxShadow: '0 1px 2px rgba(0,0,0,0.08)',
                                                    opacity: msg.isDeleted ? 0.5 : 1,
                                                    fontStyle: msg.isDeleted ? 'italic' : 'normal',
                                                }}
                                            >
                                                {msg.text}
                                                {msg.isEdited && !msg.isDeleted && (
                                                    <span style={{ fontSize: 10, opacity: 0.7, marginLeft: 6 }}>изменено</span>
                                                )}
                                            </div>
                                            {/* Реакции */}
                                            {msg.reactions.length > 0 && (
                                                <div style={{ display: 'flex', gap: 4, marginTop: 4, flexWrap: 'wrap' }}>
                                                    {msg.reactions.map(r => (
                                                        <button
                                                            key={r.emoji}
                                                            onClick={() => handleReact(msg.id, r.emoji)}
                                                            style={{
                                                                background: r.myReaction ? '#dbeafe' : '#f3f4f6',
                                                                border: r.myReaction ? '1px solid #93c5fd' : '1px solid #e5e7eb',
                                                                borderRadius: 12, padding: '2px 8px', cursor: 'pointer', fontSize: 12,
                                                                color: '#374151'
                                                            }}
                                                        >
                                                            {r.emoji} {r.count}
                                                        </button>
                                                    ))}
                                                </div>
                                            )}
                                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: isOwn ? 'flex-end' : 'flex-start', gap: 6, marginTop: 2 }}>
                                                <span style={{ fontSize: 10, color: '#9ca3af' }}>{formatTime(msg.createdAt)}</span>
                                                {!msg.isDeleted && (
                                                    <>
                                                        <button onClick={() => handleReact(msg.id, '👍')} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 12, color: '#9ca3af' }} title="Реакция">👍</button>
                                                        <button onClick={() => setReplyTo(msg)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: '#9ca3af' }} title="Ответить">↩</button>
                                                        {isOwn && (
                                                            <>
                                                                <button onClick={() => { setEditingMsg(msg); setMsgInput(msg.text); }} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: '#9ca3af' }} title="Редактировать">✏️</button>
                                                                <button onClick={() => handleDeleteMessage(msg.id)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: '#9ca3af' }} title="Удалить">🗑️</button>
                                                            </>
                                                        )}
                                                    </>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                        <div ref={messagesEndRef} />
                    </div>

                    {/* Поле ввода */}
                    <div style={{
                        padding: '10px 16px', borderTop: '1px solid #e5e7eb', background: '#fff',
                        display: 'flex', gap: 8, alignItems: 'flex-end'
                    }}>
                        <textarea
                            value={msgInput}
                            onChange={e => setMsgInput(e.target.value)}
                            onKeyDown={e => {
                                if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); }
                            }}
                            placeholder="Введите сообщение... (Enter — отправить, Shift+Enter — новая строка)"
                            rows={1}
                            style={{
                                flex: 1, padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8,
                                resize: 'none', fontSize: 14, lineHeight: 1.5, maxHeight: 120, overflowY: 'auto',
                                fontFamily: 'inherit'
                            }}
                        />
                        <button
                            onClick={handleSend}
                            disabled={!msgInput.trim() || sendingMsg}
                            style={{
                                background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8,
                                padding: '8px 16px', cursor: 'pointer', fontSize: 14, fontWeight: 600,
                                opacity: !msgInput.trim() || sendingMsg ? 0.5 : 1
                            }}
                        >
                            {editingMsg ? 'Сохранить' : '→'}
                        </button>
                    </div>
                </div>
            ) : (
                <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#9ca3af', fontSize: 15 }}>
                    Выберите чат или начните новый диалог
                </div>
            )}

            {/* ─── Диалог создания группового чата ─── */}
            {showNewGroupDialog && (
                <div style={{
                    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000,
                    display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                    <div style={{ background: '#fff', borderRadius: 12, padding: 24, width: 400, maxHeight: '70vh', display: 'flex', flexDirection: 'column', gap: 12 }}>
                        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>Новый групповой чат</h3>
                        <input
                            value={newGroupName}
                            onChange={e => setNewGroupName(e.target.value)}
                            placeholder="Название чата"
                            style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14 }}
                        />
                        <div style={{ fontSize: 13, color: '#374151', fontWeight: 500 }}>Выберите участников:</div>
                        <div style={{ overflowY: 'auto', border: '1px solid #e5e7eb', borderRadius: 8, flex: 1 }}>
                            {allUsers.map(u => (
                                <label key={u.userId} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f3f4f6' }}>
                                    <input
                                        type="checkbox"
                                        checked={selectedMemberIds.includes(u.userId)}
                                        onChange={e => {
                                            if (e.target.checked) setSelectedMemberIds(prev => [...prev, u.userId]);
                                            else setSelectedMemberIds(prev => prev.filter(id => id !== u.userId));
                                        }}
                                    />
                                    <span style={{ fontSize: 13 }}>{u.displayName}</span>
                                    <span style={{ fontSize: 11, color: '#6b7280' }}>{u.workEmail}</span>
                                </label>
                            ))}
                            {allUsers.length === 0 && <div style={{ padding: 12, color: '#9ca3af', fontSize: 13 }}>Загрузка...</div>}
                        </div>
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                            <button onClick={() => setShowNewGroupDialog(false)} style={{ padding: '8px 16px', border: '1px solid #d1d5db', borderRadius: 8, background: '#fff', cursor: 'pointer', fontSize: 14 }}>Отмена</button>
                            <button onClick={handleCreateGroup} disabled={!newGroupName.trim()} style={{ padding: '8px 16px', background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 14, fontWeight: 600 }}>Создать</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
