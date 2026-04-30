import { useState, useEffect } from 'react';
import * as api from '../../api/bpmApi';

// ─── Типы конфигурации ────────────────────────────────────────────────────────

type EscalationTrigger = 'overdue' | 'manual';
type EscalationAction = 'reassign' | 'notify' | 'complete';
type RecipientType = 'assignee' | 'initiator' | 'role' | 'expression';

interface EscalationRecipient {
    type: RecipientType;
    value: string; // имя роли или EL-выражение
}

interface EscalationConfig {
    enabled: boolean;
    triggerType: EscalationTrigger;
    /** Задержка после наступления просрочки, в минутах */
    delayMinutes: number;
    recipients: EscalationRecipient[];
    action: EscalationAction;
    /** EL-выражение для ручного триггера */
    manualCondition?: string;
}

const DEFAULT_CONFIG: EscalationConfig = {
    enabled: false,
    triggerType: 'overdue',
    delayMinutes: 60,
    recipients: [],
    action: 'notify',
};

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * EscalationTab — вкладка «Эскалация» на Sequence Flow и граничном событии.
 * Настройка триггера, задержки, получателей, действия.
 */
export function EscalationTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<EscalationConfig>({ ...DEFAULT_CONFIG });
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    // ─── Загрузка ─────────────────────────────────────────────────────────────

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const rawConfig = await api.getElementConfig(token, processId, elementId);
                if (rawConfig?.configJson) {
                    const parsed = JSON.parse(rawConfig.configJson) as Record<string, unknown>;
                    const esc = parsed.escalation as Partial<EscalationConfig> | undefined;
                    if (esc) {
                        setConfig({
                            enabled: esc.enabled ?? false,
                            triggerType: esc.triggerType ?? 'overdue',
                            delayMinutes: esc.delayMinutes ?? 60,
                            recipients: esc.recipients ?? [],
                            action: esc.action ?? 'notify',
                            manualCondition: esc.manualCondition,
                        });
                    }
                }
            } catch { /* не критично */ }
            finally { setLoading(false); }
        };
        load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [token, processId, elementId]);

    // ─── Сохранение ───────────────────────────────────────────────────────────

    const handleSave = async () => {
        setSaving(true);
        setError(null);
        try {
            // Сливаем с существующим configJson, чтобы не затереть другие ключи
            const existing = await api.getElementConfig(token, processId, elementId);
            const existingData = existing?.configJson ? JSON.parse(existing.configJson) as Record<string, unknown> : {};
            const merged = { ...existingData, escalation: config };
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(merged));
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally { setSaving(false); }
    };

    // ─── Получатели ───────────────────────────────────────────────────────────

    const addRecipient = () =>
        setConfig(prev => ({
            ...prev,
            recipients: [...prev.recipients, { type: 'assignee', value: '' }],
        }));

    const updateRecipient = (index: number, field: keyof EscalationRecipient, value: string) =>
        setConfig(prev => ({
            ...prev,
            recipients: prev.recipients.map((r, i) => i === index ? { ...r, [field]: value } : r),
        }));

    const removeRecipient = (index: number) =>
        setConfig(prev => ({ ...prev, recipients: prev.recipients.filter((_, i) => i !== index) }));

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    return (
        <div className="bpp-form">
            {/* Включить эскалацию */}
            <div className="bpp-field bpp-field--checkbox">
                <input
                    type="checkbox"
                    id="esc-enabled"
                    checked={config.enabled}
                    onChange={e => setConfig(prev => ({ ...prev, enabled: e.target.checked }))}
                />
                <label htmlFor="esc-enabled">Включить эскалацию</label>
            </div>

            {config.enabled && (
                <>
                    {/* Триггер */}
                    <div className="bpp-field">
                        <label className="bpp-label">Триггер</label>
                        <select
                            className="bpp-select"
                            value={config.triggerType}
                            onChange={e => setConfig(prev => ({ ...prev, triggerType: e.target.value as EscalationTrigger }))}
                        >
                            <option value="overdue">Просрочка задачи</option>
                            <option value="manual">Ручной (условие)</option>
                        </select>
                    </div>

                    {/* Задержка для просрочки */}
                    {config.triggerType === 'overdue' && (
                        <div className="bpp-field">
                            <label className="bpp-label">Задержка после просрочки (мин.)</label>
                            <input
                                className="bpp-input"
                                type="number"
                                min={0}
                                value={config.delayMinutes}
                                onChange={e => setConfig(prev => ({ ...prev, delayMinutes: parseInt(e.target.value) || 0 }))}
                            />
                        </div>
                    )}

                    {/* Условие для ручного триггера */}
                    {config.triggerType === 'manual' && (
                        <div className="bpp-field">
                            <label className="bpp-label">Условие (EL-выражение)</label>
                            <input
                                className="bpp-input"
                                placeholder={`\${переменная == "значение"}`}
                                value={config.manualCondition ?? ''}
                                onChange={e => setConfig(prev => ({ ...prev, manualCondition: e.target.value }))}
                            />
                        </div>
                    )}

                    {/* Получатели */}
                    <div className="bpp-section">
                        <div className="bpp-section-header">
                            <span>Получатели уведомлений</span>
                            <button className="bpp-btn-add" onClick={addRecipient}>+ Добавить</button>
                        </div>
                        {config.recipients.length === 0 && (
                            <p style={{ fontSize: 12, color: '#6b7280' }}>Получатели не указаны.</p>
                        )}
                        {config.recipients.map((r, index) => (
                            <div key={index} className="bpp-row" style={{ alignItems: 'flex-end', gap: 6 }}>
                                <div className="bpp-field" style={{ flex: '0 0 140px' }}>
                                    <label className="bpp-label">Тип</label>
                                    <select
                                        className="bpp-select"
                                        value={r.type}
                                        onChange={e => updateRecipient(index, 'type', e.target.value)}
                                    >
                                        <option value="assignee">Исполнитель</option>
                                        <option value="initiator">Инициатор</option>
                                        <option value="role">Роль</option>
                                        <option value="expression">Выражение</option>
                                    </select>
                                </div>
                                {(r.type === 'role' || r.type === 'expression') && (
                                    <div className="bpp-field" style={{ flex: 1 }}>
                                        <label className="bpp-label">{r.type === 'role' ? 'Имя роли' : 'Выражение'}</label>
                                        <input
                                            className="bpp-input"
                                            placeholder={r.type === 'role' ? 'Manager' : '${variables.approver}'}
                                            value={r.value}
                                            onChange={e => updateRecipient(index, 'value', e.target.value)}
                                        />
                                    </div>
                                )}
                                <button
                                    className="bpp-btn-remove"
                                    onClick={() => removeRecipient(index)}
                                    style={{ marginBottom: 1 }}
                                    title="Удалить"
                                >✕</button>
                            </div>
                        ))}
                    </div>

                    {/* Действие */}
                    <div className="bpp-field">
                        <label className="bpp-label">Действие при эскалации</label>
                        <select
                            className="bpp-select"
                            value={config.action}
                            onChange={e => setConfig(prev => ({ ...prev, action: e.target.value as EscalationAction }))}
                        >
                            <option value="reassign">Переназначить задачу</option>
                            <option value="notify">Только уведомить</option>
                            <option value="complete">Завершить задачу</option>
                        </select>
                    </div>
                </>
            )}

            {error && <div className="bpp-error">{error}</div>}

            <div className="bpp-actions">
                <button className="bpp-btn-primary" onClick={handleSave} disabled={saving}>
                    {saving ? 'Сохранение...' : saved ? '✓ Сохранено' : 'Сохранить'}
                </button>
            </div>
        </div>
    );
}
