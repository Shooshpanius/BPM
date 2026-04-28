// API-клиент для управления деревом подразделений (GET/POST/PUT/DELETE /api/org/units/*)

export type DepartmentStatus = 0 | 1; // 0 = Active, 1 = Archived
export const DEPARTMENT_STATUS_ACTIVE: DepartmentStatus = 0;
export const DEPARTMENT_STATUS_ARCHIVED: DepartmentStatus = 1;

export type DepartmentChangeType = 0 | 1 | 2 | 3 | 4;
export const CHANGE_TYPE_CREATED: DepartmentChangeType = 0;
export const CHANGE_TYPE_UPDATED: DepartmentChangeType = 1;
export const CHANGE_TYPE_MOVED: DepartmentChangeType = 2;
export const CHANGE_TYPE_ARCHIVED: DepartmentChangeType = 3;
export const CHANGE_TYPE_RESTORED: DepartmentChangeType = 4;

export const CHANGE_TYPE_LABELS: Record<DepartmentChangeType, string> = {
    0: 'Создано',
    1: 'Обновлено',
    2: 'Перемещено',
    3: 'Архивировано',
    4: 'Восстановлено',
};

export interface BreadcrumbItemDto {
    id: string;
    name: string;
}

export interface OrgUnitDto {
    id: string;
    organizationId: string;
    parentId?: string;
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
    status: DepartmentStatus;
    path: string;
    breadcrumb: BreadcrumbItemDto[];
    directEmployeesCount: number;
    totalEmployeesCount: number;
    createdAt: string;
    updatedAt: string;
}

export interface OrgUnitTreeDto {
    id: string;
    organizationId: string;
    parentId?: string;
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
    status: DepartmentStatus;
    path: string;
    directEmployeesCount: number;
    totalEmployeesCount: number;
    children: OrgUnitTreeDto[];
}

export interface CreateUnitRequest {
    organizationId: string;
    parentId?: string;
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
}

export interface UpdateUnitRequest {
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
    status: DepartmentStatus;
}

export interface MoveUnitRequest {
    newParentId?: string;
}

export interface UnitHistoryDto {
    id: string;
    departmentId: string;
    changedByUserId?: string;
    changedByUserName?: string;
    changedAt: string;
    changeType: DepartmentChangeType;
    oldValue?: string;
    newValue?: string;
}

// ─── Вспомогательные функции ───

async function fetchJson<T>(url: string, token: string, options?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...options,
        headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...options?.headers,
        },
    });
    if (!res.ok) {
        let message = `HTTP ${res.status}`;
        try {
            const body = await res.json();
            if (body?.error) message = body.error;
        } catch {
            // ignore
        }
        throw new Error(message);
    }
    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
}

// ─── Публичные функции ───

/** Возвращает дерево подразделений организации. */
export const getUnitsTree = (
    token: string,
    params: { organizationId: string; status?: DepartmentStatus; search?: string }
): Promise<OrgUnitTreeDto[]> => {
    const qs = new URLSearchParams({ organizationId: params.organizationId });
    if (params.status !== undefined) qs.set('status', String(params.status));
    if (params.search) qs.set('search', params.search);
    return fetchJson(`/api/org/units?${qs.toString()}`, token);
};

/** Возвращает подробную карточку подразделения с breadcrumb и счётчиками. */
export const getUnitById = (token: string, unitId: string): Promise<OrgUnitDto> =>
    fetchJson(`/api/org/units/${unitId}`, token);

/** Создаёт новое подразделение. */
export const createUnit = (token: string, request: CreateUnitRequest): Promise<OrgUnitDto> =>
    fetchJson('/api/org/units', token, {
        method: 'POST',
        body: JSON.stringify(request),
    });

/** Обновляет данные подразделения. */
export const updateUnit = (token: string, unitId: string, request: UpdateUnitRequest): Promise<OrgUnitDto> =>
    fetchJson(`/api/org/units/${unitId}`, token, {
        method: 'PUT',
        body: JSON.stringify(request),
    });

/** Архивирует подразделение (мягкое удаление). */
export const archiveUnit = (token: string, unitId: string): Promise<void> =>
    fetchJson(`/api/org/units/${unitId}`, token, { method: 'DELETE' });

/** Перемещает подразделение в новый родительский узел. */
export const moveUnit = (token: string, unitId: string, request: MoveUnitRequest): Promise<OrgUnitDto> =>
    fetchJson(`/api/org/units/${unitId}/move`, token, {
        method: 'PUT',
        body: JSON.stringify(request),
    });

/** Возвращает историю изменений подразделения. */
export const getUnitHistory = (token: string, unitId: string): Promise<UnitHistoryDto[]> =>
    fetchJson(`/api/org/units/${unitId}/history`, token);
