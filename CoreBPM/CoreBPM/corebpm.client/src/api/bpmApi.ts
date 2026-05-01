// API-клиент для управления бизнес-процессами (/api/bpm/*)

export type BpmProcessVersionStatus = 'Draft' | 'Active' | 'Obsolete';

export type BpmVariableType = 'String' | 'Int' | 'Decimal' | 'Bool' | 'Date' | 'DateTime' | 'Json' | 'File' | 'User' | 'List';

export type BpmRaciType = 'R' | 'A' | 'C' | 'I';

// ─── Статусы экземпляров процесса ────────────────────────────────────────────

export type BpmInterruptAction = 'KeepCurrent' | 'Reset' | 'MoveToNext' | 'RunScript';

export interface InstanceStatusOptionDto {
    id: string;
    name: string;
    code: string;
    sortOrder: number;
}

export interface InstanceStatusConfigDto {
    linkedVariableId?: string;
    linkedVariableName?: string;
    onInterruptAction: BpmInterruptAction;
    onInterruptScriptId?: string;
    options: InstanceStatusOptionDto[];
}

export interface UpdateStatusConfigRequest {
    linkedVariableId?: string;
    onInterruptAction: BpmInterruptAction;
    onInterruptScriptId?: string;
    createVariable: boolean;
    newVariableName?: string;
}

export interface CreateStatusOptionRequest {
    name: string;
    code?: string;
}

export interface UpdateStatusOptionRequest {
    name: string;
    code: string;
}

export interface ReorderStatusOptionsRequest {
    orderedIds: string[];
}

export interface BpmProcessListItemDto {
    id: string;
    organizationId: string;
    name: string;
    description?: string;
    activeVersionNumber?: number;
    totalVersions: number;
    createdAt: string;
    updatedAt: string;
    tags: string[];
    isTemplate: boolean;
}

export interface BpmProcessDto {
    id: string;
    organizationId: string;
    name: string;
    description?: string;
    createdByUserId: string;
    activeVersionNumber?: number;
    totalVersions: number;
    createdAt: string;
    updatedAt: string;
    tags: string[];
    isTemplate: boolean;
}

export interface BpmProcessVersionInfoDto {
    id: string;
    versionNumber: number;
    status: BpmProcessVersionStatus;
    createdByUserId: string;
    createdAt: string;
    updatedAt: string;
    publishedAt?: string;
    releaseNotes?: string;
}

export interface BpmDiagramDto {
    versionId: string;
    versionNumber: number;
    status: BpmProcessVersionStatus;
    diagramXml?: string;
    updatedAt: string;
    publishedAt?: string;
    releaseNotes?: string;
}

export type BpmInstanceNameMode = 'Manual' | 'KeyVariable' | 'Template';

export interface BpmValidationIssueDto {
    severity: 'Error' | 'Warning';
    code: string;
    message: string;
    elementId?: string;
}

export interface BpmValidationResultDto {
    versionId: string;
    versionNumber: number;
    issues: BpmValidationIssueDto[];
}

export interface BpmVersionDiffElementDto {
    changeType: 'Added' | 'Removed' | 'Changed';
    elementId: string;
    elementType: string;
    name?: string;
}

export interface BpmVersionDiffPropertyDto {
    targetType: string;
    targetId: string;
    propertyName: string;
    leftValue?: string;
    rightValue?: string;
}

export interface BpmVersionDiffDto {
    leftVersionId: string;
    rightVersionId: string;
    elements: BpmVersionDiffElementDto[];
    properties: BpmVersionDiffPropertyDto[];
}

export interface BpmProcessSettingsDto {
    processId: string;
    launchFromPortalEnabled: boolean;
    showInStartList: boolean;
    externalStartEnabled: boolean;
    externalStartMethods: string[];
    externalStartAllowedIps?: string;
    hasExternalStartToken: boolean;
    externalStartTokenPreview?: string;
    externalStartTokenUpdatedAt?: string;
    instanceNameMode: BpmInstanceNameMode;
    requestInstanceNameOnStart: boolean;
    instanceNameTemplate?: string;
    keyVariableName?: string;
    dataClassName: string;
    dataTableName: string;
    processMetricsClassName: string;
    processMetricsTableName: string;
    instanceMetricsClassName: string;
    instanceMetricsTableName: string;
    secondRuntimeEnabled: boolean;
    secondRuntimeUpgradedAt?: string;
    // KPI-цели
    targetCycleTimeMinutes?: number;
    targetOnTimePercent?: number;
    targetCostPerInstance?: number;
}

export interface UpdateBpmProcessSettingsRequest {
    launchFromPortalEnabled: boolean;
    showInStartList: boolean;
    externalStartEnabled: boolean;
    externalStartMethods: string[];
    externalStartAllowedIps?: string;
    instanceNameMode: BpmInstanceNameMode;
    requestInstanceNameOnStart: boolean;
    instanceNameTemplate?: string;
    keyVariableName?: string;
    dataClassName?: string;
    dataTableName?: string;
    processMetricsClassName?: string;
    processMetricsTableName?: string;
    instanceMetricsClassName?: string;
    instanceMetricsTableName?: string;
    secondRuntimeEnabled: boolean;
    // KPI-цели
    targetCycleTimeMinutes?: number;
    targetOnTimePercent?: number;
    targetCostPerInstance?: number;
}

