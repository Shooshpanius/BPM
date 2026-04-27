import { useAuth } from '../context/AuthContext';
import './HomePage.css';

interface HomePageProps {
    onAdmin: () => void;
}

/** Базовая главная страница после успешного входа. */
export function HomePage({ onAdmin }: HomePageProps) {
    const { logout, hasRole } = useAuth();

    return (
        <div className="hp-root">
            <header className="hp-header">
                <div className="hp-header-brand">
                    <span className="hp-logo-icon" aria-hidden="true">⬡</span>
                    <span className="hp-logo-name">Core BPM</span>
                </div>
                <nav className="hp-header-nav">
                    {hasRole('Admin') && (
                        <button className="hp-admin-btn" onClick={onAdmin}>
                            Администрирование
                        </button>
                    )}
                    <button className="hp-logout-btn" onClick={logout}>
                        Выйти
                    </button>
                </nav>
            </header>

            <main className="hp-main">
                <h1 className="hp-welcome">Добро пожаловать</h1>
                <p className="hp-desc">
                    Система управления бизнес-процессами Core BPM
                </p>
            </main>
        </div>
    );
}
