// API-клиент профиля пользователя (FR-ORG-02.1)

export interface UserProfileDto {
    id: string;
    firstName: string;
    lastName: string;
    middleName?: string;
    displayName: string;
    workEmail: string;
    phone?: string;
    mobilePhone?: string;
    internalPhone?: string;
    personalEmail?: string;
    bio?: string;
    birthDate?: string;
    birthDateVisibility: string;
    avatarUrl?: string;
    position?: string;
    department?: string;
    organization?: string;
    isActive: boolean;
}

export interface UpdateProfileRequest {
    firstName?: string;
    lastName?: string;
    middleName?: string;
    displayName?: string;
    phone?: string;
    mobilePhone?: string;
    internalPhone?: string;
    personalEmail?: string;
    bio?: string;
    birthDate?: string;
    birthDateVisibility?: string;
}

async function fetchJson<T>(url: string, token: string, options?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...options,
        headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...(options?.headers ?? {}),
        },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        let message = text || `HTTP ${res.status}`;
        try {
            const body = JSON.parse(text);
            if (body?.error) message = body.error;
        } catch { /* текст не JSON */ }
        throw new Error(message);
    }
    if (res.status === 204) return undefined as unknown as T;
    return res.json() as Promise<T>;
}

export const getUserProfile = (token: string, userId: string): Promise<UserProfileDto> =>
    fetchJson(`/api/users/${userId}/profile`, token);

export const updateUserProfile = (token: string, userId: string, req: UpdateProfileRequest): Promise<UserProfileDto> =>
    fetchJson(`/api/users/${userId}/profile`, token, { method: 'PUT', body: JSON.stringify(req) });

export const deleteUserAvatar = (token: string, userId: string): Promise<void> =>
    fetchJson(`/api/users/${userId}/avatar`, token, { method: 'DELETE' });