export interface RotateExternalTokenResponse {
    token: string;
    preview: string;
    rotatedAt: string;
}

export interface StartBpmDebugSessionRequest {
    versionId?: string;
    variables?: Record<string, string>;
}

export interface BpmDebugEventDto {
    timestamp: string;
    eventType: string;
    elementId?: string;
    message: string;
}

export interface BpmDebugSessionDto {
    sessionId: string;
    processId: string;
    versionId: string;
    versionNumber: number;
    isCompleted: boolean;
    currentElementId?: string;
    currentElementType?: string;
    variables: Record<string, string>;
    events: BpmDebugEventDto[];
}

// ─── Конфигурации элементов ──────────────────────────────────────────────────

export interface BpmElementConfigDto {
    elementId: string;
    configJson: string;
    updatedAt: string;
}

// ─── Переменные процесса ─────────────────────────────────────────────────────

export interface BpmProcessVariableDto {
    id: string;
    name: string;
    variableType: BpmVariableType;
    defaultValue?: string;
    isKeyVariable: boolean;
    isInput: boolean;
    isOutput: boolean;
    sortOrder: number;
}

export interface CreateBpmVariableRequest {
    name: string;
    variableType: BpmVariableType;
    defaultValue?: string;
    isKeyVariable: boolean;
    isInput: boolean;
    isOutput: boolean;
}

export interface UpdateBpmVariableRequest {
    name: string;
    variableType: BpmVariableType;
    defaultValue?: string;
    isKeyVariable: boolean;
    isInput: boolean;
    isOutput: boolean;
}

// ─── RACI-матрица ─────────────────────────────────────────────────────────────

export interface BpmRaciEntryDto {
    id: string;
    stage: string;
    role: string;
    raciType: BpmRaciType;
}

export interface UpsertRaciEntryRequest {
    stage: string;
    role: string;
    raciType: BpmRaciType;
}

// ─── Fetch helper ─────────────────────────────────────────────────────────────

async function fetchJson<T>(url: string, token: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...init,
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
            ...init?.headers,
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        let message = text || `HTTP ${res.status}`;
        try {
            const body = JSON.parse(text);
            if (body?.error) message = body.error;
        } catch { /* тело не является JSON */ }
        throw new Error(message);
    }
    if (res.status === 204) return undefined as unknown as T;
    return res.json() as Promise<T>;
}

// ─── Процессы ─────────────────────────────────────────────────────────────────

/** Список процессов организации. */
export const getProcesses = (token: string, organizationId: string): Promise<BpmProcessListItemDto[]> =>
    fetchJson(`/api/bpm/processes?organizationId=${organizationId}`, token);

/** Получить процесс по ID. */
export const getProcess = (token: string, processId: string): Promise<BpmProcessDto> =>
    fetchJson(`/api/bpm/processes/${processId}`, token);

/** Создать новый процесс. */
export const createProcess = (
    token: string,
    data: { organizationId: string; name: string; description?: string; tags?: string[]; isTemplate?: boolean }
): Promise<BpmProcessDto> =>
    fetchJson('/api/bpm/processes', token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить метаданные процесса. */
export const updateProcess = (
    token: string,
    processId: string,
    data: { name: string; description?: string; tags?: string[]; isTemplate?: boolean }
): Promise<BpmProcessDto> =>
    fetchJson(`/api/bpm/processes/${processId}`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

/** Удалить процесс. */
export const deleteProcess = (token: string, processId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}`, token, { method: 'DELETE' });

/** Список версий процесса. */
export const getProcessVersions = (token: string, processId: string): Promise<BpmProcessVersionInfoDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/versions`, token);

/** Получить конкретную версию процесса. */
export const getProcessVersion = (token: string, processId: string, versionId: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}`, token);

/** Получить текущую диаграмму процесса. */
export const getDiagram = (token: string, processId: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram`, token);

/** Сохранить XML-диаграмму. */
export const saveDiagram = (token: string, processId: string, diagramXml: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram`, token, {
        method: 'PUT',
        body: JSON.stringify({ diagramXml }),
    });

/** Опубликовать версию процесса. */
export const publishProcessVersion = (token: string, processId: string, versionId: string, releaseNotes?: string): Promise<BpmProcessVersionInfoDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/publish`, token, {
        method: 'POST',
        body: JSON.stringify({ releaseNotes }),
    });

/** Получить список шаблонов процессов. */
export const getProcessTemplates = (token: string, organizationId: string): Promise<BpmProcessListItemDto[]> =>
    fetchJson(`/api/bpm/processes/templates?organizationId=${organizationId}`, token);

