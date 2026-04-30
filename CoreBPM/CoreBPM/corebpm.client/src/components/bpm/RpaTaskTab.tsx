import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type RpaUnavailableAction = 'WaitAndRetry' | 'FailTask' | 'SkipTask';

interface RpaTaskConfig {
    /** Идентификатор/имя RPA-агента из реестра */
    agentId?: string;
    /** Имя агента для отображения */
    agentName?: string;
    /** Таймаут ожидания выполнения (секунды) */
    timeoutSeconds?: number;
    /** Поведение при недоступности агента */
    onUnavailable: RpaUnavailableAction;
    /** Маппинг входных переменных: varName → paramName */
    inputMapping: Record<string, string>;
    /** Маппинг выходных результатов: paramName → varName */
    outputMapping: Record<string, string>;
}

const DEFAULTS: RpaTaskConfig = {
    timeoutSeconds: 300,
    onUnavailable: 'WaitAndRetry',
    inputMapping: {},
    outputMapping: {},
};

const UNAVAIL_LABELS: Record<RpaUnavailableAction, string> = {
    WaitAndRetry: 'Ждать и повторить',
    FailTask: 'Завершить с ошибкой',
    SkipTask: 'Пропустить задачу',
};

/** Вкладка «RPA-задача» в BpmPropertiesPanel. */
export function RpaTaskTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<RpaTaskConfig>(DEFAULTS);
    const [variables, setVariables] = useState<api.BpmProcessVariableDto[]>([]);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const [newInputVar, setNewInputVar] = useState('');
    const [newInputParam, setNewInputParam] = useState('');
    const [newOutputParam, setNewOutputParam] = useState('');
    const [newOutputVar, setNewOutputVar] = useState('');

    const load = useCallback(async () => {
        try {
            const [vars, cfg] = await Promise.all([
                api.getVariables(token, processId),
                api.getElementConfig(token, processId, elementId),
            ]);
            setVariables(vars);
            if (cfg?.configJson) {
                try {
                    const parsed = JSON.parse(cfg.configJson);
                    if (parsed.rpa) setConfig({ ...DEFAULTS, ...parsed.rpa });
                } catch { /* нет конфига */ }
            }
        } catch { /* игнорируем */ }
    }, [token, processId, elementId]);

    useEffect(() => { void load(); }, [load]);

    const handleChange = <K extends keyof RpaTaskConfig>(key: K, value: RpaTaskConfig[K]) => {
        setConfig(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const addInputMapping = () => {
        if (!newInputVar || !newInputParam) return;
        setConfig(prev => ({ ...prev, inputMapping: { ...prev.inputMapping, [newInputVar]: newInputParam } }));
        setNewInputVar('');
        setNewInputParam('');
        setDirty(true);
    };

    const removeInputMapping = (key: string) => {
        setConfig(prev => {
            const next = { ...prev.inputMapping };
            delete next[key];
            return { ...prev, inputMapping: next };
        });
        setDirty(true);
    };

    const addOutputMapping = () => {
        if (!newOutputParam || !newOutputVar) return;
        setConfig(prev => ({ ...prev, outputMapping: { ...prev.outputMapping, [newOutputParam]: newOutputVar } }));
        setNewOutputParam('');
        setNewOutputVar('');
        setDirty(true);
    };

    const removeOutputMapping = (key: string) => {
        setConfig(prev => {
            const next = { ...prev.outputMapping };
            delete next[key];
            return { ...prev, outputMapping: next };
        });
        setDirty(true);
    };

    const handleSave = async () => {
        setSaving(true);
        setError(null);
        try {
            let existingJson: Record<string, unknown> = {};
            try {
                const existing = await api.getElementConfig(token, processId, elementId);
                if (existing?.configJson) existingJson = JSON.parse(existing.configJson);
            } catch { /* нет конфига */ }
            existingJson.rpa = config;
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(existingJson));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const varOptions = variables.map(v => (
        <option key={v.id} value={v.name}>{v.name} ({v.variableType})</option>
    ));

    return (
        <div className="bpp-tab-content">
            {/* Информационный баннер */}
            <div className="bpp-info-banner" style={{ background: '#fef3c7', borderLeft: '3px solid #f59e0b', padding: '8px 12px', borderRadius: 4, marginBottom: 12, fontSize: 12 }}>
                ⚠️ Реестр RPA-агентов будет доступен после реализации модуля интеграций (FR-INT).
                Здесь можно предварительно настроить маппинг переменных.
            </div>

            <div className="bpp-field-group">
                <label className="bpp-label">ID / имя RPA-агента</label>
                <input
                    className="bpp-input"
                    placeholder="например, rpa-agent-001"
                    value={config.agentId ?? ''}
                    onChange={e => handleChange('agentId', e.target.value)}
                />
            </div>

            <div className="bpp-field-group">
                <label className="bpp-label">Таймаут (секунды)</label>
                <input
                    className="bpp-input"
                    type="number"
                    min={1}
                    max={86400}
                    value={config.timeoutSeconds ?? 300}
                    onChange={e => handleChange('timeoutSeconds', parseInt(e.target.value, 10) || 300)}
                />
            </div>

            <div className="bpp-field-group">
                <label className="bpp-label">При недоступности агента</label>
                <select
                    className="bpp-input"
                    value={config.onUnavailable}
                    onChange={e => handleChange('onUnavailable', e.target.value as RpaUnavailableAction)}
                >
                    {(Object.keys(UNAVAIL_LABELS) as RpaUnavailableAction[]).map(k => (
                        <option key={k} value={k}>{UNAVAIL_LABELS[k]}</option>
                    ))}
                </select>
            </div>

            {/* Маппинг входных переменных */}
            <div className="bpp-field-group">
                <label className="bpp-label">Входные параметры (переменная → параметр агента)</label>
                {Object.entries(config.inputMapping).map(([varName, paramName]) => (
                    <div key={varName} style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 4 }}>
                        <span style={{ flex: 1, fontSize: 12 }}>{varName}</span>
                        <span style={{ color: '#9ca3af' }}>→</span>
                        <span style={{ flex: 1, fontSize: 12 }}>{paramName}</span>
                        <button className="bpp-btn-icon" onClick={() => removeInputMapping(varName)} type="button" title="Удалить">✕</button>
                    </div>
                ))}
                <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
                    <select className="bpp-input" value={newInputVar} onChange={e => setNewInputVar(e.target.value)} style={{ flex: 1 }}>
                        <option value="">— переменная —</option>
                        {varOptions}
                    </select>
                    <input className="bpp-input" placeholder="параметр" value={newInputParam} onChange={e => setNewInputParam(e.target.value)} style={{ flex: 1 }} />
                    <button className="bpp-btn" onClick={addInputMapping} type="button" disabled={!newInputVar || !newInputParam}>+</button>
                </div>
            </div>

            {/* Маппинг выходных результатов */}
            <div className="bpp-field-group">
                <label className="bpp-label">Выходные результаты (параметр агента → переменная)</label>
                {Object.entries(config.outputMapping).map(([paramName, varName]) => (
                    <div key={paramName} style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 4 }}>
                        <span style={{ flex: 1, fontSize: 12 }}>{paramName}</span>
                        <span style={{ color: '#9ca3af' }}>→</span>
                        <span style={{ flex: 1, fontSize: 12 }}>{varName}</span>
                        <button className="bpp-btn-icon" onClick={() => removeOutputMapping(paramName)} type="button" title="Удалить">✕</button>
                    </div>
                ))}
                <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
                    <input className="bpp-input" placeholder="параметр" value={newOutputParam} onChange={e => setNewOutputParam(e.target.value)} style={{ flex: 1 }} />
                    <select className="bpp-input" value={newOutputVar} onChange={e => setNewOutputVar(e.target.value)} style={{ flex: 1 }}>
                        <option value="">— переменная —</option>
                        {varOptions}
                    </select>
                    <button className="bpp-btn" onClick={addOutputMapping} type="button" disabled={!newOutputParam || !newOutputVar}>+</button>
                </div>
            </div>

            {error && <p style={{ color: '#ef4444', fontSize: 12 }}>{error}</p>}

            <button
                className="bpp-btn bpp-btn--primary"
                disabled={!dirty || saving}
                onClick={handleSave}
                type="button"
            >
                {saving ? 'Сохранение…' : 'Сохранить'}
            </button>
        </div>
    );
}
