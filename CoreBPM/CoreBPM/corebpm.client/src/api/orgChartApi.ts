// API-клиент для оргчарта (GET /api/org/chart)

import type { PositionCategory } from './adminApi';

export type { PositionCategory };

export interface OrgChartEmployeeDto {
    userId: string;
    displayName: string;
    avatarUrl?: string;
    workEmail?: string;
    phone?: string;
    positionId?: string;
    positionName?: string;
    positionCategory?: PositionCategory;
    /** Заполняется только при extended=true */
    rate?: number;
}

export interface OrgChartVacancyDto {
    positionId: string;
    positionName: string;
    vacancyCount: number;
}

export interface OrgChartNodeDto {
    id: string;
    name: string;
    shortName?: string;
    totalEmployeesCount: number;
    employees: OrgChartEmployeeDto[];
    /** Заполняется только при extended=true */
    vacancies: OrgChartVacancyDto[];
    children: OrgChartNodeDto[];
}

export interface OrgChartDto {
    departments: OrgChartNodeDto[];
    /** Сотрудники без назначенного подразделения */
    unassignedEmployees: OrgChartEmployeeDto[];
}

async function fetchJson<T>(url: string, token: string): Promise<T> {
    const res = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        let message = `HTTP ${res.status}`;
        try {
            const body = await res.json();
            if (body?.error) message = body.error;
        } catch {
            // тело не является JSON
        }
        throw new Error(message);
    }
    return res.json() as Promise<T>;
}

export interface GetOrgChartParams {
    organizationId: string;
    search?: string;
    /** Запросить расширенные данные (ставки, вакансии) — применяется для Admin/HR */
    extended?: boolean;
}

/** Возвращает дерево оргструктуры организации. */
export const getOrgChart = (token: string, params: GetOrgChartParams): Promise<OrgChartDto> => {
    const qs = new URLSearchParams({ organizationId: params.organizationId });
    if (params.search) qs.set('search', params.search);
    if (params.extended) qs.set('extended', 'true');
    return fetchJson(`/api/org/chart?${qs.toString()}`, token);
};
