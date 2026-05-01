import { useEffect, useState } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { BpmNotificationsProvider } from './context/BpmNotificationsContext';
import { LoginPage } from './pages/LoginPage';
import { HomePage } from './pages/HomePage';
import { AdminPage } from './pages/admin/AdminPage';

type AppPage = 'home' | 'admin';

/** Внутренний компонент, управляющий отображением страниц. */
function AppContent() {
    const { isAuthenticated, refresh } = useAuth();
    const [page, setPage] = useState<AppPage>('home');

    // При загрузке приложения пробуем восстановить сессию через refresh cookie
    useEffect(() => {
        refresh();
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    if (!isAuthenticated) return <LoginPage />;

    if (page === 'admin') {
        return <AdminPage onBack={() => setPage('home')} />;
    }

    return <HomePage onAdmin={() => setPage('admin')} />;
}

function App() {
    return (
        <AuthProvider>
            <BpmNotificationsProvider>
                <AppContent />
            </BpmNotificationsProvider>
        </AuthProvider>
    );
}

export default App;
