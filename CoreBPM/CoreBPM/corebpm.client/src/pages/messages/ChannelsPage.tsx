import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getChannels, createChannel, updateChannel, deleteChannel,
    subscribeChannel, unsubscribeChannel,
    getChannelPosts, createChannelPost, editChannelPost, deleteChannelPost,
    togglePostReaction, getPostComments, addPostComment, deletePostComment,
    getChannelSubscribers,
    type ChannelSummaryDto, type ChannelPostDto, type PostCommentDto, type ChannelSubscriberDto,
} from '../../api/messagesApi';

/** Страница информационных каналов (FR-MSG-01.2). */
export function ChannelsPage() {
    const { accessToken } = useAuth();

    const [channels, setChannels] = useState<ChannelSummaryDto[]>([]);
    const [selectedChannelId, setSelectedChannelId] = useState<string | null>(null);
    const [posts, setPosts] = useState<ChannelPostDto[]>([]);
    const [showNewChannel, setShowNewChannel] = useState(false);
    const [showNewPost, setShowNewPost] = useState(false);
    const [newChannelName, setNewChannelName] = useState('');
    const [newChannelDesc, setNewChannelDesc] = useState('');
    const [newChannelKind, setNewChannelKind] = useState<'Public' | 'Private'>('Public');
    const [newChannelIcon, setNewChannelIcon] = useState('📢');
    const [newPostTitle, setNewPostTitle] = useState('');
    const [newPostBody, setNewPostBody] = useState('');
    const [editingPost, setEditingPost] = useState<ChannelPostDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [editingChannel, setEditingChannel] = useState<ChannelSummaryDto | null>(null);
    const [editChannelName, setEditChannelName] = useState('');
    const [editChannelDesc, setEditChannelDesc] = useState('');
    const [editChannelIcon, setEditChannelIcon] = useState('');

    // Поиск по публикациям
    const [searchQuery, setSearchQuery] = useState('');

    // Комментарии
    const [expandedPostComments, setExpandedPostComments] = useState<Record<string, PostCommentDto[]>>({});
    const [loadingComments, setLoadingComments] = useState<Record<string, boolean>>({});
    const [newCommentText, setNewCommentText] = useState<Record<string, string>>({});

    // Подписчики
    const [showSubscribers, setShowSubscribers] = useState(false);
    const [subscribers, setSubscribers] = useState<ChannelSubscriberDto[]>([]);

    const selectedChannel = channels.find(c => c.id === selectedChannelId);

    const loadChannels = useCallback(async () => {
        if (!accessToken) return;
        try {
            const data = await getChannels(accessToken);
            setChannels(data);
        } catch { /* игнорируем */ }
    }, [accessToken]);

    useEffect(() => { loadChannels(); }, [loadChannels]);

    useEffect(() => {
        if (!selectedChannelId || !accessToken) return;
        setLoading(true);
        getChannelPosts(accessToken, selectedChannelId, 30, undefined, searchQuery || undefined)
            .then(data => { setPosts(data); setLoading(false); })
            .catch(() => setLoading(false));
    }, [selectedChannelId, accessToken, searchQuery]);

    const handleSubscribeToggle = async (channelId: string, isSubscribed: boolean) => {
        if (!accessToken) return;
        try {
            if (isSubscribed) await unsubscribeChannel(accessToken, channelId);
            else await subscribeChannel(accessToken, channelId);
            await loadChannels();
        } catch { /* ошибка */ }
    };

    const handleCreateChannel = async () => {
        if (!newChannelName.trim() || !accessToken) return;
        try {
            await createChannel(accessToken, newChannelName.trim(), newChannelDesc.trim() || null, newChannelIcon, newChannelKind);
            await loadChannels();
            setShowNewChannel(false);
            setNewChannelName('');
            setNewChannelDesc('');
        } catch { /* ошибка */ }
    };

    const handleUpdateChannel = async () => {
        if (!editingChannel || !editChannelName.trim() || !accessToken) return;
        try {
            const updated = await updateChannel(
                accessToken,
                editingChannel.id,
                editChannelName.trim(),
                editChannelDesc.trim() || null,
                editChannelIcon || null,
            );
            setChannels(prev => prev.map(c => c.id === updated.id ? { ...c, ...updated } : c));
            setEditingChannel(null);
        } catch { /* ошибка */ }
    };

    const handleDeleteChannel = async (channelId: string) => {
        if (!accessToken) return;
        if (!window.confirm('Удалить канал? Это действие необратимо.')) return;
        try {
            await deleteChannel(accessToken, channelId);
            setChannels(prev => prev.filter(c => c.id !== channelId));
            if (selectedChannelId === channelId) setSelectedChannelId(null);
        } catch (e: unknown) {
            const msg = e instanceof Error ? e.message : 'Ошибка';
            alert(msg);
        }
    };

    const handleCreatePost = async () => {
        if (!newPostBody.trim() || !selectedChannelId || !accessToken) return;
        try {
            if (editingPost) {
                const updated = await editChannelPost(accessToken, selectedChannelId, editingPost.id, newPostTitle.trim() || null, newPostBody.trim());
                setPosts(prev => prev.map(p => p.id === updated.id ? updated : p));
                setEditingPost(null);
            } else {
                const post = await createChannelPost(accessToken, selectedChannelId, newPostTitle.trim() || null, newPostBody.trim());
                setPosts(prev => [...prev, post]);
            }
            setNewPostTitle('');
            setNewPostBody('');
            setShowNewPost(false);
        } catch { /* ошибка */ }
    };

    const handleDeletePost = async (postId: string) => {
        if (!selectedChannelId || !accessToken) return;
        if (!confirm('Удалить публикацию?')) return;
        try {
            await deleteChannelPost(accessToken, selectedChannelId, postId);
            setPosts(prev => prev.filter(p => p.id !== postId));
        } catch { /* ошибка */ }
    };

    const handleToggleReaction = async (post: ChannelPostDto, emoji: string) => {
        if (!selectedChannelId || !accessToken) return;
        try {
            const reactions = await togglePostReaction(accessToken, selectedChannelId, post.id, emoji);
            setPosts(prev => prev.map(p => p.id === post.id ? { ...p, reactions } : p));
        } catch { /* ошибка */ }
    };

    const handleLoadComments = async (postId: string) => {
        if (!selectedChannelId || !accessToken) return;
        if (expandedPostComments[postId]) {
            setExpandedPostComments(prev => { const next = { ...prev }; delete next[postId]; return next; });
            return;
        }
        setLoadingComments(prev => ({ ...prev, [postId]: true }));
        try {
            const comments = await getPostComments(accessToken, selectedChannelId, postId);
            setExpandedPostComments(prev => ({ ...prev, [postId]: comments }));
        } catch { /* ошибка */ }
        setLoadingComments(prev => ({ ...prev, [postId]: false }));
    };

    const handleAddComment = async (postId: string) => {
        if (!selectedChannelId || !accessToken) return;
        const text = newCommentText[postId]?.trim();
        if (!text) return;
        try {
            const comment = await addPostComment(accessToken, selectedChannelId, postId, text);
            setExpandedPostComments(prev => ({ ...prev, [postId]: [...(prev[postId] ?? []), comment] }));
            setPosts(prev => prev.map(p => p.id === postId ? { ...p, commentCount: p.commentCount + 1 } : p));
            setNewCommentText(prev => ({ ...prev, [postId]: '' }));
        } catch { /* ошибка */ }
    };

    const handleDeleteComment = async (postId: string, commentId: string) => {
        if (!selectedChannelId || !accessToken) return;
        try {
            await deletePostComment(accessToken, selectedChannelId, postId, commentId);
            setExpandedPostComments(prev => ({
                ...prev,
                [postId]: (prev[postId] ?? []).map(c => c.id === commentId ? { ...c, isDeleted: true, text: 'Комментарий удалён' } : c),
            }));
            setPosts(prev => prev.map(p => p.id === postId ? { ...p, commentCount: Math.max(0, p.commentCount - 1) } : p));
        } catch { /* ошибка */ }
    };

    const handleShowSubscribers = async () => {
        if (!selectedChannelId || !accessToken) return;
        try {
            const subs = await getChannelSubscribers(accessToken, selectedChannelId);
            setSubscribers(subs);
            setShowSubscribers(true);
        } catch { /* ошибка */ }
    };

    const formatDate = (iso: string) => {
        const d = new Date(iso);
        return d.toLocaleDateString('ru', { day: 'numeric', month: 'long', year: 'numeric' });
    };

    return (
        <div style={{ display: 'flex', height: '100%', minHeight: 0, overflow: 'hidden' }}>
            {/* ─── Список каналов ─── */}
            <aside style={{
                width: 260, minWidth: 220, borderRight: '1px solid #e5e7eb',
                display: 'flex', flexDirection: 'column', background: '#fff'
            }}>
                <div style={{ padding: '12px 14px', borderBottom: '1px solid #e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <span style={{ fontWeight: 700, fontSize: 14, color: '#111827' }}>Каналы</span>
                    <button
                        onClick={() => setShowNewChannel(true)}
                        title="Создать канал"
                        style={{ background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 6, padding: '4px 10px', cursor: 'pointer', fontSize: 14 }}
                    >+ Канал</button>
                </div>
                <div style={{ flex: 1, overflowY: 'auto' }}>
                    {channels.length === 0 && (
                        <div style={{ padding: 16, color: '#9ca3af', fontSize: 13, textAlign: 'center' }}>
                            Нет доступных каналов.
                        </div>
                    )}
                    {channels.map(ch => (
                        <div
                            key={ch.id}
                            style={{
                                padding: '10px 14px', borderBottom: '1px solid #f3f4f6',
                                background: selectedChannelId === ch.id ? '#eff6ff' : 'none',
                                cursor: 'pointer',
                            }}
                            onClick={() => setSelectedChannelId(ch.id)}
                        >
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                <span style={{ fontSize: 20 }}>{ch.iconEmoji ?? '📢'}</span>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <div style={{ fontSize: 13, fontWeight: 600, color: '#111827', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {ch.name}
                                    </div>
                                    <div style={{ fontSize: 11, color: '#6b7280' }}>
                                        {ch.subscriberCount} подписч. · {ch.kind === 'Public' ? 'Публичный' : 'Приватный'}
                                    </div>
                                </div>
                                <button
                                    onClick={e => { e.stopPropagation(); handleSubscribeToggle(ch.id, ch.isSubscribed); }}
                                    style={{
                                        fontSize: 11, padding: '3px 8px', borderRadius: 6, cursor: 'pointer', border: 'none',
                                        background: ch.isSubscribed ? '#fee2e2' : '#d1fae5', color: ch.isSubscribed ? '#b91c1c' : '#065f46'
                                    }}
                                >
                                    {ch.isSubscribed ? 'Отписаться' : 'Подписаться'}
                                </button>
                                {ch.isAdmin && (
                                    <>
                                        <button
                                            onClick={e => { e.stopPropagation(); setEditingChannel(ch); setEditChannelName(ch.name); setEditChannelDesc(ch.description ?? ''); setEditChannelIcon(ch.iconEmoji ?? '📢'); }}
                                            title="Редактировать канал"
                                            style={{ fontSize: 11, padding: '3px 6px', borderRadius: 6, cursor: 'pointer', border: '1px solid #e5e7eb', background: '#fff', color: '#374151' }}
                                        >✏️</button>
                                        <button
                                            onClick={e => { e.stopPropagation(); handleDeleteChannel(ch.id); }}
                                            title="Удалить канал"
                                            style={{ fontSize: 11, padding: '3px 6px', borderRadius: 6, cursor: 'pointer', border: '1px solid #fca5a5', background: '#fff', color: '#dc2626' }}
                                        >🗑️</button>
                                    </>
                                )}
                            </div>
                        </div>
                    ))}
                </div>
            </aside>

            {/* ─── Содержимое канала ─── */}
            {selectedChannelId && selectedChannel ? (
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
                    {/* Шапка */}
                    <div style={{
                        padding: '12px 20px', borderBottom: '1px solid #e5e7eb', background: '#fff',
                        display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap'
                    }}>
                        <span style={{ fontSize: 22 }}>{selectedChannel.iconEmoji ?? '📢'}</span>
                        <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontWeight: 700, fontSize: 16, color: '#111827' }}>{selectedChannel.name}</div>
                            {selectedChannel.description && (
                                <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>{selectedChannel.description}</div>
                            )}
                        </div>
                        {/* Поиск по публикациям */}
                        <input
                            value={searchQuery}
                            onChange={e => setSearchQuery(e.target.value)}
                            placeholder="Поиск по публикациям..."
                            style={{ padding: '6px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 13, width: 200 }}
                        />
                        {/* Кнопка подписчиков */}
                        <button
                            onClick={handleShowSubscribers}
                            title="Подписчики"
                            style={{ background: 'none', border: '1px solid #e5e7eb', borderRadius: 8, padding: '6px 12px', cursor: 'pointer', fontSize: 13, color: '#374151' }}
                        >👥 {selectedChannel.subscriberCount}</button>
                        {selectedChannel.isAdmin && (
                            <button
                                onClick={() => { setShowNewPost(true); setEditingPost(null); setNewPostTitle(''); setNewPostBody(''); }}
                                style={{ background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, padding: '8px 16px', cursor: 'pointer', fontSize: 14, fontWeight: 600 }}
                            >+ Публикация</button>
                        )}
                    </div>

                    {/* Публикации */}
                    <div style={{ flex: 1, overflowY: 'auto', padding: '20px', display: 'flex', flexDirection: 'column', gap: 16, background: '#f9fafb' }}>
                        {loading && <div style={{ textAlign: 'center', color: '#9ca3af' }}>Загрузка...</div>}
                        {posts.length === 0 && !loading && (
                            <div style={{ textAlign: 'center', color: '#9ca3af', fontSize: 14, marginTop: 40 }}>
                                {searchQuery ? 'Публикаций по запросу не найдено.' : 'Публикаций пока нет.'}
                            </div>
                        )}
                        {posts.map(post => (
                            <div key={post.id} style={{
                                background: '#fff', borderRadius: 10, padding: '16px 20px',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.08)', border: '1px solid #e5e7eb'
                            }}>
                                {post.title && (
                                    <h3 style={{ margin: '0 0 10px', fontSize: 17, fontWeight: 700, color: '#111827' }}>
                                        {post.title}
                                    </h3>
                                )}
                                <div style={{ fontSize: 14, lineHeight: 1.6, color: '#374151', whiteSpace: 'pre-wrap' }}>
                                    {post.body}
                                </div>

                                {/* Мета + кнопки действий */}
                                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 12, paddingTop: 10, borderTop: '1px solid #f3f4f6' }}>
                                    <div style={{ width: 28, height: 28, borderRadius: '50%', background: '#e0e7ff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 600, color: '#3730a3' }}>
                                        {post.authorName?.[0]?.toUpperCase() ?? '?'}
                                    </div>
                                    <div>
                                        <div style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>{post.authorName}</div>
                                        <div style={{ fontSize: 11, color: '#9ca3af' }}>
                                            {formatDate(post.createdAt)}
                                            {post.isEdited && ' (изменено)'}
                                        </div>
                                    </div>
                                    {selectedChannel.isAdmin && (
                                        <div style={{ marginLeft: 'auto', display: 'flex', gap: 6 }}>
                                            <button
                                                onClick={() => { setEditingPost(post); setNewPostTitle(post.title ?? ''); setNewPostBody(post.body); setShowNewPost(true); }}
                                                style={{ background: 'none', border: '1px solid #d1d5db', borderRadius: 6, padding: '3px 10px', cursor: 'pointer', fontSize: 12, color: '#374151' }}
                                            >✏️ Изменить</button>
                                            <button
                                                onClick={() => handleDeletePost(post.id)}
                                                style={{ background: 'none', border: '1px solid #fca5a5', borderRadius: 6, padding: '3px 10px', cursor: 'pointer', fontSize: 12, color: '#dc2626' }}
                                            >🗑️ Удалить</button>
                                        </div>
                                    )}
                                </div>

                                {/* Реакции */}
                                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 10 }}>
                                    {(post.reactions ?? []).map(r => (
                                        <button
                                            key={r.emoji}
                                            onClick={() => handleToggleReaction(post, r.emoji)}
                                            style={{
                                                padding: '3px 8px', borderRadius: 12, border: `1px solid ${r.myReaction ? '#3b82f6' : '#e5e7eb'}`,
                                                background: r.myReaction ? '#eff6ff' : '#fff', cursor: 'pointer', fontSize: 13,
                                                color: r.myReaction ? '#1d4ed8' : '#374151'
                                            }}
                                        >{r.emoji} {r.count}</button>
                                    ))}
                                    {/* Добавить реакцию */}
                                    {['👍','❤️','😂','🎉','🔥'].map(emoji => {
                                        const existing = (post.reactions ?? []).find(r => r.emoji === emoji);
                                        if (existing) return null;
                                        return (
                                            <button
                                                key={emoji}
                                                onClick={() => handleToggleReaction(post, emoji)}
                                                title={`Реакция ${emoji}`}
                                                style={{ padding: '3px 8px', borderRadius: 12, border: '1px dashed #d1d5db', background: '#fff', cursor: 'pointer', fontSize: 13, opacity: 0.5 }}
                                            >{emoji}</button>
                                        );
                                    })}
                                </div>

                                {/* Комментарии */}
                                <div style={{ marginTop: 10 }}>
                                    <button
                                        onClick={() => handleLoadComments(post.id)}
                                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b7280', fontSize: 12, padding: 0 }}
                                    >
                                        💬 {expandedPostComments[post.id] ? 'Скрыть' : `Комментарии${post.commentCount > 0 ? ` (${post.commentCount})` : ''}`}
                                        {loadingComments[post.id] && ' ...'}
                                    </button>
                                    {expandedPostComments[post.id] && (
                                        <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 6 }}>
                                            {expandedPostComments[post.id].map(c => (
                                                <div key={c.id} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                                                    <div style={{ width: 24, height: 24, borderRadius: '50%', background: '#f3f4f6', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 600, color: '#6b7280', flexShrink: 0 }}>
                                                        {c.authorName?.[0]?.toUpperCase() ?? '?'}
                                                    </div>
                                                    <div style={{ flex: 1, background: '#f9fafb', borderRadius: 8, padding: '6px 10px' }}>
                                                        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                                                            <span style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>{c.authorName}</span>
                                                            <span style={{ fontSize: 11, color: '#9ca3af' }}>{new Date(c.createdAt).toLocaleString('ru')}</span>
                                                            {!c.isDeleted && (
                                                                <button
                                                                    onClick={() => handleDeleteComment(post.id, c.id)}
                                                                    style={{ marginLeft: 'auto', background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: '#ef4444' }}
                                                                    title="Удалить"
                                                                >✕</button>
                                                            )}
                                                        </div>
                                                        <div style={{ fontSize: 13, color: c.isDeleted ? '#9ca3af' : '#374151', fontStyle: c.isDeleted ? 'italic' : 'normal', marginTop: 2 }}>{c.text}</div>
                                                    </div>
                                                </div>
                                            ))}
                                            {/* Поле ввода комментария */}
                                            <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
                                                <input
                                                    value={newCommentText[post.id] ?? ''}
                                                    onChange={e => setNewCommentText(prev => ({ ...prev, [post.id]: e.target.value }))}
                                                    onKeyDown={e => e.key === 'Enter' && handleAddComment(post.id)}
                                                    placeholder="Написать комментарий..."
                                                    style={{ flex: 1, padding: '6px 10px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 13 }}
                                                />
                                                <button
                                                    onClick={() => handleAddComment(post.id)}
                                                    disabled={!(newCommentText[post.id]?.trim())}
                                                    style={{ padding: '6px 14px', background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 13 }}
                                                >Отправить</button>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            ) : (
                <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#9ca3af', fontSize: 15 }}>
                    Выберите канал
                </div>
            )}

            {/* ─── Диалог создания канала ─── */}
            {showNewChannel && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#fff', borderRadius: 12, padding: 24, width: 380, display: 'flex', flexDirection: 'column', gap: 12 }}>
                        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>Новый канал</h3>
                        <div style={{ display: 'flex', gap: 8 }}>
                            <input
                                value={newChannelIcon}
                                onChange={e => setNewChannelIcon(e.target.value)}
                                style={{ width: 48, padding: '8px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 18, textAlign: 'center' }}
                                placeholder="📢"
                            />
                            <input
                                value={newChannelName}
                                onChange={e => setNewChannelName(e.target.value)}
                                placeholder="Название канала *"
                                style={{ flex: 1, padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14 }}
                            />
                        </div>
                        <textarea
                            value={newChannelDesc}
                            onChange={e => setNewChannelDesc(e.target.value)}
                            placeholder="Описание (необязательно)"
                            rows={2}
                            style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14, resize: 'none', fontFamily: 'inherit' }}
                        />
                        <div style={{ display: 'flex', gap: 12 }}>
                            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: 14 }}>
                                <input type="radio" name="kind" checked={newChannelKind === 'Public'} onChange={() => setNewChannelKind('Public')} />
                                🌐 Публичный
                            </label>
                            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: 14 }}>
                                <input type="radio" name="kind" checked={newChannelKind === 'Private'} onChange={() => setNewChannelKind('Private')} />
                                🔒 Приватный
                            </label>
                        </div>
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                            <button onClick={() => setShowNewChannel(false)} style={{ padding: '8px 16px', border: '1px solid #d1d5db', borderRadius: 8, background: '#fff', cursor: 'pointer', fontSize: 14 }}>Отмена</button>
                            <button onClick={handleCreateChannel} disabled={!newChannelName.trim()} style={{ padding: '8px 16px', background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 14, fontWeight: 600 }}>Создать</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── Диалог создания/редактирования публикации ─── */}
            {showNewPost && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#fff', borderRadius: 12, padding: 24, width: 500, display: 'flex', flexDirection: 'column', gap: 12 }}>
                        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>
                            {editingPost ? 'Редактировать публикацию' : 'Новая публикация'}
                        </h3>
                        <input
                            value={newPostTitle}
                            onChange={e => setNewPostTitle(e.target.value)}
                            placeholder="Заголовок (необязательно)"
                            style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 15, fontWeight: 600 }}
                        />
                        <textarea
                            value={newPostBody}
                            onChange={e => setNewPostBody(e.target.value)}
                            placeholder="Текст публикации *"
                            rows={6}
                            style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14, resize: 'vertical', fontFamily: 'inherit' }}
                        />
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                            <button onClick={() => setShowNewPost(false)} style={{ padding: '8px 16px', border: '1px solid #d1d5db', borderRadius: 8, background: '#fff', cursor: 'pointer', fontSize: 14 }}>Отмена</button>
                            <button onClick={handleCreatePost} disabled={!newPostBody.trim()} style={{ padding: '8px 16px', background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 14, fontWeight: 600 }}>
                                {editingPost ? 'Сохранить' : 'Опубликовать'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── Диалог редактирования канала ─── */}
            {editingChannel && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#fff', borderRadius: 12, padding: 24, width: 420, display: 'flex', flexDirection: 'column', gap: 12 }}>
                        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>Редактировать канал</h3>
                        <div style={{ display: 'flex', gap: 8 }}>
                            <input
                                value={editChannelIcon}
                                onChange={e => setEditChannelIcon(e.target.value)}
                                style={{ width: 48, padding: '8px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 22, textAlign: 'center' }}
                            />
                            <input
                                value={editChannelName}
                                onChange={e => setEditChannelName(e.target.value)}
                                placeholder="Название канала *"
                                style={{ flex: 1, padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14 }}
                            />
                        </div>
                        <textarea
                            value={editChannelDesc}
                            onChange={e => setEditChannelDesc(e.target.value)}
                            placeholder="Описание (необязательно)"
                            rows={2}
                            style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: 8, fontSize: 14, resize: 'none', fontFamily: 'inherit' }}
                        />
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                            <button onClick={() => setEditingChannel(null)} style={{ padding: '8px 16px', border: '1px solid #d1d5db', borderRadius: 8, background: '#fff', cursor: 'pointer', fontSize: 14 }}>Отмена</button>
                            <button onClick={handleUpdateChannel} disabled={!editChannelName.trim()} style={{ padding: '8px 16px', background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 14, fontWeight: 600 }}>Сохранить</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── Диалог подписчиков канала ─── */}
            {showSubscribers && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#fff', borderRadius: 12, padding: 24, width: 420, maxHeight: '70vh', display: 'flex', flexDirection: 'column', gap: 12 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>Подписчики канала</h3>
                            <button onClick={() => setShowSubscribers(false)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 20, color: '#6b7280' }}>×</button>
                        </div>
                        <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
                            {subscribers.length === 0 && (
                                <div style={{ textAlign: 'center', color: '#9ca3af', fontSize: 14, padding: 16 }}>Нет подписчиков</div>
                            )}
                            {subscribers.map(s => (
                                <div key={s.userId} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 4px', borderBottom: '1px solid #f3f4f6' }}>
                                    <div style={{ width: 32, height: 32, borderRadius: '50%', background: '#e0e7ff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 600, color: '#3730a3' }}>
                                        {s.displayName?.[0]?.toUpperCase() ?? '?'}
                                    </div>
                                    <div style={{ flex: 1 }}>
                                        <div style={{ fontSize: 14, fontWeight: 600, color: '#111827' }}>{s.displayName}</div>
                                        <div style={{ fontSize: 11, color: '#9ca3af' }}>с {new Date(s.subscribedAt).toLocaleDateString('ru')}</div>
                                    </div>
                                    {s.isAdmin && (
                                        <span style={{ fontSize: 11, background: '#dbeafe', color: '#1d4ed8', borderRadius: 6, padding: '2px 8px', fontWeight: 600 }}>Администратор</span>
                                    )}
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
