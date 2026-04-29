import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import * as api from '../../api/rulesApi';
import type {
    DmnTableVersionDto,
    DmnTableVersionInfoDto,
    DmnColumnKind,
    DmnValueType,
    DmnHitPolicy,
    DmnMatchedRowDto,
} from '../../api/rulesApi';
import './DmnEditorPage.css';

const HIT_POLICY_LABELS: Record<DmnHitPolicy, string> = {
    Unique: 'UNIQUE',
    First: 'FIRST',
    Any: 'ANY',
    Collect: 'COLLECT',
    RuleOrder: 'RULE ORDER',
    OutputOrder: 'OUTPUT ORDER',
};

const VALUE_TYPE_LABELS: Record<DmnValueType, string> = {
    String: 'Строка',
    Number: 'Число',
    Date: 'Дата',
    Boolean: 'Булево',
};

const STATUS_LABELS: Record<string, string> = {
    Draft: 'Черновик',
    Published: 'Опубликована',
    Archived: 'Архив',
};

// Типы значений для html input
const VALUE_TYPE_INPUT: Record<DmnValueType, string> = {
    String: 'text',
    Number: 'text', // разрешаем диапазоны
    Date: 'text',
    Boolean: 'text',
};

interface DmnEditorPageProps {
    tableId: string;
    onBack: () => void;
}

// Рабочие структуры редактора
interface EditorColumn {
    id: string; // временный id для новых (может не совпадать с серверным)
    serverId?: string;
    name: string;
    columnKind: DmnColumnKind;
    valueType: DmnValueType;
}

interface EditorCell {
    columnId: string; // ссылка на EditorColumn.id
    value: string;
    annotation: string;
}

interface EditorRow {
    id: string;
    serverId?: string;
    cells: EditorCell[];
}

