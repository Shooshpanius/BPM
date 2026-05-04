import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getNotificationSettings, updateNotificationSettings } from '../../api/tasksApi';
import type { NotificationSettingDto } from '../../api/tasksApi';

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
};

/** Страница настроек уведомлений по задачам (FR-TASK-02.3). */
export function NotificationSettingsPage() {
    const { accessToken: token } = useAuth();
    const [settings, setSettings] = useState<NotificationSettingDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [saved, setSaved] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        getNotificationSettings(token)
            .then(setSettings)
            .catch(e => setError(e.message ?? 'Ошибка загрузки'))
            .finally(() => setLoading(false));
    }, [token]);

    const toggle = (eventType: string, field: 'inApp' | 'email') => {
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
            })));
            setSettings(updated);
            setSaved(true);
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div style={{ padding: 24 }}>Загрузка…</div>;

    return (
        <div style={{ padding: 24, maxWidth: 700 }}>
            <h2 style={{ marginBottom: 16 }}>Настройки уведомлений по задачам</h2>
            <p style={{ color: '#666', marginBottom: 20 }}>
                Выберите, какие события должны вызывать уведомления в приложении (in-app) и по email.
            </p>

            {error && <div style={{ color: '#d9534f', marginBottom: 12 }}>{error}</div>}

            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                    <tr style={{ borderBottom: '2px solid #dee2e6' }}>
                        <th style={{ textAlign: 'left', padding: '8px 12px', fontWeight: 600 }}>Событие</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>In-app</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px', fontWeight: 600 }}>Email</th>
                    </tr>
                </thead>
                <tbody>
                    {settings.map(s => (
                        <tr key={s.eventType} style={{ borderBottom: '1px solid #f0f0f0' }}>
                            <td style={{ padding: '8px 12px' }}>
                                {EVENT_TYPE_LABELS[s.eventType] ?? s.eventType}
                            </td>
                            <td style={{ textAlign: 'center', padding: '8px 12px' }}>
                                <input
                                    type="checkbox"
                                    checked={s.inApp}
                                    onChange={() => toggle(s.eventType, 'inApp')}
                                />
                            </td>
                            <td style={{ textAlign: 'center', padding: '8px 12px' }}>
                                <input
                                    type="checkbox"
                                    checked={s.email}
                                    onChange={() => toggle(s.eventType, 'email')}
                                />
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            <div style={{ marginTop: 20, display: 'flex', gap: 12, alignItems: 'center' }}>
                <button
                    onClick={handleSave}
                    disabled={saving}
                    style={{
                        padding: '8px 20px', background: '#4a90d9', color: '#fff',
                        border: 'none', borderRadius: 4, cursor: saving ? 'not-allowed' : 'pointer',
                        opacity: saving ? 0.7 : 1,
                    }}
                >
                    {saving ? 'Сохранение…' : 'Сохранить'}
                </button>
                {saved && <span style={{ color: '#5cb85c' }}>✓ Сохранено</span>}
            </div>
        </div>
    );
}
