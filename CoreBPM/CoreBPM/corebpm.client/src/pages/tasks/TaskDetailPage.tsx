import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getTask, markTaskRead, addTaskComment, getTaskComments,
    getTaskAttachments, getTaskParticipants, addTaskParticipant, removeTaskParticipant,
    getTaskRelations, addTaskRelation, removeTaskRelation,
    getTaskHistory, copyTask, reassignTask, createSubtask, listTasks,
    getAllowedActions, approvePreTask, rejectPreTask, sendTaskForApproval, approveTask, rejectTask,
    getTaskTimeLogs, addTaskTimeLog,
    TASK_STATUS_LABELS, TASK_PRIORITY_LABELS,
} from '../../api/tasksApi';
import type {
    TaskDto, TaskCommentDto, TaskAttachmentDto,
    TaskRelationDto, TaskParticipantDto, TaskHistoryEntryDto,
    TaskSummaryDto, TaskTimeLogDto,
} from '../../api/tasksApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import './TaskDetailPage.css';

interface TaskDetailPageProps {
    taskId: string;
    onBack: () => void;
}

type TabId = 'description' | 'subtasks' | 'relations' | 'participants' | 'timelogs';

const RELATION_LABELS: Record<string, string> = {
    DependsOn: 'Зависит от', Blocks: 'Блокирует', RelatedTo: 'Связана с',
};

const PARTICIPANT_ROLE_LABELS: Record<string, string> = {
    CoExecutor: 'Соисполнитель', Observer: 'Наблюдатель',
    Approver: 'Согласующий', Controller: 'Контролёр',
};

