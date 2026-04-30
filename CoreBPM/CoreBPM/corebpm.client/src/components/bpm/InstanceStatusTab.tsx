import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import type {
    InstanceStatusConfigDto,
    InstanceStatusOptionDto,
    BpmInterruptAction,
    BpmProcessVariableDto,
} from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
}

const INTERRUPT_ACTIONS: { value: BpmInterruptAction; label: string }[] = [
    { value: 'KeepCurrent', label: 'Оставить текущий статус' },
    { value: 'Reset', label: 'Обнулить статус' },
    { value: 'MoveToNext', label: 'Перевести в следующий статус' },
    { value: 'RunScript', label: 'Запустить сценарий' },
];

interface EditingOption {
    id?: string;
    name: string;
    code: string;
}

const newEditingOption = (): EditingOption => ({ name: '', code: '' });

/** Вкладка «Статусы» — CRUD пользовательских статусов экземпляров процесса. */
export function InstanceStatusTab({ processId, token }: Props) {
    const [config, setConfig] = useState<InstanceStatusConfigDto | null>(null);
    const [variables, setVariables] = useState<BpmProcessVariableDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Редактирование вариантов статусов
    const [editingOption, setEditingOption] = useState<EditingOption | null>(null);

    // Создание новой переменной
    const [createVar, setCreateVar] = useState(false);
    const [newVarName, setNewVarName] = useState('');

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [cfg, vars] = await Promise.all([
                api.getStatusConfig(token, processId),
                api.getVariables(token, processId),
            ]);
            setConfig(cfg);
            setVariables(vars);
            setError(null);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId]);

    useEffect(() => { load(); }, [load]);

    // ─── Конфигурация ────────────────────────────────────────────────────────

    const updateConfig = async (patch: Partial<{ linkedVariableId: string | undefined; onInterruptAction: BpmInterruptAction; onInterruptScriptId: string | undefined }>) => {
        if (!config) return;
        setSaving(true);
        try {
            const updated = await api.updateStatusConfig(token, processId, {
                linkedVariableId: patch.linkedVariableId ?? config.linkedVariableId,
                onInterruptAction: patch.onInterruptAction ?? config.onInterruptAction,
                onInterruptScriptId: patch.onInterruptScriptId ?? config.onInterruptScriptId,
                createVariable: false,
            });
            setConfig(updated);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const handleCreateVariable = async () => {
        if (!newVarName.trim()) { setError('Введите имя переменной'); return; }
        setSaving(true);
        try {
            const updated = await api.updateStatusConfig(token, processId, {
                linkedVariableId: undefined,
                onInterruptAction: config?.onInterruptAction ?? 'KeepCurrent',
                onInterruptScriptId: config?.onInterruptScriptId,
                createVariable: true,
                newVariableName: newVarName.trim(),
            });
            setConfig(updated);
            setCreateVar(false);
            setNewVarName('');
            // Обновить список переменных
            const vars = await api.getVariables(token, processId);
            setVariables(vars);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка создания переменной');
        } finally {
            setSaving(false);
        }
    };

    // ─── Варианты статусов ────────────────────────────────────────────────────

    const startAddOption = () => setEditingOption(newEditingOption());
    const startEditOption = (o: InstanceStatusOptionDto) => setEditingOption({ id: o.id, name: o.name, code: o.code });
    const cancelEdit = () => setEditingOption(null);

    const saveOption = async () => {
        if (!editingOption) return;
        setSaving(true);
        try {
            if (editingOption.id) {
                await api.updateStatusOption(token, processId, editingOption.id, {
                    name: editingOption.name,
                    code: editingOption.code,
                });
            } else {
                await api.createStatusOption(token, processId, {
                    name: editingOption.name,
                    code: editingOption.code || undefined,
                });
            }
            setEditingOption(null);
            await load();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const deleteOption = async (optionId: string) => {
        if (!confirm('Удалить этот вариант статуса?')) return;
        setSaving(true);
        try {
            await api.deleteStatusOption(token, processId, optionId);
            await load();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка удаления');
        } finally {
            setSaving(false);
        }
    };

    const moveOption = async (idx: number, direction: 'up' | 'down') => {
        if (!config) return;
        const options = [...config.options];
        const newIdx = direction === 'up' ? idx - 1 : idx + 1;
        if (newIdx < 0 || newIdx >= options.length) return;
        [options[idx], options[newIdx]] = [options[newIdx], options[idx]];
        const orderedIds = options.map(o => o.id);
        setSaving(true);
        try {
            await api.reorderStatusOptions(token, processId, orderedIds);
            await load();
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сортировки');
        } finally {
            setSaving(false);
        }
    };

    // ─── Рендер ───────────────────────────────────────────────────────────────

    if (loading) return <div className="bpp-hint" style={{ textAlign: 'center', padding: 16 }}>Загрузка...</div>;

    const listVars = variables.filter(v => v.variableType === 'List');
    const options = config?.options ?? [];

    return (
        <div style={{ padding: '12px 10px', display: 'flex', flexDirection: 'column', gap: 16 }}>
            {error && (
                <div className="bpp-error" style={{ marginBottom: 0 }}>{error}</div>
            )}

            {/* ─── Переменная статуса ─── */}
            <div>
                <div className="bpp-group-title" style={{ marginBottom: 8 }}>Переменная статуса</div>
                <p className="bpp-hint">
                    Выберите существующую переменную типа «Список», в которой будет храниться текущий статус каждого экземпляра,
                    или создайте новую.
                </p>

                {!createVar ? (
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                        <select
                            className="bpp-select"
                            style={{ flex: 1 }}
                            value={config?.linkedVariableId ?? ''}
                            disabled={saving}
                            onChange={e => updateConfig({ linkedVariableId: e.target.value || undefined })}
                        >
                            <option value="">— Не выбрано —</option>
                            {listVars.map(v => (
                                <option key={v.id} value={v.id}>{v.name}</option>
                            ))}
                        </select>
                        <button className="bpp-btn" disabled={saving} onClick={() => setCreateVar(true)}>
                            + Новая
                        </button>
                    </div>
                ) : (
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                        <input
                            className="bpp-input"
                            style={{ flex: 1 }}
                            placeholder="Имя переменной (camelCase)"
                            value={newVarName}
                            onChange={e => setNewVarName(e.target.value)}
                            disabled={saving}
                        />
                        <button className="bpp-btn bpp-btn-primary" disabled={saving || !newVarName.trim()} onClick={handleCreateVariable}>
                            {saving ? '...' : 'Создать'}
                        </button>
                        <button className="bpp-btn" disabled={saving} onClick={() => { setCreateVar(false); setNewVarName(''); }}>
                            Отмена
                        </button>
                    </div>
                )}

                {config?.linkedVariableName && (
                    <p className="bpp-hint" style={{ marginTop: 4 }}>
                        Привязана: <span style={{ fontFamily: 'monospace', fontWeight: 600 }}>{config.linkedVariableName}</span>
                    </p>
                )}
            </div>

            {/* ─── Действие при прерывании ─── */}
            <div>
                <div className="bpp-group-title" style={{ marginBottom: 8 }}>Действие при прерывании экземпляра</div>
                <select
                    className="bpp-select"
                    value={config?.onInterruptAction ?? 'KeepCurrent'}
                    disabled={saving}
                    onChange={e => updateConfig({ onInterruptAction: e.target.value as BpmInterruptAction })}
                >
                    {INTERRUPT_ACTIONS.map(a => (
                        <option key={a.value} value={a.value}>{a.label}</option>
                    ))}
                </select>

                {config?.onInterruptAction === 'RunScript' && (
                    <div className="bpp-field" style={{ marginTop: 8 }}>
                        <label className="bpp-label">ID сценария</label>
                        <input
                            className="bpp-input"
                            placeholder="script-id или путь к сценарию"
                            value={config.onInterruptScriptId ?? ''}
                            disabled={saving}
                            onChange={e => updateConfig({ onInterruptScriptId: e.target.value || undefined })}
                        />
                        <span className="bpp-hint">Сценарий из раздела «Сценарии» дизайнера (FR-BPM-01.7)</span>
                    </div>
                )}
            </div>

            {/* ─── Варианты статусов ─── */}
            <div>
                <div className="bpp-group-title" style={{ marginBottom: 8 }}>Варианты статусов</div>
                <p className="bpp-hint">
                    Порядок управляется стрелками — влияет на порядок в выпадающих списках и фильтрах.
                </p>

                {options.length > 0 && !editingOption && (
                    <div className="bpp-table-wrap">
                        <table className="bpp-table">
                            <thead>
                                <tr>
                                    <th>Название</th>
                                    <th>Код</th>
                                    <th style={{ width: 90 }}></th>
                                </tr>
                            </thead>
                            <tbody>
                                {options.map((o, idx) => (
                                    <tr key={o.id}>
                                        <td>{o.name}</td>
                                        <td>
                                            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{o.code}</span>
                                        </td>
                                        <td>
                                            <div style={{ display: 'flex', gap: 2 }}>
                                                <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                    onClick={() => moveOption(idx, 'up')} title="Вверх" disabled={idx === 0 || saving}>↑</button>
                                                <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                    onClick={() => moveOption(idx, 'down')} title="Вниз" disabled={idx === options.length - 1 || saving}>↓</button>
                                                <button className="bpp-btn" style={{ padding: '2px 5px' }}
                                                    onClick={() => startEditOption(o)} title="Изменить" disabled={saving}>✎</button>
                                                <button className="bpp-btn bpp-btn-danger" style={{ padding: '2px 5px' }}
                                                    onClick={() => deleteOption(o.id)} title="Удалить" disabled={saving}>✕</button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}

                {/* Форма редактирования / добавления варианта */}
                {editingOption && (
                    <div style={{ border: '1px solid #dbeafe', borderRadius: 6, padding: 12, marginBottom: 12, background: '#f0f9ff' }}>
                        <div className="bpp-group-title" style={{ marginBottom: 10 }}>
                            {editingOption.id ? 'Редактировать статус' : 'Новый статус'}
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                            <div className="bpp-field">
                                <label className="bpp-label required">Название</label>
                                <input
                                    className="bpp-input"
                                    value={editingOption.name}
                                    placeholder="Например: В работе"
                                    onChange={e => setEditingOption(prev => prev ? { ...prev, name: e.target.value } : prev)}
                                />
                            </div>
                            <div className="bpp-field">
                                <label className="bpp-label">Код (авто, если пусто)</label>
                                <input
                                    className="bpp-input"
                                    value={editingOption.code}
                                    placeholder="in-progress"
                                    onChange={e => setEditingOption(prev => prev ? { ...prev, code: e.target.value } : prev)}
                                />
                            </div>
                        </div>
                        <div className="bpp-btn-row">
                            <button className="bpp-btn bpp-btn-primary" onClick={saveOption} disabled={saving || !editingOption.name.trim()}>
                                {saving ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className="bpp-btn" onClick={cancelEdit}>Отмена</button>
                        </div>
                    </div>
                )}

                {!editingOption && (
                    <button className="bpp-btn" style={{ width: '100%', marginTop: options.length > 0 ? 8 : 0 }} onClick={startAddOption} disabled={saving}>
                        + Добавить статус
                    </button>
                )}

                {options.length === 0 && !editingOption && (
                    <p className="bpp-hint" style={{ textAlign: 'center', marginTop: 4 }}>
                        Нет вариантов. Добавьте статусы, чтобы отслеживать ход выполнения экземпляров.
                    </p>
                )}
            </div>
        </div>
    );
}
