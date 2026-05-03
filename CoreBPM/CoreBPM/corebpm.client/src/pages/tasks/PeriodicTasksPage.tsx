import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    listTasks, createPeriodicTask, getSeriesItems, stopSeries,
    TASK_STATUS_LABELS,
} from '../../api/tasksApi';
import type { TaskSummaryDto, TaskDto, PeriodicSeriesItemDto, CreatePeriodicTaskRequest } from '../../api/tasksApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';

interface PeriodicTasksPageProps {
    onOpenTask: (id: string) => void;
}

type TabId = 'active' | 'completed';

/** Страница периодических задач (FR-TASK-01.5.1). */
export function PeriodicTasksPage({ onOpenTask }: PeriodicTasksPageProps) {
    const { accessToken: token, userId } = useAuth();

    const [tab, setTab] = useState<TabId>('active');
    const [periodicTasks, setPeriodicTasks] = useState<TaskSummaryDto[]>([]);
    const [expandedId, setExpandedId] = useState<string | null>(null);
    const [seriesItems, setSeriesItems] = useState<Record<string, PeriodicSeriesItemDto[]>>({});
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const [showCreate, setShowCreate] = useState(false);

    const load = useCallback(async () => {
        if (!token) return;
        setLoading(true);
        setError(null);
        try {
            // Загружаем все задачи и фильтруем по kind=Periodic
            const all = await listTasks(token, {
                status: tab === 'active' ? '' : 'Done',
            });
            setPeriodicTasks(all.filter((t: TaskSummaryDto) => (t as any).kind === 'Periodic' || (t as any).seriesId));
        } catch (e: any) {
            setError(e.message);
        } finally {
            setLoading(false);
        }
    }, [token, tab]);

    useEffect(() => { load(); }, [load]);

    const handleExpand = async (taskId: string) => {
        if (!token) return;
        if (expandedId === taskId) { setExpandedId(null); return; }
        setExpandedId(taskId);
        if (!seriesItems[taskId]) {
            try {
                const items = await getSeriesItems(token, taskId, false);
                setSeriesItems(prev => ({ ...prev, [taskId]: items }));
            } catch { /* ignore */ }
        }
    };

    const handleStopSeries = async (rootTaskId: string) => {
        if (!token) return;
        if (!window.confirm('Остановить серию периодических задач?')) return;
        try {
            await stopSeries(token, rootTaskId);
            load();
        } catch (e: any) {
            alert(e.message);
        }
    };

    const statusLabel = (status: string) => TASK_STATUS_LABELS[status as keyof typeof TASK_STATUS_LABELS] ?? status;

    return (
        <div style={{ padding: '24px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
                <h2 style={{ margin: 0 }}>Периодические задачи</h2>
                <button
                    onClick={() => setShowCreate(true)}
                    style={{ padding: '8px 18px', background: '#1890ff', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer' }}
                >
                    + Создать
                </button>
            </div>

            {/* Вкладки */}
            <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', borderBottom: '2px solid #e8e8e8' }}>
                {(['active', 'completed'] as TabId[]).map(t => (
                    <button key={t} onClick={() => setTab(t)}
                        style={{
                            padding: '8px 20px', border: 'none', background: 'none', cursor: 'pointer',
                            fontWeight: tab === t ? 600 : 400,
                            borderBottom: tab === t ? '2px solid #1890ff' : '2px solid transparent',
                            color: tab === t ? '#1890ff' : '#555',
                        }}>
                        {t === 'active' ? 'Текущие' : 'Завершённые'}
                    </button>
                ))}
            </div>

            {loading && <div>Загрузка...</div>}
            {error && <div style={{ color: 'red' }}>{error}</div>}
            {!loading && !error && periodicTasks.length === 0 && (
                <div style={{ color: '#888', textAlign: 'center', marginTop: '40px' }}>Нет периодических задач</div>
            )}

            {periodicTasks.map(task => (
                <div key={task.id} style={{ background: '#fff', border: '1px solid #e8e8e8', borderRadius: '8px', marginBottom: '12px', overflow: 'hidden' }}>
                    {/* Заголовок серии */}
                    <div style={{ display: 'flex', alignItems: 'center', padding: '14px 16px', gap: '12px', cursor: 'pointer' }}
                        onClick={() => handleExpand(task.id)}>
                        <span style={{ fontSize: '18px' }}>{expandedId === task.id ? '▾' : '▸'}</span>
                        <span style={{ fontWeight: 600, flex: 1 }}
                            onClick={e => { e.stopPropagation(); onOpenTask(task.id); }}>
                            {task.subject}
                        </span>
                        <span style={{
                            padding: '2px 10px', borderRadius: '12px', fontSize: '12px',
                            background: '#e6f7ff', color: '#1890ff',
                        }}>
                            🔄 Периодическая
                        </span>
                        <span style={{ color: '#888', fontSize: '13px' }}>{statusLabel(task.status)}</span>
                        <button
                            onClick={e => { e.stopPropagation(); handleStopSeries(task.id); }}
                            style={{ padding: '4px 12px', background: '#fff1f0', color: '#f5222d', border: '1px solid #ffa39e', borderRadius: '4px', cursor: 'pointer', fontSize: '12px' }}>
                            Остановить
                        </button>
                    </div>

                    {/* Экземпляры серии */}
                    {expandedId === task.id && (
                        <div style={{ borderTop: '1px solid #f0f0f0', padding: '0 16px 12px' }}>
                            {!seriesItems[task.id] && <div style={{ color: '#888', padding: '8px 0' }}>Загрузка...</div>}
                            {seriesItems[task.id]?.map(item => (
                                <div key={item.id} onClick={() => onOpenTask(item.id)}
                                    style={{
                                        display: 'flex', alignItems: 'center', padding: '8px 0',
                                        borderBottom: '1px solid #f5f5f5', cursor: 'pointer',
                                        ':hover': { background: '#fafafa' } as any,
                                    }}>
                                    <span style={{ color: '#888', fontSize: '12px', minWidth: '50px' }}>T-{item.number}</span>
                                    <span style={{ flex: 1, fontSize: '14px' }}>{item.subject}</span>
                                    <span style={{ fontSize: '12px', color: '#888', minWidth: '120px' }}>
                                        {new Date(item.startDate).toLocaleDateString('ru-RU')} — {new Date(item.dueDate).toLocaleDateString('ru-RU')}
                                    </span>
                                    <span style={{
                                        padding: '2px 8px', borderRadius: '10px', fontSize: '11px',
                                        background: item.isOverdue ? '#fff1f0' : '#f6ffed',
                                        color: item.isOverdue ? '#f5222d' : '#52c41a',
                                        minWidth: '90px', textAlign: 'center',
                                    }}>
                                        {statusLabel(item.status)}
                                    </span>
                                </div>
                            ))}
                            {seriesItems[task.id]?.length === 0 && (
                                <div style={{ color: '#888', padding: '8px 0' }}>Нет экземпляров</div>
                            )}
                        </div>
                    )}
                </div>
            ))}

            {showCreate && (
                <CreatePeriodicTaskDialog
                    token={token!}
                    userId={userId!}
                    onClose={() => setShowCreate(false)}
                    onCreated={() => { setShowCreate(false); load(); }}
                />
            )}
        </div>
    );
}

// ─── Диалог создания периодической задачи ─────────────────────────────────────

interface CreatePeriodicTaskDialogProps {
    token: string;
    userId: string;
    onClose: () => void;
    onCreated: () => void;
}

const PERIODICITY_OPTIONS = [
    { value: 'Daily', label: 'Ежедневно' },
    { value: 'WorkingDays', label: 'По рабочим дням' },
    { value: 'Weekly', label: 'Еженедельно' },
    { value: 'Monthly', label: 'Ежемесячно' },
    { value: 'Quarterly', label: 'Ежеквартально' },
    { value: 'Yearly', label: 'Ежегодно' },
];

function CreatePeriodicTaskDialog({ token, userId, onClose, onCreated }: CreatePeriodicTaskDialogProps) {
    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);
    const [subject, setSubject] = useState('');
    const [description, setDescription] = useState('');
    const [assigneeId, setAssigneeId] = useState(userId);
    const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
    const [dueDate, setDueDate] = useState('');
    const [periodicity, setPeriodicity] = useState('Daily');
    const [endCondition, setEndCondition] = useState('Never');
    const [endDate, setEndDate] = useState('');
    const [lookAheadCount, setLookAheadCount] = useState(1);
    const [durationMinutes, setDurationMinutes] = useState(480);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        getDirectoryEmployees(token).then(setEmployees).catch(() => {});
    }, [token]);

    const handleSubmit = async () => {
        if (!subject.trim()) { setError('Введите тему задачи'); return; }
        if (!dueDate) { setError('Укажите срок'); return; }
        setSaving(true);
        setError(null);
        try {
            const req: CreatePeriodicTaskRequest = {
                subject: subject.trim(),
                description: description.trim() || undefined,
                assigneeUserId: assigneeId,
                startDate: new Date(startDate).toISOString(),
                dueDate: new Date(dueDate).toISOString(),
                periodicity,
                endCondition,
                endDate: endDate ? new Date(endDate).toISOString() : undefined,
                lookAheadCount,
                durationMinutes,
            };
            await createPeriodicTask(token, req);
            onCreated();
        } catch (e: any) {
            setError(e.message);
        } finally {
            setSaving(false);
        }
    };

    return (
        <div style={{
            position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.45)', zIndex: 1000,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
            <div style={{ background: '#fff', borderRadius: '12px', padding: '28px', width: '520px', maxHeight: '85vh', overflowY: 'auto' }}>
                <h3 style={{ marginTop: 0 }}>Создать периодическую задачу</h3>

                <label style={{ display: 'block', marginBottom: '12px' }}>
                    <span style={{ fontSize: '13px', color: '#888' }}>Тема *</span>
                    <input value={subject} onChange={e => setSubject(e.target.value)}
                        style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                </label>

                <label style={{ display: 'block', marginBottom: '12px' }}>
                    <span style={{ fontSize: '13px', color: '#888' }}>Описание</span>
                    <textarea value={description} onChange={e => setDescription(e.target.value)} rows={3}
                        style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', resize: 'vertical', boxSizing: 'border-box' }} />
                </label>

                <label style={{ display: 'block', marginBottom: '12px' }}>
                    <span style={{ fontSize: '13px', color: '#888' }}>Исполнитель</span>
                    <select value={assigneeId} onChange={e => setAssigneeId(e.target.value)}
                        style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px' }}>
                        {employees.map(e => <option key={e.id} value={e.id}>{e.displayName}</option>)}
                    </select>
                </label>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                    <label>
                        <span style={{ fontSize: '13px', color: '#888' }}>Дата начала *</span>
                        <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)}
                            style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                    </label>
                    <label>
                        <span style={{ fontSize: '13px', color: '#888' }}>Срок первого экземпляра *</span>
                        <input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)}
                            style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                    </label>
                </div>

                <label style={{ display: 'block', marginBottom: '12px' }}>
                    <span style={{ fontSize: '13px', color: '#888' }}>Периодичность</span>
                    <select value={periodicity} onChange={e => setPeriodicity(e.target.value)}
                        style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px' }}>
                        {PERIODICITY_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                </label>

                <label style={{ display: 'block', marginBottom: '12px' }}>
                    <span style={{ fontSize: '13px', color: '#888' }}>Условие завершения</span>
                    <select value={endCondition} onChange={e => setEndCondition(e.target.value)}
                        style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px' }}>
                        <option value="Never">Не завершать</option>
                        <option value="ByDate">Завершить в дату</option>
                    </select>
                </label>

                {endCondition === 'ByDate' && (
                    <label style={{ display: 'block', marginBottom: '12px' }}>
                        <span style={{ fontSize: '13px', color: '#888' }}>Дата окончания серии</span>
                        <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)}
                            style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                    </label>
                )}

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                    <label>
                        <span style={{ fontSize: '13px', color: '#888' }}>Создавать экземпляров вперёд</span>
                        <input type="number" min={0} max={10} value={lookAheadCount}
                            onChange={e => setLookAheadCount(Number(e.target.value))}
                            style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                    </label>
                    <label>
                        <span style={{ fontSize: '13px', color: '#888' }}>Длительность (мин)</span>
                        <input type="number" min={15} value={durationMinutes}
                            onChange={e => setDurationMinutes(Number(e.target.value))}
                            style={{ display: 'block', width: '100%', marginTop: '4px', padding: '8px', border: '1px solid #d9d9d9', borderRadius: '6px', boxSizing: 'border-box' }} />
                    </label>
                </div>

                {error && <div style={{ color: 'red', marginBottom: '12px', fontSize: '13px' }}>{error}</div>}

                <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button onClick={onClose}
                        style={{ padding: '8px 20px', border: '1px solid #d9d9d9', borderRadius: '6px', cursor: 'pointer', background: '#fff' }}>
                        Отмена
                    </button>
                    <button onClick={handleSubmit} disabled={saving}
                        style={{ padding: '8px 20px', background: '#1890ff', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer' }}>
                        {saving ? 'Создание...' : 'Создать'}
                    </button>
                </div>
            </div>
        </div>
    );
}