/** Создать процесс из шаблона. */
export const createProcessFromTemplate = (
    token: string,
    templateId: string,
    data: { organizationId: string; name: string; description?: string }
): Promise<BpmProcessDto> =>
    fetchJson(`/api/bpm/processes/${templateId}/from-template`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Создать новый черновик откатом к выбранной версии. */
export const rollbackProcessVersion = (token: string, processId: string, versionId: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/rollback`, token, { method: 'POST' });

/** Провалидировать процесс. */
export const validateProcess = (token: string, processId: string, versionId?: string): Promise<BpmValidationResultDto> =>
    fetchJson(`/api/bpm/processes/${processId}/validate`, token, {
        method: 'POST',
        body: JSON.stringify({ versionId }),
    });

/** Сравнить две версии процесса. */
export const diffProcessVersions = (token: string, processId: string, leftVersionId: string, rightVersionId: string): Promise<BpmVersionDiffDto> =>
    fetchJson(`/api/bpm/processes/${processId}/diff`, token, {
        method: 'POST',
        body: JSON.stringify({ leftVersionId, rightVersionId }),
    });

/** Получить настройки процесса. */
export const getProcessSettings = (token: string, processId: string): Promise<BpmProcessSettingsDto> =>
    fetchJson(`/api/bpm/processes/${processId}/settings`, token);

/** Обновить настройки процесса. */
export const updateProcessSettings = (token: string, processId: string, data: UpdateBpmProcessSettingsRequest): Promise<BpmProcessSettingsDto> =>
    fetchJson(`/api/bpm/processes/${processId}/settings`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

/** Ротировать токен внешнего запуска. */
export const rotateExternalToken = (token: string, processId: string): Promise<RotateExternalTokenResponse> =>
    fetchJson(`/api/bpm/processes/${processId}/settings/external-token:rotate`, token, { method: 'POST' });

/** Запустить debug-сессию процесса. */
export const startDebugSession = (token: string, processId: string, data: StartBpmDebugSessionRequest): Promise<BpmDebugSessionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/debug`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Получить debug-сессию процесса. */
export const getDebugSession = (token: string, processId: string, sessionId: string): Promise<BpmDebugSessionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/debug/${sessionId}`, token);

/** Выполнить шаг debug-сессии. */
export const stepDebugSession = (token: string, processId: string, sessionId: string): Promise<BpmDebugSessionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/debug/${sessionId}/step`, token, { method: 'POST' });

/** Завершить текущую задачу в debug-сессии. */
export const completeDebugTask = (token: string, processId: string, sessionId: string): Promise<BpmDebugSessionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/debug/${sessionId}/complete`, token, { method: 'POST' });

/** Пропустить текущую задачу в debug-сессии. */
export const skipDebugTask = (token: string, processId: string, sessionId: string): Promise<BpmDebugSessionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/debug/${sessionId}/skip`, token, { method: 'POST' });

/** Скачать PDF-регламент процесса. */
export async function downloadProcessDocument(token: string, processId: string): Promise<Blob> {
    const res = await fetch(`/api/bpm/processes/${processId}/document`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || `HTTP ${res.status}`);
    }
    return res.blob();
}

// ─── Конфигурации элементов ──────────────────────────────────────────────────

/** Получить все конфигурации элементов процесса. */
export const getElementConfigs = (token: string, processId: string): Promise<BpmElementConfigDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/element-configs`, token);

/** Получить конфигурацию конкретного элемента. */
export const getElementConfig = (token: string, processId: string, elementId: string): Promise<BpmElementConfigDto | null> =>
    fetchJson<BpmElementConfigDto>(`/api/bpm/processes/${processId}/element-configs/${encodeURIComponent(elementId)}`, token)
        .catch(e => { if (e.message?.includes('404') || e.message?.startsWith('HTTP 404')) return null; throw e; });

/** Сохранить (создать/обновить) конфигурацию элемента. */
export const upsertElementConfig = (token: string, processId: string, elementId: string, configJson: string): Promise<BpmElementConfigDto> =>
    fetchJson(`/api/bpm/processes/${processId}/element-configs/${encodeURIComponent(elementId)}`, token, {
        method: 'PUT',
        body: JSON.stringify({ configJson }),
    });

/** Удалить конфигурацию элемента. */
export const deleteElementConfig = (token: string, processId: string, elementId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/element-configs/${encodeURIComponent(elementId)}`, token, { method: 'DELETE' });

// ─── Переменные процесса ─────────────────────────────────────────────────────

/** Список переменных процесса. */
export const getVariables = (token: string, processId: string): Promise<BpmProcessVariableDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/variables`, token);

/** Создать переменную. */
export const createVariable = (token: string, processId: string, data: CreateBpmVariableRequest): Promise<BpmProcessVariableDto> =>
    fetchJson(`/api/bpm/processes/${processId}/variables`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить переменную. */
export const updateVariable = (token: string, processId: string, variableId: string, data: UpdateBpmVariableRequest): Promise<BpmProcessVariableDto> =>
    fetchJson(`/api/bpm/processes/${processId}/variables/${variableId}`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

/** Удалить переменную. */
export const deleteVariable = (token: string, processId: string, variableId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/variables/${variableId}`, token, { method: 'DELETE' });

/** Изменить порядок переменных. */
export const reorderVariables = (token: string, processId: string, orderedIds: string[]): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/variables/reorder`, token, {
        method: 'PUT',
        body: JSON.stringify({ orderedIds }),
    });

// ─── RACI-матрица ─────────────────────────────────────────────────────────────

/** Получить RACI-матрицу процесса. */
export const getRaci = (token: string, processId: string): Promise<BpmRaciEntryDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/raci`, token);

