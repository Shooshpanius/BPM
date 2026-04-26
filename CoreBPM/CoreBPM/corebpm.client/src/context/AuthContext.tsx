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

interface AuthState {
    accessToken: string | null;
    isAuthenticated: boolean;
}

interface AuthContextValue extends AuthState {
    login: (data: LoginRequest) => Promise<void>;
    logout: () => Promise<void>;
    refresh: () => Promise<boolean>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** Провайдер состояния авторизации для всего приложения. */
export function AuthProvider({ children }: { children: ReactNode }) {
    const [state, setState] = useState<AuthState>({
        accessToken: null,
        isAuthenticated: false,
    });

    const login = useCallback(async (data: LoginRequest) => {
        const res = await apiLogin(data);
        setState({ accessToken: res.accessToken, isAuthenticated: true });
    }, []);

    const logout = useCallback(async () => {
        if (state.accessToken) {
            try {
                await apiLogout(state.accessToken);
            } catch {
                // игнорируем ошибки при выходе
            }
        }
        setState({ accessToken: null, isAuthenticated: false });
    }, [state.accessToken]);

    const refresh = useCallback(async (): Promise<boolean> => {
        try {
            const res = await apiRefresh();
            setState({ accessToken: res.accessToken, isAuthenticated: true });
            return true;
        } catch {
            setState({ accessToken: null, isAuthenticated: false });
            return false;
        }
    }, []);

    return (
        <AuthContext.Provider value={{ ...state, login, logout, refresh }}>
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
