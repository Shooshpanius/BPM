// API-клиент для раздела «Сценарии» (/api/bpm/*/scripts, /api/bpm/designer/*)

import type { BpmProcessVersionStatus } from './bpmApi';

// ─── Сценарии процессов ───────────────────────────────────────────────────────

export interface BpmScriptModuleDto {
    id: string;
    processVersionId: string;
    scriptBody: string;
    language: string;
    updatedAt: string;
    publishedAt?: string;
}

export interface SaveScriptModuleRequest {
    scriptBody: string;
    language: string;
}

export interface BpmProcessVersionScriptInfoDto {
    processId: string;
    processName: string;
    versionId: string;
    versionNumber: number;
    versionStatus: BpmProcessVersionStatus;
    hasScript: boolean;
    scriptPublishedAt?: string;
}

// ─── Пользовательские расширения дизайнера ───────────────────────────────────

export interface BpmDesignerExtensionDto {
    id: string;
    organizationId: string;
    name: string;
    description?: string;
    folderPath?: string;
    scriptBody: string;
    isPublished: boolean;
    createdByUserId: string;
    createdAt: string;
    updatedAt: string;
}

export interface CreateDesignerExtensionRequest {
    organizationId: string;
    name: string;
    description?: string;
    folderPath?: string;
    scriptBody: string;
}

export interface UpdateDesignerExtensionRequest {
    name: string;
    description?: string;
    folderPath?: string;
    scriptBody: string;
}

// ─── Глобальные модули ────────────────────────────────────────────────────────

export interface BpmGlobalModuleDto {
    id: string;
    organizationId: string;
    name: string;
    description?: string;
    isPublished: boolean;
    filesCount: number;
    createdAt: string;
    updatedAt: string;
    publishedAt?: string;
}

export interface CreateGlobalModuleRequest {
    organizationId: string;
    name: string;
    description?: string;
}

export interface UpdateGlobalModuleRequest {
    name: string;
    description?: string;
}

export interface BpmGlobalModuleFileDto {
    id: string;
    moduleId: string;
    fileName: string;
    scriptBody: string;
    order: number;
    updatedAt: string;
}

export interface CreateGlobalModuleFileRequest {
    fileName: string;
    scriptBody: string;
}

export interface UpdateGlobalModuleFileRequest {
    fileName: string;
    scriptBody: string;
}