/** Заменить RACI-матрицу целиком. */
export const replaceRaci = (token: string, processId: string, entries: UpsertRaciEntryRequest[]): Promise<BpmRaciEntryDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/raci`, token, {
        method: 'PUT',
        body: JSON.stringify(entries),
    });

// ─── Пользовательские статусы экземпляров ────────────────────────────────────

/** Получить конфигурацию статусов процесса. */
export const getStatusConfig = (token: string, processId: string): Promise<InstanceStatusConfigDto> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config`, token);

/** Обновить конфигурацию статусов. */
export const updateStatusConfig = (token: string, processId: string, data: UpdateStatusConfigRequest): Promise<InstanceStatusConfigDto> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

/** Создать вариант статуса. */
export const createStatusOption = (token: string, processId: string, data: CreateStatusOptionRequest): Promise<InstanceStatusOptionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config/options`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить вариант статуса. */
export const updateStatusOption = (token: string, processId: string, optionId: string, data: UpdateStatusOptionRequest): Promise<InstanceStatusOptionDto> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config/options/${optionId}`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

/** Удалить вариант статуса. */
export const deleteStatusOption = (token: string, processId: string, optionId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config/options/${optionId}`, token, { method: 'DELETE' });

/** Изменить порядок вариантов статусов. */
export const reorderStatusOptions = (token: string, processId: string, orderedIds: string[]): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/status-config/options/reorder`, token, {
        method: 'PUT',
        body: JSON.stringify({ orderedIds }),
    });

// ─── Блокировки диаграмм ─────────────────────────────────────────────────────

export interface DiagramLockDto {
    processId: string;
    lockedByUserId: string;
    lockedByDisplayName: string;
    lockedAt: string;
    lockedUntil: string;
}

export interface AcquireLockResponse {
    isAcquired: boolean;
    lock?: DiagramLockDto;
}

/** Получить информацию об активной блокировке диаграммы (null если не заблокирована). */
export const getDiagramLock = async (token: string, processId: string): Promise<DiagramLockDto | null> => {
    const res = await fetch(`/api/bpm/processes/${processId}/diagram/lock`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (res.status === 204) return null;
    if (!res.ok) return null;
    return res.json() as Promise<DiagramLockDto>;
};

/** Захватить (или продлить) блокировку диаграммы. */
export const acquireDiagramLock = (token: string, processId: string): Promise<AcquireLockResponse> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram/lock`, token, { method: 'PUT' });

/** Снять блокировку диаграммы (идемпотентно). */
export const releaseDiagramLock = (token: string, processId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram/lock`, token, { method: 'DELETE' });

// ─── Реестр сигналов BPMN ────────────────────────────────────────────────────

export interface BpmSignalDto {
    id: string;
    name: string;
    code: string;
    description?: string;
    createdAt: string;
    updatedAt: string;
}

export interface CreateSignalRequest { name: string; code?: string; description?: string; }
export interface UpdateSignalRequest { name: string; code: string; description?: string; }

/** Список сигналов организации. */
export const getSignals = (token: string, organizationId?: string): Promise<BpmSignalDto[]> =>
    fetchJson(`/api/bpm/signals${organizationId ? `?organizationId=${organizationId}` : ''}`, token);

/** Создать сигнал. */
export const createSignal = (token: string, data: CreateSignalRequest, organizationId?: string): Promise<BpmSignalDto> =>
    fetchJson(`/api/bpm/signals${organizationId ? `?organizationId=${organizationId}` : ''}`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить сигнал. */
export const updateSignal = (token: string, id: string, data: UpdateSignalRequest): Promise<BpmSignalDto> =>
    fetchJson(`/api/bpm/signals/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить сигнал. */
export const deleteSignal = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/signals/${id}`, token, { method: 'DELETE' });

// ─── Реестр сообщений BPMN ───────────────────────────────────────────────────

export interface BpmMessageDto {
    id: string;
    name: string;
    code: string;
    description?: string;
    createdAt: string;
    updatedAt: string;
}

export interface CreateMessageRequest { name: string; code?: string; description?: string; }
export interface UpdateMessageRequest { name: string; code: string; description?: string; }

/** Список сообщений организации. */
export const getMessages = (token: string, organizationId?: string): Promise<BpmMessageDto[]> =>
    fetchJson(`/api/bpm/messages${organizationId ? `?organizationId=${organizationId}` : ''}`, token);

/** Создать сообщение. */
export const createMessage = (token: string, data: CreateMessageRequest, organizationId?: string): Promise<BpmMessageDto> =>
    fetchJson(`/api/bpm/messages${organizationId ? `?organizationId=${organizationId}` : ''}`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить сообщение. */
export const updateMessage = (token: string, id: string, data: UpdateMessageRequest): Promise<BpmMessageDto> =>
    fetchJson(`/api/bpm/messages/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить сообщение. */
export const deleteMessage = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/messages/${id}`, token, { method: 'DELETE' });

// ─── Роли процесса (Владелец/Куратор) ────────────────────────────────────────

export type BpmProcessRoleType = 'Owner' | 'Curator';
export type BpmAssigneeType = 'User' | 'Position' | 'Department';

export interface BpmProcessRoleConfigDto {
    id: string;
    roleType: BpmProcessRoleType;
    assigneeType: BpmAssigneeType;
    assigneeId: string;
    displayName: string;
    sortOrder: number;
}

export interface UpsertProcessRoleConfigItem {
    roleType: BpmProcessRoleType;
    assigneeType: BpmAssigneeType;
    assigneeId: string;
    displayName: string;
    sortOrder: number;
}

export interface UpsertProcessRoleConfigsRequest {
    items: UpsertProcessRoleConfigItem[];
}

/** Получить список ролей (Владелец/Кураторы) процесса. */
export const getProcessRoles = (token: string, processId: string): Promise<BpmProcessRoleConfigDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/roles`, token);

/** Полностью заменить роли процесса. */
export const replaceProcessRoles = (
    token: string,
    processId: string,
    data: UpsertProcessRoleConfigsRequest
): Promise<BpmProcessRoleConfigDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/roles`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });


