import { useState, useEffect } from 'react';
import * as api from '../../api/bpmApi';

// ─── Типы конфигурации ────────────────────────────────────────────────────────

type BoundaryEventType = 'timer' | 'error' | 'signal' | 'message';

interface EscalationConfig {
    triggerType: 'overdue' | 'manual';
    delayMinutes: number;
    recipients: string[];
    action: 'reassign' | 'notify' | 'complete';
}

interface BoundaryEventConfig {
    eventType: BoundaryEventType;
    isInterrupting: boolean;
    // Для timer — expr
    timerType?: 'date' | 'duration' | 'cycle';
    timerExpression?: string;
    // Для error — код ошибки
    errorCode?: string;
    // Для signal — ID из реестра
    signalRefId?: string;
    signalCode?: string;
    // Для message — ID из реестра
    messageRefId?: string;
    messageCode?: string;
    // Маршрут эскалации
    escalation?: EscalationConfig;
}

const DEFAULT_CONFIG: BoundaryEventConfig = {
    eventType: 'timer',
    isInterrupting: true,
    timerType: 'duration',
    timerExpression: 'PT1H',
};

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * BoundaryEventTab — вкладка конфигурации граничного события (BoundaryEvent).
 * Настройка типа, прерывающего/непрерывающего, ссылок на сигнал/сообщение, эскалация.
 */
