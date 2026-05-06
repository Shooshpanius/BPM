import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../../context/AuthContext';
import {
    getTask, markTaskRead, addTaskComment, getTaskComments,
    getTaskAttachments, getTaskParticipants, addTaskParticipant, removeTaskParticipant,
    getTaskRelations, addTaskRelation, removeTaskRelation,
    getTaskHistory, copyTask, reassignTask, createSubtask, listTasks,
    getAllowedActions, approvePreTask, rejectPreTask, sendTaskForApproval, approveTask, rejectTask,
    getTaskTimeLogs, addTaskTimeLog, deleteTaskTimeLog, takeControl, releaseControl,
    startTaskWork, markTaskDone, markTaskCannotDo, closeTask, rescheduleTask, reopenTask, claimTask,
    getTaskWatchers, addTaskWatcher, removeTaskWatcher,
    getTaskQuestions, askTaskQuestion, answerTaskQuestion,
    updateTask,
    watchTask, unwatchTask,
    getTaskReminders, addTaskReminder, deleteTaskReminder,
    scheduleTask, unscheduleTask,
    TASK_STATUS_LABELS, TASK_PRIORITY_LABELS,
} from '../../api/tasksApi';
import type {
    TaskDto, TaskCommentDto, TaskAttachmentDto,
    TaskRelationDto, TaskParticipantDto, TaskHistoryEntryDto,
    TaskSummaryDto, TaskTimeLogDto, TaskQuestionDto, TaskReminderDto,
} from '../../api/tasksApi';
import { getDirectoryEmployees } from '../../api/orgDirectoryApi';
import type { DirectoryEmployeeDto } from '../../api/orgDirectoryApi';
import './TaskDetailPage.css';

interface TaskDetailPageProps {
    taskId: string;
    onBack: () => void;
}

type TabId = 'description' | 'subtasks' | 'relations' | 'participants' | 'timelogs' | 'process-info' | 'watchers' | 'questions' | 'reminders';

const RELATION_LABELS: Record<string, string> = {
    DependsOn: 'Зависит от', Blocks: 'Блокирует', RelatedTo: 'Связана с',
};

const PARTICIPANT_ROLE_LABELS: Record<string, string> = {
    CoExecutor: 'Соисполнитель', Observer: 'Наблюдатель',
    Approver: 'Согласующий', Controller: 'Контролёр',
};

