import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getNotificationSettings, updateNotificationSettings,
    getDndSettings, updateDndSettings,
    getThrottleSettings, updateThrottleSettings,
} from '../../api/tasksApi';
import type { NotificationSettingDto, DndSettingsDto, ThrottleSettingDto } from '../../api/tasksApi';

const EVENT_TYPE_LABELS: Record<string, string> = {
    TaskAssigned: 'Задача назначена мне',
    TaskDone: 'Задача выполнена',
    TaskOverdue: 'Задача просрочена',
    TaskCommentAdded: 'Добавлен комментарий',
    TaskReminder: 'Напоминание о задаче',
    TaskRescheduled: 'Срок задачи перенесён',
    TaskReopened: 'Задача открыта заново',
    TaskQuestionAsked: 'Мне задан вопрос по задаче',
    TaskMentioned: 'Упомянут в задаче (@mention)',
    TaskCompleted: 'Задача завершена (все статусы)',
    TaskScheduled: 'Задача запланирована в календаре',
    ChatMessageReceived: 'Входящее сообщение в чате',
    ChannelPostPublished: 'Новый пост в подписанном канале',
    ChannelInvite: 'Приглашение в приватный канал',
    TaskApprovalRequired: 'Задача отправлена на согласование',
    TaskApprovalDecision: 'Принято решение по согласованию',
};

const DAY_LABELS = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];
const CHANNELS = ['Email', 'Sms', 'Push'];
const CHANNEL_LABELS: Record<string, string> = { Email: 'Email', Sms: 'SMS', Push: 'Push' };

const EVENT_TYPES = Object.keys(EVENT_TYPE_LABELS);

