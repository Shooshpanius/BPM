// API-клиент для предложений по улучшению бизнес-процессов (FR-BPM-03.1)

export type BpmImprovementStatus =
    | 'Pending'
    | 'Accepted'
    | 'InProgress'
    | 'Completed'
    | 'Rejected';

export const IMPROVEMENT_STATUS_LABELS: Record<BpmImprovementStatus, string> = {
    Pending: 'Ожидает рассмотрения',
    Accepted: 'Принято',
    InProgress: 'В работе',
    Completed: 'Завершено',
    Rejected: 'Отклонено',
};

export const IMPROVEMENT_STATUS_BADGE: Record<BpmImprovementStatus, string> = {
    Pending: 'badge--pending',
    Accepted: 'badge--accepted',
    InProgress: 'badge--in-progress',
    Completed: 'badge--completed',
    Rejected: 'badge--rejected',
};

export interface ImprovementDto {
    id: string;
    processId: string;
    processName: string;
    subject: string;
    description?: string;
    status: BpmImprovementStatus;
    initiatorUserId: string;
    initiatorDisplayName: string;
    assignedUserId?: string;
    assignedDisplayName?: string;
    dueDate?: string;
    reviewComment?: string;
    resolution?: string;
    sourceInstanceId?: string;
    sourceTaskElementId?: string;
    createdAt: string;
    updatedAt: string;
    reviewedAt?: string;
    completedAt?: string;
}

export interface ImprovementMonitorItemDto {
    processId: string;
    processName: string;
    pendingCount: number;
    acceptedCount: number;
    inProgressCount: number;
    completedCount: number;
    rejectedCount: number;
    totalCount: number;
    owners: string[];
    curators: string[];
}

export interface CreateImprovementRequest {
    subject: string;
    description?: string;
    sourceInstanceId?: string;
    sourceTaskElementId?: string;
}

export interface AcceptImprovementRequest {
    assignedUserId: string;
    dueDate: string;
    comment?: string;
}

export interface RejectImprovementRequest {
    comment?: string;
}

export interface CompleteImprovementRequest {
    resolution: string;
}

export type ImprovementListRole = 'All' | 'My' | 'Current';

export interface ImprovementListFilter {
    role?: ImprovementListRole;
    processId?: string;
    status?: BpmImprovementStatus;
    authorId?: string;
    dateFrom?: string;
    dateTo?: string;
}

// ─── Вспомогательная функция ─────────────────────────────────────────────────

async function apiFetch<T>(url: string, token: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...init,
        headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...(init?.headers ?? {}),
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || `HTTP ${res.status}`);
    }
    if (res.status === 204) return undefined as T;
    return res.json();
}

// ─── API-функции ─────────────────────────────────────────────────────────────

/** Создаёт предложение по улучшению процесса. */
export async function createImprovement(
    token: string,
    processId: string,
    request: CreateImprovementRequest
): Promise<ImprovementDto> {
    return apiFetch(`/api/bpm/processes/${processId}/improvements`, token, {
        method: 'POST',
        body: JSON.stringify(request),
    });
}

/** Возвращает список предложений по процессу. */
export async function listImprovementsByProcess(
    token: string,
    processId: string
): Promise<ImprovementDto[]> {
    return apiFetch(`/api/bpm/processes/${processId}/improvements`, token);
}

/** Возвращает общий список предложений с фильтрацией. */
export async function listImprovements(
    token: string,
    filter: ImprovementListFilter = {}
): Promise<ImprovementDto[]> {
    const params = new URLSearchParams();
    if (filter.role) params.set('role', filter.role);
    if (filter.processId) params.set('processId', filter.processId);
    if (filter.status) params.set('status', filter.status);
    if (filter.authorId) params.set('authorId', filter.authorId);
    if (filter.dateFrom) params.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) params.set('dateTo', filter.dateTo);
    const qs = params.toString();
    return apiFetch(`/api/bpm/improvements${qs ? `?${qs}` : ''}`, token);
}

/** Возвращает предложение по идентификатору. */
export async function getImprovement(token: string, id: string): Promise<ImprovementDto> {
    return apiFetch(`/api/bpm/improvements/${id}`, token);
}

/** Принимает предложение. */
export async function acceptImprovement(
    token: string,
    id: string,
    request: AcceptImprovementRequest
): Promise<ImprovementDto> {
    return apiFetch(`/api/bpm/improvements/${id}/accept`, token, {
        method: 'POST',
        body: JSON.stringify(request),
    });
}

/** Отклоняет предложение. */
export async function rejectImprovement(
    token: string,
    id: string,
    request: RejectImprovementRequest
): Promise<ImprovementDto> {
    return apiFetch(`/api/bpm/improvements/${id}/reject`, token, {
        method: 'POST',
        body: JSON.stringify(request),
    });
}

/** Завершает реализацию улучшения. */
export async function completeImprovement(
    token: string,
    id: string,
    request: CompleteImprovementRequest
): Promise<ImprovementDto> {
    return apiFetch(`/api/bpm/improvements/${id}/complete`, token, {
        method: 'POST',
        body: JSON.stringify(request),
    });
}

/** Возвращает монитор улучшений для процессов текущего пользователя. */
export async function getImprovementMonitorMy(token: string): Promise<ImprovementMonitorItemDto[]> {
    return apiFetch('/api/bpm/improvements/monitor/my', token);
}

/** Возвращает полный монитор улучшений (Admin). */
export async function getImprovementMonitorFull(token: string): Promise<ImprovementMonitorItemDto[]> {
    return apiFetch('/api/bpm/improvements/monitor/full', token);
}