export interface ReorderGlobalModuleFilesRequest {
    orderedIds: string[];
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

// ─── Сценарии процессов ───────────────────────────────────────────────────────

/** Список версий процессов с информацией о сценариях для данной организации. */
export const listProcessVersionScripts = (token: string, organizationId: string): Promise<BpmProcessVersionScriptInfoDto[]> =>
    fetchJson(`/api/bpm/organizations/${organizationId}/scripts`, token);

/** Получить модуль сценариев версии процесса. */
export const getScript = (token: string, processId: string, versionId: string): Promise<BpmScriptModuleDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/scripts`, token);

/** Сохранить черновик сценария. */
export const saveScript = (token: string, processId: string, versionId: string, data: SaveScriptModuleRequest): Promise<BpmScriptModuleDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/scripts`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Опубликовать сценарий. */
export const publishScript = (token: string, processId: string, versionId: string): Promise<BpmScriptModuleDto> =>
    fetchJson(`/api/bpm/processes/${processId}/versions/${versionId}/scripts/publish`, token, { method: 'POST' });

// ─── Расширения дизайнера ─────────────────────────────────────────────────────

/** Список расширений организации. */
export const listExtensions = (token: string, organizationId: string): Promise<BpmDesignerExtensionDto[]> =>
    fetchJson(`/api/bpm/designer/extensions?organizationId=${organizationId}`, token);

/** Получить расширение по ID. */
export const getExtension = (token: string, id: string): Promise<BpmDesignerExtensionDto> =>
    fetchJson(`/api/bpm/designer/extensions/${id}`, token);

/** Создать расширение. */
export const createExtension = (token: string, data: CreateDesignerExtensionRequest): Promise<BpmDesignerExtensionDto> =>
    fetchJson('/api/bpm/designer/extensions', token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить расширение. */
export const updateExtension = (token: string, id: string, data: UpdateDesignerExtensionRequest): Promise<BpmDesignerExtensionDto> =>
    fetchJson(`/api/bpm/designer/extensions/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить расширение. */
export const deleteExtension = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/designer/extensions/${id}`, token, { method: 'DELETE' });

/** Опубликовать расширение. */
export const publishExtension = (token: string, id: string): Promise<BpmDesignerExtensionDto> =>
    fetchJson(`/api/bpm/designer/extensions/${id}/publish`, token, { method: 'POST' });

/** Скопировать расширение. */
export const copyExtension = (token: string, id: string): Promise<BpmDesignerExtensionDto> =>
    fetchJson(`/api/bpm/designer/extensions/${id}/copy`, token, { method: 'POST' });

// ─── Глобальные модули ────────────────────────────────────────────────────────

/** Список глобальных модулей организации. */
export const listGlobalModules = (token: string, organizationId: string): Promise<BpmGlobalModuleDto[]> =>
    fetchJson(`/api/bpm/designer/global-modules?organizationId=${organizationId}`, token);

/** Получить глобальный модуль по ID. */
export const getGlobalModule = (token: string, id: string): Promise<BpmGlobalModuleDto> =>
    fetchJson(`/api/bpm/designer/global-modules/${id}`, token);

/** Создать глобальный модуль. */
export const createGlobalModule = (token: string, data: CreateGlobalModuleRequest): Promise<BpmGlobalModuleDto> =>
    fetchJson('/api/bpm/designer/global-modules', token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить глобальный модуль. */
export const updateGlobalModule = (token: string, id: string, data: UpdateGlobalModuleRequest): Promise<BpmGlobalModuleDto> =>
    fetchJson(`/api/bpm/designer/global-modules/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить глобальный модуль. */
export const deleteGlobalModule = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/designer/global-modules/${id}`, token, { method: 'DELETE' });

/** Опубликовать глобальный модуль. */
export const publishGlobalModule = (token: string, id: string): Promise<BpmGlobalModuleDto> =>
    fetchJson(`/api/bpm/designer/global-modules/${id}/publish`, token, { method: 'POST' });

/** Список файлов глобального модуля. */
export const listGlobalModuleFiles = (token: string, moduleId: string): Promise<BpmGlobalModuleFileDto[]> =>
    fetchJson(`/api/bpm/designer/global-modules/${moduleId}/files`, token);

/** Добавить файл в глобальный модуль. */
export const addGlobalModuleFile = (token: string, moduleId: string, data: CreateGlobalModuleFileRequest): Promise<BpmGlobalModuleFileDto> =>
    fetchJson(`/api/bpm/designer/global-modules/${moduleId}/files`, token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить файл глобального модуля. */
export const updateGlobalModuleFile = (token: string, moduleId: string, fileId: string, data: UpdateGlobalModuleFileRequest): Promise<BpmGlobalModuleFileDto> =>
    fetchJson(`/api/bpm/designer/global-modules/${moduleId}/files/${fileId}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить файл из глобального модуля. */
export const deleteGlobalModuleFile = (token: string, moduleId: string, fileId: string): Promise<void> =>
    fetchJson(`/api/bpm/designer/global-modules/${moduleId}/files/${fileId}`, token, { method: 'DELETE' });

/** Изменить порядок файлов модуля. */
export const reorderGlobalModuleFiles = (token: string, moduleId: string, data: ReorderGlobalModuleFilesRequest): Promise<void> =>
    fetchJson(`/api/bpm/designer/global-modules/${moduleId}/files/reorder`, token, { method: 'PUT', body: JSON.stringify(data) });
