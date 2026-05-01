import { useState, useEffect, useCallback, useRef } from 'react';
import * as api from '../../api/bpmApi';
import type {
    BpmProcessRoleConfigDto,
    BpmAssigneeType,
    BpmProcessRoleType,
} from '../../api/bpmApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import { getPositions } from '../../api/adminApi';
import type { PositionDto } from '../../api/adminApi';

interface Props {
    processId: string;
    token: string;
}

// ─── Вспомогательные типы ─────────────────────────────────────────────────────

/** Редактируемая запись роли (до сохранения) */
interface RoleEntry {
    localId: string;
    roleType: BpmProcessRoleType;
    assigneeType: BpmAssigneeType;
    assigneeId: string;
    displayName: string;
    sortOrder: number;
}

type SearchMode = 'User' | 'Position';

const ASSIGNEE_TYPE_LABELS: Record<BpmAssigneeType, string> = {
    User: 'Сотрудник',
    Position: 'Должность',
    Department: 'Подразделение',
};

const ASSIGNEE_TYPE_ICONS: Record<BpmAssigneeType, string> = {
    User: '👤',
    Position: '💼',
    Department: '🏢',
};

/** Вкладка «Роли» — настройка Владельца и Кураторов процесса. */
export function ProcessRolesTab({ processId, token }: Props) {
    const [entries, setEntries] = useState<RoleEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // ─── Picker состояние ──────────────────────────────────────────────────────
    const [addingRole, setAddingRole] = useState<BpmProcessRoleType | null>(null);
    const [searchMode, setSearchMode] = useState<SearchMode>('User');
    const [searchQuery, setSearchQuery] = useState('');
    const [searchResults, setSearchResults] = useState<Array<{ id: string; name: string; sub?: string }>>([]);
    const [searchLoading, setSearchLoading] = useState(false);
    const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    // ─── Загрузка ─────────────────────────────────────────────────────────────

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const data = await api.getProcessRoles(token, processId);
            setEntries(data.map(dtoToEntry));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, processId]);

    useEffect(() => { load(); }, [load]);

    // ─── Поиск назначенца ─────────────────────────────────────────────────────

    useEffect(() => {
        if (!addingRole) return;
        if (searchTimer.current) clearTimeout(searchTimer.current);
        if (!searchQuery.trim()) {
            setSearchResults([]);
            return;
        }
        searchTimer.current = setTimeout(async () => {
            setSearchLoading(true);
            try {
                if (searchMode === 'User') {
                    const emps = await getDirectoryEmployees(token, { search: searchQuery });
                    setSearchResults(emps.map(empToResult));
                } else {
                    const positions = await getPositions(token, undefined, 'Active');
                    const q = searchQuery.toLowerCase();
                    setSearchResults(
                        positions
                            .filter(p => p.name.toLowerCase().includes(q) || (p.code ?? '').toLowerCase().includes(q))
                            .map(posToResult)
                    );
                }
            } catch {
                setSearchResults([]);
            } finally {
                setSearchLoading(false);
            }
        }, 300);
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [searchQuery, searchMode, addingRole, token]);

    // ─── Мутации ──────────────────────────────────────────────────────────────

    const removeEntry = (localId: string) => {
        setEntries(prev => prev.filter(e => e.localId !== localId));
        setDirty(true);
    };

    const pickAssignee = (result: { id: string; name: string; sub?: string }) => {
        if (!addingRole) return;

        // Владелец — только один
        if (addingRole === 'Owner') {
            setEntries(prev => [
                ...prev.filter(e => e.roleType !== 'Owner'),
                makeEntry('Owner', searchMode === 'User' ? 'User' : 'Position', result),
            ]);
        } else {
            setEntries(prev => [
                ...prev,
                makeEntry('Curator', searchMode === 'User' ? 'User' : 'Position', result),
            ]);
        }
        setDirty(true);
        setAddingRole(null);
        setSearchQuery('');
        setSearchResults([]);
    };

    const cancelPicker = () => {
        setAddingRole(null);
        setSearchQuery('');
        setSearchResults([]);
    };

    // ─── Сохранение ───────────────────────────────────────────────────────────

    const save = async () => {
        setSaving(true);
        setError(null);
        try {
            const saved = await api.replaceProcessRoles(token, processId, {
                items: entries.map((e, idx) => ({
                    roleType: e.roleType,
                    assigneeType: e.assigneeType,
                    assigneeId: e.assigneeId,
                    displayName: e.displayName,
                    sortOrder: idx,
                })),
            });
            setEntries(saved.map(dtoToEntry));
            setDirty(false);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    // ─── Render ───────────────────────────────────────────────────────────────

    if (loading) return <div className="bpp-loading">Загрузка...</div>;

    const owner = entries.find(e => e.roleType === 'Owner');
    const curators = entries.filter(e => e.roleType === 'Curator');

    return (
        <div>
            {error && <p className="bpp-error" style={{ marginBottom: 8 }}>{error}</p>}

            {/* ─── Инициатор (информационно) ─────────────────────────────── */}
            <InfoBlock
                title="Инициатор"
                icon="🚀"
                text="Определяется автоматически — это пользователь, запустивший экземпляр процесса. Пул кандидатов задаётся зоной ответственности (Lane), в которой расположено стартовое событие на диаграмме."
            />

            {/* ─── Владелец ─────────────────────────────────────────────────── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Владелец процесса</div>
                <p className="bpp-hint" style={{ marginBottom: 6 }}>
                    Один пользователь или должность, ответственный за результат выполнения на всех этапах.
                </p>

                {owner ? (
                    <AssigneeChip entry={owner} onRemove={() => removeEntry(owner.localId)} />
                ) : (
                    <p className="bpp-hint" style={{ fontStyle: 'italic' }}>Владелец не назначен</p>
                )}

                {addingRole === 'Owner' ? (
                    <AssigneePicker
                        searchMode={searchMode}
                        searchQuery={searchQuery}
                        searchLoading={searchLoading}
                        searchResults={searchResults}
                        onModeChange={m => { setSearchMode(m); setSearchQuery(''); setSearchResults([]); }}
                        onQueryChange={setSearchQuery}
                        onPick={pickAssignee}
                        onCancel={cancelPicker}
                    />
                ) : (
                    <button
                        className="bpp-btn"
                        style={{ marginTop: 6 }}
                        onClick={() => { setAddingRole('Owner'); setSearchMode('User'); setSearchQuery(''); setSearchResults([]); }}
                    >
                        {owner ? '✎ Изменить владельца' : '+ Назначить владельца'}
                    </button>
                )}
            </div>

            {/* ─── Кураторы ─────────────────────────────────────────────────── */}
            <div className="bpp-group">
                <div className="bpp-group-title">Кураторы</div>
                <p className="bpp-hint" style={{ marginBottom: 6 }}>
                    Наблюдатели с правами просмотра всей информации по экземплярам. Кураторов может быть несколько.
                </p>

                {curators.length > 0 ? (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 6 }}>
                        {curators.map(e => (
                            <AssigneeChip key={e.localId} entry={e} onRemove={() => removeEntry(e.localId)} />
                        ))}
                    </div>
                ) : (
                    <p className="bpp-hint" style={{ fontStyle: 'italic' }}>Кураторы не назначены</p>
                )}

                {addingRole === 'Curator' ? (
                    <AssigneePicker
                        searchMode={searchMode}
                        searchQuery={searchQuery}
                        searchLoading={searchLoading}
                        searchResults={searchResults}
                        onModeChange={m => { setSearchMode(m); setSearchQuery(''); setSearchResults([]); }}
                        onQueryChange={setSearchQuery}
                        onPick={pickAssignee}
                        onCancel={cancelPicker}
                    />
                ) : (
                    <button
                        className="bpp-btn"
                        style={{ marginTop: 6 }}
                        onClick={() => { setAddingRole('Curator'); setSearchMode('User'); setSearchQuery(''); setSearchResults([]); }}
                    >
                        + Добавить куратора
                    </button>
                )}
            </div>

            {/* ─── Ответственный за экземпляр (информационно) ──────────────── */}
            <InfoBlock
                title="Ответственный за экземпляр"
                icon="🎯"
                text="По умолчанию совпадает с инициатором. Может быть изменён владельцем, инициатором или пользователем с соответствующими правами в ходе выполнения экземпляра (только один на экземпляр)."
            />

            {/* ─── Участник (информационно) ──────────────────────────────────── */}
            <InfoBlock
                title="Участник"
                icon="👥"
                text="Пользователь, видящий экземпляр в разделе «Мои процессы». По умолчанию: владелец и ответственный за экземпляр. Список управляем при наличии прав в ходе выполнения."
            />

            {/* ─── Информируемый (информационно) ─────────────────────────────── */}
            <InfoBlock
                title="Информируемый"
                icon="📢"
                text="Сотрудник, получающий сведения о ходе процесса организационными мерами. Не взаимодействует с системой напрямую; упоминается в документации и регламенте процесса."
            />

            {/* ─── Кнопки ───────────────────────────────────────────────────── */}
            <div className="bpp-btn-row">
                <button
                    className="bpp-btn bpp-btn-primary"
                    onClick={save}
                    disabled={saving || !dirty}
                >
                    {saving ? 'Сохранение...' : 'Сохранить роли'}
                </button>
                {!dirty && <span style={{ fontSize: 11, color: '#9ca3af', alignSelf: 'center' }}>Сохранено</span>}
            </div>
        </div>
    );
}

