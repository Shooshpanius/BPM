import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getTaskControlSettings,
    updateTaskControlSettings,
} from '../../api/tasksApi';
import type { TaskControlSettingsDto } from '../../api/tasksApi';

const CONTROL_TYPE_OPTIONS = [
    { value: 'None', label: 'Без контроля' },
    { value: 'ControlAfterExecution', label: 'Контроль выполнения' },
    { value: 'CurrentControl', label: 'Текущий контроль' },
    { value: 'NotifyOnCompletion', label: 'Оповещать при выполнении' },
];

/** Страница системных настроек контроля и трудозатрат задач (FR-TASK-01.4). */
export default function TaskControlSettingsPage() {
    const { accessToken: token } = useAuth();
    const [settings, setSettings] = useState<TaskControlSettingsDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    // Редактируемые поля
    const [defaultControlType, setDefaultControlType] = useState('None');
    const [isEffortRequired, setIsEffortRequired] = useState(false);
    const [isActivityTypeRequired, setIsActivityTypeRequired] = useState(false);

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        getTaskControlSettings(token)
            .then(s => {
                setSettings(s);
                setDefaultControlType(s.defaultControlType);
                setIsEffortRequired(s.isEffortRequired);
                setIsActivityTypeRequired(s.isActivityTypeRequired);
            })
            .catch(e => setError(e instanceof Error ? e.message : 'Ошибка загрузки'))
            .finally(() => setLoading(false));
    }, [token]);

    const handleSave = async () => {
        if (!token) return;
        setSaving(true);
        setError(null);
        setSuccess(false);
        try {
            const updated = await updateTaskControlSettings(token, {
                defaultControlType,
                isEffortRequired,
                isActivityTypeRequired,
            });
            setSettings(updated);
            setSuccess(true);
            setTimeout(() => setSuccess(false), 3000);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="admin-page__loading">Загрузка...</div>;

    return (
        <div style={{ maxWidth: 560, margin: '0 auto', padding: '24px 16px' }}>
            <h2 style={{ marginBottom: 24 }}>Настройки контроля задач</h2>

            {error && (
                <div style={{ color: '#dc2626', background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: 6, padding: '10px 14px', marginBottom: 16 }}>
                    {error}
                </div>
            )}
            {success && (
                <div style={{ color: '#166534', background: '#dcfce7', border: '1px solid #86efac', borderRadius: 6, padding: '10px 14px', marginBottom: 16 }}>
                    ✓ Настройки сохранены
                </div>
            )}

            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
                {/* Тип контроля по умолчанию */}
                <div>
                    <label style={{ fontWeight: 600, display: 'block', marginBottom: 6 }}>
                        Тип контроля по умолчанию
                    </label>
                    <select
                        value={defaultControlType}
                        onChange={e => setDefaultControlType(e.target.value)}
                        style={{ padding: '8px 12px', borderRadius: 6, border: '1px solid #d1d5db', minWidth: 280 }}>
                        {CONTROL_TYPE_OPTIONS.map(o => (
                            <option key={o.value} value={o.value}>{o.label}</option>
                        ))}
                    </select>
                    <p style={{ fontSize: 13, color: '#6b7280', marginTop: 4 }}>
                        Будет автоматически применяться к новым задачам при создании.
                    </p>
                </div>

                {/* Обязательные трудозатраты */}
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
                    <input
                        id="effortRequired"
                        type="checkbox"
                        checked={isEffortRequired}
                        onChange={e => setIsEffortRequired(e.target.checked)}
                        style={{ marginTop: 3, width: 16, height: 16, cursor: 'pointer' }} />
                    <label htmlFor="effortRequired" style={{ cursor: 'pointer' }}>
                        <span style={{ fontWeight: 600 }}>Обязательный ввод трудозатрат</span>
                        <p style={{ fontSize: 13, color: '#6b7280', margin: '4px 0 0' }}>
                            Пользователь не сможет нажать «Сделано» без предварительного добавления трудозатрат.
                        </p>
                    </label>
                </div>

                {/* Обязательный вид деятельности */}
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
                    <input
                        id="activityTypeRequired"
                        type="checkbox"
                        checked={isActivityTypeRequired}
                        onChange={e => setIsActivityTypeRequired(e.target.checked)}
                        style={{ marginTop: 3, width: 16, height: 16, cursor: 'pointer' }} />
                    <label htmlFor="activityTypeRequired" style={{ cursor: 'pointer' }}>
                        <span style={{ fontWeight: 600 }}>Обязательный вид деятельности</span>
                        <p style={{ fontSize: 13, color: '#6b7280', margin: '4px 0 0' }}>
                            Поле «Вид деятельности» является обязательным при добавлении трудозатрат.
                        </p>
                    </label>
                </div>

                {settings && (
                    <p style={{ fontSize: 12, color: '#9ca3af' }}>
                        Последнее обновление: {new Date(settings.updatedAt).toLocaleString('ru-RU')}
                    </p>
                )}

                <div>
                    <button
                        onClick={handleSave}
                        disabled={saving}
                        style={{ padding: '10px 24px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600, cursor: saving ? 'not-allowed' : 'pointer', opacity: saving ? 0.7 : 1 }}>
                        {saving ? 'Сохранение...' : 'Сохранить'}
                    </button>
                </div>
            </div>
        </div>
    );
}
