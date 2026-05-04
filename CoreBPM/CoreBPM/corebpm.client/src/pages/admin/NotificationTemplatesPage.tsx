import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getNotificationTemplates, upsertNotificationTemplate, deleteNotificationTemplate,
} from '../../api/tasksApi';
import type { NotificationTemplateDto } from '../../api/tasksApi';

const HELP_VARS = '{{user.fullName}}, {{task.name}}, {{task.number}}, {{process.name}}, {{actor.fullName}}';

/** Страница управления глобальными шаблонами уведомлений (FR-MSG-02.2). */
export function NotificationTemplatesPage() {
    const { accessToken: token } = useAuth();
    const [templates, setTemplates] = useState<NotificationTemplateDto[]>([]);
    const [editing, setEditing] = useState<Partial<NotificationTemplateDto> | null>(null);
    const [editingEventType, setEditingEventType] = useState('');
    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');

    const load = () => {
        if (!token) return;
        setLoading(true);
        getNotificationTemplates(token)
            .then(setTemplates)
            .catch(e => setError(e.message ?? 'Ошибка'))
            .finally(() => setLoading(false));
    };

    useEffect(load, [token]);

    const handleEdit = (t: NotificationTemplateDto) => {
        setEditing({ ...t });
        setEditingEventType(t.eventType);
        setError('');
    };

    const handleNew = () => {
        setEditing({
            eventType: '', eventLabel: '', emailSubjectTemplate: '',
            emailBodyTemplate: '', shortTemplate: '',
            isMandatoryInApp: false, isMandatoryEmail: false,
            isMandatorySms: false, isMandatoryPush: false, isActive: true,
        });
        setEditingEventType('');
        setError('');
    };

    const handleSave = async () => {
        if (!token || !editing) return;
        const et = editingEventType || editing.eventType || '';
        if (!et) { setError('Укажите тип события'); return; }
        setSaving(true);
        try {
            await upsertNotificationTemplate(token, et, editing);
            setEditing(null);
            load();
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async (id: string) => {
        if (!token || !confirm('Удалить шаблон?')) return;
        try {
            await deleteNotificationTemplate(token, id);
            load();
        } catch (e: unknown) {
            setError((e as Error).message ?? 'Ошибка');
        }
    };

    if (loading) return <div style={{ padding: 24 }}>Загрузка…</div>;

    const inputStyle: React.CSSProperties = {
        width: '100%', padding: '6px 10px', border: '1px solid #ced4da',
        borderRadius: 4, marginTop: 2, boxSizing: 'border-box',
    };
    const labelStyle: React.CSSProperties = { display: 'block', marginBottom: 10 };

    return (
        <div style={{ padding: 24, maxWidth: 900 }}>
            <h2 style={{ marginBottom: 4 }}>Шаблоны уведомлений</h2>
            <p style={{ color: '#666', marginBottom: 16 }}>
                Настройте тексты и обязательные каналы для каждого типа события.
                Доступные переменные: <code>{HELP_VARS}</code>
            </p>

            {error && <div style={{ color: '#d9534f', marginBottom: 12 }}>{error}</div>}

            {editing ? (
                <div style={{ background: '#f8f9fa', padding: 20, borderRadius: 6, marginBottom: 24 }}>
                    <h3 style={{ marginBottom: 16 }}>
                        {editingEventType ? `Редактирование: ${editingEventType}` : 'Новый шаблон'}
                    </h3>

                    {!editingEventType && (
                        <label style={labelStyle}>
                            Тип события (EventType)*
                            <input style={inputStyle} value={editing.eventType ?? ''}
                                onChange={e => setEditing(prev => ({ ...prev, eventType: e.target.value }))} />
                        </label>
                    )}

                    <label style={labelStyle}>
                        Название события
                        <input style={inputStyle} value={editing.eventLabel ?? ''}
                            onChange={e => setEditing(prev => ({ ...prev, eventLabel: e.target.value }))} />
                    </label>

                    <label style={labelStyle}>
                        Тема email
                        <input style={inputStyle} value={editing.emailSubjectTemplate ?? ''}
                            onChange={e => setEditing(prev => ({ ...prev, emailSubjectTemplate: e.target.value }))} />
                    </label>

                    <label style={labelStyle}>
                        Тело email (HTML)
                        <textarea style={{ ...inputStyle, height: 120 }} value={editing.emailBodyTemplate ?? ''}
                            onChange={e => setEditing(prev => ({ ...prev, emailBodyTemplate: e.target.value }))} />
                    </label>

                    <label style={labelStyle}>
                        Краткий текст (push/SMS)
                        <input style={inputStyle} value={editing.shortTemplate ?? ''}
                            onChange={e => setEditing(prev => ({ ...prev, shortTemplate: e.target.value }))} />
                    </label>

                    <div style={{ display: 'flex', gap: 20, marginBottom: 12, flexWrap: 'wrap' }}>
                        {(
                            [
                                ['isMandatoryInApp', 'Обязат. In-app'],
                                ['isMandatoryEmail', 'Обязат. Email'],
                                ['isMandatorySms', 'Обязат. SMS'],
                                ['isMandatoryPush', 'Обязат. Push'],
                            ] as [keyof NotificationTemplateDto, string][]
                        ).map(([field, label]) => (
                            <label key={field} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                <input type="checkbox"
                                    checked={!!(editing as Record<string, unknown>)[field]}
                                    onChange={e => setEditing(prev => ({ ...prev, [field]: e.target.checked }))} />
                                {label}
                            </label>
                        ))}
                        <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                            <input type="checkbox"
                                checked={editing.isActive ?? true}
                                onChange={e => setEditing(prev => ({ ...prev, isActive: e.target.checked }))} />
                            Активен
                        </label>
                    </div>

                    <div style={{ display: 'flex', gap: 10 }}>
                        <button
                            onClick={handleSave}
                            disabled={saving}
                            style={{ padding: '8px 20px', background: '#4a90d9', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                        >
                            {saving ? 'Сохранение…' : 'Сохранить'}
                        </button>
                        <button
                            onClick={() => setEditing(null)}
                            style={{ padding: '8px 16px', background: '#f0f0f0', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                        >
                            Отмена
                        </button>
                    </div>
                </div>
            ) : (
                <button
                    onClick={handleNew}
                    style={{ marginBottom: 16, padding: '8px 16px', background: '#28a745', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                >
                    + Новый шаблон
                </button>
            )}

            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                    <tr style={{ borderBottom: '2px solid #dee2e6', background: '#f8f9fa' }}>
                        <th style={{ textAlign: 'left', padding: '8px 12px' }}>Тип события</th>
                        <th style={{ textAlign: 'left', padding: '8px 12px' }}>Название</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px' }}>Обязательные</th>
                        <th style={{ textAlign: 'center', padding: '8px 12px' }}>Активен</th>
                        <th style={{ padding: '8px 12px' }}></th>
                    </tr>
                </thead>
                <tbody>
                    {templates.map(t => (
                        <tr key={t.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                            <td style={{ padding: '8px 12px', fontFamily: 'monospace', fontSize: 13 }}>{t.eventType}</td>
                            <td style={{ padding: '8px 12px' }}>{t.eventLabel || '—'}</td>
                            <td style={{ textAlign: 'center', padding: '8px 12px', fontSize: 13 }}>
                                {[
                                    t.isMandatoryInApp && 'In-app',
                                    t.isMandatoryEmail && 'Email',
                                    t.isMandatorySms && 'SMS',
                                    t.isMandatoryPush && 'Push',
                                ].filter(Boolean).join(', ') || '—'}
                            </td>
                            <td style={{ textAlign: 'center', padding: '8px 12px' }}>
                                {t.isActive ? '✓' : '—'}
                            </td>
                            <td style={{ padding: '8px 12px', whiteSpace: 'nowrap' }}>
                                <button onClick={() => handleEdit(t)}
                                    style={{ marginRight: 8, padding: '4px 10px', cursor: 'pointer', border: '1px solid #ced4da', borderRadius: 4 }}>
                                    ✏️
                                </button>
                                <button onClick={() => handleDelete(t.id)}
                                    style={{ padding: '4px 10px', cursor: 'pointer', border: '1px solid #ced4da', borderRadius: 4, color: '#d9534f' }}>
                                    🗑️
                                </button>
                            </td>
                        </tr>
                    ))}
                    {templates.length === 0 && (
                        <tr>
                            <td colSpan={5} style={{ padding: '16px 12px', color: '#999', textAlign: 'center' }}>
                                Шаблоны не настроены
                            </td>
                        </tr>
                    )}
                </tbody>
            </table>
        </div>
    );
}
