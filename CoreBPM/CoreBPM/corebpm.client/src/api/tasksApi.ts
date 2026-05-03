// API-клиент модуля «Задачи» (FR-TASK-01.1)

export type TaskStatus =
    | 'New' | 'Read' | 'InProgress' | 'PreApproval' | 'OnApproval'
    | 'Approved' | 'PreApprovalRejected' | 'ApprovalRejected' | 'Done'
    | 'DoneNeedsControl' | 'DoneControlled' | 'CannotDo'
    | 'CannotDoNeedsControl' | 'CannotDoControlled' | 'Closed' | 'Postponed';

export type TaskPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
    New: 'Новая',
    Read: 'Прочитана',
    InProgress: 'Выполняется',
    PreApproval: 'На предварит. согласовании',
    OnApproval: 'На согласовании',
    Approved: 'Согласовано',
    PreApprovalRejected: 'Отказано (предварит.)',
    ApprovalRejected: 'Отказано в согласовании',
    Done: 'Выполнено',
    DoneNeedsControl: 'Выполнено, нужен контроль',
    DoneControlled: 'Выполнено и проконтролировано',
    CannotDo: 'Невозможно выполнить',
    CannotDoNeedsControl: 'Невозможно, нужен контроль',
    CannotDoControlled: 'Невозможно, проконтролировано',
    Closed: 'Закрыто',
    Postponed: 'Отложена',
};

export const TASK_PRIORITY_LABELS: Record<TaskPriority, string> = {
    Critical: 'Критический',
    High: 'Высокий',
    Medium: 'Средний',
    Low: 'Низкий',
};

export interface TaskParticipantDto {
    id: string;
    userId: string;
    userName: string;
    role: string;
}

export interface TaskSummaryDto {
    id: string;
    number: number;
    subject: string;
    status: TaskStatus;
    priority: TaskPriority;
    categoryId?: string;
    assigneeUserId: string;
    assigneeName: string;
    dueDate: string;
    isOverdue: boolean;
    createdAt: string;
    tags: string[];
}

export interface TaskDto {
    id: string;
    number: number;
    subject: string;
    description?: string;
    status: TaskStatus;
    priority: TaskPriority;
    categoryId?: string;
    authorUserId: string;
    authorName: string;
    assigneeUserId: string;
    assigneeName: string;
    startDate: string;
    dueDate: string;
    dateCorrectionMode: string;
    plannedEffortMinutes?: number;
    /** FR-TASK-01.4: фактические трудозатраты (сумма timelogs, в минутах) */
    actualEffortMinutes: number;
    controlType: string;
    controllerUserId?: string;
    controllerName?: string;
    /** FR-TASK-01.3: согласующий */
    approverUserId?: string;
    approverName?: string;
    parentTaskId?: string;
    isOverdue: boolean;
    postponedUntil?: string;
    sourceInstanceId?: string;
    sourceElementId?: string;
    /** FR-TASK-01.5: вид задачи */
    kind: 'Regular' | 'Periodic' | 'ProcessTask' | 'Resolution';
    documentId?: string;
    seriesId?: string;
    processInfo?: ProcessTaskInfoDto;
    recurrence?: TaskRecurrenceDto;
    createdAt: string;
    updatedAt: string;
    participants: TaskParticipantDto[];
    tags: string[];
    subtaskCount: number;
    commentCount: number;
    attachmentCount: number;
}

export interface TaskCommentDto {
    id: string;
    authorUserId: string;
    authorName: string;
    body: string;
    createdAt: string;
}

export interface TaskAttachmentDto {
    id: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    uploadedByUserId: string;
    createdAt: string;
}

export interface TaskRelationDto {
    id: string;
    sourceTaskId: string;
    targetTaskId: string;
    targetSubject: string;
    targetNumber: number;
    relationType: string;
}

export interface TaskHistoryEntryDto {
    id: string;
    actorUserId: string;
    actorName: string;
    action: string;
    fieldName?: string;
    oldValue?: string;
    newValue?: string;
    createdAt: string;
}

export interface TaskTemplateDto {
    id: string;
    name: string;
    defaultAssigneeUserId?: string;
    defaultAssigneeName?: string;
    defaultPriority: string;
    defaultCategoryId?: string;
    description?: string;
    controlType: string;
    plannedEffortMinutes?: number;
    tags: string[];
    isPublic: boolean;
    createdByUserId: string;
    createdAt: string;
}

