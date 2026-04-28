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
    /** Должность из активного назначения (read-only, задаётся через /api/admin/assignments) */
    positionId?: string;
    positionName?: string;
    isActive: boolean;
    createdAt: string;
}

export interface CreateEmployeeRequest {
    userId: string;
    organizationId: string;
    departmentId: string;
}

export interface UpdateEmployeeRequest {
    departmentId: string;
    isActive: boolean;
}

export interface DepartmentDto {
    id: string;
    organizationId: string;
    organizationName: string;
    parentId?: string;
    parentName?: string;
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
    status: number;
    isActive: boolean;
    employeesCount: number;
    createdAt: string;
}

export interface DepartmentTreeDto {
    id: string;
    organizationId: string;
    parentId?: string;
    name: string;
    shortName?: string;
    code?: string;
    description?: string;
    status: number;
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

// --- Должности ---

export type PositionCategory = 'Managerial' | 'Regular' | 'Project';
export type PositionStatus = 'Active' | 'Archived';

export interface PositionDto {
    id: string;
    organizationId: string;
    name: string;
    code?: string;
    description?: string;
    departmentId?: string;
    departmentName?: string;
    category: PositionCategory;
    status: PositionStatus;
    plannedHeadcount: number;
    occupiedHeadcount: number;
    vacancyCount: number;
    createdAt: string;
    updatedAt: string;
}

export interface CreatePositionRequest {
    organizationId: string;
    name: string;
    code?: string;
    description?: string;
    departmentId?: string;
    category: PositionCategory;
    plannedHeadcount: number;
}

export interface UpdatePositionRequest {
    name: string;
    code?: string;
    description?: string;
    departmentId?: string;
    category: PositionCategory;
    status: PositionStatus;
    plannedHeadcount: number;
}

export const getPositions = (token: string, organizationId?: string, status?: PositionStatus): Promise<PositionDto[]> => {
    const params = new URLSearchParams();
    if (organizationId) params.set('organizationId', organizationId);
    if (status) params.set('status', status);
    const qs = params.toString();
    return fetchJson(qs ? `/api/org/positions?${qs}` : '/api/org/positions', token);
};

export const createPosition = (token: string, data: CreatePositionRequest): Promise<PositionDto> =>
    fetchJson('/api/admin/positions', token, { method: 'POST', body: JSON.stringify(data) });

export const updatePosition = (token: string, id: string, data: UpdatePositionRequest): Promise<PositionDto> =>
    fetchJson(`/api/admin/positions/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const archivePosition = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/positions/${id}`, token, { method: 'DELETE' });

// --- Назначения на должности ---

export interface AssignmentDto {
    id: string;
    userId: string;
    userDisplayName: string;
    userWorkEmail: string;
    positionId: string;
    positionName: string;
    organizationId: string;
    organizationName: string;
    departmentId?: string;
    departmentName?: string;
    rate: number;
    isPrimary: boolean;
    startDate: string;  // YYYY-MM-DD
    endDate?: string;   // YYYY-MM-DD | undefined
    isActive: boolean;
    createdAt: string;
}

export interface CreateAssignmentRequest {
    userId: string;
    positionId: string;
    rate: number;
    isPrimary: boolean;
    startDate: string;  // YYYY-MM-DD
    endDate?: string;   // YYYY-MM-DD | undefined
}

export interface UpdateAssignmentRequest {
    rate: number;
    isPrimary: boolean;
    startDate: string;
    endDate?: string;
}

export interface AssignmentFilters {
    userId?: string;
    positionId?: string;
    organizationId?: string;
    activeOnly?: boolean;
}

export const getAssignments = (token: string, filters?: AssignmentFilters): Promise<AssignmentDto[]> => {
    const params = new URLSearchParams();
    if (filters?.userId) params.set('userId', filters.userId);
    if (filters?.positionId) params.set('positionId', filters.positionId);
    if (filters?.organizationId) params.set('organizationId', filters.organizationId);
    if (filters?.activeOnly !== undefined) params.set('activeOnly', String(filters.activeOnly));
    const qs = params.toString();
    return fetchJson(qs ? `/api/org/assignments?${qs}` : '/api/org/assignments', token);
};

export const createAssignment = (token: string, data: CreateAssignmentRequest): Promise<AssignmentDto> =>
    fetchJson('/api/admin/assignments', token, { method: 'POST', body: JSON.stringify(data) });

export const updateAssignment = (token: string, id: string, data: UpdateAssignmentRequest): Promise<AssignmentDto> =>
    fetchJson(`/api/admin/assignments/${id}`, token, { method: 'PUT', body: JSON.stringify(data) });

export const deleteAssignment = (token: string, id: string): Promise<void> =>
    fetchJson(`/api/admin/assignments/${id}`, token, { method: 'DELETE' });