/** Страница редактора DMN-таблицы. */
export function DmnEditorPage({ tableId, onBack }: DmnEditorPageProps) {
    const { accessToken: token } = useAuth();

    const [tableName, setTableName] = useState('');
    const [hitPolicy, setHitPolicy] = useState<DmnHitPolicy>('Unique');
    const [columns, setColumns] = useState<EditorColumn[]>([]);
    const [rows, setRows] = useState<EditorRow[]>([]);
    const [versions, setVersions] = useState<DmnTableVersionInfoDto[]>([]);
    const [currentVersionId, setCurrentVersionId] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [publishing, setPublishing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [isReadOnly, setIsReadOnly] = useState(false);

    // Панель версий
    const [showVersions, setShowVersions] = useState(false);

    // Панель тестирования
    const [showTest, setShowTest] = useState(false);
    const [testInputs, setTestInputs] = useState<Record<string, string>>({});
    const [testResult, setTestResult] = useState<DmnMatchedRowDto[] | null>(null);
    const [testHitPolicy, setTestHitPolicy] = useState<DmnHitPolicy>('Unique');
    const [testError, setTestError] = useState<string | null>(null);
    const [testing, setTesting] = useState(false);

    const loadTable = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const tableDto = await api.getDmnTable(token, tableId);
            setTableName(tableDto.name);
            setHitPolicy(tableDto.hitPolicy);

            const versionList = await api.getDmnVersions(token, tableId);
            setVersions(versionList);

            // Загружаем последнюю версию
            const latest = versionList[0];
            if (latest) {
                await loadVersion(latest.id, latest.status === 'Published' || latest.status === 'Archived');
                setCurrentVersionId(latest.id);
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, tableId]); // eslint-disable-line react-hooks/exhaustive-deps

    useEffect(() => { loadTable(); }, [loadTable]);

    const loadVersion = async (versionId: string, readOnly = false) => {
        if (!token) return;
        const vDto: DmnTableVersionDto = await api.getDmnVersion(token, tableId, versionId);
        setIsReadOnly(readOnly);

        const cols: EditorColumn[] = vDto.columns.map(c => ({
            id: c.id,
            serverId: c.id,
            name: c.name,
            columnKind: c.columnKind,
            valueType: c.valueType,
        }));
        setColumns(cols);

        const rs: EditorRow[] = vDto.rows.map(r => ({
            id: r.id,
            serverId: r.id,
            cells: cols.map(col => {
                const cell = r.cells.find(c => c.columnId === col.id);
                return { columnId: col.id, value: cell?.value ?? '', annotation: cell?.annotation ?? '' };
            }),
        }));
        setRows(rs);
    };

    const newTempId = () => `_new_${Math.random().toString(36).slice(2)}`;

    // ─── Работа с колонками ────────────────────────────────────────────────────

    const addColumn = (kind: DmnColumnKind) => {
        if (isReadOnly) return;
        const newId = newTempId();
        setColumns(cols => [...cols, {
            id: newId, name: kind === 'Input' ? 'Условие' : 'Результат',
            columnKind: kind, valueType: 'String',
        }]);
        // Добавляем пустую ячейку в каждую строку
        setRows(rs => rs.map(r => ({
            ...r, cells: [...r.cells, { columnId: newId, value: '', annotation: '' }],
        })));
    };

    const updateColumn = (id: string, patch: Partial<EditorColumn>) => {
        if (isReadOnly) return;
        setColumns(cols => cols.map(c => c.id === id ? { ...c, ...patch } : c));
    };

    const removeColumn = (id: string) => {
        if (isReadOnly) return;
        setColumns(cols => cols.filter(c => c.id !== id));
        setRows(rs => rs.map(r => ({ ...r, cells: r.cells.filter(c => c.columnId !== id) })));
    };

    // ─── Работа со строками ────────────────────────────────────────────────────

    const addRow = () => {
        if (isReadOnly) return;
        const newId = newTempId();
        setRows(rs => [...rs, {
            id: newId,
            cells: columns.map(col => ({ columnId: col.id, value: '', annotation: '' })),
        }]);
    };

    const removeRow = (id: string) => {
        if (isReadOnly) return;
        setRows(rs => rs.filter(r => r.id !== id));
    };

    const updateCell = (rowId: string, columnId: string, patch: Partial<EditorCell>) => {
        if (isReadOnly) return;
        setRows(rs => rs.map(r =>
            r.id === rowId
                ? { ...r, cells: r.cells.map(c => c.columnId === columnId ? { ...c, ...patch } : c) }
                : r
        ));
    };

    // ─── Сохранение и публикация ───────────────────────────────────────────────

    const buildSaveRequest = () => {
        const colsReq = columns.map((col, idx) => ({
            id: col.serverId,
            name: col.name,
            columnKind: col.columnKind,
            valueType: col.valueType,
            order: idx,
        }));

        const rowsReq = rows.map((row, rIdx) => ({
            id: row.serverId,
            order: rIdx,
            cells: row.cells.map(cell => ({
                columnIndex: columns.findIndex(c => c.id === cell.columnId),
                value: cell.value || undefined,
                annotation: cell.annotation || undefined,
            })),
        }));

        return { columns: colsReq, rows: rowsReq };
    };

    const handleSaveDraft = async () => {
        if (!token || isReadOnly) return;
        setSaving(true);
        setError(null);
        try {
            const dto = await api.saveDmnDraft(token, tableId, buildSaveRequest());
            setCurrentVersionId(dto.id);
            setIsReadOnly(false);

            // Обновляем список версий
            const vList = await api.getDmnVersions(token, tableId);
            setVersions(vList);

            // Синхронизируем серверные Id
            const newCols: EditorColumn[] = dto.columns.map(c => ({
                id: c.id, serverId: c.id, name: c.name, columnKind: c.columnKind, valueType: c.valueType,
            }));
            setColumns(newCols);
            const newRows: EditorRow[] = dto.rows.map(r => ({
                id: r.id, serverId: r.id,
                cells: newCols.map(col => {
                    const cell = r.cells.find(c => c.columnId === col.id);
                    return { columnId: col.id, value: cell?.value ?? '', annotation: cell?.annotation ?? '' };
                }),
            }));
            setRows(newRows);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handlePublish = async () => {
        if (!token || !currentVersionId) return;
        setPublishing(true);
        setError(null);
        try {
            await api.publishDmnVersion(token, tableId, currentVersionId);
            const vList = await api.getDmnVersions(token, tableId);
            setVersions(vList);
            setIsReadOnly(true);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка публикации');
        } finally {
            setPublishing(false);
        }
    };

    const handleLoadVersion = async (vId: string, status: string) => {
        if (!token) return;
        try {
            await loadVersion(vId, status !== 'Draft');
            setCurrentVersionId(vId);
            setShowVersions(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки версии');
        }
    };

    // ─── Тестирование ─────────────────────────────────────────────────────────

    const handleTest = async () => {
        if (!token || !currentVersionId) return;
        setTesting(true);
        setTestError(null);
        setTestResult(null);
        try {
            const inputs: Record<string, string | undefined> = {};
            columns
                .filter(c => c.columnKind === 'Input')
                .forEach(c => { inputs[c.serverId ?? c.id] = testInputs[c.id] || undefined; });

            const result = await api.testDmnVersion(token, tableId, currentVersionId, { inputs });
            setTestResult(result.matchedRows);
            setTestHitPolicy(result.hitPolicy);
        } catch (e) {
            setTestError(e instanceof Error ? e.message : 'Ошибка тестирования');
        } finally {
            setTesting(false);
        }
    };

    const inputCols = columns.filter(c => c.columnKind === 'Input');
    const outputCols = columns.filter(c => c.columnKind === 'Output');
    const matchedRowIds = new Set((testResult ?? []).map(r => r.rowId));

    if (loading) {
        return (
            <div className="de-root">
                <div className="de-loading">Загрузка редактора…</div>
            </div>
        );
    }

    return (
        <div className="de-root">
            {/* Шапка редактора */}
            <div className="de-header">
                <button className="de-back-btn" onClick={onBack} title="Назад к списку">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18">
                        <path d="M19 12H5M12 5l-7 7 7 7"/>
                    </svg>
                </button>
                <div className="de-header-title">
                    <span className="de-table-name">{tableName}</span>
                    <select
                        className="de-policy-select"
                        value={hitPolicy}
                        onChange={e => setHitPolicy(e.target.value as DmnHitPolicy)}
                        disabled={isReadOnly}
                        title="Хит-политика"
                    >
                        {(Object.keys(HIT_POLICY_LABELS) as DmnHitPolicy[]).map(p => (
                            <option key={p} value={p}>{HIT_POLICY_LABELS[p]}</option>
                        ))}
                    </select>
                    {isReadOnly && <span className="de-readonly-badge">Только чтение</span>}
                </div>
                <div className="de-header-actions">
                    <button
                        className="de-btn de-btn-outline"
                        onClick={() => setShowVersions(v => !v)}
                    >
                        Версии ({versions.length})
                    </button>
                    <button
                        className="de-btn de-btn-outline"
                        onClick={() => setShowTest(t => !t)}
                    >
                        🧪 Тест
                    </button>
                    {!isReadOnly && (
                        <button
                            className="de-btn de-btn-secondary"
                            onClick={handleSaveDraft}
                            disabled={saving}
                        >
                            {saving ? 'Сохранение…' : 'Сохранить черновик'}
                        </button>
                    )}
                    {currentVersionId && !isReadOnly && (
                        <button
                            className="de-btn de-btn-primary"
                            onClick={handlePublish}
                            disabled={publishing}
                        >
                            {publishing ? 'Публикация…' : 'Опубликовать'}
                        </button>
                    )}
                </div>
            </div>

            {error && <div className="de-error">{error}</div>}

            <div className="de-body">
                {/* Основная область таблицы */}
                <div className="de-editor-area">
                    <div className="de-table-wrap">
                        <table className="de-table">
                            <thead>
                                <tr>
                                    <th className="de-th de-th-num">#</th>
                                    {columns.map(col => (
                                        <th key={col.id} className={`de-th de-th-col de-th--${col.columnKind.toLowerCase()}`}>
                                            <div className="de-col-header">
                                                <span className="de-col-kind-badge">
                                                    {col.columnKind === 'Input' ? 'ВХОД' : 'ВЫХОД'}
                                                </span>
                                                {isReadOnly ? (
                                                    <span className="de-col-name">{col.name}</span>
                                                ) : (
                                                    <input
                                                        className="de-col-name-input"
                                                        value={col.name}
                                                        onChange={e => updateColumn(col.id, { name: e.target.value })}
                                                        placeholder="Название"
                                                    />
                                                )}
                                                <div className="de-col-controls">
                                                    {!isReadOnly && (
                                                        <select
                                                            className="de-col-type-select"
                                                            value={col.valueType}
                                                            onChange={e => updateColumn(col.id, { valueType: e.target.value as DmnValueType })}
                                                            title="Тип значения"
                                                        >
                                                            {(Object.keys(VALUE_TYPE_LABELS) as DmnValueType[]).map(vt => (
                                                                <option key={vt} value={vt}>{VALUE_TYPE_LABELS[vt]}</option>
                                                            ))}
                                                        </select>
                                                    )}
                                                    {isReadOnly && (
                                                        <span className="de-col-type-badge">{VALUE_TYPE_LABELS[col.valueType]}</span>
                                                    )}
                                                    {!isReadOnly && (
                                                        <button
                                                            className="de-col-del-btn"
                                                            onClick={() => removeColumn(col.id)}
                                                            title="Удалить колонку"
                                                        >✕</button>
                                                    )}
                                                </div>
                                            </div>
                                        </th>
                                    ))}
                                    <th className="de-th de-th-ann">Аннотация</th>
                                    {!isReadOnly && <th className="de-th de-th-act"></th>}
                                </tr>
                            </thead>
                            <tbody>
                                {rows.length === 0 && (
                                    <tr>
                                        <td
                                            colSpan={columns.length + (isReadOnly ? 2 : 3)}
                                            className="de-empty-rows"
                                        >
                                            Нет строк. {!isReadOnly && 'Добавьте первое правило.'}
                                        </td>
                                    </tr>
                                )}
                                {rows.map((row, rIdx) => {
                                    const isMatched = matchedRowIds.has(row.serverId ?? row.id);
                                    return (
                                        <tr key={row.id} className={isMatched ? 'de-row-matched' : ''}>
                                            <td className="de-td de-td-num">{rIdx + 1}</td>
                                            {columns.map(col => {
                                                const cell = row.cells.find(c => c.columnId === col.id);
                                                return (
                                                    <td
                                                        key={col.id}
                                                        className={`de-td de-td--${col.columnKind.toLowerCase()}`}
                                                        title={cell?.value ? '' : 'Пустая — совпадает всегда'}
                                                    >
                                                        {isReadOnly ? (
                                                            <span className="de-cell-value">
                                                                {cell?.value || <span className="de-cell-any">—</span>}
                                                            </span>
                                                        ) : (
                                                            <input
                                                                className="de-cell-input"
                                                                type={VALUE_TYPE_INPUT[col.valueType]}
                                                                value={cell?.value ?? ''}
                                                                onChange={e => updateCell(row.id, col.id, { value: e.target.value })}
                                                                placeholder="—"
                                                                title={
                                                                    col.valueType === 'Number'
                                                                        ? 'Примеры: 5, <10, >=3, [1..10]'
                                                                        : col.valueType === 'Boolean'
                                                                            ? 'true или false'
                                                                            : undefined
                                                                }
                                                            />
                                                        )}
                                                    </td>
                                                );
                                            })}
                                            <td className="de-td de-td-ann">
                                                {isReadOnly ? (
                                                    <span className="de-cell-value">{row.cells[0]?.annotation ?? ''}</span>
                                                ) : (
                                                    <input
                                                        className="de-cell-input de-cell-input--ann"
                                                        value={row.cells[0]?.annotation ?? ''}
                                                        onChange={e => {
                                                            if (columns.length > 0)
                                                                updateCell(row.id, columns[0].id, { annotation: e.target.value });
                                                        }}
                                                        placeholder="Комментарий"
                                                    />
                                                )}
                                            </td>
                                            {!isReadOnly && (
                                                <td className="de-td de-td-act">
                                                    <button
                                                        className="de-row-del-btn"
                                                        onClick={() => removeRow(row.id)}
                                                        title="Удалить строку"
                                                    >✕</button>
                                                </td>
                                            )}
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>

                    {/* Кнопки добавления */}
                    {!isReadOnly && (
                        <div className="de-add-area">
                            <button className="de-add-row-btn" onClick={addRow}>+ Добавить строку</button>
                            <div className="de-add-col-group">
                                <button className="de-add-col-btn de-add-col-btn--input" onClick={() => addColumn('Input')}>
                                    + Вход
                                </button>
                                <button className="de-add-col-btn de-add-col-btn--output" onClick={() => addColumn('Output')}>
                                    + Выход
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Панель версий */}
                {showVersions && (
                    <aside className="de-versions-panel">
                        <div className="de-panel-header">
                            <span className="de-panel-title">История версий</span>
                            <button className="de-panel-close" onClick={() => setShowVersions(false)}>✕</button>
                        </div>
                        <div className="de-versions-list">
                            {versions.map(v => (
                                <div
                                    key={v.id}
                                    className={`de-version-item ${v.id === currentVersionId ? 'de-version-item--active' : ''}`}
                                >
                                    <div className="de-version-info">
                                        <span className="de-version-num">v{v.versionNumber}</span>
                                        <span className={`de-version-status de-version-status--${v.status.toLowerCase()}`}>
                                            {STATUS_LABELS[v.status]}
                                        </span>
                                    </div>
                                    <div className="de-version-date">
                                        {new Date(v.createdAt).toLocaleDateString('ru-RU')}
                                    </div>
                                    {v.id !== currentVersionId && (
                                        <button
                                            className="de-version-open-btn"
                                            onClick={() => handleLoadVersion(v.id, v.status)}
                                        >
                                            Открыть
                                        </button>
                                    )}
                                </div>
                            ))}
                        </div>
                    </aside>
                )}
            </div>

            {/* Панель тестирования */}
            {showTest && (
                <div className="de-test-panel">
                    <div className="de-panel-header">
                        <span className="de-panel-title">Тестирование правила</span>
                        <button className="de-panel-close" onClick={() => setShowTest(false)}>✕</button>
                    </div>
                    <div className="de-test-body">
                        <div className="de-test-inputs">
                            {inputCols.length === 0 && (
                                <p className="de-test-empty">Нет входных колонок</p>
                            )}
                            {inputCols.map(col => (
                                <div key={col.id} className="de-test-field">
                                    <label className="de-test-label">{col.name}</label>
                                    <input
                                        className="de-test-input"
                                        type={VALUE_TYPE_INPUT[col.valueType]}
                                        value={testInputs[col.id] ?? ''}
                                        onChange={e => setTestInputs(prev => ({ ...prev, [col.id]: e.target.value }))}
                                        placeholder={`${VALUE_TYPE_LABELS[col.valueType]}`}
                                    />
                                </div>
                            ))}
                        </div>
                        <button
                            className="de-btn de-btn-primary de-test-run-btn"
                            onClick={handleTest}
                            disabled={testing || !currentVersionId}
                        >
                            {testing ? 'Выполнение…' : 'Выполнить'}
                        </button>

                        {testError && <div className="de-error de-test-error">{testError}</div>}

                        {testResult !== null && (
                            <div className="de-test-results">
                                <div className="de-test-results-header">
                                    Политика: <strong>{HIT_POLICY_LABELS[testHitPolicy]}</strong>
                                    {' · '}
                                    Совпало строк: <strong>{testResult.length}</strong>
                                </div>
                                {testResult.length === 0 ? (
                                    <p className="de-test-no-match">Ни одна строка не совпала</p>
                                ) : (
                                    <table className="de-test-table">
                                        <thead>
                                            <tr>
                                                <th>Строка</th>
                                                {outputCols.map(col => (
                                                    <th key={col.id}>{col.name}</th>
                                                ))}
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {testResult.map(r => (
                                                <tr key={r.rowId}>
                                                    <td>{r.rowOrder + 1}</td>
                                                    {outputCols.map(col => (
                                                        <td key={col.id}>
                                                            {r.outputs[col.id] ?? '—'}
                                                        </td>
                                                    ))}
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                )}
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
}
