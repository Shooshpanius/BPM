import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    listTasks, createTask, exportTasksCsv, exportTasksExcel, bulkVerifyTasks,
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

/** Группы задач (FR-TASK-02.2). */
type GroupId = 'all' | 'incoming' | 'outgoing' | 'control' | 'co-exec';
const GROUP_LABELS: Record<GroupId, string> = {
    all: 'Все задачи',
    incoming: 'Входящие',
    outgoing: 'Исходящие',
    control: 'На контроле',
    'co-exec': 'Соисполнение',
};

/** Поля для сортировки. */
type SortField = 'created_at' | 'due_date' | 'priority' | 'status' | 'subject';

/** Режим отображения: таблица / карточки (FR-TASK-02.2). */
type ViewMode = 'table' | 'card';

/** Группировка в таблице (FR-TASK-02.2). */
type GroupByField = '' | 'priority' | 'status' | 'categoryId' | 'assignee' | 'dueDate';

/** Возвращает CSS-класс строки для цветового кодирования (FR-TASK-02.2). */
function getRowClass(task: TaskSummaryDto): string {
    if (task.isOverdue) return 'tasks-row--overdue';
    if (task.status === 'InProgress') return 'tasks-row--inprogress';
    if (task.status === 'CannotDo' || task.status === 'CannotDoNeedsControl') return 'tasks-row--cannotdo';
    if (task.status === 'Done' || task.status === 'DoneControlled' || task.status === 'Closed') return 'tasks-row--done';
    if (task.status === 'New' || task.status === 'Read') return 'tasks-row--new';
    return '';
}

/**
 * Возвращает бакет для группировки по дате дедлайна (FR-TASK-02.2).
 * «Просрочено» / «Сегодня» / «На этой неделе» / «Позже»
 */
function getDueDateBucket(dueDate?: string): string {
    if (!dueDate) return 'Без срока';
    const due = new Date(dueDate);
    const now = new Date();
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const todayEnd = new Date(todayStart.getTime() + 86400_000);
    const weekEnd = new Date(todayStart.getTime() + 7 * 86400_000);
    if (due < todayStart) return '🔴 Просрочено';
    if (due < todayEnd)   return '🟡 Сегодня';
    if (due < weekEnd)    return '🔵 На этой неделе';
    return '⚪ Позже';
}

/** Иконки видов задач (FR-TASK-02.2). */
function TaskKindIcon({ kind, scheduledAt, openQuestionCount }: { kind: string; scheduledAt?: string; openQuestionCount: number }) {
    if (openQuestionCount > 0) return <span title={`Вопросов: ${openQuestionCount}`}>❓</span>;
    if (scheduledAt) return <span title="Запланировано">📅</span>;
    switch (kind) {
        case 'ProcessTask': return <span title="Задача по процессу">⚙️</span>;
        case 'Periodic':    return <span title="Периодическая задача">🔄</span>;
        case 'Resolution':  return <span title="Задача-резолюция">📋</span>;
        default:            return <span title="Обычная задача">✅</span>;
    }
}