// ─── Экземпляры процессов ─────────────────────────────────────────────────────

export type BpmInstanceState = 'Active' | 'Completed' | 'Cancelled' | 'Suspended' | 'Faulted';
export type BpmInstanceLaunchSource = 'Manual' | 'Webhook' | 'Scheduler' | 'Message' | 'Signal' | 'CallActivity' | 'Batch';

export interface BpmInstanceVariableDto {
    id: string;
    name: string;
    valueJson?: string;
}

export interface BpmInstanceListItemDto {
    id: string;
    processId: string;
    processName: string;
    processVersionId: string;
    processVersionNumber: number;
    name: string;
    state: BpmInstanceState;
    launchSource: BpmInstanceLaunchSource;
    initiatorUserId?: string;
    initiatorDisplayName?: string;
    responsibleUserId?: string;
    responsibleDisplayName?: string;
    startedAt: string;
    completedAt?: string;
    cancelledAt?: string;
}

export interface BpmInstanceDto extends BpmInstanceListItemDto {
    parentInstanceId?: string;
    externalReference?: string;
    cancelReason?: string;
    updatedAt: string;
    variables: BpmInstanceVariableDto[];
}

export interface CreateInstanceRequest {
    name?: string;
    variables?: Record<string, string | null>;
    externalReference?: string;
}

export interface BpmSchedulerJobDto {
    id: string;
    processId: string;
    processVersionId: string;
    elementId: string;
    timerType: string;
    timerValue: string;
    timeZone?: string;
    isActive: boolean;
    lastFiredAt?: string;
    nextFireAt?: string;
    createdAt: string;
    updatedAt: string;
}

/** Список экземпляров процесса. */
export const getInstances = (
    token: string,
    processId: string,
    page = 1,
    pageSize = 50
): Promise<BpmInstanceListItemDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/instances?page=${page}&pageSize=${pageSize}`, token);

/** Запустить новый экземпляр процесса. */
export const createInstance = (
    token: string,
    processId: string,
    data: CreateInstanceRequest
): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/processes/${processId}/instances`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Получить экземпляр по ID. */
export const getInstance = (token: string, instanceId: string): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}`, token);

/** Получить задания планировщика для процесса. */
export const getSchedulerJobs = (token: string, processId: string): Promise<BpmSchedulerJobDto[]> =>
    fetchJson(`/api/bpm/processes/${processId}/scheduler-jobs`, token);

// ─── Управление экземпляром (FR-BPM-02.2) ────────────────────────────────────

export type BpmHistoryEventType =
    | 'Started' | 'Cancelled' | 'Completed' | 'Suspended' | 'Resumed'
    | 'ResponsibleChanged' | 'CommentAdded' | 'QuestionAdded'
    | 'VariableUpdated' | 'ParticipantAdded' | 'ParticipantRemoved'
    | 'NodeExecuted' | 'NodeFailed';

export interface BpmInstanceHistoryEntryDto {
    id: string;
    eventType: BpmHistoryEventType;
    actorUserId?: string;
    actorDisplayName?: string;
    elementId?: string;
    elementName?: string;
    durationMs?: number;
    text?: string;
    metaJson?: string;
    occurredAt: string;
}

export interface BpmInstanceParticipantDto {
    id: string;
    userId: string;
    displayName?: string;
    addedByUserId?: string;
    addedByDisplayName?: string;
    addedAt: string;
}

export interface CancelInstanceRequest { reason: string; }
export interface ChangeResponsibleRequest { newResponsibleUserId: string; }
export interface UpdateInstanceVariableRequest { valueJson?: string | null; }
export interface AddCommentRequest { text: string; isQuestion?: boolean; }
export interface AddParticipantRequest { userId: string; }

/** Прервать экземпляр. */
export const cancelInstance = (token: string, instanceId: string, data: CancelInstanceRequest): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/cancel`, token, { method: 'POST', body: JSON.stringify(data) });

