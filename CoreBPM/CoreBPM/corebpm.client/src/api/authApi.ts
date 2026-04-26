export interface LoginRequest {
    username: string;
    password: string;
    rememberMe: boolean;
}

export interface LoginResponse {
    accessToken: string;
    expiresIn: number;
}

export interface RefreshResponse {
    accessToken: string;
    expiresIn: number;
}

const BASE = '/api/auth';

/** Выполняет вход по логину/паролю. Refresh token устанавливается сервером в HttpOnly cookie. */
export async function login(data: LoginRequest): Promise<LoginResponse> {
    const res = await fetch(`${BASE}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
        credentials: 'include',
    });
    if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? 'Ошибка входа в систему');
    }
    return res.json();
}

/** Обновляет access token, используя refresh token из HttpOnly cookie. */
export async function refreshToken(): Promise<RefreshResponse> {
    const res = await fetch(`${BASE}/refresh`, {
        method: 'POST',
        credentials: 'include',
    });
    if (!res.ok) throw new Error('Сессия истекла');
    return res.json();
}

/** Выходит из системы и отзывает refresh token на сервере. */
export async function logout(accessToken: string): Promise<void> {
    await fetch(`${BASE}/logout`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${accessToken}` },
        credentials: 'include',
    });
}
