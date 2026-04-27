import {
    createContext,
    useCallback,
    useContext,
    useState,
    type ReactNode,
} from 'react';
import {
    login as apiLogin,
    logout as apiLogout,
    refreshToken as apiRefresh,
    type LoginRequest,
} from '../api/authApi';

/** Извлекает payload из JWT-токена. */
function parseJwtPayload(token: string): Record<string, unknown> {
    try {
        const [, payload] = token.split('.');
        const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
        return JSON.parse(atob(base64));
    } catch {
        return {};
    }
}

/** Извлекает роли из payload JWT-токена. */
function extractRoles(token: string): string[] {
    const payload = parseJwtPayload(token);
    const roleClaim = payload['role'] ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (Array.isArray(roleClaim)) return roleClaim as string[];
    if (typeof roleClaim === 'string') return [roleClaim];
    return [];
}

interface AuthState {
    accessToken: string | null;
    isAuthenticated: boolean;
    roles: string[];
}

interface AuthContextValue extends AuthState {
    login: (data: LoginRequest) => Promise<void>;
    logout: () => Promise<void>;
    refresh: () => Promise<boolean>;
    hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** Провайдер состояния авторизации для всего приложения. */
export function AuthProvider({ children }: { children: ReactNode }) {
    const [state, setState] = useState<AuthState>({
        accessToken: null,
        isAuthenticated: false,
        roles: [],
    });

    const login = useCallback(async (data: LoginRequest) => {
        const res = await apiLogin(data);
        setState({
            accessToken: res.accessToken,
            isAuthenticated: true,
            roles: extractRoles(res.accessToken),
        });
    }, []);

    const logout = useCallback(async () => {
        if (state.accessToken) {
            try {
                await apiLogout(state.accessToken);
            } catch {
                // игнорируем ошибки при выходе
            }
        }
        setState({ accessToken: null, isAuthenticated: false, roles: [] });
    }, [state.accessToken]);

    const refresh = useCallback(async (): Promise<boolean> => {
        try {
            const res = await apiRefresh();
            setState({
                accessToken: res.accessToken,
                isAuthenticated: true,
                roles: extractRoles(res.accessToken),
            });
            return true;
        } catch {
            setState({ accessToken: null, isAuthenticated: false, roles: [] });
            return false;
        }
    }, []);

    const hasRole = useCallback(
        (role: string) => state.roles.includes(role),
        [state.roles],
    );

    return (
        <AuthContext.Provider value={{ ...state, login, logout, refresh, hasRole }}>
            {children}
        </AuthContext.Provider>
    );
}

/** Хук для доступа к контексту авторизации. */
export function useAuth(): AuthContextValue {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth: компонент должен быть внутри AuthProvider');
    return ctx;
}
