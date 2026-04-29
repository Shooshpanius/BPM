import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import type { BpmProcessVariableDto, BpmVariableType, CreateBpmVariableRequest } from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
}

const VARIABLE_TYPES: { value: BpmVariableType; label: string }[] = [
    { value: 'String', label: 'Строка' },
    { value: 'Int', label: 'Целое число' },
    { value: 'Decimal', label: 'Дробное число' },
    { value: 'Bool', label: 'Булево' },
    { value: 'Date', label: 'Дата' },
    { value: 'DateTime', label: 'Дата и время' },
    { value: 'Json', label: 'JSON' },
    { value: 'File', label: 'Файл' },
    { value: 'User', label: 'Пользователь' },
    { value: 'List', label: 'Список' },
];

interface EditingVar {
    id?: string;
    name: string;
    variableType: BpmVariableType;
    defaultValue: string;
    isKeyVariable: boolean;
    isInput: boolean;
    isOutput: boolean;
}

const newEditingVar = (): EditingVar => ({
    name: '',
    variableType: 'String',
    defaultValue: '',
    isKeyVariable: false,
    isInput: false,
    isOutput: false,
});

/** Вкладка «Переменные» — CRUD переменных контекста процесса. */
export function ProcessVariablesTab({ processId, token }: Props) {
    const [variables, setVariables] = useState<BpmProcessVariableDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [editing, setEditing] = useState<EditingVar | null>(null);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const vars = await api.getVariables(token, processId);
            setVariables(vars);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId]);

    useEffect(() => { load(); }, [load]);

    const startAdd = () => setEditing(newEditingVar());

    const startEdit = (v: BpmProcessVariableDto) => setEditing({
        id: v.id,
        name: v.name,
        variableType: v.variableType,
        defaultValue: v.defaultValue ?? '',
        isKeyVariable: v.isKeyVariable,
        isInput: v.isInput,
        isOutput: v.isOutput,
    });

    const cancelEdit = () => setEditing(null);

    const saveEdit = async () => {
        if (!editing) return;
        setSaving(true);
        setError(null);
        try {
            const req: CreateBpmVariableRequest = {
                name: editing.name,
                variableType: editing.variableType,
                defaultValue: editing.defaultValue || undefined,
                isKeyVariable: editing.isKeyVariable,
                isInput: editing.isInput,
                isOutput: editing.isOutput,
            };
            if (editing.id) {
                await api.updateVariable(token, processId, editing.id, req);
            } else {
                await api.createVariable(token, processId, req);
            }
            await load();
            setEditing(null);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const deleteVar = async (id: string) => {
        if (!window.confirm('Удалить переменную?')) return;
        try {
            await api.deleteVariable(token, processId, id);
            await load();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка удаления');
        }
    };

    const moveUp = async (idx: number) => {
        if (idx === 0) return;
        const newOrder = [...variables];
        [newOrder[idx - 1], newOrder[idx]] = [newOrder[idx], newOrder[idx - 1]];
        setVariables(newOrder);
        await api.reorderVariables(token, processId, newOrder.map(v => v.id)).catch(() => load());
    };

    const moveDown = async (idx: number) => {
        if (idx === variables.length - 1) return;
        const newOrder = [...variables];
        [newOrder[idx], newOrder[idx + 1]] = [newOrder[idx + 1], newOrder[idx]];
        setVariables(newOrder);
        await api.reorderVariables(token, processId, newOrder.map(v => v.id)).catch(() => load());
    };

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    return (
        <div>
            {error && <p className="bpp-error" style={{ marginBottom: 8 }}>{error}</p>}

            {/* Таблица переменных */}
            {variables.length > 0 && !editing && (
                <div className="bpp-table-wrap">
                    <table className="bpp-table">
                        <thead>
                            <tr>
                                <th>Имя</th>
                                <th>Тип</th>
                                <th>По умолчанию</th>
                                <th>Флаги</th>
                                <th style={{ width: 80 }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {variables.map((v, idx) => (
                                <tr key={v.id}>
                                    <td>
                                        <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{v.name}</span>
                                        {v.isKeyVariable && (
                                            <span className="bpp-badge bpp-badge-orange" style={{ marginLeft: 4 }}>key</span>
                                        )}
                                    </td>
                                    <td>
                                        <span className="bpp-badge bpp-badge-gray">
                                            {VARIABLE_TYPES.find(t => t.value === v.variableType)?.label ?? v.variableType}
                                        </span>
                                    </td>
                                    <td style={{ fontSize: 11, color: '#6b7280' }}>
                                        {v.defaultValue ?? '—'}
                                    </td>
                                    <td>
                                        {v.isInput && <span className="bpp-badge bpp-badge-blue" style={{ marginRight: 2 }}>вход</span>}
                                        {v.isOutput && <span className="bpp-badge bpp-badge-purple">выход</span>}
                                    </td>
                                    <td>
                                        <div style={{ display: 'flex', gap: 2 }}>
                                            <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                onClick={() => moveUp(idx)} title="Вверх" disabled={idx === 0}>↑</button>
                                            <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                onClick={() => moveDown(idx)} title="Вниз" disabled={idx === variables.length - 1}>↓</button>
                                            <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                onClick={() => startEdit(v)} title="Изменить">✎</button>
                                            <button className="bpp-btn bpp-btn-danger" style={{ padding: '2px 5px' }}
                                                onClick={() => deleteVar(v.id)} title="Удалить">✕</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Форма редактирования */}
            {editing && (
                <div style={{ border: '1px solid #dbeafe', borderRadius: 6, padding: 12, marginBottom: 12, background: '#f0f9ff' }}>
                    <div className="bpp-group-title" style={{ marginBottom: 10 }}>
                        {editing.id ? 'Редактировать переменную' : 'Новая переменная'}
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        <div className="bpp-field">
                            <label className="bpp-label required">Имя (camelCase)</label>
                            <input className="bpp-input" value={editing.name}
                                onChange={e => setEditing(prev => prev ? { ...prev, name: e.target.value } : prev)}
                                placeholder="orderAmount" />
                        </div>
                        <div className="bpp-field">
                            <label className="bpp-label">Тип</label>
                            <select className="bpp-select" value={editing.variableType}
                                onChange={e => setEditing(prev => prev ? { ...prev, variableType: e.target.value as BpmVariableType } : prev)}>
                                {VARIABLE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                            </select>
                        </div>
                    </div>
                    <div className="bpp-field">
                        <label className="bpp-label">Значение по умолчанию</label>
                        <input className="bpp-input" value={editing.defaultValue}
                            onChange={e => setEditing(prev => prev ? { ...prev, defaultValue: e.target.value } : prev)}
                            placeholder="Оставьте пустым, если нет" />
                    </div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                        <label className="bpp-checkbox-row">
                            <input type="checkbox" checked={editing.isKeyVariable}
                                onChange={e => setEditing(prev => prev ? { ...prev, isKeyVariable: e.target.checked } : prev)} />
                            <span>Ключевая (название экземпляра)</span>
                        </label>
                        <label className="bpp-checkbox-row">
                            <input type="checkbox" checked={editing.isInput}
                                onChange={e => setEditing(prev => prev ? { ...prev, isInput: e.target.checked } : prev)} />
                            <span>Входная</span>
                        </label>
                        <label className="bpp-checkbox-row">
                            <input type="checkbox" checked={editing.isOutput}
                                onChange={e => setEditing(prev => prev ? { ...prev, isOutput: e.target.checked } : prev)} />
                            <span>Выходная</span>
                        </label>
                    </div>
                    <div className="bpp-btn-row">
                        <button className="bpp-btn bpp-btn-primary" onClick={saveEdit} disabled={saving}>
                            {saving ? 'Сохранение...' : 'Сохранить'}
                        </button>
                        <button className="bpp-btn" onClick={cancelEdit}>Отмена</button>
                    </div>
                </div>
            )}

            {!editing && (
                <button className="bpp-btn" style={{ width: '100%' }} onClick={startAdd}>
                    + Добавить переменную
                </button>
            )}

            {variables.length === 0 && !editing && (
                <p className="bpp-hint" style={{ textAlign: 'center', marginTop: 8 }}>
                    Нет переменных. Переменные используются для хранения данных в экземплярах процесса.
                </p>
            )}
        </div>
    );
}
