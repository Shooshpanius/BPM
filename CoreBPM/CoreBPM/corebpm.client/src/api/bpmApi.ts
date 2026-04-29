// API-клиент для управления бизнес-процессами (/api/bpm/*)

export type BpmProcessVersionStatus = 'Draft' | 'Active' | 'Obsolete';

export type BpmVariableType = 'String' | 'Int' | 'Decimal' | 'Bool' | 'Date' | 'DateTime' | 'Json' | 'File' | 'User' | 'List';

export type BpmRaciType = 'R' | 'A' | 'C' | 'I';

export interface BpmProcessListItemDto {
    id: string;
    organizationId: string;
    name: string;
    description?: string;
    activeVersionNumber?: number;
    totalVersions: number;
    createdAt: string;
    updatedAt: string;
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
}

export interface BpmProcessVersionInfoDto {
    id: string;
    versionNumber: number;
    status: BpmProcessVersionStatus;
    createdByUserId: string;
    createdAt: string;
    updatedAt: string;
}

export interface BpmDiagramDto {
    versionId: string;
    versionNumber: number;
    status: BpmProcessVersionStatus;
    diagramXml?: string;
    updatedAt: string;
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
    data: { organizationId: string; name: string; description?: string }
): Promise<BpmProcessDto> =>
    fetchJson('/api/bpm/processes', token, {
        method: 'POST',
        body: JSON.stringify(data),
    });

/** Обновить метаданные процесса. */
export const updateProcess = (
    token: string,
    processId: string,
    data: { name: string; description?: string }
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

/** Получить текущую диаграмму процесса. */
export const getDiagram = (token: string, processId: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram`, token);

/** Сохранить XML-диаграмму. */
export const saveDiagram = (token: string, processId: string, diagramXml: string): Promise<BpmDiagramDto> =>
    fetchJson(`/api/bpm/processes/${processId}/diagram`, token, {
        method: 'PUT',
        body: JSON.stringify({ diagramXml }),
    });

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