/** Страница списка задач (FR-TASK-01.1, FR-TASK-02.2, FR-TASK-02.3). */
export function TasksPage({ onOpenTask }: TasksPageProps) {
    const { accessToken: token } = useAuth();

    const [group, setGroup] = useState<GroupId>('incoming');
    // ─── Быстрые sub-фильтры внутри группы (FR-TASK-02.2)
    const [subFilter, setSubFilter] = useState<'all' | 'active' | 'overdue'>('all');
    const [tasks, setTasks] = useState<TaskSummaryDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // ─── Режим отображения и группировка (FR-TASK-02.2)
    const [viewMode, setViewMode] = useState<ViewMode>('table');
    const [groupBy, setGroupBy] = useState<GroupByField>('');

    // ─── Базовые фильтры
    const [filterStatus, setFilterStatus] = useState('');
    const [filterPriority, setFilterPriority] = useState('');
    const [filterSearch, setFilterSearch] = useState('');
    const [filterOverdue, setFilterOverdue] = useState(false);

    // ─── Сортировка по клику на заголовок таблицы (FR-TASK-02.2)
    const [sortField, setSortField] = useState<SortField>('created_at');
    const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

    const handleHeaderSort = (field: SortField) => {
        if (sortField === field) {
            setSortDir(d => d === 'asc' ? 'desc' : 'asc');
        } else {
            setSortField(field);
            setSortDir('desc');
        }
    };

    // ─── Расширенный поиск (FR-TASK-02.3)
    const [showAdvanced, setShowAdvanced] = useState(false);
    const [filterAuthorId, setFilterAuthorId] = useState('');
    const [filterAuthorName, setFilterAuthorName] = useState('');
    const [filterAuthorSearch, setFilterAuthorSearch] = useState('');
    const [filterTagValue, setFilterTagValue] = useState('');
    const [filterDateFrom, setFilterDateFrom] = useState('');
    const [filterDateTo, setFilterDateTo] = useState('');

    // ─── EQL-поиск (FR-TASK-02.2)
    const [filterEql, setFilterEql] = useState('');
    const [eqlError, setEqlError] = useState<string | null>(null);

    // ─── Сохранённые фильтры (FR-TASK-02.3)
    const [savedFilters, setSavedFilters] = useState<TaskSavedFilterDto[]>([]);
    const [showSaveFilter, setShowSaveFilter] = useState(false);
    const [newFilterName, setNewFilterName] = useState('');
    const [savingFilter, setSavingFilter] = useState(false);

    // ─── Выбор для массовых операций
    const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
    const [bulkVerifyMsg, setBulkVerifyMsg] = useState<string | null>(null);

    // ─── Подсветка кнопок при клике мимо модального окна
    const [shakeTarget, setShakeTarget] = useState<string | null>(null);
    const shakeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const triggerShake = (target: string) => {
        if (shakeTimerRef.current) clearTimeout(shakeTimerRef.current);
        setShakeTarget(target);
        shakeTimerRef.current = setTimeout(() => setShakeTarget(null), 900);
    };
    const sc = (target: string) => shakeTarget === target ? ' btn-flash' : '';

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
        setEqlError(null);
        try {
            // Вычисляем overdue/status из sub-фильтра
            const subOverdue = subFilter === 'overdue' ? true : (filterOverdue || undefined);
            // «Активные» = незавершённые статусы
            const activeStatuses = 'New,Read,InProgress,OnApproval,DoneNeedsControl,CannotDoNeedsControl';
            const subStatus = subFilter === 'active' ? activeStatuses : (filterStatus || undefined);
            const data = await listTasks(token, {
                status: subStatus,
                priority: filterPriority || undefined,
                search: filterSearch || undefined,
                isOverdue: subOverdue,
                group: group !== 'all' ? group : undefined,
                authorId: filterAuthorId || undefined,
                tagValue: filterTagValue || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
                sortBy: sortField,
                sortDir,
                eql: filterEql || undefined,
            });
            setTasks(data);
        } catch (e) {
            const msg = e instanceof Error ? e.message : 'Ошибка загрузки';
            if (msg.startsWith('EQL:')) {
                setEqlError(msg);
            } else {
                setError(msg);
            }
        } finally {
            setLoading(false);
        }
    }, [token, group, subFilter, filterStatus, filterPriority, filterSearch, filterOverdue,
        filterAuthorId, filterTagValue, filterDateFrom, filterDateTo, sortField, sortDir, filterEql]);

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
            getDirectoryEmployees(token, {}).then(data => setEmployees(data.items)).catch(() => { /* игнорируем */ });
        }
    }, [token, showAdvanced, employees.length]);

    const handleOpenCreate = async () => {
        setShowCreate(true);
        if (employees.length === 0 && token) {
            try { setEmployees((await getDirectoryEmployees(token, {})).items); } catch { /* игнорируем */ }
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

    const handleExport = async (format: 'csv' | 'xlsx' = 'csv') => {
        if (!token) return;
        try {
            const params = {
                status: filterStatus || undefined,
                priority: filterPriority || undefined,
                search: filterSearch || undefined,
                group: group !== 'all' ? group : undefined,
                authorId: filterAuthorId || undefined,
                tagValue: filterTagValue || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
            };
            let blob: Blob;
            let filename: string;
            if (format === 'xlsx') {
                blob = await exportTasksExcel(token, params);
                filename = 'tasks.xlsx';
            } else {
                blob = await exportTasksCsv(token, params);
                filename = 'tasks.csv';
            }
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
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
                sortBy: sortField,
                sortDir,
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
            if (params.sortBy) setSortField(params.sortBy as SortField);
            if (params.sortDir) setSortDir(params.sortDir as 'asc' | 'desc');
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

    // ─── Группировка отображения (FR-TASK-02.2) ───────────────────────────────
    const groupedTasks = (() => {
        if (!groupBy) return [{ key: '', tasks }];
        const map = new Map<string, TaskSummaryDto[]>();
        tasks.forEach(t => {
            let fieldValue: string;
            if (groupBy === 'priority') fieldValue = t.priority;
            else if (groupBy === 'status') fieldValue = t.status;
            else if (groupBy === 'categoryId') fieldValue = t.categoryId ?? '(не задано)';
            else if (groupBy === 'assignee') fieldValue = t.assigneeName ?? '(не задано)';
            else if (groupBy === 'dueDate') fieldValue = getDueDateBucket(t.dueDate);
            else fieldValue = '(не задано)';
            if (!map.has(fieldValue)) map.set(fieldValue, []);
            map.get(fieldValue)!.push(t);
        });
        // Для дедлайна сортируем бакеты в логичном порядке
        if (groupBy === 'dueDate') {
            const order = ['🔴 Просрочено', '🟡 Сегодня', '🔵 На этой неделе', '⚪ Позже', 'Без срока'];
            return order.filter(k => map.has(k)).map(k => ({ key: k, tasks: map.get(k)! }));
        }
        return Array.from(map.entries()).map(([key, t]) => ({ key, tasks: t }));
    })();

    return (
        <div className="tasks-page">
            <div className="tasks-page__header">
                <h2 className="tasks-page__title">Задачи</h2>
                <div className="tasks-page__actions">
                    <button className="tasks-page__btn tasks-page__btn--primary" onClick={handleOpenCreate}>+ Создать задачу</button>
                    <button className="tasks-page__btn" onClick={() => handleExport('csv')}>⬇ CSV</button>
                    <button className="tasks-page__btn" onClick={() => handleExport('xlsx')}>⬇ Excel</button>
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

            {/* ── FR-TASK-02.2: Группы задач (вкладки) */}
            <div className="tasks-page__tabs">
                {(Object.keys(GROUP_LABELS) as GroupId[]).map(g => (
                    <button key={g} className={`tasks-page__tab${group === g ? ' tasks-page__tab--active' : ''}`} onClick={() => { setGroup(g); setSubFilter('all'); }}>
                        {GROUP_LABELS[g]}
                    </button>
                ))}
            </div>

            {/* ── FR-TASK-02.2: Быстрые sub-фильтры внутри группы */}
            <div className="tasks-page__sub-tabs">
                {(['all', 'active', 'overdue'] as const).map(sf => (
                    <button
                        key={sf}
                        className={`tasks-page__sub-tab${subFilter === sf ? ' tasks-page__sub-tab--active' : ''}`}
                        onClick={() => setSubFilter(sf)}
                    >
                        {sf === 'all' ? 'Все' : sf === 'active' ? 'Активные' : 'Просроченные'}
                    </button>
                ))}
            </div>

            {/* ── FR-TASK-02.2: Режим отображения + Группировка */}
            <div className="tasks-page__view-toolbar">
                <span style={{ fontSize: 12, color: '#888' }}>Вид:</span>
                <button
                    className={`tasks-page__btn${viewMode === 'table' ? ' tasks-page__btn--accent' : ''}`}
                    onClick={() => setViewMode('table')}
                    title="Таблица"
                >≡ Таблица</button>
                <button
                    className={`tasks-page__btn${viewMode === 'card' ? ' tasks-page__btn--accent' : ''}`}
                    onClick={() => setViewMode('card')}
                    title="Карточки"
                >⊞ Карточки</button>
                <span style={{ fontSize: 12, color: '#888', marginLeft: 12 }}>Группировать:</span>
                <select className="tasks-page__select" value={groupBy} onChange={e => setGroupBy(e.target.value as GroupByField)}>
                    <option value="">Без группировки</option>
                    <option value="priority">По приоритету</option>
                    <option value="status">По статусу</option>
                    <option value="categoryId">По категории</option>
                    <option value="assignee">По исполнителю</option>
                    <option value="dueDate">По сроку</option>
                </select>
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
                            <select className="tasks-page__input" value={sortField} onChange={e => setSortField(e.target.value as SortField)}>
                                <option value="created_at">Дата создания</option>
                                <option value="due_date">Срок завершения</option>
                                <option value="priority">Приоритет</option>
                                <option value="status">Статус</option>
                                <option value="subject">Тема</option>
                            </select>
                        </div>
                        <div className="tasks-page__adv-field">
                            <label className="tasks-page__adv-label">Направление</label>
                            <select className="tasks-page__input" value={sortDir} onChange={e => setSortDir(e.target.value as 'asc' | 'desc')}>
                                <option value="desc">По убыванию</option>
                                <option value="asc">По возрастанию</option>
                            </select>
                        </div>

                        {/* EQL-поиск (FR-TASK-02.2) */}
                        <div className="tasks-page__adv-field tasks-page__adv-field--full">
                            <label className="tasks-page__adv-label">
                                EQL-запрос
                                <span title="Пример: status:InProgress AND priority:High&#10;Поля: status, priority, tag, category, overdue, search" style={{ marginLeft: 4, cursor: 'help', color: '#6b7280' }}>ℹ️</span>
                            </label>
                            <input
                                className="tasks-page__input"
                                placeholder="Например: status:InProgress AND priority:High"
                                value={filterEql}
                                onChange={e => { setFilterEql(e.target.value); setEqlError(null); }}
                            />
                            {eqlError && <div style={{ color: '#dc2626', fontSize: 12, marginTop: 2 }}>{eqlError}</div>}
                        </div>

                        {/* Кнопки */}
                        <div className="tasks-page__adv-actions">
                            <button className="tasks-page__btn tasks-page__btn--primary" onClick={load}>Найти</button>
                            <button className="tasks-page__btn" onClick={() => {
                                setFilterAuthorId(''); setFilterAuthorName(''); setFilterAuthorSearch('');
                                setFilterTagValue(''); setFilterDateFrom(''); setFilterDateTo('');
                                setSortField('created_at'); setSortDir('desc');
                                setFilterEql(''); setEqlError(null);
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

            {!loading && tasks.length > 0 && viewMode === 'table' && (
                <table className="tasks-page__table">
                    <thead>
                        <tr>
                            <th style={{ width: 32 }}></th>
                            <th className="tasks-page__th--sortable" onClick={() => handleHeaderSort('subject')}>
                                Тема {sortField === 'subject' ? (sortDir === 'asc' ? '↑' : '↓') : ''}
                            </th>
                            <th className="tasks-page__th--sortable" onClick={() => handleHeaderSort('status')}>
                                Статус {sortField === 'status' ? (sortDir === 'asc' ? '↑' : '↓') : ''}
                            </th>
                            <th className="tasks-page__th--sortable" onClick={() => handleHeaderSort('priority')}>
                                Приоритет {sortField === 'priority' ? (sortDir === 'asc' ? '↑' : '↓') : ''}
                            </th>
                            <th>Исполнитель</th>
                            <th>Автор</th>
                            <th className="tasks-page__th--sortable" onClick={() => handleHeaderSort('due_date')}>
                                Срок {sortField === 'due_date' ? (sortDir === 'asc' ? '↑' : '↓') : ''}
                            </th>
                            <th>Теги</th>
                        </tr>
                    </thead>
                    <tbody>
                        {groupedTasks.map(({ key, tasks: groupTasks }) => (
                            <>
                                {groupBy && key && (
                                    <tr key={`group-${key}`} className="tasks-page__group-header">
                                        <td colSpan={8}>
                                            <strong>{key}</strong>
                                            <span style={{ marginLeft: 8, color: '#888', fontSize: 12 }}>({groupTasks.length})</span>
                                        </td>
                                    </tr>
                                )}
                                {groupTasks.map(task => (
                                    <tr
                                        key={task.id}
                                        className={`tasks-page__row ${getRowClass(task)}`}
                                        onClick={() => onOpenTask(task.id)}
                                    >
                                        <td onClick={e => toggleSelect(task.id, e)} style={{ textAlign: 'center', cursor: 'pointer' }}>
                                            <input
                                                type="checkbox"
                                                checked={selectedIds.has(task.id)}
                                                onChange={() => {/* handled by td onClick */}}
                                                onClick={e => { e.stopPropagation(); }}
                                            />
                                        </td>
                                        <td className="tasks-page__subject">
                                            <TaskKindIcon kind={task.kind} scheduledAt={task.scheduledAt} openQuestionCount={task.openQuestionCount} />
                                            {' '}
                                            <span className="tasks-page__num" style={{ color: '#888', marginRight: 4 }}>T-{task.number}</span>
                                            {task.subject}
                                            {task.isCoExecutor && <span className="tasks-page__badge tasks-page__badge--coexec" title="Соисполнитель">★</span>}
                                        </td>
                                        <td><span className={getStatusClass(task.status, task.isOverdue)}>{TASK_STATUS_LABELS[task.status]}</span></td>
                                        <td><span className={getPriorityClass(task.priority)}>{TASK_PRIORITY_LABELS[task.priority]}</span></td>
                                        <td>{task.assigneeName}</td>
                                        <td>{task.authorName}</td>
                                        <td style={{ whiteSpace: 'nowrap' }}>{new Date(task.dueDate).toLocaleDateString('ru-RU')}</td>
                                        <td className="tasks-page__tags">{task.tags.slice(0, 3).map(tag => <span key={tag} className="tasks-page__tag">{tag}</span>)}</td>
                                    </tr>
                                ))}
                            </>
                        ))}
                    </tbody>
                </table>
            )}

            {/* FR-TASK-02.2: Режим карточек */}
            {!loading && tasks.length > 0 && viewMode === 'card' && (
                <div className="tasks-page__cards">
                    {groupedTasks.map(({ key, tasks: groupTasks }) => (
                        <div key={key || 'ungrouped'}>
                            {groupBy && key && (
                                <div className="tasks-page__card-group-title">
                                    {key} <span style={{ color: '#888', fontSize: 12 }}>({groupTasks.length})</span>
                                </div>
                            )}
                            {groupTasks.map(task => (
                                <div
                                    key={task.id}
                                    className={`tasks-page__card ${getRowClass(task)}`}
                                    onClick={() => onOpenTask(task.id)}
                                >
                                    <div className="tasks-page__card-header">
                                        <TaskKindIcon kind={task.kind} scheduledAt={task.scheduledAt} openQuestionCount={task.openQuestionCount} />
                                        <span className="tasks-page__num" style={{ color: '#888', marginRight: 4 }}>T-{task.number}</span>
                                        <span className="tasks-page__card-subject">{task.subject}</span>
                                        {task.isCoExecutor && <span className="tasks-page__badge tasks-page__badge--coexec" title="Соисполнитель">★</span>}
                                    </div>
                                    <div className="tasks-page__card-meta">
                                        <span className={getStatusClass(task.status, task.isOverdue)}>{TASK_STATUS_LABELS[task.status]}</span>
                                        <span className={getPriorityClass(task.priority)}>{TASK_PRIORITY_LABELS[task.priority]}</span>
                                        <span>{task.assigneeName}</span>
                                        <span style={{ color: '#888' }}>{new Date(task.dueDate).toLocaleDateString('ru-RU')}</span>
                                    </div>
                                    {task.tags.length > 0 && (
                                        <div className="tasks-page__tags">
                                            {task.tags.slice(0, 5).map(tag => <span key={tag} className="tasks-page__tag">{tag}</span>)}
                                        </div>
                                    )}
                                </div>
                            ))}
                        </div>
                    ))}
                </div>
            )}

            {/* ── Диалог сохранения фильтра */}
            {showSaveFilter && (
                <div className="tasks-page__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('saveFilter'); }}>
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
                                className={`tasks-page__btn tasks-page__btn--primary${sc('saveFilter')}`}
                                onClick={handleSaveFilter}
                                disabled={savingFilter || !newFilterName.trim()}
                            >
                                {savingFilter ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className={`tasks-page__btn${sc('saveFilter')}`} onClick={() => setShowSaveFilter(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Диалог создания задачи */}
            {showCreate && (
                <div className="tasks-page__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('create'); }}>
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
                            <button className={`tasks-page__btn tasks-page__btn--primary${sc('create')}`} onClick={handleCreate} disabled={saving}>
                                {saving ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className={`tasks-page__btn${sc('create')}`} onClick={() => { setShowCreate(false); resetForm(); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
