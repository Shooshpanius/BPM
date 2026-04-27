// API-клиент для административной панели

export interface OrganizationDto {
    id: string;
    name: string;
    description?: string;
    isPrimary: boolean;
    isActive: boolean;
    employeesCount: number;
    createdAt: string;
}

export interface CreateOrganizationRequest {
    name: string;
    description?: string;
    isPrimary: boolean;
    isActive: boolean;
}

export interface UpdateOrganizationRequest {
    name: string;
    description?: string;
    isPrimary: boolean;
    isActive: boolean;
}

export interface AdminUserListItemDto {
    id: string;
    firstName: string;
    lastName: string;
    middleName?: string;
    displayName: string;
    workEmail: string;
    phone?: string;
    isActive: boolean;
    username?: string;
    createdAt: string;
}

export interface CreateUserRequest {
    firstName: string;
    lastName: string;
    middleName?: string;
    workEmail: string;
    phone?: string;
    username: string;
    password: string;
    isActive: boolean;
}

export interface UpdateUserRequest {
    firstName: string;
    lastName: string;
    middleName?: string;
    workEmail: string;
    phone?: string;
    isActive: boolean;
}

export interface EmployeeDto {
    id: string;
    userId: string;
    userDisplayName: string;
    userWorkEmail: string;
    organizationId: string;
    organizationName: string;
    departmentId?: string;
    departmentName?: string;
    position?: string;
    isActive: boolean;
    createdAt: string;
}

export interface CreateEmployeeRequest {
    userId: string;
    organizationId: string;
    departmentId: string;
    position?: string;
}

export interface UpdateEmployeeRequest {
    departmentId: string;
    position?: string;
    isActive: boolean;
}

export interface DepartmentDto {
    id: string;
    organizationId: string;
    organizationName: string;
    parentId?: string;
    parentName?: string;
    name: string;
    description?: string;
    isActive: boolean;
    employeesCount: number;
    createdAt: string;
}

export interface DepartmentTreeDto {
    id: string;
    organizationId: string;
    parentId?: string;
    name: string;
    description?: string;
    isActive: boolean;
    employeesCount: number;
    children: DepartmentTreeDto[];
}

export interface CreateDepartmentRequest {
    organizationId: string;
    parentId?: string;
    name: string;
    description?: string;
}

export interface UpdateDepartmentRequest {
    parentId?: string;
    name: string;
    description?: string;
    isActive: boolean;
}

async function fetchJson<T>(url: string, token: string, options?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
            ...(options?.headers ?? {}),
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || `HTTP ${res.status}`);
    }
    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
}

// --- Организации ---

export const getOrganizations = (token: string): Promise<OrganizationDto[]> =>
    fetchJson('/api/admin/organizations', token);

export const createOrganization = (token: string, data: CreateOrganizationRequest): Promise<OrganizationDto> =>
    fetchJson('/api/admin/organizations', token, { method: 'POST', body: JSON.stringify(data) });

export const updateOrganization = (token: string, id: string, data: UpdateOrganizationRequest): Promise<OrganizationDto> =>
    fetchJson(`/api/admin/organizations/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const deleteOrganization = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/organizations/${id}`, token, { method: 'DELETE' });

export const setOrganizationPrimary = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/organizations/${id}/set-primary`, token, { method: 'POST' });

// --- Пользователи ---

export const getUsers = (token: string): Promise<AdminUserListItemDto[]> =>
    fetchJson('/api/admin/users', token);

export const createUser = (token: string, data: CreateUserRequest): Promise<AdminUserListItemDto> =>
    fetchJson('/api/admin/users', token, { method: 'POST', body: JSON.stringify(data) });

export const updateUser = (token: string, id: string, data: UpdateUserRequest): Promise<AdminUserListItemDto> =>
    fetchJson(`/api/admin/users/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const deleteUser = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/users/${id}`, token, { method: 'DELETE' });

export const getUserEmployees = (token: string, userId: string): Promise<EmployeeDto[]> =>
    fetchJson(`/api/admin/users/${userId}/employees`, token);

// --- Сотрудники ---

export const getEmployees = (token: string, organizationId?: string): Promise<EmployeeDto[]> => {
    const url = organizationId
        ? `/api/admin/employees?organizationId=${organizationId}`
        : '/api/admin/employees';
    return fetchJson(url, token);
};

export const createEmployee = (token: string, data: CreateEmployeeRequest): Promise<EmployeeDto> =>
    fetchJson('/api/admin/employees', token, { method: 'POST', body: JSON.stringify(data) });

export const updateEmployee = (token: string, id: string, data: UpdateEmployeeRequest): Promise<EmployeeDto> =>
    fetchJson(`/api/admin/employees/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const deleteEmployee = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/employees/${id}`, token, { method: 'DELETE' });

// --- Подразделения ---

export const getDepartments = (token: string, organizationId?: string): Promise<DepartmentDto[]> => {
    const url = organizationId
        ? `/api/admin/departments?organizationId=${organizationId}`
        : '/api/admin/departments';
    return fetchJson(url, token);
};

export const getDepartmentById = (token: string, id: string): Promise<DepartmentDto> =>
    fetchJson(`/api/admin/departments/${id}`, token);

export const getDepartmentsTree = (token: string, organizationId: string): Promise<DepartmentTreeDto[]> =>
    fetchJson(`/api/admin/departments/tree?organizationId=${organizationId}`, token);

export const createDepartment = (token: string, data: CreateDepartmentRequest): Promise<DepartmentDto> =>
    fetchJson('/api/admin/departments', token, { method: 'POST', body: JSON.stringify(data) });

export const updateDepartment = (token: string, id: string, data: UpdateDepartmentRequest): Promise<DepartmentDto> =>
    fetchJson(`/api/admin/departments/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const deleteDepartment = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/departments/${id}`, token, { method: 'DELETE' });