// ─── Вспомогательные компоненты ───────────────────────────────────────────────

function InfoBlock({ title, icon, text }: { title: string; icon: string; text: string }) {
    return (
        <div className="bpp-group" style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}>
            <div className="bpp-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span>{icon}</span>
                <span>{title}</span>
                <span className="bpp-badge bpp-badge-gray" style={{ fontSize: 10 }}>Авто</span>
            </div>
            <p className="bpp-hint" style={{ margin: 0, lineHeight: 1.5 }}>{text}</p>
        </div>
    );
}

function AssigneeChip({ entry, onRemove }: { entry: RoleEntry; onRemove: () => void }) {
    return (
        <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
            background: '#eff6ff',
            border: '1px solid #bfdbfe',
            borderRadius: 6,
            padding: '3px 8px',
            fontSize: 12,
        }}>
            <span title={ASSIGNEE_TYPE_LABELS[entry.assigneeType]}>{ASSIGNEE_TYPE_ICONS[entry.assigneeType]}</span>
            <span style={{ fontWeight: 500 }}>{entry.displayName}</span>
            <button
                style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, color: '#6b7280', fontSize: 13, lineHeight: 1 }}
                onClick={onRemove}
                title="Удалить"
            >✕</button>
        </div>
    );
}

interface AssigneePickerProps {
    searchMode: SearchMode;
    searchQuery: string;
    searchLoading: boolean;
    searchResults: Array<{ id: string; name: string; sub?: string }>;
    onModeChange: (mode: SearchMode) => void;
    onQueryChange: (q: string) => void;
    onPick: (result: { id: string; name: string; sub?: string }) => void;
    onCancel: () => void;
}

