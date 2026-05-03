import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    listTasks, createTask, exportTasksCsv,
    TASK_STATUS_LABELS, TASK_PRIORITY_LABELS,
} from '../../api/tasksApi';
import type { TaskSummaryDto, TaskStatus, TaskPriority } from '../../api/tasksApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import './TasksPage.css';

interface TasksPageProps {
    onOpenTask: (id: string) => void;
}

type TabId = 'all' | 'my';

/** Страница списка задач (FR-TASK-01.1). */
export function TasksPage({ onOpenTask }: TasksPageProps) {
    const { accessToken: token, userId } = useAuth();

    const [tab, setTab] = useState<TabId>('all');
    const [tasks, setTasks] = useState<TaskSummaryDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const [filterStatus, setFilterStatus] = useState('');
    const [filterPriority, setFilterPriority] = useState('');
    const [filterSearch, setFilterSearch] = useState('');
    const [filterOverdue, setFilterOverdue] = useState(false);

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
            });
            setTasks(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, tab, filterStatus, filterPriority, filterSearch, filterOverdue, userId]);

    useEffect(() => { load(); }, [load]);

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
            });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'tasks.csv';
            a.click();
            URL.revokeObjectURL(url);
        } catch { /* игнорируем */ }
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

    return (
        <div className="tasks-page">
            <div className="tasks-page__header">
                <h2 className="tasks-page__title">Задачи</h2>
                <div className="tasks-page__actions">
                    <button className="tasks-page__btn tasks-page__btn--primary" onClick={handleOpenCreate}>+ Создать задачу</button>
                    <button className="tasks-page__btn" onClick={handleExport}>Экспорт CSV</button>
                </div>
            </div>

            <div className="tasks-page__tabs">
                {(['all', 'my'] as TabId[]).map(t => (
                    <button key={t} className={`tasks-page__tab${tab === t ? ' tasks-page__tab--active' : ''}`} onClick={() => setTab(t)}>
                        {t === 'all' ? 'Все задачи' : 'Мои задачи'}
                    </button>
                ))}
            </div>

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
                <button className="tasks-page__btn" onClick={load}>Применить</button>
            </div>

            {loading && <div className="tasks-page__loading">Загрузка...</div>}
            {error && <div className="tasks-page__error">{error}</div>}
            {!loading && !error && tasks.length === 0 && <div className="tasks-page__empty">Задачи не найдены</div>}

            {!loading && tasks.length > 0 && (
                <table className="tasks-page__table">
                    <thead>
                        <tr>
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
