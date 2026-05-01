// API-клиент для пакетов миграции версий (/api/bpm/migration-packages)

// ─── Типы ─────────────────────────────────────────────────────────────────────

export type MigrationPackageStatus = 'New' | 'Running' | 'Completed' | 'CompletedWithErrors' | 'Cancelled';
export type MigrationItemStatus =
    | 'New'
    | 'InProgress'
    | 'Migrated'
    | 'CriticalError'
    | 'Busy'
    | 'RequiresManualHandling'
    | 'OtherError'
    | 'NotApplicable'
    | 'NoMigrationNeeded';

export interface MigrationPackageListItemDto {
    id: string;
    name: string;
    createdByUserId: string;
    createdByUserName: string;
    status: MigrationPackageStatus;
    isActive: boolean;
    createdAt: string;
    totalItems: number;
    migratedItems: number;
    errorItems: number;
}

export interface MigrationPackageDetailDto {
    id: string;
    name: string;
    createdByUserId: string;
    createdByUserName: string;
    status: MigrationPackageStatus;
    isActive: boolean;
    createdAt: string;
    completedAt?: string;
    totalItems: number;
    migratedItems: number;
    errorItems: number;
    pendingItems: number;
}

export interface MigrationItemDto {
    id: string;
    packageId: string;
    instanceId: string;
    instanceName: string;
    processId: string;
    processName: string;
    targetVersionId: string;
    targetVersionNumber: number;
    status: MigrationItemStatus;
    errorComment?: string;
    manualChangeUrl?: string;
    processedAt?: string;
}

export interface CreateMigrationPackageRequest {
    name: string;
    items: MigrationItemRequest[];
}

export interface MigrationItemRequest {
    instanceId: string;
    targetVersionId: string;
}

export interface ManualMigrateItemRequest {
    manualChangeUrl?: string;
}

// ─── fetchJson (локальная копия) ──────────────────────────────────────────────

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

// ─── API-методы ───────────────────────────────────────────────────────────────

/** Список пакетов миграции. */
export const getMigrationPackages = (
    token: string,
    params?: { status?: number; isActive?: boolean; page?: number; pageSize?: number }
): Promise<MigrationPackageListItemDto[]> => {
    const qs = new URLSearchParams();
    if (params?.status !== undefined) qs.set('status', String(params.status));
    if (params?.isActive !== undefined) qs.set('isActive', String(params.isActive));
    if (params?.page !== undefined) qs.set('page', String(params.page));
    if (params?.pageSize !== undefined) qs.set('pageSize', String(params.pageSize));
    const query = qs.toString() ? `?${qs.toString()}` : '';
    return fetchJson(`/api/bpm/migration-packages${query}`, token);
};

/** Детали пакета миграции. */
export const getMigrationPackage = (
    token: string,
    id: string
): Promise<MigrationPackageDetailDto> =>
    fetchJson(`/api/bpm/migration-packages/${id}`, token);

/** Создать пакет миграции. */
export const createMigrationPackage = (
    token: string,
    request: CreateMigrationPackageRequest
): Promise<MigrationPackageDetailDto> =>
    fetchJson('/api/bpm/migration-packages', token, {
        method: 'POST',
        body: JSON.stringify(request),
    });

/** Запустить пакет миграции. */
export const startMigrationPackage = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/migration-packages/${id}/start`, token, { method: 'POST' });

/** Отменить пакет миграции. */
export const cancelMigrationPackage = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/bpm/migration-packages/${id}/cancel`, token, { method: 'POST' });

/** Список элементов пакета. */
export const getMigrationPackageItems = (
    token: string,
    packageId: string,
    params?: { status?: number; page?: number; pageSize?: number }
): Promise<MigrationItemDto[]> => {
    const qs = new URLSearchParams();
    if (params?.status !== undefined) qs.set('status', String(params.status));
    if (params?.page !== undefined) qs.set('page', String(params.page));
    if (params?.pageSize !== undefined) qs.set('pageSize', String(params.pageSize));
    const query = qs.toString() ? `?${qs.toString()}` : '';
    return fetchJson(`/api/bpm/migration-packages/${packageId}/items${query}`, token);
};

/** Отметить элемент как обработанный вручную. */
export const manualMigrateItem = (
    token: string,
    packageId: string,
    itemId: string,
    request: ManualMigrateItemRequest
): Promise<void> =>
    fetchJson(`/api/bpm/migration-packages/${packageId}/items/${itemId}/manual`, token, {
        method: 'POST',
        body: JSON.stringify(request),
    });
