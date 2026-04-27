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

async function fetchJson<T>(url: string, token: string): Promise<T> {
    const res = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || `HTTP ${res.status}`);
    }
    return res.json() as Promise<T>;
}

export const getDirectoryOrganizations = (token: string): Promise<DirectoryOrganizationDto[]> =>
    fetchJson('/api/org/directory/organizations', token);

export const getDirectoryDepartmentTree = (token: string, organizationId: string): Promise<DirectoryDepartmentTreeDto[]> =>
    fetchJson(`/api/org/directory/departments/tree?organizationId=${organizationId}`, token);

export const getDirectoryEmployees = (
    token: string,
    params: { organizationId?: string; departmentId?: string; search?: string }
): Promise<DirectoryEmployeeDto[]> => {
    const qs = new URLSearchParams();
    if (params.organizationId) qs.set('organizationId', params.organizationId);
    if (params.departmentId) qs.set('departmentId', params.departmentId);
    if (params.search) qs.set('search', params.search);
    return fetchJson(`/api/org/directory/employees?${qs.toString()}`, token);
};
