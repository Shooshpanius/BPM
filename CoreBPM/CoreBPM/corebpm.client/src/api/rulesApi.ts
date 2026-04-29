// API-клиент для управления DMN-таблицами бизнес-правил (/api/rules)

export type DmnHitPolicy = 'Unique' | 'First' | 'Any' | 'Collect' | 'RuleOrder' | 'OutputOrder';
export type DmnVersionStatus = 'Draft' | 'Published' | 'Archived';
export type DmnColumnKind = 'Input' | 'Output';
export type DmnValueType = 'String' | 'Number' | 'Date' | 'Boolean';

// ─── Таблица ─────────────────────────────────────────────────────────────────

export interface DmnTableListItemDto {
    id: string;
    name: string;
    description?: string;
    hitPolicy: DmnHitPolicy;
    totalVersions: number;
    latestVersionStatus?: DmnVersionStatus;
    createdAt: string;
    updatedAt: string;
}

export interface DmnTableDto {
    id: string;
    name: string;
    description?: string;
    hitPolicy: DmnHitPolicy;
    totalVersions: number;
    createdAt: string;
    updatedAt: string;
}

export interface CreateDmnTableRequest {
    name: string;
    description?: string;
    hitPolicy: DmnHitPolicy;
}

export interface UpdateDmnTableRequest {
    name: string;
    description?: string;
    hitPolicy: DmnHitPolicy;
}

// ─── Версия ──────────────────────────────────────────────────────────────────

export interface DmnTableVersionInfoDto {
    id: string;
    versionNumber: number;
    status: DmnVersionStatus;
    createdAt: string;
    publishedAt?: string;
}

export interface DmnColumnDto {
    id: string;
    name: string;
    columnKind: DmnColumnKind;
    valueType: DmnValueType;
    order: number;
}

export interface DmnCellDto {
    id: string;
    columnId: string;
    value?: string;
    annotation?: string;
}

export interface DmnRowDto {
    id: string;
    order: number;
    cells: DmnCellDto[];
}

export interface DmnTableVersionDto {
    id: string;
    tableId: string;
    versionNumber: number;
    status: DmnVersionStatus;
    createdAt: string;
    publishedAt?: string;
    columns: DmnColumnDto[];
    rows: DmnRowDto[];
}

// ─── Запросы на сохранение ────────────────────────────────────────────────────

export interface SaveDmnColumnRequest {
    id?: string;
    name: string;
    columnKind: DmnColumnKind;
    valueType: DmnValueType;
    order: number;
}

export interface SaveDmnCellRequest {
    columnId?: string;
    columnIndex?: number;
    value?: string;
    annotation?: string;
}

export interface SaveDmnRowRequest {
    id?: string;
    order: number;
    cells: SaveDmnCellRequest[];
}

export interface SaveDmnTableVersionRequest {
    columns: SaveDmnColumnRequest[];
    rows: SaveDmnRowRequest[];
}

// ─── Тестирование ─────────────────────────────────────────────────────────────

export interface DmnTestRequest {
    inputs: Record<string, string | undefined>;
}

export interface DmnMatchedRowDto {
    rowId: string;
    rowOrder: number;
    outputs: Record<string, string | undefined>;
}

export interface DmnTestResponse {
    hitPolicy: DmnHitPolicy;
    matchedRows: DmnMatchedRowDto[];
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

// ─── Таблицы ─────────────────────────────────────────────────────────────────

/** Список всех DMN-таблиц. */
export const getDmnTables = (token: string): Promise<DmnTableListItemDto[]> =>
    fetchJson('/api/rules', token);

/** Получить DMN-таблицу по ID. */
export const getDmnTable = (token: string, tableId: string): Promise<DmnTableDto> =>
    fetchJson(`/api/rules/${tableId}`, token);

/** Создать DMN-таблицу. */
export const createDmnTable = (token: string, data: CreateDmnTableRequest): Promise<DmnTableDto> =>
    fetchJson('/api/rules', token, { method: 'POST', body: JSON.stringify(data) });

/** Обновить метаданные DMN-таблицы. */
export const updateDmnTable = (token: string, tableId: string, data: UpdateDmnTableRequest): Promise<DmnTableDto> =>
    fetchJson(`/api/rules/${tableId}`, token, { method: 'PUT', body: JSON.stringify(data) });

/** Удалить DMN-таблицу. */
export const deleteDmnTable = (token: string, tableId: string): Promise<void> =>
    fetchJson(`/api/rules/${tableId}`, token, { method: 'DELETE' });

// ─── Версии ───────────────────────────────────────────────────────────────────

/** Список версий DMN-таблицы. */
export const getDmnVersions = (token: string, tableId: string): Promise<DmnTableVersionInfoDto[]> =>
    fetchJson(`/api/rules/${tableId}/versions`, token);

/** Полная схема версии. */
export const getDmnVersion = (token: string, tableId: string, versionId: string): Promise<DmnTableVersionDto> =>
    fetchJson(`/api/rules/${tableId}/versions/${versionId}`, token);

/** Сохранить новый черновик. */
export const saveDmnDraft = (token: string, tableId: string, data: SaveDmnTableVersionRequest): Promise<DmnTableVersionDto> =>
    fetchJson(`/api/rules/${tableId}/versions`, token, { method: 'POST', body: JSON.stringify(data) });

/** Опубликовать версию. */
export const publishDmnVersion = (token: string, tableId: string, versionId: string): Promise<DmnTableVersionInfoDto> =>
    fetchJson(`/api/rules/${tableId}/versions/${versionId}/publish`, token, { method: 'POST' });

// ─── Тестирование ─────────────────────────────────────────────────────────────

/** Тестировать версию DMN-таблицы. */
export const testDmnVersion = (
    token: string,
    tableId: string,
    versionId: string,
    data: DmnTestRequest
): Promise<DmnTestResponse> =>
    fetchJson(`/api/rules/${tableId}/versions/${versionId}/test`, token, {
        method: 'POST',
        body: JSON.stringify(data),
    });
