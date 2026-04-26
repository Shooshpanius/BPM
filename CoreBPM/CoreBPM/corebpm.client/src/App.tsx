import { useEffect } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { LoginPage } from './pages/LoginPage';
import { HomePage } from './pages/HomePage';

/** Внутренний компонент, управляющий отображением страниц. */
function AppContent() {
    const { isAuthenticated, refresh } = useAuth();

    // При загрузке приложения пробуем восстановить сессию через refresh cookie
    useEffect(() => {
        refresh();
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    return isAuthenticated ? <HomePage /> : <LoginPage />;
}

function App() {
    return (
        <AuthProvider>
            <AppContent />
        </AuthProvider>
    );
}

export default App;