export interface TaskSavedFilterDto {
    id: string;
    name: string;
    filterJson: string;
    createdAt: string;
}

export interface TaskListParams {
    status?: string;
    priority?: string;
    assigneeId?: string;
    authorId?: string;
    categoryId?: string;
    tagValue?: string;
    dateFrom?: string;
    dateTo?: string;
    isOverdue?: boolean;
    search?: string;
    sortBy?: string;
    sortDir?: string;
}

async function apiFetch<T>(token: string, url: string, options?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
            ...(options?.headers ?? {}),
        },
    });
    if (!res.ok) {
        let msg = `HTTP ${res.status}`;
        try { const j = await res.json(); msg = j.error ?? j.title ?? msg; } catch { /* игнорируем */ }
        throw new Error(msg);
    }
    const text = await res.text();
    return text ? JSON.parse(text) as T : undefined as unknown as T;
}

export async function listTasks(token: string, params: TaskListParams = {}): Promise<TaskSummaryDto[]> {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.priority) q.set('priority', params.priority);
    if (params.assigneeId) q.set('assigneeId', params.assigneeId);
    if (params.authorId) q.set('authorId', params.authorId);
    if (params.categoryId) q.set('categoryId', params.categoryId);
    if (params.tagValue) q.set('tagValue', params.tagValue);
    if (params.dateFrom) q.set('dateFrom', params.dateFrom);
    if (params.dateTo) q.set('dateTo', params.dateTo);
    if (params.isOverdue !== undefined) q.set('isOverdue', String(params.isOverdue));
    if (params.search) q.set('search', params.search);
    if (params.sortBy) q.set('sortBy', params.sortBy);
    if (params.sortDir) q.set('sortDir', params.sortDir);
    return apiFetch<TaskSummaryDto[]>(token, `/api/tasks?${q}`);
}

export async function createTask(token: string, req: {
    subject: string;
    assigneeUserId: string;
    startDate: string;
    dueDate: string;
    description?: string;
    priority?: string;
    categoryId?: string;
    plannedEffortMinutes?: number;
    controlType?: string;
    controllerUserId?: string;
    parentTaskId?: string;
    approverId?: string;
    coExecutorIds?: string[];
    observerIds?: string[];
    tags?: string[];
    reminderAt?: string;
}): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, '/api/tasks', { method: 'POST', body: JSON.stringify(req) });
}

export async function getTask(token: string, id: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}`);
}

export async function updateTask(token: string, id: string, req: Record<string, unknown>): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}`, { method: 'PUT', body: JSON.stringify(req) });
}

export async function deleteTask(token: string, id: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${id}`, { method: 'DELETE' });
}

export async function copyTask(token: string, id: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/copy`, { method: 'POST' });
}

export async function markTaskRead(token: string, id: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${id}/read`, { method: 'POST' });
}

export async function reassignTask(token: string, id: string, assigneeUserId: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/assignee`, { method: 'PUT', body: JSON.stringify({ assigneeUserId, comment }) });
}

export async function createSubtask(token: string, parentId: string, req: Record<string, unknown>): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${parentId}/subtasks`, { method: 'POST', body: JSON.stringify(req) });
}

export async function getTaskComments(token: string, id: string): Promise<TaskCommentDto[]> {
    return apiFetch<TaskCommentDto[]>(token, `/api/tasks/${id}/comments`);
}

export async function addTaskComment(token: string, id: string, body: string): Promise<TaskCommentDto> {
    return apiFetch<TaskCommentDto>(token, `/api/tasks/${id}/comments`, { method: 'POST', body: JSON.stringify({ body }) });
}

export async function getTaskAttachments(token: string, id: string): Promise<TaskAttachmentDto[]> {
    return apiFetch<TaskAttachmentDto[]>(token, `/api/tasks/${id}/attachments`);
}

export async function addTaskAttachment(token: string, id: string, req: { fileName: string; contentType: string; storageKey: string; sizeBytes: number }): Promise<TaskAttachmentDto> {
    return apiFetch<TaskAttachmentDto>(token, `/api/tasks/${id}/attachments`, { method: 'POST', body: JSON.stringify(req) });
}

export async function getTaskParticipants(token: string, id: string): Promise<TaskParticipantDto[]> {
    return apiFetch<TaskParticipantDto[]>(token, `/api/tasks/${id}/participants`);
}

export async function addTaskParticipant(token: string, id: string, userId: string, role: string): Promise<TaskParticipantDto> {
    return apiFetch<TaskParticipantDto>(token, `/api/tasks/${id}/participants`, { method: 'POST', body: JSON.stringify({ userId, role }) });
}

export async function removeTaskParticipant(token: string, id: string, participantId: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${id}/participants/${participantId}`, { method: 'DELETE' });
}