export function BoundaryEventTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<BoundaryEventConfig>({ ...DEFAULT_CONFIG });
    const [signals, setSignals] = useState<api.BpmSignalDto[]>([]);
    const [messages, setMessages] = useState<api.BpmMessageDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    // ─── Загрузка ─────────────────────────────────────────────────────────────

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const [rawConfig, sigs, msgs] = await Promise.all([
                    api.getElementConfig(token, processId, elementId),
                    api.getSignals(token),
                    api.getMessages(token),
                ]);
                setSignals(sigs);
                setMessages(msgs);
                if (rawConfig?.configJson) {
                    const parsed = JSON.parse(rawConfig.configJson) as Partial<BoundaryEventConfig>;
                    setConfig({ ...DEFAULT_CONFIG, ...parsed });
                } else {
                    setConfig({ ...DEFAULT_CONFIG });
                }
            } catch { /* ошибка не критична */ }
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
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(config));
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally { setSaving(false); }
    };

    const updateEscalation = (field: keyof EscalationConfig, value: unknown) =>
        setConfig(prev => ({
            ...prev,
            escalation: {
                triggerType: 'overdue',
                delayMinutes: 60,
                recipients: [],
                action: 'notify',
                ...(prev.escalation ?? {}),
                [field]: value,
            },
        }));

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    return (
        <div className="bpp-form">
            {/* Тип граничного события */}
            <div className="bpp-field">
                <label className="bpp-label">Тип события</label>
                <select
                    className="bpp-select"
                    value={config.eventType}
                    onChange={e => setConfig(prev => ({ ...DEFAULT_CONFIG, eventType: e.target.value as BoundaryEventType, isInterrupting: prev.isInterrupting }))}
                >
                    <option value="timer">Таймер</option>
                    <option value="error">Ошибка</option>
                    <option value="signal">Сигнал</option>
                    <option value="message">Сообщение</option>
                </select>
            </div>

            {/* Прерывающее / непрерывающее */}
            <div className="bpp-field bpp-field--checkbox">
                <input
                    type="checkbox"
                    id="bev-interrupting"
                    checked={config.isInterrupting}
                    onChange={e => setConfig(prev => ({ ...prev, isInterrupting: e.target.checked }))}
                />
                <label htmlFor="bev-interrupting">Прерывающее</label>
            </div>

            {/* Таймер */}
            {config.eventType === 'timer' && (
                <>
                    <div className="bpp-field">
                        <label className="bpp-label">Вид таймера</label>
                        <select
                            className="bpp-select"
                            value={config.timerType ?? 'duration'}
                            onChange={e => setConfig(prev => ({ ...prev, timerType: e.target.value as 'date' | 'duration' | 'cycle' }))}
                        >
                            <option value="date">Дата (ISO 8601)</option>
                            <option value="duration">Длительность (PT...)</option>
                            <option value="cycle">Цикл (cron / R-cycle)</option>
                        </select>
                    </div>
                    <div className="bpp-field">
                        <label className="bpp-label">Выражение</label>
                        <input
                            className="bpp-input"
                            placeholder={config.timerType === 'duration' ? 'PT1H30M' : config.timerType === 'cycle' ? 'R3/PT10M' : '2025-12-31T23:59:00Z'}
                            value={config.timerExpression ?? ''}
                            onChange={e => setConfig(prev => ({ ...prev, timerExpression: e.target.value }))}
                        />
                    </div>
                </>
            )}

            {/* Ошибка */}
            {config.eventType === 'error' && (
                <div className="bpp-field">
                    <label className="bpp-label">Код ошибки</label>
                    <input
                        className="bpp-input"
                        placeholder="Например: PAYMENT_FAILED"
                        value={config.errorCode ?? ''}
                        onChange={e => setConfig(prev => ({ ...prev, errorCode: e.target.value }))}
                    />
                </div>
            )}

            {/* Сигнал */}
            {config.eventType === 'signal' && (
                <div className="bpp-field">
                    <label className="bpp-label">Сигнал</label>
                    <select
                        className="bpp-select"
                        value={config.signalRefId ?? ''}
                        onChange={e => {
                            const sig = signals.find(s => s.id === e.target.value);
                            setConfig(prev => ({ ...prev, signalRefId: e.target.value, signalCode: sig?.code ?? '' }));
                        }}
                    >
                        <option value="">— Выберите из реестра —</option>
                        {signals.map(s => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
                    </select>
                </div>
            )}

            {/* Сообщение */}
            {config.eventType === 'message' && (
                <div className="bpp-field">
                    <label className="bpp-label">Сообщение</label>
                    <select
                        className="bpp-select"
                        value={config.messageRefId ?? ''}
                        onChange={e => {
                            const msg = messages.find(m => m.id === e.target.value);
                            setConfig(prev => ({ ...prev, messageRefId: e.target.value, messageCode: msg?.code ?? '' }));
                        }}
                    >
                        <option value="">— Выберите из реестра —</option>
                        {messages.map(m => <option key={m.id} value={m.id}>{m.name} ({m.code})</option>)}
                    </select>
                </div>
            )}

            {/* Маршрут эскалации */}
            <div className="bpp-section">
                <div className="bpp-section-header">
                    <span>Маршрут эскалации</span>
                    {!config.escalation && (
                        <button className="bpp-btn-add" onClick={() => updateEscalation('triggerType', 'overdue')}>
                            Настроить
                        </button>
                    )}
                </div>
                {config.escalation && (
                    <>
                        <div className="bpp-field">
                            <label className="bpp-label">Триггер</label>
                            <select
                                className="bpp-select"
                                value={config.escalation.triggerType}
                                onChange={e => updateEscalation('triggerType', e.target.value)}
                            >
                                <option value="overdue">Просрочка задачи</option>
                                <option value="manual">Ручной</option>
                            </select>
                        </div>
                        {config.escalation.triggerType === 'overdue' && (
                            <div className="bpp-field">
                                <label className="bpp-label">Задержка (мин.)</label>
                                <input
                                    className="bpp-input"
                                    type="number"
                                    min={0}
                                    value={config.escalation.delayMinutes}
                                    onChange={e => updateEscalation('delayMinutes', parseInt(e.target.value) || 0)}
                                />
                            </div>
                        )}
                        <div className="bpp-field">
                            <label className="bpp-label">Получатели (через запятую)</label>
                            <input
                                className="bpp-input"
                                placeholder="manager, supervisor"
                                value={config.escalation.recipients.join(', ')}
                                onChange={e => updateEscalation('recipients', e.target.value.split(',').map(s => s.trim()).filter(Boolean))}
                            />
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Действие</label>
                            <select
                                className="bpp-select"
                                value={config.escalation.action}
                                onChange={e => updateEscalation('action', e.target.value)}
                            >
                                <option value="reassign">Переназначить задачу</option>
                                <option value="notify">Только уведомить</option>
                                <option value="complete">Завершить задачу</option>
                            </select>
                        </div>
                        <button
                            className="bpp-btn-danger"
                            style={{ fontSize: 11, marginTop: 4 }}
                            onClick={() => setConfig(prev => ({ ...prev, escalation: undefined }))}
                        >
                            Убрать маршрут
                        </button>
                    </>
                )}
            </div>

            {error && <div className="bpp-error">{error}</div>}

            <div className="bpp-actions">
                <button className="bpp-btn-primary" onClick={handleSave} disabled={saving}>
                    {saving ? 'Сохранение...' : saved ? '✓ Сохранено' : 'Сохранить'}
                </button>
            </div>
        </div>
    );
}