/** Приостановить экземпляр. */
export const suspendInstance = (token: string, instanceId: string): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/suspend`, token, { method: 'POST', body: '{}' });

/** Возобновить экземпляр. */
export const resumeInstance = (token: string, instanceId: string): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/resume`, token, { method: 'POST', body: '{}' });

/** Изменить ответственного. */
export const changeResponsible = (token: string, instanceId: string, data: ChangeResponsibleRequest): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/responsible`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Обновить переменную экземпляра. */
export const updateInstanceVariable = (token: string, instanceId: string, variableName: string, data: UpdateInstanceVariableRequest): Promise<BpmInstanceVariableDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/variables/${encodeURIComponent(variableName)}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Журнал истории экземпляра. */
export const getInstanceHistory = (token: string, instanceId: string): Promise<BpmInstanceHistoryEntryDto[]> =>
    fetchJson(`/api/bpm/instances/${instanceId}/history`, token);

/** Добавить комментарий / вопрос. */
export const addComment = (token: string, instanceId: string, data: AddCommentRequest): Promise<BpmInstanceHistoryEntryDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/comments`, token, { method: 'POST', body: JSON.stringify(data) });

/** Список участников экземпляра. */
export const getParticipants = (token: string, instanceId: string): Promise<BpmInstanceParticipantDto[]> =>
    fetchJson(`/api/bpm/instances/${instanceId}/participants`, token);

/** Добавить участника. */
export const addParticipant = (token: string, instanceId: string, data: AddParticipantRequest): Promise<BpmInstanceParticipantDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/participants`, token, { method: 'POST', body: JSON.stringify(data) });

/** Удалить участника. */
export const removeParticipant = (token: string, instanceId: string, participantUserId: string): Promise<void> =>
    fetchJson(`/api/bpm/instances/${instanceId}/participants/${participantUserId}`, token, { method: 'DELETE' });

// ─── «Мои процессы» (FR-BPM-02.3) ───────────────────────────────────────────

export type MyInstancesRole = 'All' | 'Initiator' | 'Responsible' | 'Participant';

export interface MyInstancesFilter {
    role?: MyInstancesRole;
    state?: BpmInstanceState | '';
    search?: string;
    processId?: string;
    dateFrom?: string;
    dateTo?: string;
}

export interface MyInstancesResult {
    items: BpmInstanceListItemDto[];
    total: number;
}

/** Получить «мои процессы» с фильтрацией и пагинацией. */
export const getMyInstances = (
    token: string,
    filter: MyInstancesFilter = {},
    page = 1,
    pageSize = 30
): Promise<MyInstancesResult> => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filter.role && filter.role !== 'All') params.append('role', filter.role);
    if (filter.state) params.append('state', filter.state);
    if (filter.search) params.append('search', filter.search);
    if (filter.processId) params.append('processId', filter.processId);
    if (filter.dateFrom) params.append('dateFrom', filter.dateFrom);
    if (filter.dateTo) params.append('dateTo', filter.dateTo);
    return fetchJson(`/api/bpm/instances/my?${params}`, token);
};

// ─── Сохранённые фильтры ─────────────────────────────────────────────────────

export interface BpmSavedFilterDto {
    id: string;
    name: string;
    filtersJson: string;
    createdAt: string;
}

export interface SaveFilterRequest {
    name: string;
    filtersJson: string;
}

/** Список сохранённых фильтров пользователя. */
export const getSavedFilters = (token: string): Promise<BpmSavedFilterDto[]> =>
    fetchJson('/api/bpm/saved-filters', token);

/** Создать сохранённый фильтр. */
export const createSavedFilter = (token: string, data: SaveFilterRequest): Promise<BpmSavedFilterDto> =>
    fetchJson('/api/bpm/saved-filters', token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить сохранённый фильтр. */
export const updateSavedFilter = (token: string, filterId: string, data: SaveFilterRequest): Promise<BpmSavedFilterDto> =>
    fetchJson(`/api/bpm/saved-filters/${filterId}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить сохранённый фильтр. */
export const deleteSavedFilter = (token: string, filterId: string): Promise<void> =>
    fetchJson(`/api/bpm/saved-filters/${filterId}`, token, { method: 'DELETE' });

// ─── Монитор процессов (FR-BPM-02.4) ─────────────────────────────────────────

export interface BpmProcessMonitorItemDto {
    processId: string;
    processName: string;
    processDescription?: string;
    activeVersionNumber?: number;
    publishedAt?: string;
    activeCount: number;
    suspendedCount: number;
    completedCount: number;
    cancelledCount: number;
    owners: string[];
    curators: string[];
}

export interface BpmProcessStatsDto {
    activeCount: number;
    suspendedCount: number;
    completedCount: number;
    cancelledCount: number;
    totalCount: number;
    processName: string;
    processDescription?: string;
    activeVersionNumber?: number;
    publishedAt?: string;
    createdAt: string;
    owners: string[];
    curators: string[];
}

/** «Мой монитор» — процессы, где пользователь Владелец/Куратор, со статистикой. */
export const getMyMonitorProcesses = (token: string): Promise<BpmProcessMonitorItemDto[]> =>
    fetchJson('/api/bpm/monitor/my', token);

