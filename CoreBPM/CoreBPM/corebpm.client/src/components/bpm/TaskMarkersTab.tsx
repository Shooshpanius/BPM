import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

type MarkerMiType = 'None' | 'Sequential' | 'Parallel';

interface TaskMarkersConfig {
    /** Признак цикла (Loop marker) */
    loopEnabled: boolean;
    /** Условие продолжения цикла (EL-выражение) */
    loopCondition: string;
    /** Максимальное количество итераций (0 = без ограничений) */
    loopMaxIterations: number;
    /** Тип множественного экземпляра */
    miType: MarkerMiType;
    /** Выражение для коллекции (возвращает список элементов) */
    miCollectionExpression: string;
    /** Имя переменной, в которую записывается текущий элемент коллекции */
    miItemVariable: string;
    /** Условие завершения (при истине прекращает порождать новые экземпляры) */
    miCompletionCondition: string;
    /** Признак компенсации */
    compensationEnabled: boolean;
}

const DEFAULTS: TaskMarkersConfig = {
    loopEnabled: false,
    loopCondition: '',
    loopMaxIterations: 0,
    miType: 'None',
    miCollectionExpression: '',
    miItemVariable: '',
    miCompletionCondition: '',
    compensationEnabled: false,
};

const MI_TYPE_LABELS: Record<MarkerMiType, string> = {
    None: 'Нет',
    Sequential: 'Последовательный (MI Seq)',
    Parallel: 'Параллельный (MI Par)',
};

/** Вкладка «Маркеры» для задач: Loop, MI Sequential, MI Parallel, Компенсация. */
export function TaskMarkersTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<TaskMarkersConfig>(DEFAULTS);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as { markers?: Partial<TaskMarkersConfig> };
                    if (parsed.markers) {
                        setConfig({ ...DEFAULTS, ...parsed.markers });
                    } else {
                        setConfig(DEFAULTS);
                    }
                } catch { /* невалидный JSON */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка */ }
    }, [token, processId, elementId]);

    useEffect(() => { load(); }, [load]);

    const update = <K extends keyof TaskMarkersConfig>(key: K, value: TaskMarkersConfig[K]) => {
        setConfig(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const save = async () => {
        // Маркеры хранятся под ключом "markers" внутри ConfigJson,
        // сохраняем поверх остальных полей конфига элемента
        setSaving(true);
        setError(null);
        try {
            // Загружаем текущий конфиг, чтобы не затереть другие поля (исполнитель и т.д.)
            const existing = await api.getElementConfig(token, processId, elementId);
            const currentJson = existing ? JSON.parse(existing.configJson) as Record<string, unknown> : {};
            const merged = { ...currentJson, markers: config };
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(merged));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    // Маркеры Loop и MI взаимоисключающие
    const handleLoopToggle = (enabled: boolean) => {
        setConfig(prev => ({
            ...prev,
            loopEnabled: enabled,
            miType: enabled ? 'None' : prev.miType,
        }));
        setDirty(true);
    };

    const handleMiTypeChange = (miType: MarkerMiType) => {
        setConfig(prev => ({
            ...prev,
            miType,
            loopEnabled: miType !== 'None' ? false : prev.loopEnabled,
        }));
        setDirty(true);
    };

    return (
        <div>
            {error && <p className="bpp-error">{error}</p>}

            {/* ─── Цикл (Loop) ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Цикл (Loop)</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.loopEnabled}
                        onChange={e => handleLoopToggle(e.target.checked)}
                    />
                    <span>Включить маркер цикла</span>
                </label>

                {config.loopEnabled && (
                    <>
                        <div className="bpp-field" style={{ marginTop: 8 }}>
                            <label className="bpp-label">Условие цикла (EL-выражение)</label>
                            <input
                                className="bpp-input"
                                value={config.loopCondition}
                                placeholder={'${variables.counter < 10}'}
                                onChange={e => update('loopCondition', e.target.value)}
                            />
                            <span className="bpp-hint">Задача повторяется, пока условие истинно.</span>
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Максимум итераций (0 = без ограничений)</label>
                            <input
                                className="bpp-input"
                                type="number"
                                min={0}
                                value={config.loopMaxIterations}
                                onChange={e => update('loopMaxIterations', parseInt(e.target.value, 10) || 0)}
                            />
                        </div>
                    </>
                )}
            </div>

            {/* ─── Множественный экземпляр (MI) ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Множественный экземпляр (MI)</div>
                <div className="bpp-field">
                    <label className="bpp-label">Тип</label>
                    <select
                        className="bpp-select"
                        value={config.miType}
                        onChange={e => handleMiTypeChange(e.target.value as MarkerMiType)}
                    >
                        {(Object.keys(MI_TYPE_LABELS) as MarkerMiType[]).map(v => (
                            <option key={v} value={v}>{MI_TYPE_LABELS[v]}</option>
                        ))}
                    </select>
                </div>

                {config.miType !== 'None' && (
                    <>
                        <div className="bpp-field">
                            <label className="bpp-label required">Выражение коллекции</label>
                            <input
                                className="bpp-input"
                                value={config.miCollectionExpression}
                                placeholder={'${variables.approvers}'}
                                onChange={e => update('miCollectionExpression', e.target.value)}
                            />
                            <span className="bpp-hint">
                                EL-выражение, возвращающее список. Для каждого элемента создаётся отдельный экземпляр задачи.
                            </span>
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Переменная текущего элемента</label>
                            <input
                                className="bpp-input"
                                value={config.miItemVariable}
                                placeholder="approver"
                                onChange={e => update('miItemVariable', e.target.value)}
                            />
                            <span className="bpp-hint">
                                Имя переменной, в которую записывается текущий элемент коллекции в контексте каждого экземпляра.
                            </span>
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Условие завершения</label>
                            <input
                                className="bpp-input"
                                value={config.miCompletionCondition}
                                placeholder={'${nrOfCompletedInstances >= 2}'}
                                onChange={e => update('miCompletionCondition', e.target.value)}
                            />
                            <span className="bpp-hint">
                                При истинности условия — оставшиеся экземпляры задачи прерываются.
                                Пустое значение — ждать завершения всех экземпляров.
                            </span>
                        </div>
                    </>
                )}
            </div>

            {/* ─── Компенсация ─── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Компенсация</div>
                <label className="bpp-checkbox-row">
                    <input
                        type="checkbox"
                        checked={config.compensationEnabled}
                        onChange={e => update('compensationEnabled', e.target.checked)}
                    />
                    <span>Включить маркер компенсации</span>
                </label>
                {config.compensationEnabled && (
                    <p className="bpp-hint" style={{ marginTop: 4 }}>
                        Задача помечена как компенсирующая. Она будет вызвана при откате транзакционного подпроцесса.
                        Свяжите её с исходной задачей через ассоциацию компенсации на диаграмме.
                    </p>
                )}
            </div>

            <div className="bpp-btn-row">
                <button
                    className="bpp-btn bpp-btn-primary"
                    onClick={save}
                    disabled={saving || !dirty}
                >
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