/** Страница настроек уведомлений — матрица каналов, DND и throttle (FR-MSG-02.2). */
export function NotificationSettingsPage() {
    const { accessToken: token } = useAuth();
    const [settings, setSettings] = useState<NotificationSettingDto[]>([]);
    const [dnd, setDnd] = useState<DndSettingsDto>({
        isEnabled: false, startHour: 22, endHour: 8,
        disabledDays: [], timeZone: 'UTC', applyToPush: true, applyToSms: true,
    });
    const [throttle, setThrottle] = useState<ThrottleSettingDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [savingDnd, setSavingDnd] = useState(false);
    const [savingThrottle, setSavingThrottle] = useState(false);
    const [saved, setSaved] = useState(false);
    const [savedDnd, setSavedDnd] = useState(false);
    const [savedThrottle, setSavedThrottle] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        Promise.all([
            getNotificationSettings(token),
            getDndSettings(token),
            getThrottleSettings(token),
        ])
            .then(([s, d, t]) => { setSettings(s); setDnd(d); setThrottle(t); })
            .catch(e => setError(e.message ?? 'Ошибка загрузки'))
            .finally(() => setLoading(false));
    }, [token]);

    const toggle = (eventType: string, field: 'inApp' | 'email' | 'sms' | 'push') => {
        setSettings(prev => prev.map(s =>
            s.eventType === eventType ? { ...s, [field]: !s[field] } : s
        ));
        setSaved(false);
    };

    const handleSave = async () => {
        if (!token) return;
        setSaving(true);
        setError('');
        try {
            const updated = await updateNotificationSettings(token, settings.map(s => ({
                eventType: s.eventType,
                inApp: s.inApp,
                email: s.email,
                sms: s.sms,
                push: s.push,
            })));
            setSettings(updated);
            setSaved(true);
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleSaveDnd = async () => {
        if (!token) return;
        setSavingDnd(true);
        try {
            const updated = await updateDndSettings(token, dnd);
            setDnd(updated);
            setSavedDnd(true);
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения DND');
        } finally {
            setSavingDnd(false);
        }
    };

    const toggleDndDay = (day: number) => {
        setDnd(prev => ({
            ...prev,
            disabledDays: prev.disabledDays.includes(day)
                ? prev.disabledDays.filter(d => d !== day)
                : [...prev.disabledDays, day],
        }));
        setSavedDnd(false);
    };

    /** Получить текущее значение throttle для eventType+channel (мин). */
    const getThrottleValue = (eventType: string, channel: string): number => {
        const entry = throttle.find(t => t.eventType === eventType && t.channel === channel);
        return entry?.minIntervalMinutes ?? 0;
    };

    const setThrottleValue = (eventType: string, channel: string, value: number) => {
        setThrottle(prev => {
            const exists = prev.find(t => t.eventType === eventType && t.channel === channel);
            if (exists) {
                return prev.map(t =>
                    t.eventType === eventType && t.channel === channel
                        ? { ...t, minIntervalMinutes: value }
                        : t
                );
            }
            return [...prev, { eventType, channel, minIntervalMinutes: value }];
        });
        setSavedThrottle(false);
    };

    const handleSaveThrottle = async () => {
        if (!token) return;
        setSavingThrottle(true);
        try {
            const updated = await updateThrottleSettings(token, throttle);
            setThrottle(updated);
            setSavedThrottle(true);
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения throttle');
        } finally {
            setSavingThrottle(false);
        }
    };

    if (loading) return <div style={{ padding: 24 }}>Загрузка…</div>;

    return (
        <div style={{ padding: 24, maxWidth: 900 }}>
            <h2 style={{ marginBottom: 16 }}>Настройки уведомлений</h2>
            {error && <div style={{ color: '#d9534f', marginBottom: 12 }}>{error}</div>}

            {/* ── Матрица каналов ─────────────────────────────── */}
            <h3 style={{ marginBottom: 8 }}>Матрица каналов доставки</h3>
            <p style={{ color: '#666', marginBottom: 16 }}>
                Выберите, по каким каналам получать уведомления для каждого типа события.
                🔒 — канал обязателен и не может быть отключён.
            </p>

            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 20 }}>
                <thead>
                    <tr style={{ borderBottom: '2px solid #dee2e6', background: '#f8f9fa' }}>
                        <th style={{ textAlign: 'left', padding: '8px 12px', fontWeight: 600 }}>Событие</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>In-app</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>Email</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>SMS</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>Push</th>
                    </tr>
                </thead>
                <tbody>
                    {settings.map(s => (
                        <tr key={s.eventType} style={{ borderBottom: '1px solid #f0f0f0' }}>
                            <td style={{ padding: '8px 12px' }}>
                                {EVENT_TYPE_LABELS[s.eventType] ?? s.eventType}
                                {s.hasMandatory && <span title="Есть обязательные каналы" style={{ marginLeft: 6, color: '#888' }}>🔒</span>}
                            </td>
                            {(['inApp', 'email', 'sms', 'push'] as const).map(field => (
                                <td key={field} style={{ textAlign: 'center', padding: '8px 12px' }}>
                                    <input
                                        type="checkbox"
                                        checked={s[field]}
                                        onChange={() => toggle(s.eventType, field)}
                                    />
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>

            <div style={{ marginBottom: 32, display: 'flex', gap: 12, alignItems: 'center' }}>
                <button
                    onClick={handleSave}
                    disabled={saving}
                    style={{
                        padding: '8px 20px', background: '#4a90d9', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: saving ? 'not-allowed' : 'pointer',
                        opacity: saving ? 0.7 : 1,
                    }}
                >
                    {saving ? 'Сохранение…' : 'Сохранить матрицу'}
                </button>
                {saved && <span style={{ color: '#5cb85c' }}>✓ Сохранено</span>}
            </div>

            {/* ── Ограничение частоты (Throttle) ──────────────── */}
            <h3 style={{ marginBottom: 8 }}>Ограничение частоты уведомлений</h3>
            <p style={{ color: '#666', marginBottom: 16 }}>
                Задайте минимальный интервал (в минутах) между уведомлениями одного типа по каждому каналу.
                0 — без ограничения.
            </p>

            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 20 }}>
                <thead>
                    <tr style={{ borderBottom: '2px solid #dee2e6', background: '#f8f9fa' }}>
                        <th style={{ textAlign: 'left', padding: '8px 12px', fontWeight: 600 }}>Событие</th>
                        {CHANNELS.map(ch => (
                            <th key={ch} style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>
                                {CHANNEL_LABELS[ch]} (мин)
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {EVENT_TYPES.map(eventType => (
                        <tr key={eventType} style={{ borderBottom: '1px solid #f0f0f0' }}>
                            <td style={{ padding: '8px 12px' }}>
                                {EVENT_TYPE_LABELS[eventType]}
                            </td>
                            {CHANNELS.map(ch => (
                                <td key={ch} style={{ textAlign: 'center', padding: '8px 12px' }}>
                                    <input
                                        type="number"
                                        min={0}
                                        step={10}
                                        value={getThrottleValue(eventType, ch)}
                                        onChange={e => setThrottleValue(eventType, ch, Number(e.target.value))}
                                        style={{
                                            width: 70, padding: '4px 6px',
                                            border: '1px solid #ced4da', borderRadius: 4, textAlign: 'center',
                                        }}
                                    />
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>

            <div style={{ marginBottom: 32, display: 'flex', gap: 12, alignItems: 'center' }}>
                <button
                    onClick={handleSaveThrottle}
                    disabled={savingThrottle}
                    style={{
                        padding: '8px 20px', background: '#4a90d9', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: savingThrottle ? 'not-allowed' : 'pointer',
                        opacity: savingThrottle ? 0.7 : 1,
                    }}
                >
                    {savingThrottle ? 'Сохранение…' : 'Сохранить ограничения'}
                </button>
                {savedThrottle && <span style={{ color: '#5cb85c' }}>✓ Сохранено</span>}
            </div>

            {/* ── Режим «Не беспокоить» ───────────────────────── */}
            <h3 style={{ marginBottom: 8 }}>Режим «Не беспокоить»</h3>
            <p style={{ color: '#666', marginBottom: 16 }}>
                В указанный период push и SMS уведомления не отправляются.
            </p>

            <div style={{ background: '#f8f9fa', padding: 16, borderRadius: 6, marginBottom: 20 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12, fontWeight: 600 }}>
                    <input
                        type="checkbox"
                        checked={dnd.isEnabled}
                        onChange={e => { setDnd(prev => ({ ...prev, isEnabled: e.target.checked })); setSavedDnd(false); }}
                    />
                    Включить режим «Не беспокоить»
                </label>

                <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginBottom: 12 }}>
                    <label>
                        Начало:&nbsp;
                        <input
                            type="number" min={0} max={23} value={dnd.startHour}
                            onChange={e => { setDnd(prev => ({ ...prev, startHour: Number(e.target.value) })); setSavedDnd(false); }}
                            style={{ width: 60, padding: '4px 8px', border: '1px solid #ced4da', borderRadius: 4 }}
                        />
                        &nbsp;:00
                    </label>
                    <label>
                        Конец:&nbsp;
                        <input
                            type="number" min={0} max={23} value={dnd.endHour}
                            onChange={e => { setDnd(prev => ({ ...prev, endHour: Number(e.target.value) })); setSavedDnd(false); }}
                            style={{ width: 60, padding: '4px 8px', border: '1px solid #ced4da', borderRadius: 4 }}
                        />
                        &nbsp;:00
                    </label>
                    <label>
                        Часовой пояс:&nbsp;
                        <input
                            type="text" value={dnd.timeZone}
                            onChange={e => { setDnd(prev => ({ ...prev, timeZone: e.target.value })); setSavedDnd(false); }}
                            style={{ width: 160, padding: '4px 8px', border: '1px solid #ced4da', borderRadius: 4 }}
                        />
                    </label>
                </div>

                <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
                    {DAY_LABELS.map((label, i) => (
                        <button
                            key={i}
                            onClick={() => toggleDndDay(i)}
                            style={{
                                padding: '4px 10px', border: '1px solid #ced4da', borderRadius: 4,
                                background: dnd.disabledDays.includes(i) ? '#4a90d9' : '#fff',
                                color: dnd.disabledDays.includes(i) ? '#fff' : '#333',
                                cursor: 'pointer',
                            }}
                        >
                            {label}
                        </button>
                    ))}
                </div>

                <div style={{ display: 'flex', gap: 16 }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <input type="checkbox" checked={dnd.applyToPush}
                            onChange={e => { setDnd(prev => ({ ...prev, applyToPush: e.target.checked })); setSavedDnd(false); }} />
                        Блокировать Push
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <input type="checkbox" checked={dnd.applyToSms}
                            onChange={e => { setDnd(prev => ({ ...prev, applyToSms: e.target.checked })); setSavedDnd(false); }} />
                        Блокировать SMS
                    </label>
                </div>
            </div>

            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                <button
                    onClick={handleSaveDnd}
                    disabled={savingDnd}
                    style={{
                        padding: '8px 20px', background: '#4a90d9', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: savingDnd ? 'not-allowed' : 'pointer',
                        opacity: savingDnd ? 0.7 : 1,
                    }}
                >
                    {savingDnd ? 'Сохранение…' : 'Сохранить DND'}
                </button>
                {savedDnd && <span style={{ color: '#5cb85c' }}>✓ Сохранено</span>}
            </div>
        </div>
    );
}