/** «Полный монитор» — все процессы системы со статистикой (Admin). */
export const getFullMonitorProcesses = (token: string): Promise<BpmProcessMonitorItemDto[]> =>
    fetchJson('/api/bpm/monitor/full', token);

/** Детальная статистика для страницы монитора конкретного процесса. */
export const getProcessStats = (token: string, processId: string): Promise<BpmProcessStatsDto> =>
    fetchJson(`/api/bpm/processes/${processId}/stats`, token);

// ─── Очередь исполнения (FR-BPM-02.5) ────────────────────────────────────────

export type BpmJobStatus = 'Pending' | 'Running' | 'Scheduled' | 'Completed' | 'Failed';

export interface BpmExecutionJobDto {
    id: string;
    processId: string;
    processName: string;
    instanceId?: string;
    instanceName?: string;
    elementId: string;
    elementType: string;
    operationName?: string;
    status: BpmJobStatus;
    attemptNumber: number;
    maxAttempts: number;
    nextRunAt?: string;
    startedAt?: string;
    completedAt?: string;
    failedAt?: string;
    lastError?: string;
    serverHost?: string;
    isTimer: boolean;
    timerDeadline?: string;
    createdAt: string;
}

export interface QueueStatsDto {
    pending: number;
    running: number;
    scheduled: number;
    failed: number;
    total: number;
}

export interface NodeAnalyticsDto {
    elementId: string;
    elementName?: string;
    executionCount: number;
    avgDurationMs: number;
    p50DurationMs: number;
    p95DurationMs: number;
    errorCount: number;
}

export interface GetQueueParams {
    status?: BpmJobStatus;
    instanceName?: string;
    processId?: string;
    includeScheduled?: boolean;
    page?: number;
    pageSize?: number;
}

/** Получить список заданий в очереди исполнения. */
export const getQueue = (token: string, params: GetQueueParams = {}): Promise<BpmExecutionJobDto[]> => {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.instanceName) q.set('instanceName', params.instanceName);
    if (params.processId) q.set('processId', params.processId);
    if (params.includeScheduled) q.set('includeScheduled', 'true');
    if (params.page) q.set('page', String(params.page));
    if (params.pageSize) q.set('pageSize', String(params.pageSize));
    return fetchJson(`/api/admin/queue?${q}`, token);
};

/** Получить счётчики статусов очереди. */
export const getQueueStats = (token: string): Promise<QueueStatsDto> =>
    fetchJson('/api/admin/queue/stats', token);

/** Принудительно повторить задание. */
export const retryJob = (token: string, jobId: string): Promise<void> =>
    fetchJson(`/api/admin/queue/${jobId}/retry`, token, {
        method: 'POST',
    });

/** Отменить таймерное задание. */
export const cancelQueueTimer = (token: string, jobId: string): Promise<void> =>
    fetchJson(`/api/admin/queue/${jobId}/cancel`, token, { method: 'POST' });

/** Перенести время запуска таймера. */
export const rescheduleTimer = (token: string, jobId: string, newRunAt: string): Promise<void> =>
    fetchJson(`/api/admin/queue/${jobId}/reschedule`, token, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newRunAt }),
    });

/** Аналитика узлов процесса. */
export const getNodeAnalytics = (
    token: string,
    processId: string,
    from?: string,
    to?: string,
): Promise<NodeAnalyticsDto[]> => {
    const q = new URLSearchParams({ processId });
    if (from) q.set('from', from);
    if (to) q.set('to', to);
    return fetchJson(`/api/analytics/nodes?${q}`, token);
};

// ─── Документирование процессов (FR-BPM-02.6) ────────────────────────────────

export interface ProcessDocVersionDto {
    versionId: string;
    versionNumber: number;
    publishedAt?: string;
    publishedByUserId: string;
    releaseNotes?: string;
    hasSnapshot: boolean;
}

export interface ProcessDocumentationItemDto {
    processId: string;
    processName: string;
    processDescription?: string;
    isDeleted: boolean;
    tags: string[];
    publishedVersions: ProcessDocVersionDto[];
}

export interface DocSnapshotDto {
    snapshotId: string;
    processId: string;
    processName: string;
    processVersionId: string;
    versionNumber: number;
    generatedAt: string;
    htmlContent: string;
}

/** Документация «Мои процессы» (Владелец/Куратор). */
export const getMyDocumentation = (token: string): Promise<ProcessDocumentationItemDto[]> =>
    fetchJson('/api/bpm/documentation/my', token);

/** Полная документация (Admin). */
export const getAllDocumentation = (token: string, includeDeleted = false): Promise<ProcessDocumentationItemDto[]> =>
    fetchJson(`/api/bpm/documentation/all?includeDeleted=${includeDeleted}`, token);

/** HTML-снапшот документации версии процесса. */
export const getDocSnapshot = (token: string, processId: string, versionId: string): Promise<DocSnapshotDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/snapshot`, token);

/** Пересоздать снапшот (Admin). */
export const regenerateDocSnapshot = (token: string, processId: string, versionId: string): Promise<void> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/snapshot/regenerate`, token, {
        method: 'POST',
    });

// ─── Пакетный запуск (FR-BPM-02.1) ───────────────────────────────────────────

