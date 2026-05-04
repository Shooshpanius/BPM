import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    listTasks, createTask, exportTasksCsv, bulkVerifyTasks,
    getTaskSavedFilters, createTaskSavedFilter, deleteTaskSavedFilter,
    TASK_STATUS_LABELS, TASK_PRIORITY_LABELS,
} from '../../api/tasksApi';
import type { TaskSummaryDto, TaskStatus, TaskPriority, TaskSavedFilterDto } from '../../api/tasksApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import './TasksPage.css';

interface TasksPageProps {
    onOpenTask: (id: string) => void;
}

type TabId = 'all' | 'my';

/** Страница списка задач (FR-TASK-01.1, FR-TASK-02.3). */
export function TasksPage({ onOpenTask }: TasksPageProps) {
    const { accessToken: token, userId } = useAuth();

    const [tab, setTab] = useState<TabId>('all');
    const [tasks, setTasks] = useState<TaskSummaryDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // ─── Базовые фильтры
    const [filterStatus, setFilterStatus] = useState('');
    const [filterPriority, setFilterPriority] = useState('');
    const [filterSearch, setFilterSearch] = useState('');
    const [filterOverdue, setFilterOverdue] = useState(false);

    // ─── Расширенный поиск (FR-TASK-02.3)
    const [showAdvanced, setShowAdvanced] = useState(false);
    const [filterAuthorId, setFilterAuthorId] = useState('');
    const [filterAuthorName, setFilterAuthorName] = useState('');
    const [filterAuthorSearch, setFilterAuthorSearch] = useState('');
    const [filterTagValue, setFilterTagValue] = useState('');
    const [filterDateFrom, setFilterDateFrom] = useState('');
    const [filterDateTo, setFilterDateTo] = useState('');
    const [filterSortBy, setFilterSortBy] = useState('created_at');
    const [filterSortDir, setFilterSortDir] = useState('desc');

    // ─── Сохранённые фильтры (FR-TASK-02.3)
    const [savedFilters, setSavedFilters] = useState<TaskSavedFilterDto[]>([]);
    const [showSaveFilter, setShowSaveFilter] = useState(false);
    const [newFilterName, setNewFilterName] = useState('');
    const [savingFilter, setSavingFilter] = useState(false);

    // ─── Выбор для массовых операций
    const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
    const [bulkVerifyMsg, setBulkVerifyMsg] = useState<string | null>(null);

    // ─── Форма создания задачи
    const [showCreate, setShowCreate] = useState(false);
    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);
    const [empSearch, setEmpSearch] = useState('');
    const [form, setForm] = useState({
        subject: '',
        assigneeUserId: '',
        assigneeName: '',
        startDate: new Date().toISOString().slice(0, 16),
        dueDate: '',
        priority: 'Medium' as TaskPriority,
        description: '',
        categoryId: '',
        plannedEffortMinutes: '',
    });
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState<string | null>(null);

    const filteredEmployees = employees.filter(e =>
        e.displayName.toLowerCase().includes(empSearch.toLowerCase())
    );
    const filteredAuthorEmployees = employees.filter(e =>
        e.displayName.toLowerCase().includes(filterAuthorSearch.toLowerCase())
    );

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            const data = await listTasks(token, {
                status: filterStatus || undefined,
                priority: filterPriority || undefined,
                search: filterSearch || undefined,
                isOverdue: filterOverdue || undefined,
                assigneeId: tab === 'my' && userId ? userId : undefined,
                authorId: filterAuthorId || undefined,
                tagValue: filterTagValue || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
                sortBy: filterSortBy,
                sortDir: filterSortDir,
            });
            setTasks(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, tab, filterStatus, filterPriority, filterSearch, filterOverdue, userId,
        filterAuthorId, filterTagValue, filterDateFrom, filterDateTo, filterSortBy, filterSortDir]);

    useEffect(() => { load(); }, [load]);

    // Загрузка сохранённых фильтров
    useEffect(() => {
        if (!token) return;
        getTaskSavedFilters(token).then(setSavedFilters).catch(() => { /* игнорируем */ });
    }, [token]);

    // Загрузка списка сотрудников для фильтра «Автор»
    useEffect(() => {
        if (!token || employees.length > 0) return;
        if (showAdvanced) {
            getDirectoryEmployees(token, {}).then(setEmployees).catch(() => { /* игнорируем */ });
        }
    }, [token, showAdvanced, employees.length]);

    const handleOpenCreate = async () => {
        setShowCreate(true);
        if (employees.length === 0 && token) {
            try { setEmployees(await getDirectoryEmployees(token, {})); } catch { /* игнорируем */ }
        }
    };

    const handleCreate = async () => {
        if (!token) return;
        if (!form.subject.trim()) { setSaveError('Укажите тему задачи'); return; }
        if (!form.assigneeUserId) { setSaveError('Выберите исполнителя'); return; }
        if (!form.dueDate) { setSaveError('Укажите срок завершения'); return; }
        setSaving(true);
        setSaveError(null);
        try {
            const task = await createTask(token, {
                subject: form.subject.trim(),
                assigneeUserId: form.assigneeUserId,
                startDate: new Date(form.startDate).toISOString(),
                dueDate: new Date(form.dueDate).toISOString(),
                priority: form.priority,
                description: form.description || undefined,
                categoryId: form.categoryId || undefined,
                plannedEffortMinutes: form.plannedEffortMinutes ? parseInt(form.plannedEffortMinutes) : undefined,
            });
            setShowCreate(false);
            resetForm();
            onOpenTask(task.id);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : 'Ошибка создания');
        } finally {
            setSaving(false);
        }
    };

    const resetForm = () => {
        setForm({ subject: '', assigneeUserId: '', assigneeName: '', startDate: new Date().toISOString().slice(0, 16), dueDate: '', priority: 'Medium', description: '', categoryId: '', plannedEffortMinutes: '' });
        setEmpSearch('');
        setSaveError(null);
    };

    const handleExport = async () => {
        if (!token) return;
        try {
            const blob = await exportTasksCsv(token, {
                status: filterStatus || undefined,
                priority: filterPriority || undefined,
                search: filterSearch || undefined,
                assigneeId: tab === 'my' && userId ? userId : undefined,
                authorId: filterAuthorId || undefined,
                tagValue: filterTagValue || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
            });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'tasks.csv';
            a.click();
            URL.revokeObjectURL(url);
        } catch { /* игнорируем */ }
    };

    const handleBulkVerify = async () => {
        if (!token || selectedIds.size === 0) return;
        setBulkVerifyMsg(null);
        try {
            const result = await bulkVerifyTasks(token, Array.from(selectedIds));
            setBulkVerifyMsg(`Контроль принят по ${result.acceptedCount} задач(ам)`);
            setSelectedIds(new Set());
            load();
        } catch (e) {
            setBulkVerifyMsg(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    // ─── Сохранение фильтра
    const handleSaveFilter = async () => {
        if (!token || !newFilterName.trim()) return;
        setSavingFilter(true);
        try {
            const filterParams = {
                status: filterStatus || undefined,
                priority: filterPriority || undefined,
                search: filterSearch || undefined,
                isOverdue: filterOverdue || undefined,
                authorId: filterAuthorId || undefined,
                tagValue: filterTagValue || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
                sortBy: filterSortBy,
                sortDir: filterSortDir,
            };
            const newFilter = await createTaskSavedFilter(token, newFilterName.trim(), JSON.stringify(filterParams));
            setSavedFilters(prev => [...prev, newFilter]);
            setShowSaveFilter(false);
            setNewFilterName('');
        } catch { /* игнорируем */ } finally {
            setSavingFilter(false);
        }
    };

    // Применить сохранённый фильтр
    const handleApplySavedFilter = (f: TaskSavedFilterDto) => {
        try {
            const params = JSON.parse(f.filterJson);
            setFilterStatus(params.status ?? '');
            setFilterPriority(params.priority ?? '');
            setFilterSearch(params.search ?? '');
            setFilterOverdue(params.isOverdue ?? false);
            setFilterAuthorId(params.authorId ?? '');
            setFilterTagValue(params.tagValue ?? '');
            setFilterDateFrom(params.dateFrom ?? '');
            setFilterDateTo(params.dateTo ?? '');
            setFilterSortBy(params.sortBy ?? 'created_at');
            setFilterSortDir(params.sortDir ?? 'desc');
            setShowAdvanced(true);
        } catch { /* игнорируем */ }
    };

    const handleDeleteSavedFilter = async (id: string) => {
        if (!token) return;
        try {
            await deleteTaskSavedFilter(token, id);
            setSavedFilters(prev => prev.filter(f => f.id !== id));
        } catch { /* игнорируем */ }
    };

    const toggleSelect = (id: string, e: React.MouseEvent) => {
        e.stopPropagation();
        setSelectedIds(prev => {
            const next = new Set(prev);
            next.has(id) ? next.delete(id) : next.add(id);
            return next;
        });
    };

    const getStatusClass = (status: TaskStatus, isOverdue: boolean) => {
        if (isOverdue) return 'task-badge task-badge--overdue';
        if (status === 'Done' || status === 'DoneControlled') return 'task-badge task-badge--done';
        if (status === 'Closed') return 'task-badge task-badge--closed';
        if (status === 'InProgress') return 'task-badge task-badge--in-progress';
        return 'task-badge task-badge--default';
    };

    const getPriorityClass = (priority: TaskPriority) => {
        const map: Record<TaskPriority, string> = { Critical: 'task-priority--critical', High: 'task-priority--high', Medium: 'task-priority--medium', Low: 'task-priority--low' };
        return `task-priority ${map[priority]}`;
    };

    const hasAdvancedFilters = !!(filterAuthorId || filterTagValue || filterDateFrom || filterDateTo);

    return (
        <div className="tasks-page">
            <div className="tasks-page__header">
                <h2 className="tasks-page__title">Задачи</h2>
                <div className="tasks-page__actions">
                    <button className="tasks-page__btn tasks-page__btn--primary" onClick={handleOpenCreate}>+ Создать задачу</button>
                    <button className="tasks-page__btn" onClick={handleExport}>Экспорт CSV</button>
                    {selectedIds.size > 0 && (
                        <button
                            className="tasks-page__btn tasks-page__btn--success"
                            onClick={handleBulkVerify}
                            title="Принять контроль по выбранным задачам"
                        >
                            ✓ Подтвердить выбранные ({selectedIds.size})
                        </button>
                    )}
                </div>
            </div>
            {bulkVerifyMsg && (
                <div style={{ padding: '8px 16px', background: '#f6ffed', border: '1px solid #b7eb8f', borderRadius: 4, marginBottom: 12, fontSize: 13 }}>
                    {bulkVerifyMsg}
                </div>
            )}

            <div className="tasks-page__tabs">
                {(['all', 'my'] as TabId[]).map(t => (
                    <button key={t} className={`tasks-page__tab${tab === t ? ' tasks-page__tab--active' : ''}`} onClick={() => setTab(t)}>
                        {t === 'all' ? 'Все задачи' : 'Мои задачи'}
                    </button>
                ))}
            </div>

            {/* ── Основные фильтры + переключатель расширенного поиска */}
            <div className="tasks-page__filters">
                <input className="tasks-page__search" type="text" placeholder="Поиск по теме..." value={filterSearch} onChange={e => setFilterSearch(e.target.value)} />
                <select className="tasks-page__select" value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
                    <option value="">Все статусы</option>
                    {(Object.keys(TASK_STATUS_LABELS) as TaskStatus[]).map(s => (
                        <option key={s} value={s}>{TASK_STATUS_LABELS[s]}</option>
                    ))}
                </select>
                <select className="tasks-page__select" value={filterPriority} onChange={e => setFilterPriority(e.target.value)}>
                    <option value="">Все приоритеты</option>
                    {(Object.keys(TASK_PRIORITY_LABELS) as TaskPriority[]).map(p => (
                        <option key={p} value={p}>{TASK_PRIORITY_LABELS[p]}</option>
                    ))}
                </select>
                <label className="tasks-page__overdue-label">
                    <input type="checkbox" checked={filterOverdue} onChange={e => setFilterOverdue(e.target.checked)} /> Только просроченные
                </label>
                <button
                    className={`tasks-page__btn${hasAdvancedFilters ? ' tasks-page__btn--accent' : ''}`}
                    onClick={() => setShowAdvanced(v => !v)}
                    title="Расширенный поиск"
                >
                    🔎 {showAdvanced ? 'Скрыть фильтры' : 'Расширенный поиск'}
                    {hasAdvancedFilters && ' •'}
                </button>
                <button className="tasks-page__btn" onClick={load}>Применить</button>
            </div>

            {/* ── Расширенный поиск (FR-TASK-02.3) ──────────────────────────── */}
            {showAdvanced && (
                <div className="tasks-page__advanced-filters">
                    <div className="tasks-page__advanced-body">
                        {/* Автор */}
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Автор</label>
                            <div style={{ position: 'relative' }}>
                                <input
                                    className="tasks-page__input"
                                    placeholder="Начните вводить имя..."
                                    value={filterAuthorSearch || filterAuthorName}
                                    onChange={e => { setFilterAuthorSearch(e.target.value); if (!e.target.value) { setFilterAuthorId(''); setFilterAuthorName(''); } }}
                                />
                                {filterAuthorSearch && filteredAuthorEmployees.length > 0 && (
                                    <div className="tasks-page__dropdown">
                                        {filteredAuthorEmployees.slice(0, 6).map(e => (
                                            <div key={e.userId}
                                                className="tasks-page__dropdown-item"
                                                onClick={() => { setFilterAuthorId(e.userId); setFilterAuthorName(e.displayName); setFilterAuthorSearch(''); }}
                                            >{e.displayName}</div>
                                        ))}
                                    </div>
                                )}
                                {filterAuthorId && (
                                    <div style={{ fontSize: 12, color: '#555', marginTop: 2 }}>✓ {filterAuthorName}</div>
                                )}
                            </div>
                        </div>

                        {/* Тег */}
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Тег</label>
                            <input
                                className="tasks-page__input"
                                placeholder="Значение тега"
                                value={filterTagValue}
                                onChange={e => setFilterTagValue(e.target.value)}
                            />
                        </div>

                        {/* Срок от/до */}
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Срок с</label>
                            <input
                                className="tasks-page__input"
                                type="date"
                                value={filterDateFrom}
                                onChange={e => setFilterDateFrom(e.target.value)}
                            />
                        </div>
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Срок по</label>
                            <input
                                className="tasks-page__input"
                                type="date"
                                value={filterDateTo}
                                onChange={e => setFilterDateTo(e.target.value)}
                            />
                        </div>

                        {/* Сортировка */}
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Сортировка</label>
                            <select className="tasks-page__input" value={filterSortBy} onChange={e => setFilterSortBy(e.target.value)}>
                                <option value="created_at">Дата создания</option>
                                <option value="due_date">Срок завершения</option>
                                <option value="priority">Приоритет</option>
                                <option value="status">Статус</option>
                            </select>
                        </div>
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Направление</label>
                            <select className="tasks-page__input" value={filterSortDir} onChange={e => setFilterSortDir(e.target.value)}>
                                <option value="desc">По убыванию</option>
                                <option value="asc">По возрастанию</option>
                            </select>
                        </div>

                        {/* Кнопки */}
                        <div className="tasks-page__adv-actions">
                            <button className="tasks-page__btn tasks-page__btn--primary" onClick={load}>Найти</button>
                            <button className="tasks-page__btn" onClick={() => {
                                setFilterAuthorId(''); setFilterAuthorName(''); setFilterAuthorSearch('');
                                setFilterTagValue(''); setFilterDateFrom(''); setFilterDateTo('');
                                setFilterSortBy('created_at'); setFilterSortDir('desc');
                            }}>Сбросить</button>
                            <button className="tasks-page__btn" onClick={() => setShowSaveFilter(true)} title="Сохранить текущий набор фильтров">
                                💾 Сохранить как фильтр
                            </button>
                        </div>
                    </div>

                    {/* ── Дерево сохранённых фильтров справа */}
                    {savedFilters.length > 0 && (
                        <div className="tasks-page__saved-filters">
                            <div className="tasks-page__saved-filters-title">Сохранённые фильтры</div>
                            {savedFilters.map(f => (
                                <div key={f.id} className="tasks-page__saved-filter-item">
                                    <button
                                        className="tasks-page__saved-filter-name"
                                        onClick={() => handleApplySavedFilter(f)}
                                        title="Применить фильтр"
                                    >
                                        📂 {f.name}
                                    </button>
                                    <button
                                        className="tasks-page__saved-filter-del"
                                        onClick={() => handleDeleteSavedFilter(f.id)}
                                        title="Удалить фильтр"
                                    >×</button>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            )}

            {loading && <div className="tasks-page__loading">Загрузка...</div>}
            {error && <div className="tasks-page__error">{error}</div>}
            {!loading && !error && tasks.length === 0 && <div className="tasks-page__empty">Задачи не найдены</div>}

            {!loading && tasks.length > 0 && (
                <table className="tasks-page__table">
                    <thead>
                        <tr>
                            <th style={{ width: 32 }}></th>
                            <th>№</th>
                            <th>Тема</th>
                            <th>Статус</th>
                            <th>Приоритет</th>
                            <th>Исполнитель</th>
                            <th>Срок</th>
                            <th>Теги</th>
                        </tr>
                    </thead>
                    <tbody>
                        {tasks.map(task => (
                            <tr key={task.id} className={`tasks-page__row${task.isOverdue ? ' tasks-page__row--overdue' : ''}`} onClick={() => onOpenTask(task.id)}>
                                <td onClick={e => toggleSelect(task.id, e)} style={{ textAlign: 'center', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={selectedIds.has(task.id)}
                                        onChange={() => {/* handled by td onClick */}}
                                        onClick={e => { e.stopPropagation(); }}
                                    />
                                </td>
                                <td className="tasks-page__num">T-{task.number}</td>
                                <td className="tasks-page__subject">{task.subject}</td>
                                <td><span className={getStatusClass(task.status, task.isOverdue)}>{TASK_STATUS_LABELS[task.status]}</span></td>
                                <td><span className={getPriorityClass(task.priority)}>{TASK_PRIORITY_LABELS[task.priority]}</span></td>
                                <td>{task.assigneeName}</td>
                                <td>{new Date(task.dueDate).toLocaleDateString('ru-RU')}</td>
                                <td className="tasks-page__tags">{task.tags.slice(0, 3).map(tag => <span key={tag} className="tasks-page__tag">{tag}</span>)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}

            {/* ── Диалог сохранения фильтра */}
            {showSaveFilter && (
                <div className="tasks-page__overlay" onClick={() => setShowSaveFilter(false)}>
                    <div className="tasks-page__dialog" style={{ maxWidth: 420 }} onClick={e => e.stopPropagation()}>
                        <h3 className="tasks-page__dialog-title">Сохранить фильтр</h3>
                        <label className="tasks-page__label">
                            Название фильтра
                            <input
                                className="tasks-page__input"
                                placeholder="Например: Мои высокоприоритетные"
                                value={newFilterName}
                                onChange={e => setNewFilterName(e.target.value)}
                                autoFocus
                            />
                        </label>
                        <div className="tasks-page__dialog-footer">
                            <button
                                className="tasks-page__btn tasks-page__btn--primary"
                                onClick={handleSaveFilter}
                                disabled={savingFilter || !newFilterName.trim()}
                            >
                                {savingFilter ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className="tasks-page__btn" onClick={() => setShowSaveFilter(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Диалог создания задачи */}
            {showCreate && (
                <div className="tasks-page__overlay" onClick={() => { setShowCreate(false); resetForm(); }}>
                    <div className="tasks-page__dialog" onClick={e => e.stopPropagation()}>
                        <h3 className="tasks-page__dialog-title">Создать задачу</h3>

                        <label className="tasks-page__label">
                            Тема <span className="tasks-page__required">*</span>
                            <input className="tasks-page__input" value={form.subject} onChange={e => setForm(f => ({ ...f, subject: e.target.value }))} placeholder="Краткая формулировка задачи" />
                        </label>

                        <label className="tasks-page__label">
                            Исполнитель <span className="tasks-page__required">*</span>
                            <input className="tasks-page__input" placeholder="Поиск сотрудника..." value={empSearch} onChange={e => setEmpSearch(e.target.value)} />
                            {empSearch && filteredEmployees.length > 0 && (
                                <div className="tasks-page__dropdown">
                                    {filteredEmployees.slice(0, 8).map(e => (
                                        <div key={e.userId}
                                            className={`tasks-page__dropdown-item${form.assigneeUserId === e.userId ? ' tasks-page__dropdown-item--selected' : ''}`}
                                            onClick={() => { setForm(f => ({ ...f, assigneeUserId: e.userId, assigneeName: e.displayName })); setEmpSearch(e.displayName); }}
                                        >{e.displayName}</div>
                                    ))}
                                </div>
                            )}
                        </label>

                        <div className="tasks-page__row-fields">
                            <label className="tasks-page__label">
                                Дата начала
                                <input className="tasks-page__input" type="datetime-local" value={form.startDate} onChange={e => setForm(f => ({ ...f, startDate: e.target.value }))} />
                            </label>
                            <label className="tasks-page__label">
                                Срок завершения <span className="tasks-page__required">*</span>
                                <input className="tasks-page__input" type="datetime-local" value={form.dueDate} onChange={e => setForm(f => ({ ...f, dueDate: e.target.value }))} />
                            </label>
                        </div>

                        <label className="tasks-page__label">
                            Приоритет
                            <select className="tasks-page__input" value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value as TaskPriority }))}>
                                {(Object.keys(TASK_PRIORITY_LABELS) as TaskPriority[]).map(p => <option key={p} value={p}>{TASK_PRIORITY_LABELS[p]}</option>)}
                            </select>
                        </label>

                        <label className="tasks-page__label">
                            Описание
                            <textarea className="tasks-page__input tasks-page__textarea" value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} rows={3} />
                        </label>

                        <div className="tasks-page__row-fields">
                            <label className="tasks-page__label">
                                Категория
                                <input className="tasks-page__input" value={form.categoryId} onChange={e => setForm(f => ({ ...f, categoryId: e.target.value }))} placeholder="Категория задачи" />
                            </label>
                            <label className="tasks-page__label">
                                Трудозатраты (мин.)
                                <input className="tasks-page__input" type="number" min="0" value={form.plannedEffortMinutes} onChange={e => setForm(f => ({ ...f, plannedEffortMinutes: e.target.value }))} />
                            </label>
                        </div>

                        {saveError && <div className="tasks-page__error">{saveError}</div>}

                        <div className="tasks-page__dialog-footer">
                            <button className="tasks-page__btn tasks-page__btn--primary" onClick={handleCreate} disabled={saving}>
                                {saving ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className="tasks-page__btn" onClick={() => { setShowCreate(false); resetForm(); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
