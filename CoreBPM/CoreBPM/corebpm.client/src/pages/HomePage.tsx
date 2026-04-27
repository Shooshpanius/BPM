import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { Sidebar, type SidebarSection } from '../components/Sidebar';
import { ContactsPage } from './contacts/ContactsPage';
import './HomePage.css';

interface HomePageProps {
    onAdmin: () => void;
}

/** Основная страница приложения: шапка + сайдбар + содержимое раздела. */
export function HomePage({ onAdmin }: HomePageProps) {
    const { logout, hasRole } = useAuth();
    const [section, setSection] = useState<SidebarSection>('contacts');

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

            <div className="hp-body">
                <Sidebar active={section} onSelect={setSection} />
                <main className="hp-content">
                    {section === 'contacts' && <ContactsPage />}
                </main>
            </div>
        </div>
    );
}
