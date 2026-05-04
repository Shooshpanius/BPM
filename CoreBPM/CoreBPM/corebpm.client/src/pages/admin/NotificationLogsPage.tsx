import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getDeliveryLogs, getDeliveryLogsExportUrl,
    getNotificationLogRetention, updateNotificationLogRetention,
} from '../../api/tasksApi';
import type { DeliveryLogEntryDto } from '../../api/tasksApi';

const CHANNEL_LABELS: Record<string, string> = {
    InApp: 'In-app', Email: 'Email', Sms: 'SMS', Push: 'Push',
};

const STATUS_LABELS: Record<string, string> = {
    Sent: 'Отправлено',
    Failed: 'Ошибка',
    SkippedUserSettings: 'Пропущено (настройки)',
    SkippedDnd: 'Пропущено (DND)',
    SkippedThrottle: 'Пропущено (throttle)',
};

const STATUS_COLORS: Record<string, string> = {
    Sent: '#5cb85c',
    Failed: '#d9534f',
    SkippedUserSettings: '#888',
    SkippedDnd: '#f0ad4e',
    SkippedThrottle: '#9b59b6',
};

/** Журнал доставки уведомлений с экспортом и настройками retention (администратор). FR-MSG-02.2. */
export function NotificationLogsPage() {
    const { accessToken: token } = useAuth();
    const [items, setItems] = useState<DeliveryLogEntryDto[]>([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const [filterUserId, setFilterUserId] = useState('');
    const [filterType, setFilterType] = useState('');
    const [filterChannel, setFilterChannel] = useState('');
    const [filterStatus, setFilterStatus] = useState('');
    const [filterFrom, setFilterFrom] = useState('');
    const [filterTo, setFilterTo] = useState('');
    const [page, setPage] = useState(1);
    const pageSize = 50;

    // Retention settings
    const [retentionDays, setRetentionDays] = useState(90);
    const [savingRetention, setSavingRetention] = useState(false);
    const [savedRetention, setSavedRetention] = useState(false);

    const load = useCallback(() => {
        if (!token) return;
        setLoading(true);
        getDeliveryLogs(token, {
            userId: filterUserId || undefined,
            type: filterType || undefined,
            channel: filterChannel || undefined,
            status: filterStatus || undefined,
            from: filterFrom || undefined,
            to: filterTo || undefined,
            page,
            pageSize,
        })
            .then(r => { setItems(r.items); setTotal(r.total); })
            .catch(e => setError(e.message ?? 'Ошибка'))
            .finally(() => setLoading(false));
    }, [token, filterUserId, filterType, filterChannel, filterStatus, filterFrom, filterTo, page]);

    useEffect(() => { load(); }, [load]);

    useEffect(() => {
        if (!token) return;
        getNotificationLogRetention(token)
            .then(r => setRetentionDays(r.retentionDays))
            .catch(() => {/* не критично */});
    }, [token]);

    const handleSaveRetention = async () => {
        if (!token) return;
        setSavingRetention(true);
        try {
            const updated = await updateNotificationLogRetention(token, { retentionDays });
            setRetentionDays(updated.retentionDays);
            setSavedRetention(true);
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения');
        } finally {
            setSavingRetention(false);
        }
    };

    const handleExport = () => {
        const url = getDeliveryLogsExportUrl({
            userId: filterUserId || undefined,
            type: filterType || undefined,
            channel: filterChannel || undefined,
            status: filterStatus || undefined,
            from: filterFrom || undefined,
            to: filterTo || undefined,
        });
        window.open(url, '_blank');
    };

    const inputStyle: React.CSSProperties = {
        padding: '5px 8px', border: '1px solid #ced4da', borderRadius: 4, fontSize: 13,
    };

    return (
        <div style={{ padding: 24, maxWidth: 1100 }}>
            <h2 style={{ marginBottom: 12 }}>Журнал доставки уведомлений</h2>
            {error && <div style={{ color: '#d9534f', marginBottom: 12 }}>{error}</div>}

            {/* Настройки хранения журнала */}
            <div style={{
                background: '#f8f9fa', border: '1px solid #dee2e6', borderRadius: 6,
                padding: '12px 16px', marginBottom: 20, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap',
            }}>
                <span style={{ fontWeight: 600, fontSize: 14 }}>Хранение журнала:</span>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 14 }}>
                    Срок хранения:&nbsp;
                    <input
                        type="number" min={0} step={30} value={retentionDays}
                        onChange={e => { setRetentionDays(Number(e.target.value)); setSavedRetention(false); }}
                        style={{ ...inputStyle, width: 80 }}
                    />
                    &nbsp;дней (0 = бессрочно)
                </label>
                <button
                    onClick={handleSaveRetention}
                    disabled={savingRetention}
                    style={{
                        padding: '5px 14px', background: '#6c757d', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 13,
                    }}
                >
                    {savingRetention ? 'Сохранение…' : 'Сохранить'}
                </button>
                {savedRetention && <span style={{ color: '#5cb85c', fontSize: 13 }}>✓ Сохранено</span>}
            </div>

            {/* Фильтры */}
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 16 }}>
                <input style={inputStyle} placeholder="ID пользователя"
                    value={filterUserId} onChange={e => { setFilterUserId(e.target.value); setPage(1); }} />
                <input style={inputStyle} placeholder="Тип события"
                    value={filterType} onChange={e => { setFilterType(e.target.value); setPage(1); }} />
                <select style={inputStyle} value={filterChannel} onChange={e => { setFilterChannel(e.target.value); setPage(1); }}>
                    <option value="">Все каналы</option>
                    <option value="InApp">In-app</option>
                    <option value="Email">Email</option>
                    <option value="Sms">SMS</option>
                    <option value="Push">Push</option>
                </select>
                <select style={inputStyle} value={filterStatus} onChange={e => { setFilterStatus(e.target.value); setPage(1); }}>
                    <option value="">Все статусы</option>
                    <option value="Sent">Отправлено</option>
                    <option value="Failed">Ошибка</option>
                    <option value="SkippedUserSettings">Пропущено (настройки)</option>
                    <option value="SkippedDnd">Пропущено (DND)</option>
                    <option value="SkippedThrottle">Пропущено (throttle)</option>
                </select>
                <input type="date" style={inputStyle}
                    value={filterFrom} onChange={e => { setFilterFrom(e.target.value); setPage(1); }} />
                <input type="date" style={inputStyle}
                    value={filterTo} onChange={e => { setFilterTo(e.target.value); setPage(1); }} />
                <button onClick={load}
                    style={{ padding: '5px 14px', background: '#4a90d9', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>
                    Обновить
                </button>
                <button onClick={handleExport}
                    style={{ padding: '5px 14px', background: '#28a745', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                    title="Экспортировать все найденные записи в CSV">
                    ⬇ CSV
                </button>
            </div>

            {loading ? (
                <div style={{ padding: 20, color: '#888' }}>Загрузка…</div>
            ) : (
                <>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                        <thead>
                            <tr style={{ borderBottom: '2px solid #dee2e6', background: '#f8f9fa' }}>
                                <th style={{ textAlign: 'left', padding: '8px 10px' }}>Пользователь</th>
                                <th style={{ textAlign: 'left', padding: '8px 10px' }}>Тип события</th>
                                <th style={{ textAlign: 'center', padding: '8px 10px' }}>Канал</th>
                                <th style={{ textAlign: 'center', padding: '8px 10px' }}>Статус</th>
                                <th style={{ textAlign: 'left', padding: '8px 10px' }}>Ошибка</th>
                                <th style={{ textAlign: 'left', padding: '8px 10px' }}>Время</th>
                            </tr>
                        </thead>
                        <tbody>
                            {items.map(l => (
                                <tr key={l.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                    <td style={{ padding: '6px 10px' }}>{l.userFullName || l.userId}</td>
                                    <td style={{ padding: '6px 10px', fontFamily: 'monospace' }}>{l.eventType}</td>
                                    <td style={{ textAlign: 'center', padding: '6px 10px' }}>
                                        {CHANNEL_LABELS[l.channel] ?? l.channel}
                                    </td>
                                    <td style={{ textAlign: 'center', padding: '6px 10px' }}>
                                        <span style={{
                                            color: STATUS_COLORS[l.status] ?? '#333',
                                            fontWeight: 600,
                                        }}>
                                            {STATUS_LABELS[l.status] ?? l.status}
                                        </span>
                                    </td>
                                    <td style={{ padding: '6px 10px', color: '#d9534f', fontSize: 12 }}>
                                        {l.error || ''}
                                    </td>
                                    <td style={{ padding: '6px 10px', color: '#888' }}>
                                        {new Date(l.createdAt).toLocaleString('ru')}
                                    </td>
                                </tr>
                            ))}
                            {items.length === 0 && (
                                <tr>
                                    <td colSpan={6} style={{ padding: '20px', color: '#999', textAlign: 'center' }}>
                                        Записей не найдено
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>

                    {/* Пагинация */}
                    <div style={{ marginTop: 12, display: 'flex', gap: 8, alignItems: 'center' }}>
                        <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}
                            style={{ padding: '4px 12px', cursor: 'pointer', border: '1px solid #ced4da', borderRadius: 4 }}>
                            ← Назад
                        </button>
                        <span style={{ color: '#555', fontSize: 13 }}>
                            Стр. {page} из {Math.max(1, Math.ceil(total / pageSize))} (всего {total})
                        </span>
                        <button disabled={page >= Math.ceil(total / pageSize)} onClick={() => setPage(p => p + 1)}
                            style={{ padding: '4px 12px', cursor: 'pointer', border: '1px solid #ced4da', borderRadius: 4 }}>
                            Вперёд →
                        </button>
                    </div>
                </>
            )}
        </div>
    );
}