function AssigneePicker({
    searchMode,
    searchQuery,
    searchLoading,
    searchResults,
    onModeChange,
    onQueryChange,
    onPick,
    onCancel,
}: AssigneePickerProps) {
    return (
        <div style={{ marginTop: 8, border: '1px solid #d1d5db', borderRadius: 8, padding: 10, background: '#fff' }}>
            {/* Переключатель типа */}
            <div style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
                {(['User', 'Position'] as SearchMode[]).map(m => (
                    <button
                        key={m}
                        className={`bpp-btn${searchMode === m ? ' bpp-btn-primary' : ''}`}
                        style={{ padding: '2px 10px', fontSize: 11 }}
                        onClick={() => onModeChange(m)}
                    >
                        {m === 'User' ? '👤 Сотрудник' : '💼 Должность'}
                    </button>
                ))}
            </div>

            {/* Поисковая строка */}
            <input
                className="bpp-input"
                placeholder={searchMode === 'User' ? 'Поиск сотрудника...' : 'Поиск должности...'}
                value={searchQuery}
                onChange={e => onQueryChange(e.target.value)}
                autoFocus
            />

            {/* Результаты */}
            {searchLoading && (
                <p className="bpp-hint" style={{ margin: '6px 0 0' }}>Поиск...</p>
            )}
            {!searchLoading && searchResults.length > 0 && (
                <div style={{ marginTop: 6, maxHeight: 160, overflowY: 'auto', border: '1px solid #e5e7eb', borderRadius: 6 }}>
                    {searchResults.map(r => (
                        <div
                            key={r.id}
                            style={{ padding: '6px 10px', cursor: 'pointer', borderBottom: '1px solid #f3f4f6', fontSize: 12 }}
                            onMouseEnter={e => (e.currentTarget.style.background = '#f0f9ff')}
                            onMouseLeave={e => (e.currentTarget.style.background = '')}
                            onClick={() => onPick(r)}
                        >
                            <span style={{ fontWeight: 500 }}>{r.name}</span>
                            {r.sub && <span style={{ color: '#6b7280', marginLeft: 8 }}>{r.sub}</span>}
                        </div>
                    ))}
                </div>
            )}
            {!searchLoading && searchQuery && searchResults.length === 0 && (
                <p className="bpp-hint" style={{ margin: '6px 0 0' }}>Ничего не найдено</p>
            )}

            <div style={{ marginTop: 8, textAlign: 'right' }}>
                <button className="bpp-btn" style={{ fontSize: 11 }} onClick={onCancel}>Отмена</button>
            </div>
        </div>
    );
}

// ─── Утилиты ─────────────────────────────────────────────────────────────────

function dtoToEntry(dto: BpmProcessRoleConfigDto): RoleEntry {
    return {
        localId: dto.id,
        roleType: dto.roleType,
        assigneeType: dto.assigneeType,
        assigneeId: dto.assigneeId,
        displayName: dto.displayName,
        sortOrder: dto.sortOrder,
    };
}

function makeEntry(
    roleType: BpmProcessRoleType,
    assigneeType: BpmAssigneeType,
    result: { id: string; name: string }
): RoleEntry {
    return {
        localId: crypto.randomUUID(),
        roleType,
        assigneeType,
        assigneeId: result.id,
        displayName: result.name,
        sortOrder: 0,
    };
}

function empToResult(emp: DirectoryEmployeeDto): { id: string; name: string; sub?: string } {
    return {
        id: emp.id,
        name: emp.displayName,
        sub: emp.position ?? emp.departmentName,
    };
}

function posToResult(pos: PositionDto): { id: string; name: string; sub?: string } {
    return {
        id: pos.id,
        name: pos.name,
        sub: pos.code ?? undefined,
    };
}
