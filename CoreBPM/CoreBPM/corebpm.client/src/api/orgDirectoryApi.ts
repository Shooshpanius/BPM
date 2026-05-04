// API-клиент адресной книги (GET /api/org/directory/*)

export interface DirectoryOrganizationDto {
    id: string;
    name: string;
    employeesCount: number;
}

export interface DirectoryDepartmentTreeDto {
    id: string;
    organizationId: string;
    parentId?: string;
    name: string;
    employeesCount: number;
    children: DirectoryDepartmentTreeDto[];
}

export interface DirectoryEmployeeDto {
    id: string;
    userId: string;
    displayName: string;
    firstName?: string;
    lastName?: string;
    middleName?: string;
    workEmail: string;
    phone?: string;
    avatarUrl?: string;
    position?: string;
    organizationId: string;
    organizationName: string;
    departmentId?: string;
    departmentName?: string;
}

export interface DirectoryEmployeesPagedDto {
    items: DirectoryEmployeeDto[];
    total: number;
    page: number;
    pageSize: number;
}

async function fetchJson<T>(url: string, token: string): Promise<T> {
    const res = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        let message = text || `HTTP ${res.status}`;
        try {
            const body = JSON.parse(text);
            if (body?.error) message = body.error;
        } catch { /* тело не является JSON — используем текст как есть */ }
        throw new Error(message);
    }
    return res.json() as Promise<T>;
}

export const getDirectoryOrganizations = (token: string): Promise<DirectoryOrganizationDto[]> =>
    fetchJson('/api/org/directory/organizations', token);

export const getDirectoryDepartmentTree = (token: string, organizationId: string): Promise<DirectoryDepartmentTreeDto[]> =>
    fetchJson(`/api/org/directory/departments/tree?organizationId=${organizationId}`, token);

export interface GetEmployeesParams {
    organizationId?: string;
    departmentId?: string;
    search?: string;
    position?: string;
    sortBy?: string;
    sortDir?: 'asc' | 'desc';
    page?: number;
    pageSize?: number;
}

export const getDirectoryEmployees = (
    token: string,
    params: GetEmployeesParams
): Promise<DirectoryEmployeesPagedDto> => {
    const qs = new URLSearchParams();
    if (params.organizationId) qs.set('organizationId', params.organizationId);
    if (params.departmentId) qs.set('departmentId', params.departmentId);
    if (params.search) qs.set('search', params.search);
    if (params.position) qs.set('position', params.position);
    if (params.sortBy) qs.set('sortBy', params.sortBy);
    if (params.sortDir) qs.set('sortDir', params.sortDir);
    if (params.page) qs.set('page', String(params.page));
    if (params.pageSize) qs.set('pageSize', String(params.pageSize));
    return fetchJson(`/api/org/directory/employees?${qs.toString()}`, token);
};

export const exportDirectoryEmployees = (
    token: string,
    params: Omit<GetEmployeesParams, 'page' | 'pageSize' | 'sortDir'>
): Promise<Blob> => {
    const qs = new URLSearchParams();
    if (params.organizationId) qs.set('organizationId', params.organizationId);
    if (params.departmentId) qs.set('departmentId', params.departmentId);
    if (params.search) qs.set('search', params.search);
    if (params.position) qs.set('position', params.position);
    if (params.sortBy) qs.set('sortBy', params.sortBy);
    return fetch(`/api/org/directory/employees/export?${qs.toString()}`, {
        headers: { Authorization: `Bearer ${token}` },
    }).then(r => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.blob();
    });
};

