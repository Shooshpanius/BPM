import { useState, useEffect, useCallback } from 'react';
import * as api from '../../api/bpmApi';
import type { BpmRaciEntryDto, BpmRaciType, UpsertRaciEntryRequest } from '../../api/bpmApi';

interface Props {
    processId: string;
    token: string;
}

const RACI_TYPES: BpmRaciType[] = ['R', 'A', 'C', 'I'];

const RACI_DESCRIPTIONS: Record<BpmRaciType, string> = {
    R: 'Responsible — несёт ответственность, делает работу',
    A: 'Accountable — утверждает результат',
    C: 'Consulted — консультирует',
    I: 'Informed — информируется',
};

const RACI_COLORS: Record<BpmRaciType, string> = {
    R: 'bpp-badge-blue',
    A: 'bpp-badge-orange',
    C: 'bpp-badge-green',
    I: 'bpp-badge-gray',
};

/** Уникальные значения в массиве */
const unique = <T,>(arr: T[]): T[] => [...new Set(arr)];

/** Вкладка «RACI-матрица» — таблица этапы × роли. */
export function RaciMatrixTab({ processId, token }: Props) {
    const [entries, setEntries] = useState<BpmRaciEntryDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Форма добавления строки/столбца
    const [newStage, setNewStage] = useState('');
    const [newRole, setNewRole] = useState('');

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const data = await api.getRaci(token, processId);
            setEntries(data);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId]);

    useEffect(() => { load(); }, [load]);

    const stages = unique(entries.map(e => e.stage)).sort();
    const roles = unique(entries.map(e => e.role)).sort();

    const getCell = (stage: string, role: string): BpmRaciType | null => {
        return entries.find(e => e.stage === stage && e.role === role)?.raciType ?? null;
    };

    const toggleCell = (stage: string, role: string, type: BpmRaciType) => {
        const current = getCell(stage, role);
        if (current === type) {
            // Снимаем
            setEntries(prev => prev.filter(e => !(e.stage === stage && e.role === role)));
        } else {
            // Устанавливаем
            const newEntry: BpmRaciEntryDto = {
                id: crypto.randomUUID(),
                stage,
                role,
                raciType: type,
            };
            setEntries(prev => [...prev.filter(e => !(e.stage === stage && e.role === role)), newEntry]);
        }
        setDirty(true);
    };

    const addStage = () => {
        const s = newStage.trim();
        if (!s || stages.includes(s)) return;
        // Добавляем временную запись с плейсхолдером роли; при сохранении она фильтруется
        setNewStage('');
        setEntries(prev => [
            ...prev,
            { id: crypto.randomUUID(), stage: s, role: '__placeholder__', raciType: 'I' as BpmRaciType },
        ]);
        setDirty(true);
    };

    const addRole = () => {
        const r = newRole.trim();
        if (!r || roles.includes(r)) return;
        // Добавляем временную запись с плейсхолдером этапа; при сохранении она фильтруется
        setNewRole('');
        setEntries(prev => [
            ...prev,
            { id: crypto.randomUUID(), stage: '__placeholder__', role: r, raciType: 'I' as BpmRaciType },
        ]);
        setDirty(true);
    };

    const removeStage = (stage: string) => {
        setEntries(prev => prev.filter(e => e.stage !== stage));
        setDirty(true);
    };

    const removeRole = (role: string) => {
        setEntries(prev => prev.filter(e => e.role !== role));
        setDirty(true);
    };

    const save = async () => {
        setSaving(true);
        setError(null);
        try {
            // Фильтруем плейсхолдеры
            const toSave: UpsertRaciEntryRequest[] = entries
                .filter(e => e.stage !== '__placeholder__' && e.role !== '__placeholder__')
                .map(e => ({ stage: e.stage, role: e.role, raciType: e.raciType }));
            const saved = await api.replaceRaci(token, processId, toSave);
            setEntries(saved);
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    const displayStages = stages.filter(s => s !== '__placeholder__');
    const displayRoles = roles.filter(r => r !== '__placeholder__');

    return (
        <div>
            {error && <p className="bpp-error" style={{ marginBottom: 8 }}>{error}</p>}

            {/* Легенда */}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 10 }}>
                {RACI_TYPES.map(t => (
                    <span key={t} className={`bpp-badge ${RACI_COLORS[t]}`} title={RACI_DESCRIPTIONS[t]}>
                        {t} — {RACI_DESCRIPTIONS[t].split(' — ')[0].replace('Responsible', 'Ответственный')
                            .replace('Accountable', 'Согласующий')
                            .replace('Consulted', 'Консультант')
                            .replace('Informed', 'Информируется')}
                    </span>
                ))}
            </div>

            {/* Матрица */}
            {displayStages.length > 0 && displayRoles.length > 0 ? (
                <div className="bpp-table-wrap">
                    <table className="bpp-table">
                        <thead>
                            <tr>
                                <th>Этап \ Роль</th>
                                {displayRoles.map(role => (
                                    <th key={role} style={{ textAlign: 'center' }}>
                                        {role}
                                        <button
                                            className="bpp-btn bpp-btn-danger"
                                            style={{ padding: '0 4px', marginLeft: 4, fontSize: 10 }}
                                            onClick={() => removeRole(role)}
                                            title="Удалить роль"
                                        >✕</button>
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {displayStages.map(stage => (
                                <tr key={stage}>
                                    <td>
                                        <span style={{ fontSize: 11, fontWeight: 500 }}>{stage}</span>
                                        <button
                                            className="bpp-btn bpp-btn-danger"
                                            style={{ padding: '0 4px', marginLeft: 6, fontSize: 10 }}
                                            onClick={() => removeStage(stage)}
                                            title="Удалить этап"
                                        >✕</button>
                                    </td>
                                    {displayRoles.map(role => {
                                        const current = getCell(stage, role);
                                        return (
                                            <td key={role} className="bpp-raci-cell">
                                                <div style={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap' }}>
                                                    {RACI_TYPES.map(t => (
                                                        <button
                                                            key={t}
                                                            className={`bpp-badge ${current === t ? RACI_COLORS[t] : 'bpp-badge-gray'}`}
                                                            style={{
                                                                cursor: 'pointer',
                                                                border: current === t ? '1px solid currentColor' : '1px solid transparent',
                                                                padding: '1px 6px',
                                                                fontFamily: 'inherit',
                                                                opacity: current && current !== t ? 0.4 : 1,
                                                            }}
                                                            onClick={() => toggleCell(stage, role, t)}
                                                            title={RACI_DESCRIPTIONS[t]}
                                                        >
                                                            {t}
                                                        </button>
                                                    ))}
                                                </div>
                                            </td>
                                        );
                                    })}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ) : (
                <p className="bpp-hint" style={{ textAlign: 'center', marginBottom: 12 }}>
                    Добавьте этапы и роли для построения матрицы.
                </p>
            )}

            {/* Добавление этапа и роли */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 12 }}>
                <div>
                    <label className="bpp-label">Новый этап</label>
                    <div style={{ display: 'flex', gap: 4 }}>
                        <input className="bpp-input" value={newStage}
                            onChange={e => setNewStage(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && addStage()}
                            placeholder="Согласование" />
                        <button className="bpp-btn" onClick={addStage}>+</button>
                    </div>
                </div>
                <div>
                    <label className="bpp-label">Новая роль</label>
                    <div style={{ display: 'flex', gap: 4 }}>
                        <input className="bpp-input" value={newRole}
                            onChange={e => setNewRole(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && addRole()}
                            placeholder="Менеджер" />
                        <button className="bpp-btn" onClick={addRole}>+</button>
                    </div>
                </div>
            </div>

            <div className="bpp-btn-row">
                <button className="bpp-btn bpp-btn-primary" onClick={save} disabled={saving || !dirty}>
                    {saving ? 'Сохранение...' : 'Сохранить матрицу'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}
