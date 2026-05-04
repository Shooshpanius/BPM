import { useState, useEffect, useCallback } from 'react';
import {
    getNotifications,
    markNotificationRead,
    markAllNotificationsRead,
    deleteNotification,
    type InboxEntryDto,
} from '../api/notificationsApi';
import { useBpmNotifications } from '../context/BpmNotificationsContext';
import { usePushNotifications } from '../hooks/usePushNotifications';

// ─── Типы фильтра ──────────────────────────────────────────────────────────────

type FilterTab = 'all' | 'unread' | 'read';

const TYPE_LABELS: Record<string, string> = {
    TaskAssigned: 'Назначена задача',
    TaskDone: 'Задача выполнена',
    TaskReminder: 'Напоминание',
    ChannelInvite: 'Приглашение в канал',
    NewChannelPost: 'Публикация в канале',
    ImprovementStatusChanged: 'Предложение по процессу',
    MigrationPackageCompleted: 'Миграция завершена',
    JobFailed: 'Ошибка задания',
};

const PAGE_SIZE = 20;

// ─── Компонент ────────────────────────────────────────────────────────────────

export default function NotificationsPage() {
    const { markAllRead: ctxMarkAllRead } = useBpmNotifications();
    const { permission, subscribed, loading: pushLoading, isSupported, subscribe, unsubscribe } = usePushNotifications();

    const [tab, setTab] = useState<FilterTab>('all');
    const [typeFilter, setTypeFilter] = useState('');
    const [items, setItems] = useState<InboxEntryDto[]>([]);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [loading, setLoading] = useState(false);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const read = tab === 'unread' ? false : tab === 'read' ? true : undefined;
            const res = await getNotifications({
                read,
                type: typeFilter || undefined,
                page,
                pageSize: PAGE_SIZE,
            });
            setItems(res.items);
            setTotal(res.total);
        } finally {
            setLoading(false);
        }
    }, [tab, typeFilter, page]);

    useEffect(() => { void load(); }, [load]);

    const handleMarkRead = async (id: string) => {
        await markNotificationRead(id);
        setItems(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n));
    };

    const handleMarkAllRead = async () => {
        await markAllNotificationsRead();
        ctxMarkAllRead();
        setItems(prev => prev.map(n => ({ ...n, isRead: true })));
    };

    const handleDelete = async (id: string) => {
        await deleteNotification(id);
        setItems(prev => prev.filter(n => n.id !== id));
        setTotal(t => Math.max(0, t - 1));
    };

    const handleClick = (n: InboxEntryDto) => {
        if (!n.isRead) void markNotificationRead(n.id);
        // Навигация через window.location если есть ссылка
        if (n.link) window.location.hash = n.link;
    };

    const totalPages = Math.ceil(total / PAGE_SIZE);

    return (
        <div style={{ maxWidth: 720, margin: '0 auto', padding: '24px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20 }}>
                <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700 }}>🔔 Уведомления</h1>
                <button
                    onClick={handleMarkAllRead}
                    style={{
                        padding: '6px 14px', borderRadius: 6, border: '1px solid #d1d5db',
                        background: 'white', cursor: 'pointer', fontSize: 13,
                    }}
                >
                    Прочитать все
                </button>
            </div>

            {/* Web Push подписка (FR-MSG-02.1) */}
            {isSupported && (
                <div style={{
                    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                    padding: '12px 16px', borderRadius: 8, marginBottom: 16,
                    background: subscribed ? '#f0fdf4' : '#eff6ff',
                    border: `1px solid ${subscribed ? '#bbf7d0' : '#bfdbfe'}`,
                }}>
                    <div>
                        <div style={{ fontWeight: 600, fontSize: 14, color: subscribed ? '#166534' : '#1d4ed8' }}>
                            {subscribed ? '🔔 Push-уведомления включены' : '🔕 Push-уведомления отключены'}
                        </div>
                        <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>
                            {subscribed
                                ? 'Вы будете получать уведомления даже при закрытой вкладке'
                                : 'Включите, чтобы получать уведомления когда вкладка закрыта'}
                        </div>
                    </div>
                    {permission === 'denied' ? (
                        <span style={{ fontSize: 12, color: '#dc2626' }}>Браузер заблокировал уведомления</span>
                    ) : (
                        <button
                            onClick={subscribed ? unsubscribe : subscribe}
                            disabled={pushLoading}
                            style={{
                                padding: '7px 14px', borderRadius: 6, border: 'none', cursor: 'pointer',
                                fontSize: 13, fontWeight: 500,
                                background: subscribed ? '#fee2e2' : '#3b82f6',
                                color: subscribed ? '#dc2626' : '#fff',
                            }}
                        >
                            {pushLoading ? '...' : subscribed ? 'Отписаться' : 'Подписаться'}
                        </button>
                    )}
                </div>
            )}

            {/* Фильтры */}
            <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
                {(['all', 'unread', 'read'] as FilterTab[]).map(t => (
                    <button
                        key={t}
                        onClick={() => { setTab(t); setPage(1); }}
                        style={{
                            padding: '5px 12px', borderRadius: 6, fontSize: 13, cursor: 'pointer',
                            background: tab === t ? '#3b82f6' : 'white',
                            color: tab === t ? 'white' : '#374151',
                            border: '1px solid ' + (tab === t ? '#3b82f6' : '#d1d5db'),
                        }}
                    >
                        {t === 'all' ? 'Все' : t === 'unread' ? 'Непрочитанные' : 'Прочитанные'}
                    </button>
                ))}
                <select
                    value={typeFilter}
                    onChange={e => { setTypeFilter(e.target.value); setPage(1); }}
                    style={{
                        padding: '5px 10px', borderRadius: 6, fontSize: 13,
                        border: '1px solid #d1d5db', background: 'white', cursor: 'pointer',
                    }}
                >
                    <option value="">Все типы</option>
                    {Object.entries(TYPE_LABELS).map(([v, l]) => (
                        <option key={v} value={v}>{l}</option>
                    ))}
                </select>
            </div>

            {/* Список */}
            {loading ? (
                <p style={{ color: '#6b7280', textAlign: 'center', padding: 32 }}>Загрузка…</p>
            ) : items.length === 0 ? (
                <div style={{ textAlign: 'center', padding: 48, color: '#9ca3af' }}>
                    <div style={{ fontSize: 40, marginBottom: 8 }}>🔕</div>
                    <p>Уведомлений нет</p>
                </div>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    {items.map(n => (
                        <div
                            key={n.id}
                            onClick={() => handleClick(n)}
                            style={{
                                display: 'flex', alignItems: 'flex-start', gap: 12,
                                padding: '12px 14px', borderRadius: 8,
                                background: n.isRead ? 'white' : '#eff6ff',
                                border: '1px solid ' + (n.isRead ? '#f3f4f6' : '#bfdbfe'),
                                cursor: n.link ? 'pointer' : 'default',
                            }}
                        >
                            <span style={{ fontSize: 22, flexShrink: 0, paddingTop: 2 }}>
                                {typeEmoji(n.type)}
                            </span>
                            <div style={{ flex: 1, minWidth: 0 }}>
                                <div style={{ fontWeight: n.isRead ? 400 : 600, fontSize: 14, marginBottom: 2 }}>
                                    {n.title}
                                </div>
                                {n.body && (
                                    <div style={{ fontSize: 13, color: '#6b7280', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {n.body}
                                    </div>
                                )}
                                <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 4 }}>
                                    {formatDate(n.createdAt)}
                                </div>
                            </div>
                            <div style={{ display: 'flex', gap: 6, flexShrink: 0 }} onClick={e => e.stopPropagation()}>
                                {!n.isRead && (
                                    <button
                                        onClick={() => handleMarkRead(n.id)}
                                        title="Отметить прочитанным"
                                        style={iconBtn}
                                    >✓</button>
                                )}
                                <button
                                    onClick={() => handleDelete(n.id)}
                                    title="Удалить"
                                    style={{ ...iconBtn, color: '#ef4444' }}
                                >🗑</button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Пагинация */}
            {totalPages > 1 && (
                <div style={{ display: 'flex', gap: 8, justifyContent: 'center', marginTop: 20 }}>
                    <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} style={pageBtn}>←</button>
                    <span style={{ padding: '6px 12px', fontSize: 13, color: '#374151' }}>
                        {page} / {totalPages}
                    </span>
                    <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} style={pageBtn}>→</button>
                </div>
            )}
        </div>
    );
}

// ─── Стили ────────────────────────────────────────────────────────────────────

const iconBtn: React.CSSProperties = {
    background: 'none', border: 'none', cursor: 'pointer',
    padding: '4px 6px', fontSize: 14, color: '#6b7280', borderRadius: 4,
};

const pageBtn: React.CSSProperties = {
    padding: '6px 14px', borderRadius: 6, border: '1px solid #d1d5db',
    background: 'white', cursor: 'pointer', fontSize: 14,
};

function typeEmoji(type: string): string {
    switch (type) {
        case 'TaskAssigned': return '📋';
        case 'TaskDone': return '✅';
        case 'TaskReminder': return '⏰';
        case 'ChannelInvite': return '📨';
        case 'NewChannelPost': return '📢';
        case 'ImprovementStatusChanged': return '💡';
        case 'MigrationPackageCompleted': return '📦';
        case 'JobFailed': return '❌';
        default: return '🔔';
    }
}

function formatDate(iso: string): string {
    try {
        const d = new Date(iso);
        return d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch { return iso; }
}
