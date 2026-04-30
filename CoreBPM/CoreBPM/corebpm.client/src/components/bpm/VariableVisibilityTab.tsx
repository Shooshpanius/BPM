import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type DisplayMode = 'ByTask' | 'FlatList';

interface VariableVisibilityConfig {
    /** Список идентификаторов переменных, видимых в этой ЗО */
    visibleVariableIds: string[];
    /** Режим отображения: по задачам / плоский список */
    displayMode: DisplayMode;
}

const DEFAULTS: VariableVisibilityConfig = {
    visibleVariableIds: [],
    displayMode: 'ByTask',
};

/** Вкладка «Видимость переменных» для дорожки (Lane). */
export function VariableVisibilityTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<VariableVisibilityConfig>(DEFAULTS);
    const [variables, setVariables] = useState<api.BpmProcessVariableDto[]>([]);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Загружаем переменные процесса и текущую конфигурацию видимости
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
                    if (parsed.variableVisibility) {
                        setConfig({ ...DEFAULTS, ...parsed.variableVisibility });
                    }
                } catch { /* пустой конфиг */ }
            }
        } catch { /* игнорируем */ }
    }, [token, processId, elementId]);

    useEffect(() => { void load(); }, [load]);

    const handleToggleVariable = (varId: string) => {
        setConfig(prev => {
            const already = prev.visibleVariableIds.includes(varId);
            return {
                ...prev,
                visibleVariableIds: already
                    ? prev.visibleVariableIds.filter(id => id !== varId)
                    : [...prev.visibleVariableIds, varId],
            };
        });
        setDirty(true);
    };

    const handleSelectAll = () => {
        setConfig(prev => ({ ...prev, visibleVariableIds: variables.map(v => v.id) }));
        setDirty(true);
    };

    const handleClearAll = () => {
        setConfig(prev => ({ ...prev, visibleVariableIds: [] }));
        setDirty(true);
    };

    const handleSave = async () => {
        setSaving(true);
        setError(null);
        try {
            // Читаем существующий конфиг, мержим и сохраняем
            let existingJson: Record<string, unknown> = {};
            try {
                const existing = await api.getElementConfig(token, processId, elementId);
                if (existing?.configJson) existingJson = JSON.parse(existing.configJson);
            } catch { /* нет конфига */ }
            existingJson.variableVisibility = config;
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(existingJson));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="bpp-tab-content">
            <div className="bpp-field-group">
                <label className="bpp-label">Режим отображения</label>
                <select
                    className="bpp-input"
                    value={config.displayMode}
                    onChange={e => {
                        setConfig(prev => ({ ...prev, displayMode: e.target.value as DisplayMode }));
                        setDirty(true);
                    }}
                >
                    <option value="ByTask">По задачам</option>
                    <option value="FlatList">Плоский список</option>
                </select>
            </div>

            <div className="bpp-field-group">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                    <label className="bpp-label" style={{ marginBottom: 0 }}>
                        Видимые переменные
                        {config.visibleVariableIds.length > 0 && (
                            <span style={{ marginLeft: 6, color: '#6b7280', fontWeight: 400 }}>
                                ({config.visibleVariableIds.length} из {variables.length})
                            </span>
                        )}
                    </label>
                    <span style={{ display: 'flex', gap: 6 }}>
                        <button className="bpp-btn-link" onClick={handleSelectAll} type="button">Все</button>
                        <button className="bpp-btn-link" onClick={handleClearAll} type="button">Нет</button>
                    </span>
                </div>
                {variables.length === 0 ? (
                    <p className="bpp-hint">Нет переменных. Добавьте переменные в разделе «Переменные».</p>
                ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {variables.map(v => (
                            <label key={v.id} style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
                                <input
                                    type="checkbox"
                                    checked={config.visibleVariableIds.includes(v.id)}
                                    onChange={() => handleToggleVariable(v.id)}
                                />
                                <span>
                                    <strong>{v.name}</strong>
                                    <span style={{ marginLeft: 4, color: '#9ca3af', fontSize: 11 }}>{v.variableType}</span>
                                    {v.isKeyVariable && <span style={{ marginLeft: 4, color: '#f59e0b', fontSize: 10 }}>★ ключевая</span>}
                                </span>
                            </label>
                        ))}
                    </div>
                )}
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
