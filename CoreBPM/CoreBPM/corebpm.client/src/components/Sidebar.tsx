import { useAuth } from '../context/AuthContext';
import { APP_VERSION, LAST_PR_DATE } from '../version';
import './Sidebar.css';

export type SidebarSection = 'contacts' | 'org-structure' | 'bpm-processes' | 'bpm-my-processes' | 'bpm-monitor' | 'bpm-queue' | 'bpm-documentation' | 'bpm-rules' | 'bpm-forms' | 'bpm-scripts';

interface SidebarProps {
    active: SidebarSection;
    onSelect: (section: SidebarSection) => void;
}

/** Вертикальный сайдбар навигации (~60px) с иконками разделов. */
export function Sidebar({ active, onSelect }: SidebarProps) {
    const { hasRole } = useAuth();
    const canManageOrg = hasRole('Admin') || hasRole('HR');

    return (
        <nav className="sidebar" aria-label="Разделы системы">
            <SidebarItem
                id="contacts"
                label="Контакты"
                active={active === 'contacts'}
                onClick={() => onSelect('contacts')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                        <circle cx="9" cy="7" r="4"/>
                        <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
                        <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                    </svg>
                }
            />
            <div className="sidebar-divider" role="separator" />
            <SidebarItem
                id="bpm-processes"
                label="Процессы"
                active={active === 'bpm-processes'}
                onClick={() => onSelect('bpm-processes')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <rect x="2" y="7" width="6" height="5" rx="1"/>
                        <rect x="16" y="7" width="6" height="5" rx="1"/>
                        <rect x="9" y="14" width="6" height="5" rx="1"/>
                        <path d="M8 9.5h2.5a1.5 1.5 0 0 1 1.5 1.5v1"/>
                        <path d="M16 9.5h-2.5A1.5 1.5 0 0 0 12 11v1"/>
                        <path d="M12 12v2"/>
                    </svg>
                }
            />
            <SidebarItem
                id="bpm-my-processes"
                label="Мои процессы"
                active={active === 'bpm-my-processes'}
                onClick={() => onSelect('bpm-my-processes')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <circle cx="12" cy="8" r="4"/>
                        <path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                        <path d="M16 11l1.5 1.5L21 9"/>
                    </svg>
                }
            />
            <SidebarItem
                id="bpm-monitor"
                label="Монитор"
                active={active === 'bpm-monitor'}
                onClick={() => onSelect('bpm-monitor')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <rect x="2" y="3" width="20" height="14" rx="2"/>
                        <path d="M8 21h8M12 17v4"/>
                        <path d="M7 8h2v5H7zM11 6h2v7h-2zM15 10h2v3h-2z"/>
                    </svg>
                }
            />
            {hasRole('Admin') && (
            <SidebarItem
                id="bpm-queue"
                label="Очередь"
                active={active === 'bpm-queue'}
                onClick={() => onSelect('bpm-queue')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M3 6h18M3 12h18M3 18h18"/>
                        <circle cx="21" cy="6" r="0" fill="currentColor"/>
                    </svg>
                }
            />
            )}
            <SidebarItem
                id="bpm-documentation"
                label="Документирование"
                active={active === 'bpm-documentation'}
                onClick={() => onSelect('bpm-documentation')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                        <polyline points="10 9 9 9 8 9"/>
                    </svg>
                }
            />
            <SidebarItem
                id="bpm-rules"
                label="Бизнес-правила"
                active={active === 'bpm-rules'}
                onClick={() => onSelect('bpm-rules')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <rect x="3" y="3" width="18" height="18" rx="2"/>
                        <path d="M3 9h18"/>
                        <path d="M3 15h18"/>
                        <path d="M9 3v18"/>
                        <path d="M15 3v18"/>
                    </svg>
                }
            />
            <SidebarItem
                id="bpm-forms"
                label="Формы задач"
                active={active === 'bpm-forms'}
                onClick={() => onSelect('bpm-forms')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <rect x="3" y="3" width="18" height="18" rx="2"/>
                        <path d="M7 8h10"/>
                        <path d="M7 12h6"/>
                        <path d="M7 16h4"/>
                    </svg>
                }
            />
            <SidebarItem
                id="bpm-scripts"
                label="Сценарии"
                active={active === 'bpm-scripts'}
                onClick={() => onSelect('bpm-scripts')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <polyline points="16 18 22 12 16 6"/>
                        <polyline points="8 6 2 12 8 18"/>
                    </svg>
                }
            />
            {canManageOrg && (
                <>
                    <div className="sidebar-divider" role="separator" />
                    <SidebarItem
                        id="org-structure"
                        label="Оргструктура"
                        active={active === 'org-structure'}
                        onClick={() => onSelect('org-structure')}
                        icon={
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <rect x="3" y="3" width="6" height="4" rx="1"/>
                                <rect x="9" y="10" width="6" height="4" rx="1"/>
                                <rect x="15" y="17" width="6" height="4" rx="1"/>
                                <rect x="3" y="17" width="6" height="4" rx="1"/>
                                <path d="M6 7v3"/>
                                <path d="M12 14v3"/>
                                <path d="M6 10h12"/>
                            </svg>
                        }
                    />
                </>
            )}
            <div className="sidebar-version" aria-label="Версия системы">
                <span>v{APP_VERSION}</span>
                <span>{LAST_PR_DATE}</span>
            </div>
        </nav>
    );
}

interface SidebarItemProps {
    id: string;
    label: string;
    active: boolean;
    onClick: () => void;
    icon: React.ReactNode;
}

function SidebarItem({ label, active, onClick, icon }: SidebarItemProps) {
    return (
        <button
            className={`sidebar-item${active ? ' active' : ''}`}
            onClick={onClick}
            title={label}
            aria-label={label}
            aria-pressed={active}
        >
            <span className="sidebar-icon">{icon}</span>
            <span className="sidebar-label">{label}</span>
        </button>
    );
}