/** Карточка задачи с 4 вкладками (FR-TASK-01.1). */
export function TaskDetailPage({ taskId, onBack }: TaskDetailPageProps) {
    const { accessToken: token } = useAuth();
    const [task, setTask] = useState<TaskDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [tab, setTab] = useState<TabId>('description');

    const [comments, setComments] = useState<TaskCommentDto[]>([]);
    const [attachments, setAttachments] = useState<TaskAttachmentDto[]>([]);
    const [history, setHistory] = useState<TaskHistoryEntryDto[]>([]);
    const [participants, setParticipants] = useState<TaskParticipantDto[]>([]);
    const [relations, setRelations] = useState<TaskRelationDto[]>([]);
    const [subtasks, setSubtasks] = useState<TaskSummaryDto[]>([]);
    const [timeLogs, setTimeLogs] = useState<TaskTimeLogDto[]>([]);

    // ─── FR-TASK-01.4: Добавление трудозатрат ────────────────────────────────
    const [showAddTimeLog, setShowAddTimeLog] = useState(false);
    const [timeLogDuration, setTimeLogDuration] = useState('');
    const [timeLogStartDate, setTimeLogStartDate] = useState('');
    const [timeLogComment, setTimeLogComment] = useState('');
    const [timeLogSaving, setTimeLogSaving] = useState(false);

    const [commentText, setCommentText] = useState('');
    const [commentSaving, setCommentSaving] = useState(false);

    const [showReassign, setShowReassign] = useState(false);
    const [reassignUserId, setReassignUserId] = useState('');
    const [reassignComment, setReassignComment] = useState('');
    const [reassignSearch, setReassignSearch] = useState('');
    const [reassignSaving, setReassignSaving] = useState(false);

    const [showAddParticipant, setShowAddParticipant] = useState(false);
    const [participantUserId, setParticipantUserId] = useState('');
    const [participantRole, setParticipantRole] = useState('Observer');
    const [participantSearch, setParticipantSearch] = useState('');
    const [participantSaving, setParticipantSaving] = useState(false);

    const [showAddRelation, setShowAddRelation] = useState(false);
    const [relationTargetNumber, setRelationTargetNumber] = useState('');
    const [relationType, setRelationType] = useState('RelatedTo');
    const [relationSaving, setRelationSaving] = useState(false);
    const [relationError, setRelationError] = useState<string | null>(null);

    const [showCreateSubtask, setShowCreateSubtask] = useState(false);
    const [subtaskSubject, setSubtaskSubject] = useState('');
    const [subtaskAssigneeId, setSubtaskAssigneeId] = useState('');
    const [subtaskSearch, setSubtaskSearch] = useState('');
    const [subtaskSaving, setSubtaskSaving] = useState(false);

    const [employees, setEmployees] = useState<DirectoryEmployeeDto[]>([]);

    // FR-TASK-01.3: согласование
    const [allowedActions, setAllowedActions] = useState<{ action: string; label: string }[]>([]);
    const [approvalComment, setApprovalComment] = useState('');
    const [showSendForApproval, setShowSendForApproval] = useState(false);
    const [sendApprovalApproverQuery, setSendApprovalApproverQuery] = useState('');
    const [sendApprovalApproverId, setSendApprovalApproverId] = useState('');
    const [sendApprovalComment, setSendApprovalComment] = useState('');
    const [approvalSaving, setApprovalSaving] = useState(false);
    const [approvalError, setApprovalError] = useState<string | null>(null);

    const loadEmployees = useCallback(async () => {
        if (employees.length === 0 && token) {
            try { setEmployees(await getDirectoryEmployees(token)); } catch { /* игнорируем */ }
        }
    }, [employees.length, token]);

    const loadTask = useCallback(async () => {
        if (!token) return;
        try {
            const [data, actions] = await Promise.all([
                getTask(token, taskId),
                getAllowedActions(token, taskId).catch(() => []),
            ]);
            setTask(data);
            setAllowedActions(actions);
            if (data.status === 'New') {
                await markTaskRead(token, taskId);
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Ошибка загрузки');
        } finally {
            setLoading(false);
        }
    }, [token, taskId]);

    const loadTabData = useCallback(async () => {
        if (!token) return;
        try {
            if (tab === 'description') {
                const [cmts, atts, hist] = await Promise.all([
                    getTaskComments(token, taskId),
                    getTaskAttachments(token, taskId),
                    getTaskHistory(token, taskId),
                ]);
                setComments(cmts);
                setAttachments(atts);
                setHistory(hist.slice(0, 20));
            } else if (tab === 'participants') {
                setParticipants(await getTaskParticipants(token, taskId));
            } else if (tab === 'relations') {
                setRelations(await getTaskRelations(token, taskId));
            } else if (tab === 'subtasks') {
                const all = await listTasks(token, {});
                setSubtasks(all.filter((t: TaskSummaryDto) => t.id !== taskId));
            } else if (tab === 'timelogs') {
                setTimeLogs(await getTaskTimeLogs(token, taskId));
            }
        } catch { /* игнорируем */ }
    }, [token, taskId, tab]);

    useEffect(() => { loadTask(); }, [loadTask]);
    useEffect(() => { loadTabData(); }, [loadTabData]);

    const handleAddComment = async () => {
        if (!token || !commentText.trim()) return;
        setCommentSaving(true);
        try {
            const c = await addTaskComment(token, taskId, commentText.trim());
            setComments(prev => [...prev, c]);
            setCommentText('');
        } finally {
            setCommentSaving(false);
        }
    };

    // FR-TASK-01.3: обработчики согласования
    const hasAction = (action: string) => allowedActions.some(a => a.action === action);

    const handleApprovalAction = async (action: () => Promise<TaskDto>) => {
        if (!token) return;
        setApprovalSaving(true);
        setApprovalError(null);
        try {
            const updated = await action();
            setTask(updated);
            const actions = await getAllowedActions(token, taskId).catch(() => []);
            setAllowedActions(actions);
            setApprovalComment('');
        } catch (e) {
            setApprovalError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setApprovalSaving(false);
        }
    };

    const handleSendForApproval = async () => {
        if (!token) return;
        setApprovalSaving(true);
        setApprovalError(null);
        try {
            const updated = await sendTaskForApproval(token, taskId, sendApprovalApproverId || undefined, sendApprovalComment || undefined);
            setTask(updated);
            const actions = await getAllowedActions(token, taskId).catch(() => []);
            setAllowedActions(actions);
            setShowSendForApproval(false);
            setSendApprovalApproverId('');
            setSendApprovalApproverQuery('');
            setSendApprovalComment('');
        } catch (e) {
            setApprovalError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setApprovalSaving(false);
        }
    };

    const handleCopy = async () => {
        if (!token) return;
        try {
            await copyTask(token, taskId);
            alert('Задача скопирована');
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    // FR-TASK-01.4: добавить трудозатраты
    const handleAddTimeLog = async () => {
        if (!token || !timeLogDuration) return;
        const mins = parseInt(timeLogDuration, 10);
        if (isNaN(mins) || mins <= 0) { alert('Введите корректную длительность в минутах.'); return; }
        setTimeLogSaving(true);
        try {
            const log = await addTaskTimeLog(token, taskId, {
                durationMinutes: mins,
                startDate: timeLogStartDate || new Date().toISOString(),
                comment: timeLogComment || undefined,
            });
            setTimeLogs(prev => [...prev, log]);
            setShowAddTimeLog(false);
            setTimeLogDuration('');
            setTimeLogStartDate('');
            setTimeLogComment('');
            // Обновить задачу, чтобы обновить actualEffortMinutes
            const updated = await getTask(token, taskId);
            setTask(updated);
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setTimeLogSaving(false);
        }
    };

    const handleReassign = async () => {
        if (!token || !reassignUserId) return;
        setReassignSaving(true);
        try {
            const updated = await reassignTask(token, taskId, reassignUserId, reassignComment || undefined);
            setTask(updated);
            setShowReassign(false);
            setReassignUserId('');
            setReassignComment('');
            setReassignSearch('');
        } finally {
            setReassignSaving(false);
        }
    };

    const handleAddParticipant = async () => {
        if (!token || !participantUserId) return;
        setParticipantSaving(true);
        try {
            const p = await addTaskParticipant(token, taskId, participantUserId, participantRole);
            setParticipants(prev => [...prev, p]);
            setShowAddParticipant(false);
            setParticipantUserId('');
            setParticipantSearch('');
        } finally {
            setParticipantSaving(false);
        }
    };

    const handleRemoveParticipant = async (pId: string) => {
        if (!token) return;
        await removeTaskParticipant(token, taskId, pId);
        setParticipants(prev => prev.filter(p => p.id !== pId));
    };

    const handleAddRelation = async () => {
        if (!token || !relationTargetNumber) return;
        setRelationSaving(true);
        setRelationError(null);
        try {
            const all = await listTasks(token, {});
            const target = all.find((t: TaskSummaryDto) => t.number === parseInt(relationTargetNumber));
            if (!target) { setRelationError(`Задача T-${relationTargetNumber} не найдена`); return; }
            const rel = await addTaskRelation(token, taskId, target.id, relationType);
            setRelations(prev => [...prev, rel]);
            setShowAddRelation(false);
            setRelationTargetNumber('');
        } catch (e) {
            setRelationError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setRelationSaving(false);
        }
    };

    const handleRemoveRelation = async (rId: string) => {
        if (!token) return;
        await removeTaskRelation(token, taskId, rId);
        setRelations(prev => prev.filter(r => r.id !== rId));
    };

    const handleCreateSubtask = async () => {
        if (!token || !subtaskSubject.trim() || !subtaskAssigneeId) return;
        setSubtaskSaving(true);
        try {
            const now = new Date();
            const due = new Date(now);
            due.setDate(due.getDate() + 7);
            await createSubtask(token, taskId, {
                subject: subtaskSubject.trim(),
                assigneeUserId: subtaskAssigneeId,
                startDate: now.toISOString(),
                dueDate: due.toISOString(),
            });
            setShowCreateSubtask(false);
            setSubtaskSubject('');
            setSubtaskAssigneeId('');
            setSubtaskSearch('');
            loadTabData();
        } finally {
            setSubtaskSaving(false);
        }
    };

    const filteredForReassign = employees.filter(e => e.displayName.toLowerCase().includes(reassignSearch.toLowerCase()));
    const filteredForParticipant = employees.filter(e => e.displayName.toLowerCase().includes(participantSearch.toLowerCase()));
    const filteredForSubtask = employees.filter(e => e.displayName.toLowerCase().includes(subtaskSearch.toLowerCase()));

    if (loading) return <div className="task-detail__loading">Загрузка...</div>;
    if (error) return <div className="task-detail__error">{error}</div>;
    if (!task) return null;

    return (
        <div className="task-detail">
            <div className="task-detail__header">
                <button className="task-detail__back" onClick={onBack}>← Назад</button>
                <div className="task-detail__title-block">
                    <span className="task-detail__number">T-{task.number}</span>
                    <h2 className="task-detail__title">{task.subject}</h2>
                </div>
                <div className="task-detail__header-actions">
                    <button className="task-detail__btn" onClick={() => { setShowReassign(true); loadEmployees(); }}>Переназначить</button>
                    <button className="task-detail__btn" onClick={handleCopy}>Копировать</button>
                    {/* FR-TASK-01.3: кнопки согласования */}
                    {hasAction('approve-pre') && (
                        <button className="task-detail__btn task-detail__btn--success" disabled={approvalSaving}
                            onClick={() => handleApprovalAction(() => approvePreTask(token!, taskId, approvalComment || undefined))}>
                            ✓ Согласовать (предв.)
                        </button>
                    )}
                    {hasAction('reject-pre') && (
                        <button className="task-detail__btn task-detail__btn--danger" disabled={approvalSaving}
                            onClick={() => handleApprovalAction(() => rejectPreTask(token!, taskId, approvalComment || undefined))}>
                            ✗ Отказать (предв.)
                        </button>
                    )}
                    {hasAction('approve') && (
                        <button className="task-detail__btn task-detail__btn--success" disabled={approvalSaving}
                            onClick={() => handleApprovalAction(() => approveTask(token!, taskId, approvalComment || undefined))}>
                            ✓ Согласовать
                        </button>
                    )}
                    {hasAction('reject') && (
                        <button className="task-detail__btn task-detail__btn--danger" disabled={approvalSaving}
                            onClick={() => handleApprovalAction(() => rejectTask(token!, taskId, approvalComment || undefined))}>
                            ✗ Отказать
                        </button>
                    )}
                    {hasAction('send-for-approval') && (
                        <button className="task-detail__btn" disabled={approvalSaving}
                            onClick={() => { setShowSendForApproval(true); loadEmployees(); }}>
                            📤 На согласование
                        </button>
                    )}
                </div>
            </div>

            <div className="task-detail__meta">
                <span className="task-detail__meta-item"><strong>Статус:</strong> {TASK_STATUS_LABELS[task.status]}</span>
                <span className="task-detail__meta-item"><strong>Приоритет:</strong> {TASK_PRIORITY_LABELS[task.priority]}</span>
                <span className="task-detail__meta-item"><strong>Исполнитель:</strong> {task.assigneeName}</span>
                <span className="task-detail__meta-item"><strong>Автор:</strong> {task.authorName}</span>
                <span className={`task-detail__meta-item${task.isOverdue ? ' task-detail__meta-item--overdue' : ''}`}>
                    <strong>Срок:</strong> {new Date(task.dueDate).toLocaleString('ru-RU')}
                    {task.isOverdue && <span className="task-detail__overdue-badge"> ⚠ Просрочена</span>}
                </span>
                {/* FR-TASK-01.3: согласующий */}
                {task.approverName && (
                    <span className="task-detail__meta-item">
                        <strong>Согласующий:</strong>{' '}
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                            {task.approverName}
                            {(task.status === 'PreApproval' || task.status === 'OnApproval') && (
                                <span style={{ fontSize: 11, background: '#fef9c3', border: '1px solid #fde047', color: '#92400e', borderRadius: 4, padding: '1px 6px' }}>
                                    ⏳ Ожидает решения
                                </span>
                            )}
                            {(task.status === 'PreApprovalRejected' || task.status === 'ApprovalRejected') && (
                                <span style={{ fontSize: 11, background: '#fee2e2', border: '1px solid #fca5a5', color: '#991b1b', borderRadius: 4, padding: '1px 6px' }}>
                                    ✗ Отказано
                                </span>
                            )}
                            {task.status === 'New' && task.approverName && allowedActions.length === 0 && (
                                <span style={{ fontSize: 11, background: '#dcfce7', border: '1px solid #86efac', color: '#166534', borderRadius: 4, padding: '1px 6px' }}>
                                    ✓ Согласовано
                                </span>
                            )}
                        </span>
                    </span>
                )}
                {task.categoryId && <span className="task-detail__meta-item"><strong>Категория:</strong> {task.categoryId}</span>}
                {task.plannedEffortMinutes && (
                    <span className="task-detail__meta-item">
                        <strong>Трудозатраты:</strong>{' '}
                        {task.actualEffortMinutes > 0
                            ? <>{task.actualEffortMinutes} / {task.plannedEffortMinutes} мин.</>
                            : <>{task.plannedEffortMinutes} мин. (план)</>
                        }
                    </span>
                )}
                {task.tags.length > 0 && (
                    <span className="task-detail__meta-item">
                        <strong>Теги:</strong> {task.tags.map(tag => <span key={tag} className="task-detail__tag">{tag}</span>)}
                    </span>
                )}
                {task.sourceInstanceId && (
                    <span className="task-detail__meta-item">
                        <strong>Источник:</strong>{' '}
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: 4, padding: '2px 8px', fontSize: 12, color: '#1d4ed8' }}>
                            🔗 Из процесса
                            {task.sourceElementId && (
                                <span style={{ color: '#6b7280', fontSize: 11 }}>
                                    (узел: {task.sourceElementId})
                                </span>
                            )}
                        </span>
                    </span>
                )}
            </div>

            <div className="task-detail__tabs">
                {([
                    { id: 'description', label: 'Описание' },
                    { id: 'subtasks', label: `Подзадачи (${task.subtaskCount})` },
                    { id: 'relations', label: 'Связи' },
                    { id: 'participants', label: 'Участники' },
                    { id: 'timelogs', label: 'Трудозатраты' },
                ] as { id: TabId; label: string }[]).map(t => (
                    <button key={t.id} className={`task-detail__tab${tab === t.id ? ' task-detail__tab--active' : ''}`} onClick={() => setTab(t.id)}>{t.label}</button>
                ))}
            </div>

            <div className="task-detail__body">
                {/* ─── Описание ─── */}
                {tab === 'description' && (
                    <div className="task-detail__tab-content">
                        {task.description && (
                            <div className="task-detail__description">
                                <h4>Описание</h4>
                                <p>{task.description}</p>
                            </div>
                        )}
                        {attachments.length > 0 && (
                            <div className="task-detail__section">
                                <h4>Вложения ({attachments.length})</h4>
                                <ul className="task-detail__attachments">
                                    {attachments.map(a => (
                                        <li key={a.id} className="task-detail__attachment">
                                            📎 {a.fileName} <span className="task-detail__file-size">({Math.round(a.sizeBytes / 1024)} KB)</span>
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                        <div className="task-detail__section">
                            <h4>Комментарии ({comments.length})</h4>
                            <div className="task-detail__comments">
                                {comments.map(c => (
                                    <div key={c.id} className="task-detail__comment">
                                        <div className="task-detail__comment-header">
                                            <strong>{c.authorName}</strong>
                                            <span className="task-detail__comment-date">{new Date(c.createdAt).toLocaleString('ru-RU')}</span>
                                        </div>
                                        <p className="task-detail__comment-body">{c.body}</p>
                                    </div>
                                ))}
                            </div>
                            <div className="task-detail__comment-form">
                                <textarea className="task-detail__comment-input" placeholder="Добавить комментарий..." value={commentText} onChange={e => setCommentText(e.target.value)} rows={2} />
                                <button className="task-detail__btn task-detail__btn--primary" onClick={handleAddComment} disabled={commentSaving || !commentText.trim()}>
                                    {commentSaving ? 'Отправка...' : 'Отправить'}
                                </button>
                            </div>
                        </div>
                        {history.length > 0 && (
                            <div className="task-detail__section">
                                <h4>История изменений</h4>
                                <ul className="task-detail__history">
                                    {history.map(h => (
                                        <li key={h.id} className="task-detail__history-item">
                                            <span className="task-detail__history-actor">{h.actorName}</span>
                                            {' — '}{h.action}
                                            {h.fieldName && <span> поле «{h.fieldName}»: {h.oldValue} → {h.newValue}</span>}
                                            <span className="task-detail__history-date">{new Date(h.createdAt).toLocaleString('ru-RU')}</span>
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                    </div>
                )}

                {/* ─── Подзадачи ─── */}
                {tab === 'subtasks' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Подзадачи</h4>
                            <button className="task-detail__btn" onClick={() => { setShowCreateSubtask(true); loadEmployees(); }}>+ Создать подзадачу</button>
                        </div>
                        {subtasks.length === 0
                            ? <div className="task-detail__empty">Подзадач нет</div>
                            : (
                                <table className="task-detail__table">
                                    <thead><tr><th>№</th><th>Тема</th><th>Статус</th><th>Исполнитель</th></tr></thead>
                                    <tbody>
                                        {subtasks.map(s => (
                                            <tr key={s.id}>
                                                <td>T-{s.number}</td>
                                                <td>{s.subject}</td>
                                                <td>{TASK_STATUS_LABELS[s.status]}</td>
                                                <td>{s.assigneeName}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )
                        }
                    </div>
                )}

                {/* ─── Связи ─── */}
                {tab === 'relations' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Связи</h4>
                            <button className="task-detail__btn" onClick={() => setShowAddRelation(true)}>+ Добавить связь</button>
                        </div>
                        {relations.length === 0
                            ? <div className="task-detail__empty">Связей нет</div>
                            : (
                                <ul className="task-detail__relations">
                                    {relations.map(r => (
                                        <li key={r.id} className="task-detail__relation">
                                            <span className="task-detail__relation-type">{RELATION_LABELS[r.relationType] ?? r.relationType}</span>
                                            → <strong>T-{r.targetNumber}</strong> {r.targetSubject}
                                            <button className="task-detail__remove-btn" onClick={() => handleRemoveRelation(r.id)} title="Удалить связь">✕</button>
                                        </li>
                                    ))}
                                </ul>
                            )
                        }
                    </div>
                )}

                {/* ─── Участники ─── */}
                {tab === 'participants' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Участники</h4>
                            <button className="task-detail__btn" onClick={() => { setShowAddParticipant(true); loadEmployees(); }}>+ Добавить</button>
                        </div>
                        {participants.length === 0
                            ? <div className="task-detail__empty">Дополнительных участников нет</div>
                            : (
                                <ul className="task-detail__participants">
                                    {participants.map(p => (
                                        <li key={p.id} className="task-detail__participant">
                                            <span className="task-detail__participant-role">{PARTICIPANT_ROLE_LABELS[p.role] ?? p.role}</span>
                                            : <strong>{p.userName}</strong>
                                            <button className="task-detail__remove-btn" onClick={() => handleRemoveParticipant(p.id)} title="Удалить участника">✕</button>
                                        </li>
                                    ))}
                                </ul>
                            )
                        }
                    </div>
                )}

                {/* ─── Трудозатраты ─── */}
                {tab === 'timelogs' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Трудозатраты</h4>
                            <button className="task-detail__btn" onClick={() => setShowAddTimeLog(true)}>+ Добавить</button>
                        </div>
                        {task.plannedEffortMinutes && (
                            <div className="task-detail__effort-summary">
                                <span>Плановые: <strong>{task.plannedEffortMinutes} мин.</strong></span>
                                <span>Фактические: <strong>{task.actualEffortMinutes} мин.</strong></span>
                            </div>
                        )}
                        {timeLogs.length === 0
                            ? <div className="task-detail__empty">Трудозатраты не добавлены</div>
                            : (
                                <table className="task-detail__table">
                                    <thead>
                                        <tr>
                                            <th>Дата</th>
                                            <th>Сотрудник</th>
                                            <th>Длительность</th>
                                            <th>Вид деятельности</th>
                                            <th>Комментарий</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {timeLogs.map(l => (
                                            <tr key={l.id}>
                                                <td>{new Date(l.startDate).toLocaleDateString('ru-RU')}</td>
                                                <td>{l.userName}</td>
                                                <td>{l.durationMinutes} мин.</td>
                                                <td>{l.activityTypeName ?? '—'}</td>
                                                <td>{l.comment ?? '—'}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )
                        }
                    </div>
                )}
            </div>

            {/* Диалог переназначения */}
            {showReassign && (
                <div className="task-detail__overlay" onClick={() => setShowReassign(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Переназначить исполнителя</h3>
                        <input className="task-detail__input" placeholder="Поиск сотрудника..." value={reassignSearch} onChange={e => setReassignSearch(e.target.value)} />
                        {filteredForReassign.length > 0 && reassignSearch && (
                            <div className="task-detail__dropdown">
                                {filteredForReassign.slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${reassignUserId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setReassignUserId(e.userId); setReassignSearch(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <textarea className="task-detail__input" placeholder="Комментарий (необязательно)" value={reassignComment} onChange={e => setReassignComment(e.target.value)} rows={2} />
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleReassign} disabled={reassignSaving || !reassignUserId}>
                                {reassignSaving ? 'Сохранение...' : 'Переназначить'}
                            </button>
                            <button className="task-detail__btn" onClick={() => setShowReassign(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог добавления участника */}
            {showAddParticipant && (
                <div className="task-detail__overlay" onClick={() => setShowAddParticipant(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Добавить участника</h3>
                        <select className="task-detail__input" value={participantRole} onChange={e => setParticipantRole(e.target.value)}>
                            <option value="CoExecutor">Соисполнитель</option>
                            <option value="Observer">Наблюдатель</option>
                            <option value="Approver">Согласующий</option>
                            <option value="Controller">Контролёр</option>
                        </select>
                        <input className="task-detail__input" placeholder="Поиск сотрудника..." value={participantSearch} onChange={e => setParticipantSearch(e.target.value)} />
                        {filteredForParticipant.length > 0 && participantSearch && (
                            <div className="task-detail__dropdown">
                                {filteredForParticipant.slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${participantUserId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setParticipantUserId(e.userId); setParticipantSearch(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleAddParticipant} disabled={participantSaving || !participantUserId}>
                                {participantSaving ? 'Добавление...' : 'Добавить'}
                            </button>
                            <button className="task-detail__btn" onClick={() => setShowAddParticipant(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог добавления связи */}
            {showAddRelation && (
                <div className="task-detail__overlay" onClick={() => setShowAddRelation(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Добавить связь</h3>
                        <select className="task-detail__input" value={relationType} onChange={e => setRelationType(e.target.value)}>
                            <option value="RelatedTo">Связана с</option>
                            <option value="DependsOn">Зависит от</option>
                            <option value="Blocks">Блокирует</option>
                        </select>
                        <input className="task-detail__input" type="number" placeholder="Номер задачи (например, 42)" value={relationTargetNumber} onChange={e => setRelationTargetNumber(e.target.value)} />
                        {relationError && <div className="task-detail__error">{relationError}</div>}
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleAddRelation} disabled={relationSaving || !relationTargetNumber}>
                                {relationSaving ? 'Добавление...' : 'Добавить'}
                            </button>
                            <button className="task-detail__btn" onClick={() => { setShowAddRelation(false); setRelationError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог создания подзадачи */}
            {showCreateSubtask && (
                <div className="task-detail__overlay" onClick={() => setShowCreateSubtask(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Создать подзадачу</h3>
                        <input className="task-detail__input" placeholder="Тема подзадачи" value={subtaskSubject} onChange={e => setSubtaskSubject(e.target.value)} />
                        <input className="task-detail__input" placeholder="Поиск исполнителя..." value={subtaskSearch} onChange={e => setSubtaskSearch(e.target.value)} />
                        {filteredForSubtask.length > 0 && subtaskSearch && (
                            <div className="task-detail__dropdown">
                                {filteredForSubtask.slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${subtaskAssigneeId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setSubtaskAssigneeId(e.userId); setSubtaskSearch(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleCreateSubtask} disabled={subtaskSaving || !subtaskSubject.trim() || !subtaskAssigneeId}>
                                {subtaskSaving ? 'Создание...' : 'Создать'}
                            </button>
                            <button className="task-detail__btn" onClick={() => setShowCreateSubtask(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
            {/* FR-TASK-01.3: диалог отправки на согласование */}
            {showSendForApproval && (
                <div className="task-detail__overlay" onClick={() => setShowSendForApproval(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Отправить на согласование</h3>
                        <input className="task-detail__input" placeholder="Поиск согласующего..." value={sendApprovalApproverQuery}
                            onChange={e => { setSendApprovalApproverQuery(e.target.value); setSendApprovalApproverId(''); }} />
                        {sendApprovalApproverQuery && employees.filter(e => e.displayName.toLowerCase().includes(sendApprovalApproverQuery.toLowerCase())).slice(0, 8).length > 0 && (
                            <div className="task-detail__dropdown">
                                {employees.filter(e => e.displayName.toLowerCase().includes(sendApprovalApproverQuery.toLowerCase())).slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${sendApprovalApproverId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setSendApprovalApproverId(e.userId); setSendApprovalApproverQuery(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <textarea className="task-detail__input" placeholder="Комментарий (необязательно)"
                            value={sendApprovalComment} onChange={e => setSendApprovalComment(e.target.value)} rows={3} />
                        {approvalError && <p className="task-detail__error">{approvalError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleSendForApproval} disabled={approvalSaving}>
                                {approvalSaving ? 'Отправка...' : 'Отправить'}
                            </button>
                            <button className="task-detail__btn" onClick={() => { setShowSendForApproval(false); setApprovalError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* FR-TASK-01.3: поле комментария к решению по согласованию */}
            {(hasAction('approve-pre') || hasAction('reject-pre') || hasAction('approve') || hasAction('reject')) && (
                <div style={{ padding: '8px 16px', background: '#fff8e1', borderTop: '1px solid #ffe082' }}>
                    <input className="task-detail__input" style={{ maxWidth: 420 }}
                        placeholder="Комментарий к решению (необязательно)"
                        value={approvalComment} onChange={e => setApprovalComment(e.target.value)} />
                    {approvalError && <p className="task-detail__error">{approvalError}</p>}
                </div>
            )}

            {/* FR-TASK-01.4: Диалог добавления трудозатрат */}
            {showAddTimeLog && (
                <div className="task-detail__overlay" onClick={() => setShowAddTimeLog(false)}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Добавить трудозатраты</h3>
                        <label className="task-detail__field-label">Длительность (мин.) *</label>
                        <input className="task-detail__input" type="number" min="1" placeholder="Например, 60"
                            value={timeLogDuration} onChange={e => setTimeLogDuration(e.target.value)} />
                        <label className="task-detail__field-label">Дата начала</label>
                        <input className="task-detail__input" type="datetime-local"
                            value={timeLogStartDate}
                            onChange={e => setTimeLogStartDate(e.target.value ? new Date(e.target.value).toISOString() : '')} />
                        <label className="task-detail__field-label">Комментарий</label>
                        <textarea className="task-detail__input" placeholder="Необязательно"
                            value={timeLogComment} onChange={e => setTimeLogComment(e.target.value)} rows={2} />
                        <div className="task-detail__dialog-footer">
                            <button className="task-detail__btn task-detail__btn--primary" onClick={handleAddTimeLog}
                                disabled={timeLogSaving || !timeLogDuration}>
                                {timeLogSaving ? 'Сохранение...' : 'Добавить'}
                            </button>
                            <button className="task-detail__btn" onClick={() => setShowAddTimeLog(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