export async function getTaskRelations(token: string, id: string): Promise<TaskRelationDto[]> {
    return apiFetch<TaskRelationDto[]>(token, `/api/tasks/${id}/relations`);
}

export async function addTaskRelation(token: string, id: string, targetTaskId: string, relationType: string): Promise<TaskRelationDto> {
    return apiFetch<TaskRelationDto>(token, `/api/tasks/${id}/relations`, { method: 'POST', body: JSON.stringify({ targetTaskId, relationType }) });
}

export async function removeTaskRelation(token: string, id: string, relationId: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${id}/relations/${relationId}`, { method: 'DELETE' });
}

export async function addTaskTag(token: string, id: string, value: string): Promise<{ id: string; value: string }> {
    return apiFetch<{ id: string; value: string }>(token, `/api/tasks/${id}/tags`, { method: 'POST', body: JSON.stringify({ value }) });
}

export async function removeTaskTag(token: string, id: string, tagId: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${id}/tags/${tagId}`, { method: 'DELETE' });
}

export async function getTaskHistory(token: string, id: string): Promise<TaskHistoryEntryDto[]> {
    return apiFetch<TaskHistoryEntryDto[]>(token, `/api/tasks/${id}/history`);
}

export async function exportTasksCsv(token: string, params: TaskListParams = {}): Promise<Blob> {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.priority) q.set('priority', params.priority);
    if (params.assigneeId) q.set('assigneeId', params.assigneeId);
    if (params.search) q.set('search', params.search);
    const res = await fetch(`/api/tasks/export?${q}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.blob();
}

export async function getTaskSavedFilters(token: string): Promise<TaskSavedFilterDto[]> {
    return apiFetch<TaskSavedFilterDto[]>(token, '/api/tasks/saved-filters');
}

export async function createTaskSavedFilter(token: string, name: string, filterJson: string): Promise<TaskSavedFilterDto> {
    return apiFetch<TaskSavedFilterDto>(token, '/api/tasks/saved-filters', { method: 'POST', body: JSON.stringify({ name, filterJson }) });
}

export async function deleteTaskSavedFilter(token: string, id: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/saved-filters/${id}`, { method: 'DELETE' });
}

export async function listTaskTemplates(token: string): Promise<TaskTemplateDto[]> {
    return apiFetch<TaskTemplateDto[]>(token, '/api/task-templates');
}

export async function createTaskTemplate(token: string, req: Record<string, unknown>): Promise<TaskTemplateDto> {
    return apiFetch<TaskTemplateDto>(token, '/api/task-templates', { method: 'POST', body: JSON.stringify(req) });
}

export async function updateTaskTemplate(token: string, id: string, req: Record<string, unknown>): Promise<TaskTemplateDto> {
    return apiFetch<TaskTemplateDto>(token, `/api/task-templates/${id}`, { method: 'PUT', body: JSON.stringify(req) });
}

export async function deleteTaskTemplate(token: string, id: string): Promise<void> {
    await apiFetch<void>(token, `/api/task-templates/${id}`, { method: 'DELETE' });
}

// ─── FR-TASK-01.3: Согласование ──────────────────────────────────────────────

export interface TaskApprovalStateDto {
    approverUserId?: string;
    approverName?: string;
    status: string;
    lastDecisionComment?: string;
    lastDecisionAt?: string;
}

export async function getAllowedActions(token: string, id: string): Promise<{ action: string; label: string }[]> {
    return apiFetch<{ action: string; label: string }[]>(token, `/api/tasks/${id}/actions`);
}

export async function approvePreTask(token: string, id: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/actions/approve-pre`, { method: 'POST', body: JSON.stringify({ comment }) });
}

export async function rejectPreTask(token: string, id: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/actions/reject-pre`, { method: 'POST', body: JSON.stringify({ comment }) });
}

