import { useState, useEffect } from 'react';
import * as api from '../../api/bpmApi';

// ─── Типы конфигурации ────────────────────────────────────────────────────────

type EventKind = 'signal' | 'message' | 'other';

interface SignalMessageConfig {
    kind: EventKind;
    refId?: string;     // ID записи в реестре bpm_signals / bpm_messages
    refCode?: string;   // Код сигнала/сообщения (проставляется в BPMN XML)
    // Для throw-событий — маппинг переменных в полезную нагрузку
    payloadMappings: PayloadMapping[];
}

interface PayloadMapping {
    variableName: string;
    payloadField: string;
}

const DEFAULT_CONFIG: SignalMessageConfig = {
    kind: 'signal',
    refId: '',
    refCode: '',
    payloadMappings: [],
};

// ─── Пропсы ───────────────────────────────────────────────────────────────────

interface Props {
    processId: string;
    token: string;
    elementId: string;
    /** true — событие throw (генерирует), false — catch (ловит) */
    isThrow: boolean;
    /** Вид события по умолчанию (определяется из BPMN eventDefinition) */
    defaultKind?: EventKind;
}

// ─── Компонент ───────────────────────────────────────────────────────────────

/**
 * SignalMessageEventTab — вкладка конфигурации событий сигнала и сообщения.
 * Позволяет выбрать сигнал/сообщение из глобального реестра и настроить маппинг данных.
 */
export function SignalMessageEventTab({ processId, token, elementId, isThrow, defaultKind = 'signal' }: Props) {
    const [config, setConfig] = useState<SignalMessageConfig>({ ...DEFAULT_CONFIG, kind: defaultKind });
    const [signals, setSignals] = useState<api.BpmSignalDto[]>([]);
    const [messages, setMessages] = useState<api.BpmMessageDto[]>([]);
    const [variables, setVariables] = useState<api.BpmProcessVariableDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [saved, setSaved] = useState(false);

    // ─── Загрузка данных ──────────────────────────────────────────────────────

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const [rawConfig, sigs, msgs, vars] = await Promise.all([
                    api.getElementConfig(token, processId, elementId),
                    api.getSignals(token),
                    api.getMessages(token),
                    api.getVariables(token, processId),
                ]);
                setSignals(sigs);
                setMessages(msgs);
                setVariables(vars);
                if (rawConfig?.configJson) {
                    const parsed = JSON.parse(rawConfig.configJson) as Partial<SignalMessageConfig>;
                    setConfig({
                        kind: parsed.kind ?? defaultKind,
                        refId: parsed.refId ?? '',
                        refCode: parsed.refCode ?? '',
                        payloadMappings: parsed.payloadMappings ?? [],
                    });
                } else {
                    setConfig({ ...DEFAULT_CONFIG, kind: defaultKind });
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

    // ─── Payload mappings ─────────────────────────────────────────────────────

    const addMapping = () =>
        setConfig(prev => ({ ...prev, payloadMappings: [...prev.payloadMappings, { variableName: '', payloadField: '' }] }));

    const updateMapping = (index: number, field: keyof PayloadMapping, value: string) =>
        setConfig(prev => ({
            ...prev,
            payloadMappings: prev.payloadMappings.map((m, i) => i === index ? { ...m, [field]: value } : m),
        }));

    const removeMapping = (index: number) =>
        setConfig(prev => ({ ...prev, payloadMappings: prev.payloadMappings.filter((_, i) => i !== index) }));

    // ─── Выбор записи из реестра ─────────────────────────────────────────────

    const handleRefChange = (refId: string) => {
        const code = config.kind === 'signal'
            ? signals.find(s => s.id === refId)?.code ?? ''
            : messages.find(m => m.id === refId)?.code ?? '';
        setConfig(prev => ({ ...prev, refId, refCode: code }));
    };

    const registry = config.kind === 'signal' ? signals : messages;

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    return (
        <div className="bpp-form">
            {/* Тип события */}
            <div className="bpp-field">
                <label className="bpp-label">Тип</label>
                <select
                    className="bpp-select"
                    value={config.kind}
                    onChange={e => setConfig(prev => ({ ...prev, kind: e.target.value as EventKind, refId: '', refCode: '' }))}
                >
                    <option value="signal">Сигнал</option>
                    <option value="message">Сообщение</option>
                </select>
            </div>

            {/* Выбор из реестра */}
            <div className="bpp-field">
                <label className="bpp-label">{config.kind === 'signal' ? 'Сигнал' : 'Сообщение'}</label>
                <select
                    className="bpp-select"
                    value={config.refId ?? ''}
                    onChange={e => handleRefChange(e.target.value)}
                >
                    <option value="">— Выберите из реестра —</option>
                    {registry.map(item => (
                        <option key={item.id} value={item.id}>{item.name} ({item.code})</option>
                    ))}
                </select>
                {registry.length === 0 && (
                    <p style={{ fontSize: 11, color: '#9ca3af', marginTop: 4 }}>
                        Реестр пуст. Добавьте {config.kind === 'signal' ? 'сигналы' : 'сообщения'} через раздел настроек.
                    </p>
                )}
            </div>

            {/* Код (readonly — берётся из реестра) */}
            {config.refCode && (
                <div className="bpp-field">
                    <label className="bpp-label">Код</label>
                    <input className="bpp-input" value={config.refCode} readOnly style={{ background: '#f3f4f6' }} />
                </div>
            )}

            {/* Маппинг полезной нагрузки — только для throw-событий */}
            {isThrow && (
                <div className="bpp-section">
                    <div className="bpp-section-header">
                        <span>Маппинг полезной нагрузки</span>
                        <button className="bpp-btn-add" onClick={addMapping}>+ Добавить</button>
                    </div>
                    {config.payloadMappings.length === 0 && (
                        <p style={{ fontSize: 12, color: '#6b7280' }}>Нет маппингов. Данные не будут включены в нагрузку события.</p>
                    )}
                    {config.payloadMappings.map((mapping, index) => (
                        <div key={index} className="bpp-row" style={{ alignItems: 'flex-end', gap: 6 }}>
                            <div className="bpp-field" style={{ flex: 1 }}>
                                <label className="bpp-label">Переменная процесса</label>
                                <select
                                    className="bpp-select"
                                    value={mapping.variableName}
                                    onChange={e => updateMapping(index, 'variableName', e.target.value)}
                                >
                                    <option value="">— Выберите —</option>
                                    {variables.map(v => (
                                        <option key={v.id} value={v.name}>{v.name}</option>
                                    ))}
                                </select>
                            </div>
                            <div className="bpp-field" style={{ flex: 1 }}>
                                <label className="bpp-label">Поле нагрузки</label>
                                <input
                                    className="bpp-input"
                                    placeholder="fieldName"
                                    value={mapping.payloadField}
                                    onChange={e => updateMapping(index, 'payloadField', e.target.value)}
                                />
                            </div>
                            <button
                                className="bpp-btn-remove"
                                onClick={() => removeMapping(index)}
                                style={{ marginBottom: 1 }}
                                title="Удалить маппинг"
                            >✕</button>
                        </div>
                    ))}
                </div>
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
