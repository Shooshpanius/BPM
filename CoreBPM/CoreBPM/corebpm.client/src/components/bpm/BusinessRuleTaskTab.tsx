import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import { getDmnTables, getDmnVersion, getDmnVersions } from '../../api/rulesApi';
import type { DmnTableListItemDto, DmnColumnDto } from '../../api/rulesApi';

interface Props {
    processId: string;
    token: string;
    elementId: string;
}

interface VariableMapping {
    /** ID колонки DMN-таблицы */
    columnId: string;
    /** Имя колонки для отображения */
    columnName: string;
    /** Имя переменной процесса */
    variableName: string;
}

interface BusinessRuleConfig {
    /** ID выбранной DMN-таблицы */
    tableId?: string;
    /** ID опубликованной версии таблицы */
    versionId?: string;
    /** Маппинг входных переменных процесса → входные поля правила */
    inputMappings: VariableMapping[];
    /** Маппинг выходных полей правила → переменные процесса */
    outputMappings: VariableMapping[];
}

const DEFAULTS: BusinessRuleConfig = {
    inputMappings: [],
    outputMappings: [],
};

/** Вкладка «Выполнение» для BusinessRuleTask — выбор DMN-таблицы и маппинг переменных. */
export function BusinessRuleTaskTab({ processId, token, elementId }: Props) {
    const [config, setConfig] = useState<BusinessRuleConfig>(DEFAULTS);
    const [tables, setTables] = useState<DmnTableListItemDto[]>([]);
    const [inputColumns, setInputColumns] = useState<DmnColumnDto[]>([]);
    const [outputColumns, setOutputColumns] = useState<DmnColumnDto[]>([]);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Загрузка конфигурации из API
    const load = useCallback(async () => {
        try {
            const dto = await api.getElementConfig(token, processId, elementId);
            if (dto) {
                try {
                    const parsed = JSON.parse(dto.configJson) as Partial<BusinessRuleConfig>;
                    setConfig({ ...DEFAULTS, ...parsed });
                } catch { /* невалидный JSON */ }
            } else {
                setConfig(DEFAULTS);
            }
            setDirty(false);
        } catch { /* сетевая ошибка */ }
    }, [token, processId, elementId]);

    // Загрузка списка опубликованных таблиц
    useEffect(() => {
        getDmnTables(token).then(all => {
            // Показываем только таблицы с опубликованной версией
            setTables(all.filter(t => t.latestVersionStatus === 'Published'));
        }).catch(() => {/* игнорируем */ });
    }, [token]);

    useEffect(() => { load(); }, [load]);

    // При изменении выбранной таблицы — загружаем колонки последней опубликованной версии
    useEffect(() => {
        if (!config.tableId) {
            setInputColumns([]);
            setOutputColumns([]);
            return;
        }
        const fetchColumns = async () => {
            try {
                const versions = await getDmnVersions(token, config.tableId!);
                const published = versions.find(v => v.status === 'Published');
                if (!published) return;
                const version = await getDmnVersion(token, config.tableId!, published.id);
                setInputColumns(version.columns.filter(c => c.columnKind === 'Input'));
                setOutputColumns(version.columns.filter(c => c.columnKind === 'Output'));
                // Обновляем versionId в конфиге
                if (config.versionId !== published.id) {
                    setConfig(prev => ({ ...prev, versionId: published.id }));
                }
            } catch { /* игнорируем ошибки загрузки */ }
        };
        fetchColumns();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [token, config.tableId]);

    const update = <K extends keyof BusinessRuleConfig>(key: K, value: BusinessRuleConfig[K]) => {
        setConfig(prev => ({ ...prev, [key]: value }));
        setDirty(true);
    };

    const updateMapping = (
        kind: 'input' | 'output',
        columnId: string,
        columnName: string,
        variableName: string
    ) => {
        const key = kind === 'input' ? 'inputMappings' : 'outputMappings';
        setConfig(prev => {
            const existing = prev[key].find(m => m.columnId === columnId);
            if (existing) {
                return {
                    ...prev,
                    [key]: prev[key].map(m =>
                        m.columnId === columnId ? { columnId, columnName, variableName } : m
                    ),
                };
            }
            return {
                ...prev,
                [key]: [...prev[key], { columnId, columnName, variableName }],
            };
        });
        setDirty(true);
    };

    const getMappedVariable = (kind: 'input' | 'output', columnId: string): string => {
        const key = kind === 'input' ? 'inputMappings' : 'outputMappings';
        return config[key].find(m => m.columnId === columnId)?.variableName ?? '';
    };

    const save = async () => {
        setSaving(true);
        setError(null);
        try {
            await api.upsertElementConfig(token, processId, elementId, JSON.stringify(config));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            {error && <p className="bpp-error">{error}</p>}

            {/* Выбор таблицы правил */}
            <div className="bpp-group">
                <div className="bpp-group-title">Таблица бизнес-правил (DMN)</div>
                <div className="bpp-field">
                    <label className="bpp-label required">Таблица правил</label>
                    <select
                        className="bpp-select"
                        value={config.tableId ?? ''}
                        onChange={e => {
                            update('tableId', e.target.value || undefined);
                            // Сбрасываем маппинги при смене таблицы
                            setConfig(prev => ({
                                ...prev,
                                tableId: e.target.value || undefined,
                                versionId: undefined,
                                inputMappings: [],
                                outputMappings: [],
                            }));
                            setDirty(true);
                        }}
                    >
                        <option value="">— Выберите таблицу правил —</option>
                        {tables.map(t => (
                            <option key={t.id} value={t.id}>{t.name}</option>
                        ))}
                    </select>
                    {tables.length === 0 && (
                        <span className="bpp-hint">Нет опубликованных таблиц правил. Создайте и опубликуйте таблицу в разделе «Бизнес-правила».</span>
                    )}
                </div>
            </div>

            {/* Маппинг входных переменных */}
            {config.tableId && inputColumns.length > 0 && (
                <div className="bpp-group">
                    <div className="bpp-group-title">Входные данные (переменные процесса → условия правила)</div>
                    <p className="bpp-hint">Укажите, из каких переменных процесса берётся значение для каждого входного условия правила.</p>
                    {inputColumns.map(col => (
                        <div key={col.id} className="bpp-field">
                            <label className="bpp-label">
                                {col.name}
                                <span style={{ fontFamily: 'monospace', fontSize: 10, color: '#9ca3af', marginLeft: 4 }}>
                                    ({col.valueType})
                                </span>
                            </label>
                            <input
                                className="bpp-input"
                                placeholder="Имя переменной процесса"
                                value={getMappedVariable('input', col.id)}
                                onChange={e => updateMapping('input', col.id, col.name, e.target.value)}
                            />
                        </div>
                    ))}
                </div>
            )}

            {/* Маппинг выходных переменных */}
            {config.tableId && outputColumns.length > 0 && (
                <div className="bpp-group">
                    <div className="bpp-group-title">Выходные данные (результат правила → переменные процесса)</div>
                    <p className="bpp-hint">Укажите, в какие переменные процесса записывается результат выполнения правила.</p>
                    {outputColumns.map(col => (
                        <div key={col.id} className="bpp-field">
                            <label className="bpp-label">
                                {col.name}
                                <span style={{ fontFamily: 'monospace', fontSize: 10, color: '#9ca3af', marginLeft: 4 }}>
                                    ({col.valueType})
                                </span>
                            </label>
                            <input
                                className="bpp-input"
                                placeholder="Имя переменной процесса"
                                value={getMappedVariable('output', col.id)}
                                onChange={e => updateMapping('output', col.id, col.name, e.target.value)}
                            />
                        </div>
                    ))}
                </div>
            )}

            {config.tableId && inputColumns.length === 0 && outputColumns.length === 0 && (
                <p className="bpp-hint" style={{ padding: '8px 0' }}>
                    Загрузка структуры таблицы правил...
                </p>
            )}

            <div className="bpp-btn-row">
                <button
                    className="bpp-btn bpp-btn-primary"
                    onClick={save}
                    disabled={saving || !dirty || !config.tableId}
                >
                    {saving ? 'Сохранение...' : 'Сохранить'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
