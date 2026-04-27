import { useState, type FormEvent } from 'react';
import { useAuth } from '../context/AuthContext';
import { LAST_PR_NUMBER, LAST_PR_DATE } from '../version';
import './LoginPage.css';

/** Страница входа в систему. */
export function LoginPage() {
    const { login } = useAuth();
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [rememberMe, setRememberMe] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    async function handleSubmit(e: FormEvent) {
        e.preventDefault();
        if (!username.trim() || !password) return;
        setError(null);
        setLoading(true);
        try {
            await login({ username: username.trim(), password, rememberMe });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Ошибка входа');
        } finally {
            setLoading(false);
        }
    }

    return (
        <div className="lp-root">
            <div className="lp-card">
                <div className="lp-logo" aria-hidden="true">
                    <span className="lp-logo-icon">⬡</span>
                    <span className="lp-logo-name">Core BPM</span>
                </div>

                <h1 className="lp-title">Вход в систему</h1>

                <form className="lp-form" onSubmit={handleSubmit} noValidate>
                    <div className="lp-field">
                        <label className="lp-label" htmlFor="username">
                            Логин
                        </label>
                        <input
                            id="username"
                            className="lp-input"
                            type="text"
                            autoComplete="username"
                            value={username}
                            onChange={e => setUsername(e.target.value)}
                            required
                            placeholder="Введите логин"
                            disabled={loading}
                        />
                    </div>

                    <div className="lp-field">
                        <label className="lp-label" htmlFor="password">
                            Пароль
                        </label>
                        <input
                            id="password"
                            className="lp-input"
                            type="password"
                            autoComplete="current-password"
                            value={password}
                            onChange={e => setPassword(e.target.value)}
                            required
                            placeholder="Введите пароль"
                            disabled={loading}
                        />
                    </div>

                    <label className="lp-remember">
                        <input
                            type="checkbox"
                            checked={rememberMe}
                            onChange={e => setRememberMe(e.target.checked)}
                            disabled={loading}
                        />
                        <span>Запомнить меня</span>
                    </label>

                    {error && (
                        <p className="lp-error" role="alert">
                            {error}
                        </p>
                    )}

                    <button
                        className="lp-btn"
                        type="submit"
                        disabled={loading || !username.trim() || !password}
                    >
                        {loading ? 'Выполняется вход…' : 'Войти'}
                    </button>
                </form>

                <p className="lp-version">
                    #{LAST_PR_NUMBER}
                    <br />
                    {LAST_PR_DATE}
                </p>
            </div>
        </div>
    );
}
