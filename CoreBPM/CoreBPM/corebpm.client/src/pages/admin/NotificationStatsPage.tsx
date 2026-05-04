import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getDeliveryStats } from '../../api/tasksApi';
import type { DeliveryStatsDto } from '../../api/tasksApi';

const CHANNEL_LABELS: Record<string, string> = {
    InApp: 'In-app', Email: 'Email', Sms: 'SMS', Push: 'Push',
};

const CHANNEL_COLORS: Record<string, string> = {
    InApp: '#4a90d9', Email: '#e67e22', Sms: '#27ae60', Push: '#8e44ad',
};

/** Страница статистики доставки уведомлений (администратор). FR-MSG-02.2. */
export function NotificationStatsPage() {
    const { accessToken: token } = useAuth();
    const [stats, setStats] = useState<DeliveryStatsDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [from, setFrom] = useState('');
    const [to, setTo] = useState('');

    const loadStats = (fromVal?: string, toVal?: string) => {
        if (!token) return;
        setLoading(true);
        setError('');
        getDeliveryStats(token, { from: fromVal || undefined, to: toVal || undefined })
            .then(s => setStats(s))
            .catch(e => setError(e.message ?? 'Ошибка загрузки статистики'))
            .finally(() => setLoading(false));
    };

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        setError('');
        getDeliveryStats(token, {})
            .then(s => setStats(s))
            .catch(e => setError(e.message ?? 'Ошибка загрузки статистики'))
            .finally(() => setLoading(false));
    }, [token]);

    const inputStyle: React.CSSProperties = {
        padding: '5px 8px', border: '1px solid #ced4da', borderRadius: 4, fontSize: 13,
    };

    const badgeStyle = (color: string): React.CSSProperties => ({
        display: 'inline-block', padding: '2px 8px', borderRadius: 12,
        background: color, color: '#fff', fontWeight: 600, fontSize: 13,
    });

    return (
        <div style={{ padding: 24, maxWidth: 1000 }}>
            <h2 style={{ marginBottom: 16 }}>Статистика доставки уведомлений</h2>
            {error && <div style={{ color: '#d9534f', marginBottom: 12 }}>{error}</div>}

            {/* Фильтр периода */}
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 24, flexWrap: 'wrap' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 14 }}>
                    С:&nbsp;
                    <input type="date" style={inputStyle} value={from}
                        onChange={e => setFrom(e.target.value)} />
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 14 }}>
                    По:&nbsp;
                    <input type="date" style={inputStyle} value={to}
                        onChange={e => setTo(e.target.value)} />
                </label>
                <button
                    onClick={() => loadStats(from, to)}
                    disabled={loading}
                    style={{
                        padding: '6px 16px', background: '#4a90d9', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: 'pointer',
                    }}
                >
                    {loading ? 'Загрузка…' : 'Применить'}
                </button>
                <button
                    onClick={() => { setFrom(''); setTo(''); loadStats(); }}
                    style={{
                        padding: '6px 16px', background: '#6c757d', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: 'pointer',
                    }}
                >
                    Сбросить
                </button>
            </div>

            {!stats && loading && <div style={{ color: '#888' }}>Загрузка…</div>}

            {stats && (
                <>
                    {/* Карточки по каналам */}
                    <h3 style={{ marginBottom: 12 }}>По каналам доставки</h3>
                    <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 32 }}>
                        {stats.byChannel.map(ch => (
                            <div key={ch.channel} style={{
                                background: '#fff', border: '1px solid #dee2e6', borderRadius: 8,
                                padding: '16px 20px', minWidth: 200, flex: '1 1 200px',
                                borderTop: `4px solid ${CHANNEL_COLORS[ch.channel] ?? '#999'}`,
                            }}>
                                <div style={{ fontWeight: 700, fontSize: 16, marginBottom: 10 }}>
                                    {CHANNEL_LABELS[ch.channel] ?? ch.channel}
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: 6, fontSize: 13 }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Отправлено:</span>
                                        <span style={badgeStyle('#5cb85c')}>{ch.sent}</span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Ошибки:</span>
                                        <span style={badgeStyle('#d9534f')}>{ch.failed}</span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Пропущено (настройки):</span>
                                        <span style={badgeStyle('#aaa')}>{ch.skippedUserSettings}</span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Пропущено (DND):</span>
                                        <span style={badgeStyle('#f0ad4e')}>{ch.skippedDnd}</span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Пропущено (throttle):</span>
                                        <span style={badgeStyle('#9b59b6')}>{ch.skippedThrottle}</span>
                                    </div>
                                    <div style={{
                                        borderTop: '1px solid #dee2e6', marginTop: 4, paddingTop: 6,
                                        display: 'flex', justifyContent: 'space-between', fontWeight: 600,
                                    }}>
                                        <span>Всего:</span>
                                        <span>{ch.total}</span>
                                    </div>
                                    {ch.total > 0 && (
                                        <div style={{ fontSize: 11, color: '#888', textAlign: 'right' }}>
                                            Успех: {Math.round((ch.sent / ch.total) * 100)}%
                                        </div>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>

                    {/* Топ типов событий */}
                    <h3 style={{ marginBottom: 12 }}>Топ-10 типов событий</h3>
                    {stats.topEventTypes.length === 0 ? (
                        <div style={{ color: '#999' }}>Нет данных</div>
                    ) : (
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                            <thead>
                                <tr style={{ borderBottom: '2px solid #dee2e6', background: '#f8f9fa' }}>
                                    <th style={{ textAlign: 'left', padding: '8px 10px', fontWeight: 600 }}>Тип события</th>
                                    <th style={{ textAlign: 'center', padding: '8px 10px', fontWeight: 600 }}>Всего</th>
                                    <th style={{ textAlign: 'center', padding: '8px 10px', fontWeight: 600 }}>Отправлено</th>
                                    <th style={{ textAlign: 'center', padding: '8px 10px', fontWeight: 600 }}>Ошибки</th>
                                    <th style={{ textAlign: 'center', padding: '8px 10px', fontWeight: 600 }}>% успеха</th>
                                </tr>
                            </thead>
                            <tbody>
                                {stats.topEventTypes.map(ev => (
                                    <tr key={ev.eventType} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                        <td style={{ padding: '6px 10px', fontFamily: 'monospace' }}>{ev.eventType}</td>
                                        <td style={{ textAlign: 'center', padding: '6px 10px', fontWeight: 600 }}>{ev.total}</td>
                                        <td style={{ textAlign: 'center', padding: '6px 10px', color: '#5cb85c' }}>{ev.sent}</td>
                                        <td style={{ textAlign: 'center', padding: '6px 10px', color: '#d9534f' }}>{ev.failed}</td>
                                        <td style={{ textAlign: 'center', padding: '6px 10px' }}>
                                            {ev.total > 0
                                                ? <span style={{ color: '#5cb85c', fontWeight: 600 }}>
                                                    {Math.round((ev.sent / ev.total) * 100)}%
                                                  </span>
                                                : '—'
                                            }
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </>
            )}
        </div>
    );
}