export async function sendTaskForApproval(token: string, id: string, approverId?: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/actions/send-for-approval`, { method: 'POST', body: JSON.stringify({ approverId, comment }) });
}

export async function approveTask(token: string, id: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/actions/approve`, { method: 'POST', body: JSON.stringify({ comment }) });
}

export async function rejectTask(token: string, id: string, comment?: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/actions/reject`, { method: 'POST', body: JSON.stringify({ comment }) });
}

export async function getTaskApprovalState(token: string, id: string): Promise<TaskApprovalStateDto> {
    return apiFetch<TaskApprovalStateDto>(token, `/api/tasks/${id}/approval`);
}

// ─── FR-TASK-01.4: Контроль и трудозатраты ───────────────────────────────────

export interface TaskTimeLogDto {
    id: string;
    taskId: string;
    userId: string;
    userName: string;
    activityTypeId?: string;
    activityTypeName?: string;
    durationMinutes: number;
    startDate: string;
    comment?: string;
    createdAt: string;
}

export interface TaskActivityTypeDto {
    id: string;
    name: string;
    isActive: boolean;
    createdAt: string;
}

/** Изменить контролёра и/или тип контроля задачи. */
export async function updateTaskControl(
    token: string,
    id: string,
    req: { controllerUserId?: string | null; controlType?: string },
): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${id}/control`, { method: 'PUT', body: JSON.stringify(req) });
}

/** Добавить трудозатраты к задаче. */
export async function addTaskTimeLog(
    token: string,
    id: string,
    req: { durationMinutes: number; startDate: string; activityTypeId?: string; comment?: string },
): Promise<TaskTimeLogDto> {
    return apiFetch<TaskTimeLogDto>(token, `/api/tasks/${id}/timelogs`, { method: 'POST', body: JSON.stringify(req) });
}

/** Получить журнал трудозатрат задачи. */
export async function getTaskTimeLogs(token: string, id: string): Promise<TaskTimeLogDto[]> {
    return apiFetch<TaskTimeLogDto[]>(token, `/api/tasks/${id}/timelogs`);
}

/** Удалить запись трудозатрат задачи. */
export async function deleteTaskTimeLog(token: string, taskId: string, logId: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${taskId}/timelogs/${logId}`, { method: 'DELETE' });
}

/** Взять задачу на текущий контроль. */
export async function takeControl(token: string, taskId: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${taskId}/actions/take-control`, { method: 'POST' });
}

/** Снять задачу с контроля. */
export async function releaseControl(token: string, taskId: string): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, `/api/tasks/${taskId}/actions/release-control`, { method: 'POST' });
}

/** Получить справочник видов деятельности (Admin). */
export async function listActivityTypes(token: string): Promise<TaskActivityTypeDto[]> {
    return apiFetch<TaskActivityTypeDto[]>(token, '/api/admin/activity-types');
}

/** Создать вид деятельности (Admin). */
export async function createActivityType(token: string, name: string, isActive = true): Promise<TaskActivityTypeDto> {
    return apiFetch<TaskActivityTypeDto>(token, '/api/admin/activity-types', { method: 'POST', body: JSON.stringify({ name, isActive }) });
}

/** Обновить вид деятельности (Admin). */
export async function updateActivityType(token: string, id: string, name: string, isActive: boolean): Promise<TaskActivityTypeDto> {
    return apiFetch<TaskActivityTypeDto>(token, `/api/admin/activity-types/${id}`, { method: 'PUT', body: JSON.stringify({ name, isActive }) });
}

/** Удалить вид деятельности (Admin). */
export async function deleteActivityType(token: string, id: string): Promise<void> {
    await apiFetch<void>(token, `/api/admin/activity-types/${id}`, { method: 'DELETE' });
}

// ─── TaskControlSettings API (Admin) ──────────────────────────────────────

export interface TaskControlSettingsDto {
    defaultControlType: string;
    isEffortRequired: boolean;
    isActivityTypeRequired: boolean;
    updatedAt: string;
}

/** Получить системные настройки контроля и трудозатрат (Admin). */
export async function getTaskControlSettings(token: string): Promise<TaskControlSettingsDto> {
    return apiFetch<TaskControlSettingsDto>(token, '/api/admin/task-control-settings');
}