/** Карточка задачи с 4 вкладками (FR-TASK-01.1). */
export function TaskDetailPage({ taskId, onBack }: TaskDetailPageProps) {
    const { accessToken: token, userId } = useAuth();
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
    // FR-TASK-02.1: наблюдатели и вопросы
    const [watchers, setWatchers] = useState<TaskParticipantDto[]>([]);
    const [questions, setQuestions] = useState<TaskQuestionDto[]>([]);

    // ─── FR-TASK-02.1: Диалоги расширенных действий ──────────────────────────
    const [showDoneDialog, setShowDoneDialog] = useState(false);
    const [doneComment, setDoneComment] = useState('');
    const [doneEffortMinutes, setDoneEffortMinutes] = useState('');
    const [doneCopyAttachments, setDoneCopyAttachments] = useState(false);
    const [doneNotifyCoExec, setDoneNotifyCoExec] = useState(false);
    const [doneSaving, setDoneSaving] = useState(false);

    const [showCannotDoDialog, setShowCannotDoDialog] = useState(false);
    const [cannotDoComment, setCannotDoComment] = useState('');
    const [cannotDoNotifyCoExec, setCannotDoNotifyCoExec] = useState(false);
    const [cannotDoSaving, setCannotDoSaving] = useState(false);

    const [showStartDialog, setShowStartDialog] = useState(false);
    const [startComment, setStartComment] = useState('');
    const [startNotifyCoExec, setStartNotifyCoExec] = useState(false);
    const [startSaving, setStartSaving] = useState(false);

    const [showCloseDialog, setShowCloseDialog] = useState(false);
    const [closeComment, setCloseComment] = useState('');
    const [closeNotifyCoExec, setCloseNotifyCoExec] = useState(false);
    const [closeSaving, setCloseSaving] = useState(false);

    const [showRescheduleDialog, setShowRescheduleDialog] = useState(false);
    const [rescheduleDate, setRescheduleDate] = useState('');
    const [rescheduleComment, setRescheduleComment] = useState('');
    const [rescheduleSaving, setRescheduleSaving] = useState(false);

    // FR-TASK-02.1: наблюдатели
    const [showAddWatcher, setShowAddWatcher] = useState(false);
    const [watcherSearch, setWatcherSearch] = useState('');
    const [watcherUserId, setWatcherUserId] = useState('');
    const [watcherSaving, setWatcherSaving] = useState(false);

    // FR-TASK-02.1: вопросы
    const [showAskQuestion, setShowAskQuestion] = useState(false);
    const [questionText, setQuestionText] = useState('');
    const [questionRecipientId, setQuestionRecipientId] = useState('');
    const [questionRecipientSearch, setQuestionRecipientSearch] = useState('');
    const [questionSaving, setQuestionSaving] = useState(false);
    const [showAnswerDialog, setShowAnswerDialog] = useState<string | null>(null);
    const [answerText, setAnswerText] = useState('');
    const [answerSaving, setAnswerSaving] = useState(false);
    const [actionError, setActionError] = useState<string | null>(null);

    // FR-TASK-02.1: диалог редактирования задачи
    const [showEditDialog, setShowEditDialog] = useState(false);
    const [editSubject, setEditSubject] = useState('');
    const [editDescription, setEditDescription] = useState('');
    const [editPriority, setEditPriority] = useState('');
    const [editDueDate, setEditDueDate] = useState('');
    const [editPlannedEffort, setEditPlannedEffort] = useState('');
    const [editSaving, setEditSaving] = useState(false);
    const [editError, setEditError] = useState<string | null>(null);

    // FR-TASK-02.1: диалог создания подзадачи — расширенные поля
    const [subtaskDueDate, setSubtaskDueDate] = useState('');
    const [subtaskPriority, setSubtaskPriority] = useState('Medium');
    const [subtaskDescription, setSubtaskDescription] = useState('');

    // ─── FR-TASK-02.3: Напоминания, планирование, self-subscribe ─────────────
    const [reminders, setReminders] = useState<TaskReminderDto[]>([]);
    const [showAddReminder, setShowAddReminder] = useState(false);
    const [reminderDate, setReminderDate] = useState('');
    const [reminderNote, setReminderNote] = useState('');
    const [reminderSaving, setReminderSaving] = useState(false);

    const [showScheduleDialog, setShowScheduleDialog] = useState(false);
    const [scheduleDate, setScheduleDate] = useState('');
    const [scheduleSaving, setScheduleSaving] = useState(false);

    const [watchSaving, setWatchSaving] = useState(false);

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

    // ─── Подсветка кнопок при клике мимо модального окна ─────────────────────
    const [shakeTarget, setShakeTarget] = useState<string | null>(null);
    const shakeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const triggerShake = (target: string) => {
        if (shakeTimerRef.current) clearTimeout(shakeTimerRef.current);
        setShakeTarget(target);
        shakeTimerRef.current = setTimeout(() => setShakeTarget(null), 900);
    };
    const sc = (target: string) => shakeTarget === target ? ' btn-flash' : '';

    const loadEmployees = useCallback(async () => {
        if (employees.length === 0 && token) {
            try { setEmployees((await getDirectoryEmployees(token, {})).items); } catch { /* игнорируем */ }
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
                setSubtasks(await listTasks(token, { parentTaskId: taskId }));
            } else if (tab === 'timelogs') {
                setTimeLogs(await getTaskTimeLogs(token, taskId));
            } else if (tab === 'watchers') {
                setWatchers(await getTaskWatchers(token, taskId));
            } else if (tab === 'questions') {
                setQuestions(await getTaskQuestions(token, taskId));
            } else if (tab === 'reminders') {
                setReminders(await getTaskReminders(token, taskId));
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

    // FR-TASK-01.4: удалить запись трудозатрат
    const handleDeleteTimeLog = async (logId: string) => {
        if (!token) return;
        if (!window.confirm('Удалить запись трудозатрат?')) return;
        try {
            await deleteTaskTimeLog(token, taskId, logId);
            setTimeLogs(prev => prev.filter(l => l.id !== logId));
            const updated = await getTask(token, taskId);
            setTask(updated);
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    // FR-TASK-01.4: взять / снять задачу с контроля
    const handleTakeControl = async () => {
        if (!token) return;
        try {
            const updated = await takeControl(token, taskId);
            setTask(updated);
            const actions = await getAllowedActions(token, taskId).catch(() => []);
            setAllowedActions(actions);
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    const handleReleaseControl = async () => {
        if (!token) return;
        if (!window.confirm('Снять задачу с контроля?')) return;
        try {
            const updated = await releaseControl(token, taskId);
            setTask(updated);
            const actions = await getAllowedActions(token, taskId).catch(() => []);
            setAllowedActions(actions);
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    // FR-TASK-02.1: действия с расширенными диалогами
    const refreshTask = async () => {
        if (!token) return;
        const [data, actions] = await Promise.all([
            getTask(token, taskId),
            getAllowedActions(token, taskId).catch(() => []),
        ]);
        setTask(data);
        setAllowedActions(actions);
    };

    const handleStartWork = async () => {
        if (!token) return;
        setStartSaving(true);
        setActionError(null);
        try {
            await startTaskWork(token, taskId, {
                comment: startComment || undefined,
                notifyCoExecutors: startNotifyCoExec,
            });
            setShowStartDialog(false);
            setStartComment('');
            setStartNotifyCoExec(false);
            await refreshTask();
        } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setStartSaving(false);
        }
    };

    const handleMarkDone = async () => {
        if (!token) return;
        setDoneSaving(true);
        setActionError(null);
        try {
            await markTaskDone(token, taskId, {
                comment: doneComment || undefined,
                effortMinutes: doneEffortMinutes ? parseInt(doneEffortMinutes, 10) : undefined,
                copyAttachmentsFromSubtasks: doneCopyAttachments,
                notifyCoExecutors: doneNotifyCoExec,
            });
            setShowDoneDialog(false);
            setDoneComment('');
            setDoneEffortMinutes('');
            setDoneCopyAttachments(false);
            setDoneNotifyCoExec(false);
            await refreshTask();
        } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setDoneSaving(false);
        }
    };

    const handleMarkCannotDo = async () => {
        if (!token) return;
        setCannotDoSaving(true);
        setActionError(null);
        try {
            await markTaskCannotDo(token, taskId, {
                comment: cannotDoComment || undefined,
                notifyCoExecutors: cannotDoNotifyCoExec,
            });
            setShowCannotDoDialog(false);
            setCannotDoComment('');
            setCannotDoNotifyCoExec(false);
            await refreshTask();
        } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setCannotDoSaving(false);
        }
    };

    const handleCloseTask = async () => {
        if (!token) return;
        setCloseSaving(true);
        setActionError(null);
        try {
            await closeTask(token, taskId, {
                comment: closeComment || undefined,
                notifyCoExecutors: closeNotifyCoExec,
            });
            setShowCloseDialog(false);
            setCloseComment('');
            setCloseNotifyCoExec(false);
            await refreshTask();
        } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setCloseSaving(false);
        }
    };

    const handleReschedule = async () => {
        if (!token || !rescheduleDate) return;
        setRescheduleSaving(true);
        setActionError(null);
        try {
            await rescheduleTask(token, taskId, new Date(rescheduleDate).toISOString(), rescheduleComment || undefined);
            setShowRescheduleDialog(false);
            setRescheduleDate('');
            setRescheduleComment('');
            await refreshTask();
        } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setRescheduleSaving(false);
        }
    };

    const handleReopen = async () => {
        if (!token) return;
        if (!window.confirm('Открыть задачу заново?')) return;
        try {
            await reopenTask(token, taskId);
            await refreshTask();
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    const handleClaim = async () => {
        if (!token) return;
        if (!window.confirm('Взять задачу на себя?')) return;
        try {
            await claimTask(token, taskId);
            await refreshTask();
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        }
    };

    // FR-TASK-02.1: наблюдатели
    const handleAddWatcher = async () => {
        if (!token || !watcherUserId) return;
        setWatcherSaving(true);
        try {
            const w = await addTaskWatcher(token, taskId, watcherUserId);
            setWatchers(prev => [...prev.filter(p => p.id !== w.id), w]);
            setShowAddWatcher(false);
            setWatcherUserId('');
            setWatcherSearch('');
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setWatcherSaving(false);
        }
    };

    const handleRemoveWatcher = async (uid: string) => {
        if (!token) return;
        await removeTaskWatcher(token, taskId, uid);
        setWatchers(prev => prev.filter(p => p.userId !== uid));
    };

    // FR-TASK-02.1: вопросы
    const handleAskQuestion = async () => {
        if (!token || !questionText.trim() || !questionRecipientId) return;
        setQuestionSaving(true);
        try {
            const q = await askTaskQuestion(token, taskId, questionText.trim(), questionRecipientId);
            setQuestions(prev => [...prev, q]);
            setShowAskQuestion(false);
            setQuestionText('');
            setQuestionRecipientId('');
            setQuestionRecipientSearch('');
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setQuestionSaving(false);
        }
    };

    const handleAnswerQuestion = async (questionId: string) => {
        if (!token || !answerText.trim()) return;
        setAnswerSaving(true);
        try {
            const q = await answerTaskQuestion(token, taskId, questionId, answerText.trim());
            setQuestions(prev => prev.map(x => x.id === q.id ? q : x));
            setShowAnswerDialog(null);
            setAnswerText('');
        } catch (e) {
            alert(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setAnswerSaving(false);
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

    // FR-TASK-02.1: редактирование задачи
    const openEditDialog = () => {
        if (!task) return;
        setEditSubject(task.subject);
        setEditDescription(task.description ?? '');
        setEditPriority(task.priority);
        setEditDueDate(task.dueDate ? new Date(task.dueDate).toISOString().slice(0, 16) : '');
        setEditPlannedEffort(task.plannedEffortMinutes ? String(task.plannedEffortMinutes) : '');
        setEditError(null);
        setShowEditDialog(true);
    };

    const handleEdit = async () => {
        if (!token || !editSubject.trim()) return;
        setEditSaving(true);
        setEditError(null);
        try {
            const updated = await updateTask(token, taskId, {
                subject: editSubject.trim(),
                description: editDescription.trim() || undefined,
                priority: editPriority || undefined,
                dueDate: editDueDate ? new Date(editDueDate).toISOString() : undefined,
                plannedEffortMinutes: editPlannedEffort ? parseInt(editPlannedEffort, 10) : undefined,
            });
            setTask(updated);
            setShowEditDialog(false);
        } catch (e) {
            setEditError(e instanceof Error ? e.message : 'Ошибка');
        } finally {
            setEditSaving(false);
        }
    };

    const handleCreateSubtask = async () => {
        if (!token || !subtaskSubject.trim() || !subtaskAssigneeId) return;
        setSubtaskSaving(true);
        try {
            const now = new Date();
            const due = subtaskDueDate
                ? new Date(subtaskDueDate)
                : (() => { const d = new Date(now); d.setDate(d.getDate() + 7); return d; })();
            await createSubtask(token, taskId, {
                subject: subtaskSubject.trim(),
                assigneeUserId: subtaskAssigneeId,
                startDate: now.toISOString(),
                dueDate: due.toISOString(),
                description: subtaskDescription.trim() || undefined,
                priority: subtaskPriority,
            });
            setShowCreateSubtask(false);
            setSubtaskSubject('');
            setSubtaskAssigneeId('');
            setSubtaskSearch('');
            setSubtaskDueDate('');
            setSubtaskPriority('Medium');
            setSubtaskDescription('');
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
                    {/* FR-TASK-02.1: редактирование задачи */}
                    {hasAction('edit') && (
                        <button className="task-detail__btn" onClick={openEditDialog}>
                            ✏️ Изменить
                        </button>
                    )}
                    {/* FR-TASK-02.1: основные действия с расширенными диалогами */}
                    {hasAction('start') && (
                        <button className="task-detail__btn task-detail__btn--primary"
                            onClick={() => setShowStartDialog(true)}>
                            ▶ Начать работу
                        </button>
                    )}
                    {hasAction('done') && (
                        <button className="task-detail__btn task-detail__btn--success"
                            onClick={() => setShowDoneDialog(true)}>
                            ✓ Сделано
                        </button>
                    )}
                    {hasAction('cannot-do') && (
                        <button className="task-detail__btn task-detail__btn--danger"
                            onClick={() => setShowCannotDoDialog(true)}>
                            ✗ Невозможно
                        </button>
                    )}
                    {hasAction('close') && (
                        <button className="task-detail__btn task-detail__btn--danger"
                            onClick={() => setShowCloseDialog(true)}>
                            🚫 Закрыть
                        </button>
                    )}
                    {hasAction('reschedule') && (
                        <button className="task-detail__btn"
                            onClick={() => setShowRescheduleDialog(true)}>
                            📅 Перенести срок
                        </button>
                    )}
                    {hasAction('reopen') && (
                        <button className="task-detail__btn"
                            onClick={handleReopen}>
                            🔄 Открыть заново
                        </button>
                    )}
                    {hasAction('claim') && (
                        <button className="task-detail__btn task-detail__btn--primary"
                            onClick={handleClaim}>
                            👋 Взять задачу
                        </button>
                    )}
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
                    {/* FR-TASK-02.3: Подписка / Отписка */}
                    {(() => {
                        const isWatcher = watchers.some(w => w.userId === userId);
                        return isWatcher
                            ? (
                                <button className="task-detail__btn" disabled={watchSaving}
                                    onClick={async () => {
                                        if (!token) return;
                                        setWatchSaving(true);
                                        try {
                                            await unwatchTask(token, taskId);
                                            setWatchers(prev => prev.filter(w => w.userId !== userId));
                                        } finally { setWatchSaving(false); }
                                    }}>
                                    👁 Отписаться
                                </button>
                            )
                            : (
                                <button className="task-detail__btn" disabled={watchSaving}
                                    onClick={async () => {
                                        if (!token) return;
                                        setWatchSaving(true);
                                        try {
                                            const w = await watchTask(token, taskId);
                                            setWatchers(prev => [...prev, w]);
                                        } finally { setWatchSaving(false); }
                                    }}>
                                    👁 Подписаться
                                </button>
                            );
                    })()}
                    {/* FR-TASK-02.3: Запланировать в календаре */}
                    {task.scheduledAt
                        ? (
                            <button className="task-detail__btn" title={`Запланировано на ${new Date(task.scheduledAt).toLocaleString('ru-RU')}`}
                                onClick={async () => {
                                    if (!token) return;
                                    const t = await unscheduleTask(token, taskId);
                                    setTask(t);
                                }}>
                                📅 Запланировано ✕
                            </button>
                        )
                        : (
                            <button className="task-detail__btn"
                                onClick={() => {
                                    const d = new Date();
                                    d.setHours(d.getHours() + 1, 0, 0, 0);
                                    setScheduleDate(d.toISOString().slice(0, 16));
                                    setShowScheduleDialog(true);
                                }}>
                                📅 Запланировать
                            </button>
                        )
                    }
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
                {/* FR-TASK-02.3: Запланировано */}
                {task.scheduledAt && (
                    <span className="task-detail__meta-item">
                        <strong>📅 Запланировано:</strong> {new Date(task.scheduledAt).toLocaleString('ru-RU')}
                    </span>
                )}
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
                {/* FR-TASK-01.4: Контроль */}
                {task.controlType && task.controlType !== 'None' && (
                    <span className="task-detail__meta-item">
                        <strong>Контроль:</strong>{' '}
                        {{ ControlAfterExecution: 'Контроль выполнения', CurrentControl: 'Текущий контроль', NotifyOnCompletion: 'Оповещать при выполнении' }[task.controlType] ?? task.controlType}
                        {task.controllerName && <> — {task.controllerName}</>}
                    </span>
                )}
                {hasAction('take-control') && (
                    <span className="task-detail__meta-item">
                        <button className="task-detail__btn task-detail__btn--sm" onClick={handleTakeControl}>
                            👁 Взять на контроль
                        </button>
                    </span>
                )}
                {hasAction('release-control') && (
                    <span className="task-detail__meta-item">
                        <button className="task-detail__btn task-detail__btn--sm task-detail__btn--danger" onClick={handleReleaseControl}>
                            ✕ Снять с контроля
                        </button>
                    </span>
                )}
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
                {/* FR-TASK-01.5: вид задачи */}
                {task.kind && task.kind !== 'Regular' && (
                    <span className="task-detail__meta-item">
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: 4,
                            padding: '2px 10px', borderRadius: 12, fontSize: 12, fontWeight: 600,
                            background: task.kind === 'Periodic' ? '#e6f7ff' : task.kind === 'ProcessTask' ? '#eff6ff' : '#fff7e6',
                            color: task.kind === 'Periodic' ? '#1890ff' : task.kind === 'ProcessTask' ? '#1d4ed8' : '#d46b08',
                            border: `1px solid ${task.kind === 'Periodic' ? '#91d5ff' : task.kind === 'ProcessTask' ? '#bfdbfe' : '#ffd591'}`,
                        }}>
                            {task.kind === 'Periodic' && '🔄 Периодическая'}
                            {task.kind === 'ProcessTask' && '⚙️ Задача по процессу'}
                            {task.kind === 'Resolution' && '📄 Задача по резолюции'}
                        </span>
                    </span>
                )}
                {/* FR-TASK-01.5.3: ссылка на документ */}
                {task.documentId && (
                    <span className="task-detail__meta-item">
                        <strong>Документ:</strong>{' '}
                        <span style={{ color: '#1890ff', cursor: 'pointer' }}
                            onClick={() => window.open(`/documents/${task.documentId}`, '_blank')}>
                            🔗 Открыть документ
                        </span>
                    </span>
                )}
                {/* FR-TASK-01.5: скачать вложения архивом */}
                {(task.kind === 'ProcessTask' || task.kind === 'Resolution') && task.attachmentCount > 0 && (
                    <span className="task-detail__meta-item">
                        <a
                            href={`/api/tasks/${task.id}/attachments/download`}
                            download
                            style={{ color: '#1890ff', textDecoration: 'none', fontSize: 13 }}>
                            📦 Скачать вложения ({task.attachmentCount})
                        </a>
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
                    { id: 'watchers', label: `👁 Наблюдатели (${watchers.length})` },
                    { id: 'questions', label: `❓ Вопросы (${questions.length})` },
                    { id: 'reminders', label: `⏰ Напоминания (${reminders.length})` },
                    ...(task.kind === 'ProcessTask' && task.processInfo
                        ? [{ id: 'process-info' as TabId, label: '⚙️ Процесс' }]
                        : []),
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
                                {task.subtaskActualEffortMinutes > 0 && (
                                    <span>По подзадачам: <strong>{task.subtaskActualEffortMinutes} мин.</strong></span>
                                )}
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
                                            <th></th>
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
                                                <td>
                                                    <button
                                                        className="task-detail__btn task-detail__btn--sm task-detail__btn--danger"
                                                        title="Удалить запись"
                                                        onClick={() => handleDeleteTimeLog(l.id)}>
                                                        ✕
                                                    </button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )
                        }
                    </div>
                )}
                {/* FR-TASK-01.5.2: Информация о процессе */}
                {tab === 'process-info' && task.processInfo && (
                    <div className="task-detail__tab-content">
                        <h4 style={{ marginTop: 0 }}>Информация о процессе</h4>
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <tbody>
                                <tr style={{ borderBottom: '1px solid #f0f0f0' }}>
                                    <td style={{ padding: '10px 8px', color: '#888', width: '40%' }}>Процесс</td>
                                    <td style={{ padding: '10px 8px', fontWeight: 600 }}>
                                        {task.processInfo.processName}{' '}
                                        <span style={{ fontSize: 12, color: '#888' }}>({task.processInfo.processVersionNumber})</span>
                                    </td>
                                </tr>
                                <tr style={{ borderBottom: '1px solid #f0f0f0' }}>
                                    <td style={{ padding: '10px 8px', color: '#888' }}>Экземпляр</td>
                                    <td style={{ padding: '10px 8px' }}>
                                        <span style={{ color: '#1890ff', cursor: 'pointer' }}
                                            onClick={() => window.open(`/bpm/instances/${task.processInfo!.instanceId}`, '_blank')}>
                                            🔗 {task.processInfo.instanceTitle || task.processInfo.instanceId}
                                        </span>
                                    </td>
                                </tr>
                                <tr style={{ borderBottom: '1px solid #f0f0f0' }}>
                                    <td style={{ padding: '10px 8px', color: '#888' }}>Дата запуска</td>
                                    <td style={{ padding: '10px 8px' }}>{new Date(task.processInfo.launchedAt).toLocaleString('ru-RU')}</td>
                                </tr>
                                <tr style={{ borderBottom: '1px solid #f0f0f0' }}>
                                    <td style={{ padding: '10px 8px', color: '#888' }}>Инициатор</td>
                                    <td style={{ padding: '10px 8px' }}>{task.processInfo.initiatorName}</td>
                                </tr>
                                {task.processInfo.ownerName && (
                                    <tr>
                                        <td style={{ padding: '10px 8px', color: '#888' }}>Ответственный</td>
                                        <td style={{ padding: '10px 8px' }}>{task.processInfo.ownerName}</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                )}

                {/* ─── FR-TASK-02.1: Наблюдатели ─── */}
                {tab === 'watchers' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Наблюдатели</h4>
                            <button className="task-detail__btn" onClick={() => { setShowAddWatcher(true); loadEmployees(); }}>+ Добавить</button>
                        </div>
                        {watchers.length === 0
                            ? <div className="task-detail__empty">Наблюдателей нет</div>
                            : (
                                <ul className="task-detail__participants">
                                    {watchers.map(w => (
                                        <li key={w.id} className="task-detail__participant">
                                            👁 <strong>{w.userName}</strong>
                                            <button className="task-detail__remove-btn" onClick={() => handleRemoveWatcher(w.userId)} title="Удалить наблюдателя">✕</button>
                                        </li>
                                    ))}
                                </ul>
                            )
                        }
                    </div>
                )}

                {/* ─── FR-TASK-02.1: Вопросы ─── */}
                {tab === 'questions' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Вопросы</h4>
                            <button className="task-detail__btn" onClick={() => { setShowAskQuestion(true); loadEmployees(); }}>+ Задать вопрос</button>
                        </div>
                        {questions.length === 0
                            ? <div className="task-detail__empty">Вопросов нет</div>
                            : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                                    {questions.map(q => (
                                        <div key={q.id} style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 12, background: '#f9fafb' }}>
                                            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                                                <span style={{ fontWeight: 600 }}>❓ {q.authorName}</span>
                                                <span style={{ fontSize: 12, color: '#888' }}>{new Date(q.createdAt).toLocaleString('ru-RU')}</span>
                                            </div>
                                            <p style={{ margin: '0 0 8px', color: '#374151' }}>{q.questionText}</p>
                                            <p style={{ margin: 0, fontSize: 12, color: '#6b7280' }}>→ {q.recipientName}</p>
                                            {q.answerText
                                                ? (
                                                    <div style={{ marginTop: 8, padding: '8px 12px', background: '#dcfce7', borderRadius: 6, borderLeft: '3px solid #86efac' }}>
                                                        <strong style={{ fontSize: 12, color: '#166534' }}>✓ Ответ:</strong>
                                                        <p style={{ margin: '4px 0 0', color: '#166534', fontSize: 13 }}>{q.answerText}</p>
                                                        {q.answeredAt && <p style={{ margin: '2px 0 0', fontSize: 11, color: '#888' }}>{new Date(q.answeredAt).toLocaleString('ru-RU')}</p>}
                                                    </div>
                                                )
                                                : (
                                                    <button className="task-detail__btn task-detail__btn--sm"
                                                        style={{ marginTop: 8 }}
                                                        onClick={() => { setShowAnswerDialog(q.id); setAnswerText(''); }}>
                                                        Ответить
                                                    </button>
                                                )
                                            }
                                        </div>
                                    ))}
                                </div>
                            )
                        }
                    </div>
                )}

                {/* ─── FR-TASK-02.3: Напоминания ─── */}
                {tab === 'reminders' && (
                    <div className="task-detail__tab-content">
                        <div className="task-detail__section-header">
                            <h4>Напоминания</h4>
                            <button className="task-detail__btn" onClick={() => {
                                const d = new Date(); d.setHours(d.getHours() + 1);
                                setReminderDate(d.toISOString().slice(0, 16));
                                setReminderNote('');
                                setShowAddReminder(true);
                            }}>+ Добавить</button>
                        </div>
                        {reminders.length === 0
                            ? <div className="task-detail__empty">Напоминаний нет</div>
                            : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                                    {reminders.map(r => (
                                        <div key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 12px', border: '1px solid #e5e7eb', borderRadius: 6, background: r.isSent ? '#f9fafb' : '#fffbeb' }}>
                                            <span style={{ fontSize: 20 }}>{r.isSent ? '✅' : '⏰'}</span>
                                            <div style={{ flex: 1 }}>
                                                <div style={{ fontWeight: 500 }}>{new Date(r.remindAt).toLocaleString('ru-RU')}</div>
                                                {r.note && <div style={{ fontSize: 13, color: '#6b7280' }}>{r.note}</div>}
                                                {r.isSent && <div style={{ fontSize: 11, color: '#9ca3af' }}>Отправлено</div>}
                                            </div>
                                            {!r.isSent && (
                                                <button className="task-detail__btn task-detail__btn--danger task-detail__btn--sm"
                                                    onClick={async () => {
                                                        if (!token) return;
                                                        await deleteTaskReminder(token, taskId, r.id);
                                                        setReminders(prev => prev.filter(x => x.id !== r.id));
                                                    }}>
                                                    ✕
                                                </button>
                                            )}
                                        </div>
                                    ))}
                                </div>
                            )
                        }
                    </div>
                )}
            </div>

            {/* Диалог переназначения */}
            {showReassign && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('reassign'); }}>
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
                            <button className={`task-detail__btn task-detail__btn--primary${sc('reassign')}`} onClick={handleReassign} disabled={reassignSaving || !reassignUserId}>
                                {reassignSaving ? 'Сохранение...' : 'Переназначить'}
                            </button>
                            <button className={`task-detail__btn${sc('reassign')}`} onClick={() => setShowReassign(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог добавления участника */}
            {showAddParticipant && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('addParticipant'); }}>
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
                            <button className={`task-detail__btn task-detail__btn--primary${sc('addParticipant')}`} onClick={handleAddParticipant} disabled={participantSaving || !participantUserId}>
                                {participantSaving ? 'Добавление...' : 'Добавить'}
                            </button>
                            <button className={`task-detail__btn${sc('addParticipant')}`} onClick={() => setShowAddParticipant(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог добавления связи */}
            {showAddRelation && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('addRelation'); }}>
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
                            <button className={`task-detail__btn task-detail__btn--primary${sc('addRelation')}`} onClick={handleAddRelation} disabled={relationSaving || !relationTargetNumber}>
                                {relationSaving ? 'Добавление...' : 'Добавить'}
                            </button>
                            <button className={`task-detail__btn${sc('addRelation')}`} onClick={() => { setShowAddRelation(false); setRelationError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог создания подзадачи */}
            {showCreateSubtask && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('createSubtask'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>Создать подзадачу</h3>
                        <label className="task-detail__field-label">Тема *</label>
                        <input className="task-detail__input" placeholder="Тема подзадачи" value={subtaskSubject} onChange={e => setSubtaskSubject(e.target.value)} />
                        <label className="task-detail__field-label">Исполнитель *</label>
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
                        <label className="task-detail__field-label">Срок</label>
                        <input className="task-detail__input" type="datetime-local"
                            value={subtaskDueDate}
                            onChange={e => setSubtaskDueDate(e.target.value)} />
                        <label className="task-detail__field-label">Приоритет</label>
                        <select className="task-detail__input" value={subtaskPriority} onChange={e => setSubtaskPriority(e.target.value)}>
                            <option value="Low">Низкий</option>
                            <option value="Medium">Средний</option>
                            <option value="High">Высокий</option>
                            <option value="Critical">Критический</option>
                        </select>
                        <label className="task-detail__field-label">Описание</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={subtaskDescription} onChange={e => setSubtaskDescription(e.target.value)} rows={2} />
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('createSubtask')}`} onClick={handleCreateSubtask} disabled={subtaskSaving || !subtaskSubject.trim() || !subtaskAssigneeId}>
                                {subtaskSaving ? 'Создание...' : 'Создать'}
                            </button>
                            <button className={`task-detail__btn${sc('createSubtask')}`} onClick={() => setShowCreateSubtask(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
            {/* FR-TASK-01.3: диалог отправки на согласование */}
            {showSendForApproval && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('sendForApproval'); }}>
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
                            <button className={`task-detail__btn task-detail__btn--primary${sc('sendForApproval')}`} onClick={handleSendForApproval} disabled={approvalSaving}>
                                {approvalSaving ? 'Отправка...' : 'Отправить'}
                            </button>
                            <button className={`task-detail__btn${sc('sendForApproval')}`} onClick={() => { setShowSendForApproval(false); setApprovalError(null); }}>Отмена</button>
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
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('addTimeLog'); }}>
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
                            <button className={`task-detail__btn task-detail__btn--primary${sc('addTimeLog')}`} onClick={handleAddTimeLog}
                                disabled={timeLogSaving || !timeLogDuration}>
                                {timeLogSaving ? 'Сохранение...' : 'Добавить'}
                            </button>
                            <button className={`task-detail__btn${sc('addTimeLog')}`} onClick={() => setShowAddTimeLog(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── FR-TASK-02.1: Диалоги новых действий ─── */}

            {/* Диалог: Начать работу */}
            {showStartDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('start'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>▶ Начать работу</h3>
                        <label className="task-detail__field-label">Комментарий</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={startComment} onChange={e => setStartComment(e.target.value)} rows={2} />
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
                            <input type="checkbox" checked={startNotifyCoExec} onChange={e => setStartNotifyCoExec(e.target.checked)} />
                            Уведомить соисполнителей
                        </label>
                        {actionError && <p className="task-detail__error">{actionError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('start')}`} onClick={handleStartWork} disabled={startSaving}>
                                {startSaving ? 'Обновление...' : 'Начать работу'}
                            </button>
                            <button className={`task-detail__btn${sc('start')}`} onClick={() => { setShowStartDialog(false); setActionError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Сделано */}
            {showDoneDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('done'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>✓ Отметить выполненной</h3>
                        <label className="task-detail__field-label">Комментарий</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={doneComment} onChange={e => setDoneComment(e.target.value)} rows={2} />
                        <label className="task-detail__field-label">Затраченное время (мин.)</label>
                        <input className="task-detail__input" type="number" min="1" placeholder="Например, 60" value={doneEffortMinutes} onChange={e => setDoneEffortMinutes(e.target.value)} />
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
                            <input type="checkbox" checked={doneCopyAttachments} onChange={e => setDoneCopyAttachments(e.target.checked)} />
                            Скопировать вложения из подзадач
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
                            <input type="checkbox" checked={doneNotifyCoExec} onChange={e => setDoneNotifyCoExec(e.target.checked)} />
                            Уведомить соисполнителей
                        </label>
                        {actionError && <p className="task-detail__error">{actionError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--success${sc('done')}`} onClick={handleMarkDone} disabled={doneSaving}>
                                {doneSaving ? 'Обновление...' : 'Подтвердить'}
                            </button>
                            <button className={`task-detail__btn${sc('done')}`} onClick={() => { setShowDoneDialog(false); setActionError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Невозможно выполнить */}
            {showCannotDoDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('cannotDo'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>✗ Невозможно выполнить</h3>
                        <label className="task-detail__field-label">Комментарий (причина)</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={cannotDoComment} onChange={e => setCannotDoComment(e.target.value)} rows={3} />
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
                            <input type="checkbox" checked={cannotDoNotifyCoExec} onChange={e => setCannotDoNotifyCoExec(e.target.checked)} />
                            Уведомить соисполнителей
                        </label>
                        {actionError && <p className="task-detail__error">{actionError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--danger${sc('cannotDo')}`} onClick={handleMarkCannotDo} disabled={cannotDoSaving}>
                                {cannotDoSaving ? 'Обновление...' : 'Подтвердить'}
                            </button>
                            <button className={`task-detail__btn${sc('cannotDo')}`} onClick={() => { setShowCannotDoDialog(false); setActionError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Закрыть задачу */}
            {showCloseDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('closeDialog'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>🚫 Закрыть задачу</h3>
                        <label className="task-detail__field-label">Комментарий (причина)</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={closeComment} onChange={e => setCloseComment(e.target.value)} rows={2} />
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8 }}>
                            <input type="checkbox" checked={closeNotifyCoExec} onChange={e => setCloseNotifyCoExec(e.target.checked)} />
                            Уведомить соисполнителей
                        </label>
                        {actionError && <p className="task-detail__error">{actionError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--danger${sc('closeDialog')}`} onClick={handleCloseTask} disabled={closeSaving}>
                                {closeSaving ? 'Обновление...' : 'Закрыть'}
                            </button>
                            <button className={`task-detail__btn${sc('closeDialog')}`} onClick={() => { setShowCloseDialog(false); setActionError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Перенести срок */}
            {showRescheduleDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('reschedule'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>📅 Перенести срок</h3>
                        <label className="task-detail__field-label">Новый срок *</label>
                        <input className="task-detail__input" type="datetime-local"
                            value={rescheduleDate}
                            onChange={e => setRescheduleDate(e.target.value)} />
                        <label className="task-detail__field-label">Комментарий</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={rescheduleComment} onChange={e => setRescheduleComment(e.target.value)} rows={2} />
                        {actionError && <p className="task-detail__error">{actionError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('reschedule')}`} onClick={handleReschedule}
                                disabled={rescheduleSaving || !rescheduleDate}>
                                {rescheduleSaving ? 'Сохранение...' : 'Перенести'}
                            </button>
                            <button className={`task-detail__btn${sc('reschedule')}`} onClick={() => { setShowRescheduleDialog(false); setActionError(null); }}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Добавить наблюдателя */}
            {showAddWatcher && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('addWatcher'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>👁 Добавить наблюдателя</h3>
                        <input className="task-detail__input" placeholder="Поиск сотрудника..." value={watcherSearch} onChange={e => { setWatcherSearch(e.target.value); setWatcherUserId(''); }} />
                        {watcherSearch && employees.filter(e => e.displayName.toLowerCase().includes(watcherSearch.toLowerCase())).slice(0, 8).length > 0 && (
                            <div className="task-detail__dropdown">
                                {employees.filter(e => e.displayName.toLowerCase().includes(watcherSearch.toLowerCase())).slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${watcherUserId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setWatcherUserId(e.userId); setWatcherSearch(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('addWatcher')}`} onClick={handleAddWatcher} disabled={watcherSaving || !watcherUserId}>
                                {watcherSaving ? 'Добавление...' : 'Добавить'}
                            </button>
                            <button className={`task-detail__btn${sc('addWatcher')}`} onClick={() => setShowAddWatcher(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Задать вопрос */}
            {showAskQuestion && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('askQuestion'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>❓ Задать вопрос</h3>
                        <label className="task-detail__field-label">Получатель *</label>
                        <input className="task-detail__input" placeholder="Поиск сотрудника..." value={questionRecipientSearch}
                            onChange={e => { setQuestionRecipientSearch(e.target.value); setQuestionRecipientId(''); }} />
                        {questionRecipientSearch && employees.filter(e => e.displayName.toLowerCase().includes(questionRecipientSearch.toLowerCase())).slice(0, 8).length > 0 && (
                            <div className="task-detail__dropdown">
                                {employees.filter(e => e.displayName.toLowerCase().includes(questionRecipientSearch.toLowerCase())).slice(0, 8).map(e => (
                                    <div key={e.userId} className={`task-detail__dropdown-item${questionRecipientId === e.userId ? ' task-detail__dropdown-item--selected' : ''}`}
                                        onClick={() => { setQuestionRecipientId(e.userId); setQuestionRecipientSearch(e.displayName); }}>
                                        {e.displayName}
                                    </div>
                                ))}
                            </div>
                        )}
                        <label className="task-detail__field-label">Текст вопроса *</label>
                        <textarea className="task-detail__input" placeholder="Введите вопрос..." value={questionText} onChange={e => setQuestionText(e.target.value)} rows={3} />
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('askQuestion')}`} onClick={handleAskQuestion}
                                disabled={questionSaving || !questionText.trim() || !questionRecipientId}>
                                {questionSaving ? 'Отправка...' : 'Задать вопрос'}
                            </button>
                            <button className={`task-detail__btn${sc('askQuestion')}`} onClick={() => setShowAskQuestion(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Диалог: Ответить на вопрос */}
            {showAnswerDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('answerDialog'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>✏️ Ответить на вопрос</h3>
                        <textarea className="task-detail__input" placeholder="Введите ответ..." value={answerText} onChange={e => setAnswerText(e.target.value)} rows={4} />
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('answerDialog')}`} onClick={() => handleAnswerQuestion(showAnswerDialog)}
                                disabled={answerSaving || !answerText.trim()}>
                                {answerSaving ? 'Отправка...' : 'Ответить'}
                            </button>
                            <button className={`task-detail__btn${sc('answerDialog')}`} onClick={() => setShowAnswerDialog(null)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── FR-TASK-02.1: Диалог редактирования задачи ─── */}
            {showEditDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('editDialog'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>✏️ Изменить задачу</h3>
                        <label className="task-detail__field-label">Тема *</label>
                        <input className="task-detail__input" placeholder="Тема задачи" value={editSubject}
                            onChange={e => setEditSubject(e.target.value)} />
                        <label className="task-detail__field-label">Описание</label>
                        <textarea className="task-detail__input" placeholder="Необязательно" value={editDescription}
                            onChange={e => setEditDescription(e.target.value)} rows={3} />
                        <label className="task-detail__field-label">Приоритет</label>
                        <select className="task-detail__input" value={editPriority}
                            onChange={e => setEditPriority(e.target.value)}>
                            <option value="Low">Низкий</option>
                            <option value="Medium">Средний</option>
                            <option value="High">Высокий</option>
                            <option value="Critical">Критический</option>
                        </select>
                        <label className="task-detail__field-label">Срок</label>
                        <input className="task-detail__input" type="datetime-local" value={editDueDate}
                            onChange={e => setEditDueDate(e.target.value)} />
                        <label className="task-detail__field-label">Плановые трудозатраты (мин.)</label>
                        <input className="task-detail__input" type="number" min="1" placeholder="Например, 60"
                            value={editPlannedEffort} onChange={e => setEditPlannedEffort(e.target.value)} />
                        {editError && <p className="task-detail__error">{editError}</p>}
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('editDialog')}`} onClick={handleEdit}
                                disabled={editSaving || !editSubject.trim()}>
                                {editSaving ? 'Сохранение...' : 'Сохранить'}
                            </button>
                            <button className={`task-detail__btn${sc('editDialog')}`} onClick={() => setShowEditDialog(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* FR-TASK-02.3: Диалог добавления напоминания */}
            {showAddReminder && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('addReminder'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>⏰ Добавить напоминание</h3>
                        <label className="task-detail__field-label">Дата и время</label>
                        <input className="task-detail__input" type="datetime-local" value={reminderDate}
                            onChange={e => setReminderDate(e.target.value)} />
                        <label className="task-detail__field-label">Заметка (необязательно)</label>
                        <input className="task-detail__input" type="text" placeholder="Например, позвонить заказчику" value={reminderNote}
                            onChange={e => setReminderNote(e.target.value)} />
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('addReminder')}`}
                                disabled={reminderSaving || !reminderDate}
                                onClick={async () => {
                                    if (!token || !reminderDate) return;
                                    setReminderSaving(true);
                                    try {
                                        const r = await addTaskReminder(token, taskId, new Date(reminderDate).toISOString(), reminderNote || undefined);
                                        setReminders(prev => [...prev, r]);
                                        setShowAddReminder(false);
                                    } finally { setReminderSaving(false); }
                                }}>
                                {reminderSaving ? 'Сохранение...' : 'Добавить'}
                            </button>
                            <button className={`task-detail__btn${sc('addReminder')}`} onClick={() => setShowAddReminder(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}

            {/* FR-TASK-02.3: Диалог планирования задачи */}
            {showScheduleDialog && (
                <div className="task-detail__overlay" onClick={(e) => { if (e.target === e.currentTarget) triggerShake('schedule'); }}>
                    <div className="task-detail__dialog" onClick={e => e.stopPropagation()}>
                        <h3>📅 Запланировать задачу</h3>
                        <label className="task-detail__field-label">Дата и время</label>
                        <input className="task-detail__input" type="datetime-local" value={scheduleDate}
                            onChange={e => setScheduleDate(e.target.value)} />
                        <div className="task-detail__dialog-footer">
                            <button className={`task-detail__btn task-detail__btn--primary${sc('schedule')}`}
                                disabled={scheduleSaving || !scheduleDate}
                                onClick={async () => {
                                    if (!token || !scheduleDate) return;
                                    setScheduleSaving(true);
                                    try {
                                        const t = await scheduleTask(token, taskId, new Date(scheduleDate).toISOString());
                                        setTask(t);
                                        setShowScheduleDialog(false);
                                    } finally { setScheduleSaving(false); }
                                }}>
                                {scheduleSaving ? 'Сохранение...' : 'Запланировать'}
                            </button>
                            <button className={`task-detail__btn${sc('schedule')}`} onClick={() => setShowScheduleDialog(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
