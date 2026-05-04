// API-клиент настроек пользователя (FR-ORG-02.3)

export interface UserPreferencesDto {
    language: string;
    timeZone?: string;
    theme: string;
    dateFormat?: string;
    pageSize: number;
}

export interface UpdatePreferencesRequest {
    language?: string;
    timeZone?: string;
    theme?: string;
    dateFormat?: string;
    pageSize?: number;
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
    return res.json() as Promise<T>;
}

export const getUserPreferences = (token: string, userId: string): Promise<UserPreferencesDto> =>
    fetchJson(`/api/users/${userId}/preferences`, token);

export const updateUserPreferences = (
    token: string,
    userId: string,
    req: UpdatePreferencesRequest
): Promise<UserPreferencesDto> =>
    fetchJson(`/api/users/${userId}/preferences`, token, { method: 'PUT', body: JSON.stringify(req) });
