import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type NotificationTrigger = 'Created' | 'Overdue' | 'Completed' | 'Reassigned';
type NotificationChannel = 'InApp' | 'Email';
type RecipientType = 'Assignee' | 'Initiator' | 'Role' | 'Expression';

interface NotificationRule {
    id: string;
    trigger: NotificationTrigger;
    recipientType: RecipientType;
    recipientValue: string;
    channels: NotificationChannel[];
    template: string;
}

interface NotificationsConfig {
    rules: NotificationRule[];
}

const newRule = (): NotificationRule => ({
    id: crypto.randomUUID(),
    trigger: 'Overdue',
    recipientType: 'Assignee',
    recipientValue: '',
    channels: ['InApp'],
    template: '',
});

const TRIGGER_LABELS: Record<NotificationTrigger, string> = {
    Created: 'Задача создана',
    Overdue: 'Задача просрочена',
    Completed: 'Задача выполнена',
    Reassigned: 'Задача переназначена',
};

/** Вкладка «Уведомления» для UserTask. */
export function NotificationsTab({ processId, token, elementId }: Props) {
    const [rules, setRules] = useState<NotificationRule[]>([]);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [expanded, setExpanded] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as { notifications?: NotificationsConfig };
                    setRules(parsed.notifications?.rules ?? []);
                } catch { setRules([]); }
            } else {
                setRules([]);
            }
            setDirty(false);
        } catch { setRules([]); }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const addRule = () => {
        const r = newRule();
        setRules(prev => [...prev, r]);
        setExpanded(r.id);
        setDirty(true);
    };

    const removeRule = (id: string) => {
        setRules(prev => prev.filter(r => r.id !== id));
        setDirty(true);
    };

    const updateRule = (id: string, patch: Partial<NotificationRule>) => {
        setRules(prev => prev.map(r => r.id === id ? { ...r, ...patch } : r));
        setDirty(true);
    };

    const toggleChannel = (id: string, channel: NotificationChannel) => {
        setRules(prev => prev.map(r => {
            if (r.id !== id) return r;
            const channels = r.channels.includes(channel)
                ? r.channels.filter(c => c !== channel)
                : [...r.channels, channel];
            return { ...r, channels };
        }));
        setDirty(true);
    };

    const save = async () => {
        setSaving(true);
        try {
            // Читаем текущий конфиг, чтобы не перезаписать другие вкладки (UserTask, ServiceTask и т.д.)
            const existing = await api.getElementConfig(token, processId, elementId);
            let existingConfig: Record<string, unknown> = {};
            if (existing) {
                try { existingConfig = JSON.parse(existing.configJson); } catch { /* невалидный JSON */ }
            }
            const merged = { ...existingConfig, notifications: { rules } };
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(merged));
            setDirty(false);
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            <div className="bpp-group">
                <div className="bpp-group-title">Правила уведомлений</div>
                {rules.length === 0 && (
                    <p className="bpp-hint">Уведомления не настроены. Нажмите «Добавить правило».</p>
                )}
                {rules.map(rule => (
                    <div key={rule.id} style={{ border: '1px solid #e5e7eb', borderRadius: 4, marginBottom: 8 }}>
                        {/* Заголовок правила */}
                        <div
                            style={{
                                display: 'flex', alignItems: 'center', padding: '6px 10px',
                                background: '#f9fafb', cursor: 'pointer', gap: 8,
                            }}
                            onClick={() => setExpanded(prev => prev === rule.id ? null : rule.id)}
                        >
                            <span style={{ flex: 1, fontSize: 12, fontWeight: 500 }}>
                                {TRIGGER_LABELS[rule.trigger]}
                                {rule.channels.map(c => (
                                    <span key={c} className={`bpp-badge ${c === 'InApp' ? 'bpp-badge-blue' : 'bpp-badge-orange'}`}
                                        style={{ marginLeft: 4 }}>{c === 'InApp' ? 'In-app' : 'Email'}</span>
                                ))}
                            </span>
                            <button
                                className="bpp-btn bpp-btn-danger"
                                style={{ padding: '2px 8px', fontSize: 11 }}
                                onClick={e => { e.stopPropagation(); removeRule(rule.id); }}
                            >✕</button>
                        </div>
                        {/* Содержимое правила */}
                        {expanded === rule.id && (
                            <div style={{ padding: '10px 10px 6px' }}>
                                <div className="bpp-field">
                                    <label className="bpp-label">Триггер</label>
                                    <select className="bpp-select" value={rule.trigger}
                                        onChange={e => updateRule(rule.id, { trigger: e.target.value as NotificationTrigger })}>
                                        {(Object.entries(TRIGGER_LABELS) as [NotificationTrigger, string][]).map(([k, v]) => (
                                            <option key={k} value={k}>{v}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="bpp-field">
                                    <label className="bpp-label">Тип получателя</label>
                                    <select className="bpp-select" value={rule.recipientType}
                                        onChange={e => updateRule(rule.id, { recipientType: e.target.value as RecipientType })}>
                                        <option value="Assignee">Исполнитель задачи</option>
                                        <option value="Initiator">Инициатор процесса</option>
                                        <option value="Role">Роль</option>
                                        <option value="Expression">Выражение</option>
                                    </select>
                                </div>
                                {(rule.recipientType === 'Role' || rule.recipientType === 'Expression') && (
                                    <div className="bpp-field">
                                        <label className="bpp-label">
                                            {rule.recipientType === 'Role' ? 'Название роли' : 'Выражение (${variables.manager})'}
                                        </label>
                                        <input className="bpp-input" value={rule.recipientValue}
                                            onChange={e => updateRule(rule.id, { recipientValue: e.target.value })}
                                            placeholder={rule.recipientType === 'Role' ? 'Manager' : '${variables.approver}'} />
                                    </div>
                                )}
                                <div className="bpp-field">
                                    <label className="bpp-label">Каналы доставки</label>
                                    <label className="bpp-checkbox-row">
                                        <input type="checkbox" checked={rule.channels.includes('InApp')}
                                            onChange={() => toggleChannel(rule.id, 'InApp')} />
                                        <span>In-app (в системе)</span>
                                    </label>
                                    <label className="bpp-checkbox-row">
                                        <input type="checkbox" checked={rule.channels.includes('Email')}
                                            onChange={() => toggleChannel(rule.id, 'Email')} />
                                        <span>Email</span>
                                    </label>
                                </div>
                                <div className="bpp-field">
                                    <label className="bpp-label">Шаблон сообщения</label>
                                    <textarea className="bpp-textarea" rows={3} value={rule.template}
                                        onChange={e => updateRule(rule.id, { template: e.target.value })}
                                        placeholder="Задача «{{taskName}}» просрочена. Исполнитель: {{assignee}}." />
                                    <p className="bpp-hint">Используйте {'{{variableName}}'} для подстановки переменных процесса.</p>
                                </div>
                            </div>
                        )}
                    </div>
                ))}
                <button className="bpp-btn" onClick={addRule} style={{ width: '100%', marginTop: 4 }}>
                    + Добавить правило
                </button>
            </div>

            <div className="bpp-btn-row">
                <button className="bpp-btn bpp-btn-primary" onClick={save} disabled={saving || !dirty}>
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
