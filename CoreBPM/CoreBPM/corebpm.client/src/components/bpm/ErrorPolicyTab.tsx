import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import './ErrorPolicyTab.css';

interface ErrorPolicy {
    maxRetries: number;
    retryDelaySeconds: number;
    boundaryErrorCode: string;
    useBoundaryErrorEvent: boolean;
}

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

const DEFAULTS: ErrorPolicy = {
    maxRetries: 9,
    retryDelaySeconds: 60,
    boundaryErrorCode: '',
    useBoundaryErrorEvent: false,
};

/** Вкладка «Обработка ошибок» в BpmPropertiesPanel для ServiceTask / ScriptTask (FR-BPM-02.5). */
export function ErrorPolicyTab({ processId, token, elementId }: Props) {
    const [policy, setPolicy] = useState<ErrorPolicy>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Record<string, unknown>;
                    if (parsed.errorPolicy) {
                        setPolicy({ ...DEFAULTS, ...(parsed.errorPolicy as Partial<ErrorPolicy>) });
                    }
                } catch { /* невалидный JSON */ }
            }
        } catch { /* элемент новый */ }
        setDirty(false);
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const save = async (updated: ErrorPolicy) => {
        setSaving(true);
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            let base: Record<string, unknown> = {};
            if (dto) {
                try { base = JSON.parse(dto.configJson); } catch { /* empty */ }
            }
            const configJson = JSON.stringify({ ...base, errorPolicy: updated });
            await api.upsertElementConfig(token, processId, elementId, configJson);
            setDirty(false);
        } catch { /* ошибка сохранения */ }
        finally { setSaving(false); }
    };

    const update = (patch: Partial<ErrorPolicy>) => {
        const updated = { ...policy, ...patch };
        setPolicy(updated);
        setDirty(true);
    };

    return (
        <div className="ep-root">
            <div className="ep-section">
                <h4 className="ep-section-title">Политика повторных попыток</h4>

                <div className="ep-field">
                    <label className="ep-label">
                        Максимум попыток
                        <span className="ep-hint"> (0 — без повторов, макс. 9)</span>
                    </label>
                    <input
                        className="ep-input ep-input--sm"
                        type="number"
                        min={0}
                        max={9}
                        value={policy.maxRetries}
                        onChange={e => update({ maxRetries: Math.min(9, Math.max(0, parseInt(e.target.value) || 0)) })}
                    />
                </div>

                <div className="ep-field">
                    <label className="ep-label">Задержка между попытками (секунды)</label>
                    <input
                        className="ep-input ep-input--sm"
                        type="number"
                        min={1}
                        max={3600}
                        value={policy.retryDelaySeconds}
                        onChange={e => update({ retryDelaySeconds: Math.max(1, parseInt(e.target.value) || 60) })}
                    />
                </div>
            </div>

            <div className="ep-section">
                <h4 className="ep-section-title">Граничное событие ошибки</h4>

                <label className="ep-checkbox-label">
                    <input
                        type="checkbox"
                        checked={policy.useBoundaryErrorEvent}
                        onChange={e => update({ useBoundaryErrorEvent: e.target.checked })}
                    />
                    <span>Использовать граничное событие ошибки</span>
                </label>
                <p className="ep-desc">
                    Если задача завершилась ошибкой после исчерпания попыток, активируется присоединённое
                    граничное событие ошибки (Error Boundary Event). Экземпляр не переходит в статус «Ошибка».
                </p>

                {policy.useBoundaryErrorEvent && (
                    <div className="ep-field">
                        <label className="ep-label">Код ошибки (errorCode)</label>
                        <input
                            className="ep-input"
                            type="text"
                            placeholder="Например: SERVICE_ERROR"
                            value={policy.boundaryErrorCode}
                            onChange={e => update({ boundaryErrorCode: e.target.value })}
                        />
                        <span className="ep-hint">Оставьте пустым для перехвата любой ошибки</span>
                    </div>
                )}
            </div>

            {dirty && (
                <div className="ep-save-bar">
                    <button
                        className="ep-save-btn"
                        onClick={() => save(policy)}
                        disabled={saving}
                    >
                        {saving ? 'Сохранение…' : 'Сохранить'}
                    </button>
                </div>
            )}
        </div>
    );
}