/** Обновить системные настройки контроля и трудозатрат (Admin). */
export async function updateTaskControlSettings(
    token: string,
    req: { defaultControlType: string; isEffortRequired: boolean; isActivityTypeRequired: boolean },
): Promise<TaskControlSettingsDto> {
    return apiFetch<TaskControlSettingsDto>(token, '/api/admin/task-control-settings', {
        method: 'PUT',
        body: JSON.stringify(req),
    });
}

// ─── FR-TASK-01.5: Типы задач ─────────────────────────────────────────────────

export interface ProcessTaskInfoDto {
    instanceId: string;
    instanceTitle: string;
    processName: string;
    processVersionNumber: string;
    launchedAt: string;
    initiatorUserId: string;
    initiatorName: string;
    ownerUserId?: string;
    ownerName?: string;
}

export interface TaskRecurrenceDto {
    id: string;
    rootTaskId: string;
    periodicity: string;
    endCondition: string;
    endDate?: string;
    lookAheadCount: number;
    durationMinutes: number;
    isActive: boolean;
}

export interface PeriodicSeriesItemDto {
    id: string;
    number: number;
    subject: string;
    status: string;
    startDate: string;
    dueDate: string;
    isOverdue: boolean;
}

export interface CreatePeriodicTaskRequest {
    subject: string;
    description?: string;
    priority?: string;
    categoryId?: string;
    assigneeUserId: string;
    startDate: string;
    dueDate: string;
    plannedEffortMinutes?: number;
    controlType?: string;
    controllerUserId?: string;
    tags?: string[];
    periodicity: string;
    endCondition: string;
    endDate?: string;
    lookAheadCount: number;
    durationMinutes: number;
}

export interface CreateResolutionTaskRequest {
    subject: string;
    description?: string;
    assigneeUserId: string;
    startDate: string;
    dueDate: string;
    documentId: string;
}

/** Создать периодическую задачу (FR-TASK-01.5.1). */
export async function createPeriodicTask(token: string, req: CreatePeriodicTaskRequest): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, '/api/tasks/periodic', { method: 'POST', body: JSON.stringify(req) });
}

/** Получить экземпляры серии периодической задачи (FR-TASK-01.5.1). */
export async function getSeriesItems(token: string, rootTaskId: string, activeOnly = false): Promise<PeriodicSeriesItemDto[]> {
    return apiFetch<PeriodicSeriesItemDto[]>(token, `/api/tasks/${rootTaskId}/series?activeOnly=${activeOnly}`);
}

/** Обновить конфигурацию серии (FR-TASK-01.5.1). */
export async function updateSeries(token: string, rootTaskId: string, req: Partial<{
    periodicity: string; endCondition: string; endDate?: string; lookAheadCount: number; durationMinutes: number;
}>): Promise<TaskRecurrenceDto> {
    return apiFetch<TaskRecurrenceDto>(token, `/api/tasks/${rootTaskId}/series`, { method: 'PUT', body: JSON.stringify(req) });
}

/** Остановить серию периодических задач (FR-TASK-01.5.1). */
export async function stopSeries(token: string, rootTaskId: string): Promise<void> {
    await apiFetch<void>(token, `/api/tasks/${rootTaskId}/series`, { method: 'DELETE' });
}

/** Создать задачу по резолюции документа (FR-TASK-01.5.3). */
export async function createResolutionTask(token: string, req: CreateResolutionTaskRequest): Promise<TaskDto> {
    return apiFetch<TaskDto>(token, '/api/tasks/resolution', { method: 'POST', body: JSON.stringify(req) });
}

/** Получить задачи-резолюции по документу (FR-TASK-01.5.3). */
export async function getDocumentResolutions(token: string, documentId: string): Promise<TaskDto[]> {
    return apiFetch<TaskDto[]>(token, `/api/documents/${documentId}/resolutions`);
}

/** Получить детали процесса для задачи по процессу (FR-TASK-01.5.2). */
export async function getProcessTaskInfo(token: string, taskId: string): Promise<ProcessTaskInfoDto | null> {
    try {
        return await apiFetch<ProcessTaskInfoDto>(token, `/api/tasks/${taskId}/process-info`);
    } catch {
        return null;
    }
}

/** Скачать все вложения задачи архивом ZIP (FR-TASK-01.5.2, FR-TASK-01.5.3). */
export function getDownloadAttachmentsUrl(taskId: string): string {
    return `/api/tasks/${taskId}/attachments/download`;
}