export interface BatchLaunchItem {
    name?: string;
    variables?: Record<string, string | null>;
}

export interface BatchLaunchRequest {
    items: BatchLaunchItem[];
}

export interface BatchLaunchItemResult {
    success: boolean;
    instanceId?: string;
    instanceName?: string;
    error?: string;
}

export interface BatchLaunchResult {
    total: number;
    created: number;
    failed: number;
    items: BatchLaunchItemResult[];
}

/** Пакетный запуск нескольких экземпляров одного процесса. */
export const batchCreateInstances = (
    token: string,
    processId: string,
    data: BatchLaunchRequest
): Promise<BatchLaunchResult> =>
    fetchJson(`/api/bpm/processes/${processId}/instances/batch`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

// ─── Прямое переключение версии (FR-BPM-02.2) ────────────────────────────────

export interface SwitchInstanceVersionRequest {
    targetVersionId: string;
}

/** Переключить работающий экземпляр на другую версию процесса. */
export const switchInstanceVersion = (
    token: string,
    instanceId: string,
    data: SwitchInstanceVersionRequest
): Promise<BpmInstanceDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/version`, token, {
        method: 'PUT',
        body: JSON.stringify(data),
    });

// ─── Экспорт в CSV (FR-BPM-02.3 / FR-BPM-02.4) ───────────────────────────────

/** Скачать результаты «Мои процессы» в CSV. Возвращает Blob. */
export const exportMyInstances = async (
    token: string,
    filter: MyInstancesFilter = {}
): Promise<Blob> => {
    const params = new URLSearchParams();
    if (filter.role && filter.role !== 'All') params.append('role', filter.role);
    if (filter.state) params.append('state', filter.state);
    if (filter.search) params.append('search', filter.search);
    if (filter.processId) params.append('processId', filter.processId);
    if (filter.dateFrom) params.append('dateFrom', filter.dateFrom);
    if (filter.dateTo) params.append('dateTo', filter.dateTo);
    const resp = await fetch(`/api/bpm/instances/my/export?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!resp.ok) throw new Error(`Ошибка экспорта: ${resp.status}`);
    return resp.blob();
};

/** Скачать список экземпляров процесса в CSV. Возвращает Blob. */
export const exportProcessInstances = async (
    token: string,
    processId: string
): Promise<Blob> => {
    const resp = await fetch(`/api/bpm/processes/${processId}/instances/export`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!resp.ok) throw new Error(`Ошибка экспорта: ${resp.status}`);
    return resp.blob();
};

// ─── Дашборд мониторинга (FR-BPM-02.4) ───────────────────────────────────────

export interface BpmDashboardTopProcessDto {
    processId: string;
    processName: string;
    activeCount: number;
    totalCount: number;
}

export interface BpmDashboardDto {
    totalProcesses: number;
    activeInstances: number;
    suspendedInstances: number;
    completedInstances: number;
    cancelledInstances: number;
    faultedInstances: number;
    failedJobs: number;
    topActiveProcesses: BpmDashboardTopProcessDto[];
}

/** Сводная статистика для дашборда мониторинга. */
export const getBpmDashboard = (token: string): Promise<BpmDashboardDto> =>
    fetchJson('/api/bpm/dashboard', token);

// ─── Движок выполнения BPMN (FR-BPM Execution Engine) ────────────────────────

export type BpmTokenStatus = 'Active' | 'WaitingUserAction' | 'WaitingSignal' | 'WaitingMessage' | 'Completed' | 'WaitingJoin' | 'WaitingTimer' | 'WaitingCallActivity';

export interface BpmTokenDto {
    id: string;
    instanceId: string;
    elementId: string;
    elementType: string;
    elementName?: string;
    status: BpmTokenStatus;
    signalCode?: string;
    messageCode?: string;
    createdAt: string;
    completedAt?: string;
}

export interface CompleteUserTaskRequest {
    outputVariables?: Record<string, string | null>;
}

export interface SendSignalRequest {
    signalCode: string;
}

export interface SendMessageRequest {
    messageCode: string;
    correlationKey?: string;
}

/** Список активных токенов экземпляра. */
export const getTokens = (token: string, instanceId: string): Promise<BpmTokenDto[]> =>
    fetchJson(`/api/bpm/instances/${instanceId}/tokens`, token);

/** Завершить UserTask/ReceiveTask с передачей выходных переменных. */
export const completeToken = (
    token: string,
    instanceId: string,
    elementId: string,
    data: CompleteUserTaskRequest = {}
): Promise<BpmTokenDto> =>
    fetchJson(`/api/bpm/instances/${instanceId}/tokens/${encodeURIComponent(elementId)}/complete`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Отправить сигнал BPMN. */
export const sendSignal = (token: string, signalCode: string): Promise<void> =>
    fetchJson(`/api/events/signals/${encodeURIComponent(signalCode)}`, token, { method: 'POST' });

/** Отправить сообщение BPMN. */
export const sendMessage = (token: string, messageCode: string, data: SendMessageRequest): Promise<void> =>
    fetchJson(`/api/events/messages/${encodeURIComponent(messageCode)}`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });
