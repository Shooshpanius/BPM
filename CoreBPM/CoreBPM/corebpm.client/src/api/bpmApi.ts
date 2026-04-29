// API-клиент для управления бизнес-процессами (/api/bpm/*)

export type BpmProcessVersionStatus = 'Draft' | 'Active' | 'Obsolete';

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
